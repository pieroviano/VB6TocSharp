using System.Collections.Generic;
using static Vb6ToCSharp.Runtime.VbStrings;
using static Vb6ToCSharp.Runtime.VbInteraction;
using static Vb6ToCSharp.CodeConversion.ConversionUtility;

namespace Vb6ToCSharp.CodeConversion;

/// <summary>
/// What the code conversion has to know about ADO (Microsoft ActiveX Data Objects), the one type library VB6 projects
/// use often enough to convert without a wrapper. VB6 calls a parameterized property like a method
/// (<c>rs.Fields("Text")</c>); C# reaches the same member through an indexer.
/// </summary>
public static class AdoInterop
{
    /// <summary>The library's prefix in a VB6 declaration (<c>Dim rs As ADODB.Recordset</c>); it may be left out.</summary>
    private const string Prefix = "ADODB.";

    /// <summary>The objects of the library, as a VB6 declaration names them.</summary>
    private static readonly string[] Types =
    {
        "Connection", "Command", "Recordset", "Record", "Stream", "Parameter", "Parameters",
        "Field", "Fields", "Error", "Errors", "Property", "Properties",
    };

    /// <summary>The members of those objects that take an argument: collections, and the collection indexer itself.</summary>
    private static readonly string[] IndexedMembers = { "Fields", "Parameters", "Properties", "Errors", "Item", "Collect" };

    /// <summary>
    /// The methods whose arguments VB6 may leave out, as "Type.Member:parameter,parameter,...". The parameters are
    /// in the type library's order and carry the names the .NET library declares for them, because that is what a
    /// named argument has to spell; <c>&amp;</c> marks a ByRef parameter, which VB6 may omit but C# must write.
    /// <para>
    /// An omitted argument is written as no argument at all - the ones after it become named - so the parameter's
    /// own default applies, which is the value the type library declares (an omitted CursorType is
    /// <c>adOpenUnspecified</c>, not the zero a <c>default</c> would give).
    /// </para>
    /// </summary>
    private const string Signatures =
            "Connection.Open:ConnectionString,UserId,Password,Options;"
          + "Connection.Execute:CommandText,&RecordsAffected,Options;"
          + "Command.Execute:&RecordsAffected,Parameters,Options;"
          + "Command.CreateParameter:Name,Type,Direction,Size,Value;"
          + "Recordset.Open:Source,ActiveConnection,CursorType,LockType,Options;"
          + "Recordset.AddNew:FieldList,Values;"
          + "Recordset.Update:FieldList,Values;"
          + "Recordset.Delete:AffectRecords;"
          + "Recordset.UpdateBatch:AffectRecords;"
          + "Recordset.CancelBatch:AffectRecords;"
          + "Recordset.Find:Criteria,SkipRecords,SearchDirection,Start;"
          + "Recordset.Move:NumRecords,Start;"
          + "Recordset.Save:Destination,PersistFormat;"
          + "Recordset.NextRecordset:&RecordsAffected;"
          + "Recordset.Requery:Options;"
          + "Recordset.Resync:AffectRecords,ResyncValues;"
          + "Record.Open:Source,ActiveConnection,Mode,CreateOptions,Options,UserName,Password;"
          + "Stream.Open:Source,Mode,Options,UserName,Password;";

    /// <summary>The type without the library prefix: <c>ADODB.Recordset</c> and <c>Recordset</c> are the same type.</summary>
    private static string Bare(string type)
    {
        var t = Trim(type);
        return LCase(Left(t, Len(Prefix))) == LCase(Prefix) ? Mid(t, Len(Prefix) + 1) : t;
    }

    /// <summary>Whether <paramref name="type"/> is one of the library's objects, qualified or not.</summary>
    public static bool IsType(string type) => Has(Types, Bare(type));

    /// <summary>Whether <paramref name="type"/> is a Recordset, whose default member is a field of the current row.</summary>
    public static bool IsRecordset(string type) => LCase(Bare(type)) == "recordset";

    /// <summary>
    /// Whether <paramref name="member"/> of an object of type <paramref name="type"/> is a parameterized property,
    /// which VB6 calls with parentheses and C# indexes.
    /// </summary>
    public static bool IsIndexedMember(string type, string member) => IsType(type) && Has(IndexedMembers, Trim(member));

    /// <summary>The parameters <see cref="Signatures"/> declares for a method, or null when it declares none.</summary>
    private static string[] Signature(string type, string member)
    {
        if (!IsType(type)) return null;
        var key = LCase(Bare(type) + "." + Trim(member)) + ":";
        foreach (var s in Split(Signatures, ";"))
        {
            if (LMatch(LCase(s), key)) return Split(Mid(s, Len(key) + 1), ",");
        }
        return null;
    }

    /// <summary>Whether the conversion can write a call out instead of late binding it through the runtime.</summary>
    public static bool HasSignature(string type, string member) => Signature(type, member) != null;

    /// <summary>
    /// The argument list of a call that left arguments out, as C# writes it: positional up to the first omission,
    /// named after it, and <c>out _</c> for a ByRef parameter VB6 left out, whose result it discards too.
    /// <para>
    /// Null when the call cannot be written out and stays late bound: the library declares no such method, the call
    /// passes more arguments than it has parameters, or it passes a variable to a ByRef parameter - VB6 reads the
    /// value back from it, and neither form of conversion can return it yet.
    /// </para>
    /// </summary>
    /// <param name="args">The converted arguments; one left out of the VB6 call is <see cref="CodeConverter.MissingArgument"/>.</param>
    public static string Arguments(string type, string member, IList<string> args)
    {
        var names = Signature(type, member);
        if (names == null || args.Count > names.Length || DiscardsByRefArgument(names, args)) return null;
        var last = args.Count - 1;
        while (last >= 0 && Omitted(args[last])) last = last - 1; // an omitted argument at the end is just left off
        var o = "";
        var named = false; // an argument was left out: every argument after it has to be named
        for (var i = 0; i <= last; i++)
        {
            var name = names[i];
            var byRef = Left(name, 1) == "&";
            if (byRef) name = Mid(name, 2);
            string arg;
            if (byRef)
            { // C# cannot leave a ByRef argument out, and VB6 discards what the callee writes to an omitted one
                arg = "out _";
            }
            else if (Omitted(args[i]))
            {
                named = true; // skipped, so the parameter's own default applies, as it does in VB6
                continue;
            }
            else
            {
                arg = IIf(named, LCase(Left(name, 1)) + Mid(name, 2) + ": ", "") + args[i];
            }
            o = o + IIf(o == "", "", ", ") + arg;
        }
        return o;
    }

    /// <summary>Whether the call passes a variable to a ByRef parameter, whose value VB6 reads back from it.</summary>
    private static bool DiscardsByRefArgument(string[] names, IList<string> args)
    {
        for (var i = 0; i < args.Count && i < names.Length; i++)
        {
            if (Left(names[i], 1) == "&" && !Omitted(args[i])) return true;
        }
        return false;
    }

    /// <summary>An argument the VB6 call left out.</summary>
    private static bool Omitted(string arg) => Trim(arg) == CodeConverter.MissingArgument;

    private static bool Has(string[] names, string name)
    {
        foreach (var n in names)
        {
            if (LCase(n) == LCase(name)) return true;
        }
        return false;
    }
}
