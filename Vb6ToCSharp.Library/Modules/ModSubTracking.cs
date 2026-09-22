using System.Collections.Generic;
using static Microsoft.VisualBasic.Constants;
using static Microsoft.VisualBasic.Strings;
using static Vb6ToCSharp.Modules.ModConvert;
using static Vb6ToCSharp.Modules.ModRegEx;
using static Vb6ToCSharp.Modules.ModUtils;
using static Vb6ToCSharp.Modules.ModVb6ToCs;
using static Vb6ToCSharp.VbExtension;


namespace Vb6ToCSharp.Modules;

public static class ModSubTracking
{
    // Option Explicit
    public class Variable
    {
        public string name = "";
        public string asType = "";
        public string asArray = "";
        public bool param = false;
        public bool retVal = false;
        public bool assigned = false;
        public bool used = false;
        public bool assignedBeforeUsed = false;
        public bool usedBeforeAssigned = false;
        public bool vb6Array = false; // a VB6Array<T> (non-zero lower bound), not a T[]
        public string fixedLen = ""; // String * n: the length expression
    }
    public class Property
    {
        public string name = "";
        public bool asPublic = false;
        public string asType = "";
        public bool asFunc = false;
        public string getter = "";
        public string setter = "";
        public string origArgName = "";
        public string funcArgs = "";
        public string origProto = "";
        public string getArgs = ""; // VB6 parameters of Property Get
        public string letArgs = ""; // VB6 parameters of Property Let / Set (the last one is the value)
    }
    private static bool lockout = false;
    private static List<Variable> vars = new List<Variable> { }; 
    private static List<Property> props = new List<Property> { }; 
    private static List<Variable> moduleVars = new List<Variable> { }; // module-level variables of the file being converted
    private static List<string> unknownAssigned = new List<string> { }; // names assigned in the procedure without a declaration


    public static bool Analyze
    {
        get
        {
            var analyze = lockout;

            return analyze;
        }
    }


    public static void SubBegin(bool setLockout = false)
    {
        if (!setLockout)
        {
            var nVars = new List<Variable> { };

            vars = nVars;
            unknownAssigned = new List<string> { };
        }
    }

    private static int SubParamIndex(string p)
    {
        var subParamIndex = 0;
        // TODO (not supported):   On Error GoTo NoEntries
        for (subParamIndex = 0; subParamIndex < vars.Count; subParamIndex++)
        {
            if (vars[subParamIndex].name == p)
            {
                return subParamIndex;

            }
        }
        subParamIndex = -1;
        return subParamIndex;
    }

    /// <summary>Forgets the module-level variables (a new file is being converted).</summary>
    public static void ClearModuleVars()
    {
        moduleVars = new List<Variable> { };
    }

    /// <summary>Declares a module-level variable: procedures see its type and array shape.</summary>
    public static void ModuleVarDecl(string p, string asType, string asArray)
    {
        moduleVars.RemoveAll(v => v.name == p);
        moduleVars.Add(new Variable { name = p, asType = asType, asArray = asArray });
    }

    public static Variable SubParam(string p)
    {
        var subParam =
            // TODO (not supported): On Error Resume Next
            SubParamIndex(p) >= 0 ? vars[SubParamIndex(p)] : moduleVars.Find(v => v.name == p) ?? new Variable(); // locals shadow module variables
        return subParam;
    }

    public static void SubParamDecl(string p, string asType, string asArray, bool isParam, bool isReturn)
    {
        if (lockout)
        {
            return;

        }

        var n = vars.Count;
        vars.Add(new Variable());
        vars[n].name = p;
        vars[n].asType = asType;
        vars[n].param = isParam;
        vars[n].retVal = isReturn;
        vars[n].asArray = asArray;
    }

    public static void SubParamAssign(string p)
    {
        if (lockout)
        {
            return;

        }

        var k = SubParamIndex(p);
        if (k >= 0)
        {
            vars[k].assigned = true;
            if (!vars[k].used)
            {
                vars[k].assignedBeforeUsed = true;
            }
        }
        else if (p != "" && !moduleVars.Exists(v => v.name == p) && !unknownAssigned.Contains(p))
        {
            unknownAssigned.Add(p);
        }
    }

    /// <summary>Names the procedure assigns without declaring them (implicit variables when there is no Option Explicit).</summary>
    public static List<string> UnknownAssigned()
    {
        return new List<string>(unknownAssigned);
    }

    /// <summary>A property of the class being converted.</summary>
    public static bool IsPropertyName(string p)
    {
        return PropIndex(p) >= 0;
    }

    public static void SubParamUsed(string p)
    {
        if (lockout)
        {
            return;

        }

        var k = SubParamIndex(p);
        if (k >= 0)
        {
            vars[k].used = true;
            if (!vars[k].assigned)
            {
                vars[k].usedBeforeAssigned = true;
            }
        }
    }

    public static void SubParamUsedList(string s)
    {
        var sp = new string[0];

        if (lockout)
        {
            return;

        }

        sp = Split(s, ",");
        foreach (var iterL in sp)
        {
            var l = iterL;
            if (l != "")
            {
                SubParamUsed(l);
            }
        }
    }

    public static void ClearProperties()
    {
        var nProps = new List<Property> { };

        props = nProps;
    }

    private static int PropIndex(string p)
    {
        var propIndex = 0;
        // TODO (not supported):   On Error GoTo NoEntries
        for (propIndex = 0; propIndex < props.Count; propIndex++)
        {
            if (props[propIndex].name == p)
            {
                return propIndex;

            }
        }
        propIndex = -1;
        return propIndex;
    }

    public static void AddProperty(string s)
    {
        var asPublic = false;

        var asFunc = false;

        var gsl = "";
        var pArgName = "";
        var pType = "";


        var pro = SplitWord(s, 1, vbCr);
        var origProto = pro;

        s = NlTrim(Replace(s, pro, ""));
        if (Right(s, 12) == "End Property")
        {
            s = NlTrim(Left(s, Len(s) - 12));
        }


        if (LMatch(pro, "Public "))
        {
            pro = Mid(pro, 8); // if one is public, both are...
            asPublic = true;
        }
        if (LMatch(pro, "Private "))
        {
            pro = Mid(pro, 9);
        }
        if (LMatch(pro, "Friend "))
        {
            pro = Mid(pro, 8);
        }
        if (LMatch(pro, "Static "))
        {
            pro = Mid(pro, 8);
        }
        if (LMatch(pro, "Property "))
        {
            pro = Mid(pro, 10);
        }

        if (LMatch(pro, "Get "))
        {
            pro = Mid(pro, 5);
            gsl = "get";
        }
        if (LMatch(pro, "Let "))
        {
            pro = Mid(pro, 5);
            gsl = "let";
        }
        if (LMatch(pro, "Set "))
        {
            pro = Mid(pro, 5);
            gsl = "set";
        }
        var pName = RegExNMatch(pro, patToken);
        pro = Mid(pro, Len(pName) + 1);
        if (LMatch(pro, "("))
        {
            pro = Mid(pro, 2);
        }
        var pArgs = NextBy(pro, ")");
        if ((gsl == "get" && pArgs != "") || (gsl != "get" && InStr(pArgs, ",") > 0))
        {
            asFunc = true;
        }
        if (gsl == "set" || gsl == "let")
        {
            var fArg = Trim(SplitWord(pArgs, -1, ","));
            if (LMatch(fArg, "ByVal "))
            {
                fArg = Mid(fArg, 7);
            }
            if (LMatch(fArg, "ByRef "))
            {
                fArg = Mid(fArg, 7);
            }
            pArgName = SplitWord(fArg, 1);
            if (SplitWord(fArg, 2, " ") == "As")
            {
                pType = SplitWord(fArg, 3, " ");
            }
            else
            {
                pType = "Variant";
            }
        }
        pro = Mid(pro, Len(pArgs) + 1);
        if (LMatch(pro, ")"))
        {
            pro = Trim(Mid(pro, 2));
        }
        if (LMatch(pro, "As "))
        {
            pro = Mid(pro, 4);
            pType = pro;
        }

        if (pType == "")
        {
            pType = "Variant";
        }


        var x = PropIndex(pName);
        if (x == -1)
        {
            x = props.Count;
            props.Add(new Property());
        }

        props[x].name = pName;
        props[x].origProto = origProto;
        if (asPublic)
        {
            props[x].asPublic = true; // if one is public, both are...
        }
        switch (gsl)
        {
            case "get":
                props[x].getter = ConvertSub(s, false, vbTriState.vbFalse);
                props[x].asType = ConvertDataType(pType);
                props[x].asFunc = props[x].asFunc || asFunc;
                props[x].funcArgs = pArgs;
                props[x].getArgs = pArgs;
                break;
            case "set":
            case "let":
                props[x].setter = ConvertSub(s, false, vbTriState.vbFalse);
                props[x].origArgName = pArgName;
                if (pType != "")
                {
                    props[x].asType = ConvertDataType(pType);
                }
                if (asFunc)
                {
                    props[x].asFunc = true;
                }
                if (pArgs != "")
                {
                    props[x].funcArgs = pArgs;
                }
                props[x].letArgs = pArgs;
                break;
        }
    }

    public static string ReadOutProperties(bool asModule = false)
    {
        var T = "";

        var r = "";
        var n = vbCrLf;
        for (var I = 0; I < props.Count; I++)
        {
            var p = props[I];
            if (p.name == "" || p.getter == "" && p.setter == "")
            {
                continue;
            }
            var initial = p.asType == "string" ? "\"\"" : "default(" + p.asType + ")"; // VB6 returns the default when never assigned
            // Implements: IFoo_Name is the explicit implementation of IFoo.Name (no access modifier)
            var iface = ModConvertClasses.ImplementedInterface(p.name);
            var mods = iface != null ? "" : (p.asPublic ? "public " : "") + (asModule ? "static " : "");
            var declName = iface != null ? iface + "." + Mid(p.name, Len(iface) + 2) : p.name;
            if (p.asFunc)
            { // a property with parameters: a getter method Name(args) and a setter method set_Name(args, value), as callers use them
                if (p.getter != "")
                {
                    var args = PropertyParameters(p.getArgs, false, out _);
                    r = r + mods + p.asType + " " + declName + "(" + args + ") {";
                    r = r + n + "  " + p.asType + " " + p.name + " = " + initial + ";";
                    r = r + n + "  " + Replace(p.getter, ExitPropertyMark, "return " + p.name + ";");
                    r = r + n + "  return " + p.name + ";";
                    r = r + n + "}" + n;
                }
                if (p.setter != "")
                {
                    var args = PropertyParameters(p.letArgs, true, out var valueName);
                    T = ReplaceToken(p.setter, "value", "valueOrig");
                    T = ReplaceToken(T, valueName, "value");
                    T = Replace(T, ExitPropertyMark, "return;");
                    r = r + mods + "void set_" + (iface != null ? Mid(p.name, Len(iface) + 2) : p.name) + "(" + args + (args == "" ? "" : ", ") + p.asType + " value) {";
                    r = r + n + "  " + T;
                    r = r + n + "}" + n;
                }
                continue;
            }
            r = r + mods + p.asType + " " + declName;
            r = r + " {";

            if (p.getter != "")
            {
                r = r + n + "  get {";
                r = r + n + "    " + p.asType + " " + p.name + " = " + initial + ";";
                T = p.getter;
                T = Replace(T, ExitPropertyMark, "return " + p.name + ";");
                r = r + n + "    " + T;
                r = r + n + "  return " + p.name + ";";
                r = r + n + "  }";
            }
            if (p.setter != "")
            {
                r = r + n + "  set {";
                T = p.setter;
                T = ReplaceToken(T, "value", "valueOrig");
                T = ReplaceToken(T, p.origArgName, "value"); // whole identifiers only
                T = Replace(T, ExitPropertyMark, "return;");
                r = r + n + "    " + T;
                r = r + n + "  }";
            }
            r = r + n + "}";
            r = r + n;
        }

        var readOutProperties = r;
        return readOutProperties;
    }

    /// <summary>
    /// C# parameters of a property with arguments (passed by value: callers pass expressions); for Let / Set the last VB6
    /// parameter is the assigned value and is left out (its name is returned).
    /// </summary>
    private static string PropertyParameters(string vbArgs, bool dropValue, out string valueName)
    {
        valueName = "";
        var list = ModConvertStatements.SplitTopLevel(vbArgs ?? "");
        list.RemoveAll(a => Trim(a) == "");
        if (dropValue && list.Count > 0)
        {
            var last = Trim(list[list.Count - 1]);
            foreach (var kw in new[] { "ByVal ", "ByRef ", "Optional " })
            {
                if (LMatch(last, kw))
                {
                    last = Trim(Mid(last, Len(kw) + 1));
                }
            }
            valueName = SplitWord(last, 1);
            list.RemoveAt(list.Count - 1);
        }
        var r = new List<string>();
        foreach (var a in list)
        {
            var c = ConvertParameter(a, true);
            if (LMatch(c, "ref "))
            {
                c = Mid(c, 5);
            }
            if (LMatch(c, "out "))
            {
                c = Mid(c, 5);
            }
            r.Add(c);
        }
        return string.Join(", ", r);
    }
}
