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
                props[x].asFunc = asFunc;
                props[x].funcArgs = pArgs;
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
                break;
        }
    }

    public static string ReadOutProperties(bool asModule = false)
    {
        // TODO (not supported): On Error Resume Next

        var T = "";

        var r = "";
        var m = "";
        var n = vbCrLf;
        for (var I = 0; I < props.Count; I++)
        {
            if (props[I].name != "" && !(props[I].getter == "" && props[I].setter == ""))
            {
                if (props[I].asPublic)
                {
                    r = r + "public ";
                }
                if (asModule)
                {
                    r = r + "static ";
                }

                //          If .Getter = "" Then R = R & "writeonly "
                //          If .Setter = "" Then R = R & "readonly "
                if (props[I].asFunc)
                {
                    r = r + " // TODO: Arguments not allowed on properties: " + props[I].funcArgs + vbCrLf;
                    r = r + " //       " + props[I].origProto + vbCrLf;
                }
                r = r + m + props[I].asType + " " + props[I].name;
                r = r + " {";

                if (props[I].getter != "")
                {
                    r = r + n + "  get {";
                    r = r + n + "    " + props[I].asType + " " + props[I].name + " = " + (props[I].asType == "string" ? "\"\"" : "default(" + props[I].asType + ")") + ";"; // VB6 returns the default when never assigned
                    T = props[I].getter;
                    T = Replace(T, ExitPropertyMark, "return " + props[I].name + ";");
                    r = r + n + "    " + T;
                    r = r + n + "  return " + props[I].name + ";";
                    r = r + n + "  }";
                }
                if (props[I].setter != "")
                {
                    r = r + n + "  set {";
                    T = props[I].setter;
                    T = ReplaceToken(T, "value", "valueOrig");
                    T = ReplaceToken(T, props[I].origArgName, "value"); // whole identifiers only
                    T = Replace(T, ExitPropertyMark, "return;");
                    r = r + n + "    " + T;
                    r = r + n + "  }";
                }
                r = r + n + "}";
                r = r + n;
            }
        }

        var readOutProperties = r;
        return readOutProperties;
    }
}