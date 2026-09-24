using static Vb6ToCSharp.Runtime.VbStrings;

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

    private static bool Has(string[] names, string name)
    {
        foreach (var n in names)
        {
            if (LCase(n) == LCase(name)) return true;
        }
        return false;
    }
}
