using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using static Vb6ToCSharp.Runtime.VbConstants;
using static Vb6ToCSharp.Runtime.VbStrings;
using static Vb6ToCSharp.Parsing.ProjectConfigurationParser;
using static Vb6ToCSharp.CodeConversion.CodeConverter;
using static Vb6ToCSharp.Parsing.ProjectFiles;
using static Vb6ToCSharp.CodeConversion.SubTracking;
using static Vb6ToCSharp.Infrastructure.TextFiles;
using static Vb6ToCSharp.CodeConversion.ConversionUtility;
using static Vb6ToCSharp.CodeConversion.Vb6ToCsConverter;
using Vb6ToCSharp.CodeGeneration;
using Vb6ToCSharp.Runtime;

namespace Vb6ToCSharp.CodeConversion;

/// <summary>
/// VB6 statements and lexical forms that have no direct C# counterpart: error handling, file I/O, the Mid/LSet/RSet
/// statements, computed GoTo, ReDim/Erase, Select Case labels, conditional compilation, type suffixes, literals.
/// Input lines are already de-commented and de-stringed (string literals are tokens).
/// </summary>
public static class StatementsConverter
{
    /// <summary>Catch variable of the generated handlers (not a valid VB6 identifier, so it never clashes).</summary>
    public const string CatchVar = "vbErr_";
    public const string SetProjectError = "Microsoft.VisualBasic.CompilerServices.ProjectData.SetProjectError";

    private const string Id = "[A-Za-z_][A-Za-z0-9_]*";

    // ---------------------------------------------------------------- module options (reset by BeginFile)

    /// <summary>VB6 source of the file being converted (for declarations that depend on its procedures, e.g. WithEvents handlers).</summary>
    public static string FileSource = "";

    /// <summary>#define / #undef lines that open the C# file being converted (from BeginFile).</summary>
    public static string FileHeader = "";

    /// <summary>Option Base of the file: the default lower bound of arrays.</summary>
    public static int OptionBase;

    /// <summary>Option Compare Text: string comparisons ignore case.</summary>
    public static bool OptionCompareText;

    /// <summary>Arrays with a non-zero lower bound become zero-based T[] sized ub + 1 (pragma ArrayBounds ForceZero).</summary>
    public static bool ForceZeroBounds;

    /// <summary>Option Explicit: without it, names assigned without a declaration are implicit locals.</summary>
    public static bool OptionExplicit = true;

    /// <summary>Public variables / constants of the project's standard modules (visible everywhere without a qualifier).</summary>
    private static readonly HashSet<string> projectGlobals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static string globalsProject;

    /// <summary>A public variable or constant of a standard module of the project.</summary>
    public static bool IsProjectGlobal(string name)
    {
        if (globalsProject != VbpFile)
        {
            globalsProject = VbpFile;
            projectGlobals.Clear();
            try
            {
                var folder = FilePath(VbpFile);
                foreach (var f in Split(VbpModules(VbpFile), vbCrLf))
                {
                    if (Trim(f) == "" || !System.IO.File.Exists(folder + f)) continue;
                    foreach (Match m in Regex.Matches(ReadEntireFile(folder + f), "(?m)^(?:Public|Global)\\s+(?:WithEvents\\s+|Const\\s+)?(" + Id + ")"))
                    {
                        projectGlobals.Add(m.Groups[1].Value);
                    }
                }
            }
            catch (Exception)
            {
                // no project
            }
        }
        return projectGlobals.Contains(name);
    }

    /// <summary>Module-level "As New" variables are auto-instancing properties (pragma AutoNew).</summary>
    public static bool AutoNew = true;

    /// <summary>DefType statements: first letter (upper case) to VB6 type.</summary>
    private static readonly Dictionary<char, string> defTypes = new Dictionary<char, string>();

    /// <summary>User-defined types known to the conversion (this file's, and the project's).</summary>
    private static readonly HashSet<string> udts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static string udtProject;

    /// <summary>Resets the per-file options (Option Base/Compare, DefType, pragmas) and module variables.</summary>
    public static void ResetFileOptions()
    {
        OptionBase = 0;
        OptionCompareText = false;
        ForceZeroBounds = false;
        AutoNew = true;
        defTypes.Clear();
        ClearModuleVars();
    }

    /// <summary>Applies a module-level Option / DefType statement; returns the C# comment to emit, or null.</summary>
    public static string ApplyModuleOption(string l)
    {
        l = Trim(l);
        Match m;
        if ((m = Regex.Match(l, "^Option Base ([01])$")).Success)
        {
            OptionBase = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            return "// VB6 " + l + " (arrays declared without a lower bound start at " + OptionBase + ")";
        }
        if (l == "Option Compare Text" || l == "Option Compare Binary" || l == "Option Compare Database")
        {
            OptionCompareText = l != "Option Compare Binary";
            return "// VB6 " + l;
        }
        if ((m = Regex.Match(l, "^Def(Bool|Byte|Int|Lng|Cur|Sng|Dbl|Dec|Date|Str|Obj|Var) (.+)$")).Success)
        {
            var type = DefTypeName(m.Groups[1].Value);
            foreach (var range in SplitTopLevel(m.Groups[2].Value))
            {
                var r = Regex.Match(range, "^([A-Za-z])(?: *- *([A-Za-z]))?$");
                if (!r.Success) continue;
                var from = char.ToUpperInvariant(r.Groups[1].Value[0]);
                var to = r.Groups[2].Success ? char.ToUpperInvariant(r.Groups[2].Value[0]) : from;
                for (var c = from; c <= to; c++) defTypes[c] = type;
            }
            return "// VB6 " + l + " (applied to declarations without a type)";
        }
        return null;
    }

    private static string DefTypeName(string d)
    {
        switch (d)
        {
            case "Bool": return "Boolean";
            case "Int": return "Integer";
            case "Lng": return "Long";
            case "Cur": return "Currency";
            case "Sng": return "Single";
            case "Dbl": return "Double";
            case "Dec": return "Variant";
            case "Str": return "String";
            case "Obj": return "Object";
            case "Var": return "Variant";
            default: return d; // Byte, Date
        }
    }

    /// <summary>The VB6 type of a name declared without As / type character: DefType, else Variant.</summary>
    public static string ImplicitType(string name)
    {
        name = Trim(name);
        return name != "" && defTypes.TryGetValue(char.ToUpperInvariant(name[0]), out var t) ? t : "Variant";
    }

    /// <summary>Forgets the project-wide facts read so far (UDTs, globals, classes, pragmas): a new conversion run.</summary>
    public static void ResetProjectCaches()
    {
        udtProject = null;
        udts.Clear();
        globalsProject = null;
        ClassesConverter.ResetCaches();
        PragmaConverter.ResetCaches();
        ProjectGroup.ResetCaches();
    }

    /// <summary>Registers a user-defined type declared in the file being converted.</summary>
    public static void RegisterUdt(string name) => udts.Add(name);

    /// <summary>Whether <paramref name="vbType"/> is a user-defined type (this file's or any project module's).</summary>
    public static bool IsUdt(string vbType)
    {
        if (string.IsNullOrEmpty(vbType)) return false;
        if (udtProject != VbpFile)
        {
            udtProject = VbpFile;
            try
            {
                var folder = FilePath(VbpFile);
                var files = VbpModules(VbpFile) + vbCrLf + VbpClasses(VbpFile) + vbCrLf + VbpForms(VbpFile) + vbCrLf + VbpUserControls(VbpFile);
                foreach (var f in Split(files, vbCrLf))
                {
                    if (Trim(f) == "" || !System.IO.File.Exists(folder + f)) continue;
                    foreach (Match m in Regex.Matches(ReadEntireFile(folder + f), "(?ms)^(?:Public |Private |Global )?Type (" + Id + ")(.*?)^End Type"))
                    {
                        udts.Add(m.Groups[1].Value);
                        RegisterUdtFields(m.Groups[1].Value, m.Groups[2].Value);
                    }
                }
            }
            catch (Exception)
            {
                // no project: only the types of the converted file are known
            }
        }
        return udts.Contains(vbType);
    }

    /// <summary>Array fields of the known UDTs (type name to field names).</summary>
    private static readonly Dictionary<string, HashSet<string>> udtArrayFields = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Records the array fields of a UDT, read from its Type ... End Type source.</summary>
    public static void RegisterUdtFields(string name, string typeBlock)
    {
        var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in Regex.Matches(typeBlock ?? "", "(?m)^\\s*(" + Id + ")\\s*\\(")) fields.Add(m.Groups[1].Value);
        udtArrayFields[name] = fields;
    }

    /// <summary>"var.Field" where var is a UDT and Field one of its arrays: indexed with [ ].</summary>
    public static bool IsUdtArrayField(string dotted)
    {
        var parts = (dotted ?? "").Split('.');
        if (parts.Length != 2) return false;
        var type = SubParam(parts[0]).asType;
        return IsUdt(type) && udtArrayFields.TryGetValue(type, out var f) && f.Contains(parts[1]);
    }

    /// <summary>An operand typed Integer / Long / Byte (VB6 And / Or on it are bitwise).</summary>
    public static bool IsIntegralOperand(string raw)
    {
        var t = ExprType(raw);
        return t == "Integer" || t == "Long" || t == "Byte";
    }

    /// <summary>VB6 functions whose .NET counterpart has another name (Math / Microsoft.VisualBasic.Strings).</summary>
    public static string RuntimeFunction(string name)
    {
        switch (name)
        {
            case "Sgn": return "Sign";
            case "Sqr": return "Sqrt";
            case "Atn": return "Atan";
            case "String": return "StrDup";
            default: return null;
        }
    }

    /// <summary>Initial value of a VB6 variable (an initialized UDT for user-defined types).</summary>
    public static string DefaultValue(string vbType) => IsUdt(vbType) ? "NewStruct<" + vbType + ">()" : ConvertDefaultDefault(vbType);

    /// <summary>C# types whose arrays need no per-element initialization (new T[n] is VB6-correct).</summary>
    public static bool IsPlainValueType(string cType)
    {
        switch (cType)
        {
            case "short": case "int": case "long": case "byte": case "float": case "double": case "decimal": case "bool": case "DateTime":
                return true;
            default:
                return false;
        }
    }

    /// <summary>Bounds of one dimension ("ub" or "lb To ub", lb defaulting to Option Base), converted; returns the zero-based element count (ub + 1).</summary>
    public static string DimBounds(string dim, out string lower, out string upper)
    {
        var m = Regex.Match(Trim(dim), "^(.+) To (.+)$");
        var lb = m.Success ? Trim(m.Groups[1].Value) : OptionBase.ToString(CultureInfo.InvariantCulture);
        var ub = m.Success ? Trim(m.Groups[2].Value) : Trim(dim);
        lower = Regex.IsMatch(lb, "^-?[0-9]+$") ? lb : ConvertValue(lb);
        upper = Regex.IsMatch(ub, "^-?[0-9]+$") ? ub : ConvertValue(ub);
        if (Regex.IsMatch(ub, "^-?[0-9]+$")) return (long.Parse(ub, CultureInfo.InvariantCulture) + 1).ToString(CultureInfo.InvariantCulture);
        return upper + " + 1";
    }

    // ---------------------------------------------------------------- implicit conversions (VB6 converts on assignment, C# does not)

    private static readonly HashSet<string> NumericFunctions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Len", "LenB", "InStr", "InStrB", "InStrRev", "Asc", "AscW", "AscB", "UBound", "LBound", "Val", "Int", "Fix", "Abs", "Sgn",
        "CInt", "CLng", "CByte", "CSng", "CDbl", "CCur", "Round", "StrComp", "FreeFile", "Loc", "LOF", "Seek", "FileLen",
        "Year", "Month", "Day", "Hour", "Minute", "Second", "Weekday", "DatePart", "DateDiff", "Timer", "Rnd", "Sqr", "Exp", "Log",
        "Sin", "Cos", "Tan", "Atn", "RGB", "QBColor", "ColorTranslate", "Err().Number",
    };

    /// <summary>VB6 type of a raw (VB6) operand when it can be told: a literal, a declared variable, a numeric function.</summary>
    public static string OperandType(string raw)
    {
        raw = Trim(raw);
        if (Regex.IsMatch(raw, "^-?[0-9]+[%&]?$") || Regex.IsMatch(raw, "^&[HhOo][0-9A-Fa-f]+&?$")) return "Long";
        if (Regex.IsMatch(raw, "^-?[0-9]+\\.[0-9]+[#!]?$")) return "Double";
        if (Regex.IsMatch(raw, "^" + ConverterUtils.deStringTokenBase + "[0-9]+$")) return "String";
        if (raw == "True" || raw == "False") return "Boolean";
        if (Regex.IsMatch(raw, "^" + Id + "$"))
        {
            var v = SubParam(raw);
            return v.name != "" && v.asArray == "" ? v.asType : "";
        }
        var call = Regex.Match(raw, "^(" + Id + ")\\$?\\(");
        if (call.Success && MatchParen(raw, call.Length - 1) == raw.Length - 1)
        {
            var fn = call.Groups[1].Value;
            if (NumericFunctions.Contains(fn)) return "Double";
            if (RefScanner.IsFuncRef(fn)) return RefScanner.FuncRefDeclRet(fn);
            var arr = SubParam(fn);
            if (arr.name != "" && arr.asArray != "") return arr.asType; // an array element
        }
        return "";
    }

    private static bool IsNumericType(string t) => t == "Integer" || t == "Long" || t == "Byte" || t == "Single" || t == "Double" || t == "Currency";

    private static readonly string[] NumericRank = { "Byte", "Integer", "Long", "Currency", "Single", "Double" };

    /// <summary>
    /// VB6 type of an expression, from its operands: comparisons are Boolean, &amp; String, "/" and "^" Double, arithmetic
    /// the widest operand (Currency with Single / Double is Double). "" when an operand's type is unknown.
    /// </summary>
    public static string ExprType(string raw)
    {
        raw = Trim(raw);
        while (raw.Length > 1 && raw[0] == '(' && MatchParen(raw, 0) == raw.Length - 1) raw = Trim(raw.Substring(1, raw.Length - 2));
        if (LMatch(raw, "-")) return ExprType(Mid(raw, 2));
        var single = OperandType(raw);
        if (single != "" || SplitTopLevel(raw, ' ').Count == 1) return single;
        var types = new List<string>();
        var ops = new List<string>();
        var s = raw;
        var op = "";
        for (var guard = 0; guard < 200; guard++)
        {
            var f = NextByOp(s, 1, ref op);
            if (f == "") break;
            if (string.IsNullOrEmpty(op) && ops.Count == 0) return ""; // no operator: a term this analysis cannot type
            types.Add(LMatch(Trim(f), "Not ") ? ExprType(Mid(Trim(f), 5)) : ExprType(f));
            if (string.IsNullOrEmpty(op)) break;
            ops.Add(Trim(op));
            s = Mid(s, Len(f) + Len(op) + 1);
            if (s == "") break;
        }
        if (ops.Count == 0) return "";
        if (ops.Exists(o => o == "=" || o == "<>" || o == "<" || o == ">" || o == "<=" || o == ">=" || o == "Is" || o == "Like")) return "Boolean";
        if (ops.Exists(o => o == "&")) return "String";
        if (ops.Exists(o => o == "And" || o == "Or" || o == "Xor" || o == "Eqv" || o == "Imp"))
        {
            return types.TrueForAll(t => t == "Boolean") ? "Boolean" : types.TrueForAll(IsNumericType) ? "Long" : "";
        }
        if (ops.Exists(o => o == "/" || o == "^")) return types.TrueForAll(t => t == "" || IsNumericType(t)) ? "Double" : "";
        if (!types.TrueForAll(IsNumericType)) return types.Exists(t => t == "String") && ops.TrueForAll(o => o == "+") ? "String" : "";
        if (types.Contains("Currency") && (types.Contains("Double") || types.Contains("Single"))) return "Double";
        var rank = 0;
        foreach (var t in types) rank = Math.Max(rank, Array.IndexOf(NumericRank, t));
        return ops.Exists(o => o == "\\" || o == "Mod") && rank <= 2 ? "Long" : NumericRank[rank];
    }

    private static bool IsLogical(string op) => op == "And" || op == "Or" || op == "Xor" || op == "Eqv" || op == "Imp";

    /// <summary>
    /// String comparisons (an operand typed String, the whole side of the comparison): under Option Compare Text all of them
    /// ignore case (TextCompare); relational ones, which C# does not define on strings, compare ordinally otherwise.
    /// </summary>
    public static void FoldStringComparisons(List<string> raws, List<string> parts, List<string> ops)
    {
        for (var k = 0; k < ops.Count; k++)
        {
            var op = ops[k];
            if (!(op == "=" || op == "<>" || op == "<" || op == ">" || op == "<=" || op == ">=")) continue;
            if (!(k == 0 || IsLogical(ops[k - 1])) || !(k + 1 == ops.Count || IsLogical(ops[k + 1]))) continue;
            if (OperandType(raws[k]) != "String" && OperandType(raws[k + 1]) != "String") continue;
            var cs = op == "=" ? "==" : op == "<>" ? "!=" : op;
            string folded;
            if (OptionCompareText) folded = "TextCompare(" + parts[k] + ", " + parts[k + 1] + ") " + cs + " 0";
            else if (op == "=" || op == "<>") continue; // C# string equality is VB6 Binary compare
            else folded = "string.CompareOrdinal(" + parts[k] + ", " + parts[k + 1] + ") " + cs + " 0";
            parts[k] = folded;
            raws[k] = "True";
            parts.RemoveAt(k + 1);
            raws.RemoveAt(k + 1);
            ops.RemoveAt(k);
            k--;
        }
    }

    /// <summary>Option Compare Text: the compare argument the VB6 string functions default to, appended after <paramref name="argCount"/> arguments.</summary>
    public static string CompareArgument(string function, int argCount)
    {
        if (!OptionCompareText) return "";
        const string text = "CompareMethod.Text";
        switch (function)
        {
            case "InStr": return argCount == 2 || argCount == 3 ? ", " + text : ""; // InStr(s1, s2, compare) and InStr(start, s1, s2, compare)
            case "InStrRev": return argCount == 2 ? ", -1, " + text : argCount == 3 ? ", " + text : "";
            case "StrComp": return argCount == 2 ? ", " + text : "";
            case "Replace": return argCount == 3 ? ", 1, -1, " + text : argCount == 4 ? ", -1, " + text : argCount == 5 ? ", " + text : "";
            case "Split": return argCount == 1 ? ", \" \", -1, " + text : argCount == 2 ? ", -1, " + text : argCount == 3 ? ", " + text : "";
            case "Filter": return argCount == 2 ? ", true, " + text : argCount == 3 ? ", " + text : "";
            default: return "";
        }
    }

    /// <summary>C# cannot mix decimal with float / double: in such an expression Currency operands become double (VB6 gives a Double).</summary>
    public static void PromoteCurrency(List<string> raws, List<string> parts)
    {
        var types = raws.ConvertAll(OperandType);
        if (!types.Contains("Currency") || !(types.Contains("Double") || types.Contains("Single"))) return;
        for (var i = 0; i < parts.Count && i < types.Count; i++)
        {
            if (types[i] == "Currency") parts[i] = "(double)" + parts[i];
        }
    }

    /// <summary>VB6 conversion functions as VB Migration Partner maps them (CInt returns a 16-bit Integer).</summary>
    public static string ConversionFunction(string name)
    {
        switch (name)
        {
            case "CStr": return "Conversions.ToString";
            case "CInt": return "Conversions.ToShort";
            case "CLng": return "Conversions.ToInteger";
            case "CByte": return "Conversions.ToByte";
            case "CSng": return "Conversions.ToSingle";
            case "CDbl": return "Conversions.ToDouble";
            case "CCur": return "Conversions.ToDecimal";
            case "CDec": return "Conversions.ToDecimal";
            case "CBool": return "Conversions.ToBoolean";
            case "CDate": case "CVDate": return "Conversions.ToDate";
            case "CVar": return "(object)";
            default: return null;
        }
    }

    /// <summary>
    /// The value assigned to a <paramref name="target"/>-typed variable, converted the way VB6 converts implicitly
    /// (Microsoft.VisualBasic Conversions: rounding to even, string parsing), where C# would not compile or would differ.
    /// </summary>
    public static string ImplicitConversion(string target, string raw, string cs)
    {
        var source = ExprType(raw);
        if (source == target || cs == "null") return cs;
        var intLiteral = Regex.IsMatch(Trim(raw), "^-?[0-9]+$");
        var floating = cs.Contains("Pow(") || source == "Double" || source == "Single" || source == "Currency";
        string To(string fn) => "Conversions." + fn + "(" + cs + ")";
        switch (target)
        {
            case "Integer": return intLiteral ? cs : To("ToShort");
            case "Byte": return intLiteral ? cs : To("ToByte");
            case "Single": return intLiteral ? cs : To("ToSingle");
            case "Currency": return intLiteral ? cs : To("ToDecimal");
            case "Long":
                return source == "Integer" || source == "Byte" || intLiteral || source == "" && !floating ? cs : To("ToInteger");
            case "Double":
                return source == "" || IsNumericType(source) && source != "Currency" ? cs : To("ToDouble");
            case "Variant":
            case "Object":
                return cs;
            case "String":
                return source == "" ? cs : To("ToString");
            case "Boolean":
                return source == "" ? cs : To("ToBoolean");
            case "Date":
                return source == "" ? cs : To("ToDate");
            default:
                return cs;
        }
    }

    /// <summary>The left operand of VB6 "/" as a double (so the division is not an integer one).</summary>
    public static string AsDouble(string raw, string cs)
    {
        var type = OperandType(raw);
        if (type == "Double" || type == "Single" || cs.Contains("Pow(")) return cs;
        if (Regex.IsMatch(Trim(raw), "^-?[0-9]+$")) return cs + ".0";
        if (type == "Integer" || type == "Long" || type == "Byte" || type == "Currency") return "(double)" + cs;
        return "Conversions.ToDouble(" + cs + ")";
    }

    /// <summary>
    /// A VB6 condition (If / While / Until) as a C# bool: a numeric or Variant operand is tested against zero as VB6 does;
    /// Not on a number stays bitwise (Not 5 is -6, which is True).
    /// </summary>
    public static string ConditionValue(string vb)
    {
        vb = Trim(vb);
        var negate = false;
        var inner = vb;
        if (LMatch(inner, "Not ") && SplitTopLevel(Mid(inner, 5), ' ').Count == 1)
        {
            negate = true;
            inner = Trim(Mid(inner, 5));
        }
        var type = SplitTopLevel(inner, ' ').Count == 1 ? OperandType(inner) : "";
        if (IsNumericType(type))
        {
            var cs = ConvertValue(inner);
            // VB6 Not converts to Long rounding to even (a cast would truncate: Not -0.6 is Not -1)
            return negate ? "~(" + (type == "Integer" || type == "Long" || type == "Byte" ? cs : "Conversions.ToLong(" + cs + ")") + ") != 0" : cs + " != 0";
        }
        if (type == "Variant" || type == "String")
        {
            var cs = "Conversions.ToBoolean(" + ConvertValue(inner) + ")";
            return negate ? "!" + cs : cs;
        }
        return ConvertValue(vb);
    }

    /// <summary>C# keywords a VB6 identifier can be (they are not VB6 keywords); VB6 is case-insensitive, C# keywords are lowercase.</summary>
    private static readonly Regex CsOnlyKeywords = new Regex(
        "(?<![A-Za-z0-9_.])(abstract|base|bool|break|catch|char|checked|class|continue|decimal|default|delegate|explicit|extern|finally|fixed|float|foreach"
        + "|implicit|int|interface|internal|lock|namespace|null|operator|out|override|params|protected|readonly|ref|sbyte|sealed|short|sizeof"
        + "|stackalloc|struct|switch|this|throw|try|uint|ulong|unchecked|unsafe|ushort|using|virtual|void|volatile)(?![A-Za-z0-9_])");

    /// <summary>
    /// Renames VB6 identifiers that are C# keywords (<c>fixed</c> → <c>fixed_</c>), consistently in declarations and uses.
    /// Applied to de-stringed, de-commented code (string literals are tokens and stay untouched).
    /// </summary>
    public static string EscapeKeywords(string line) => string.IsNullOrEmpty(line) ? line : CsOnlyKeywords.Replace(line, "$1_");

    // ---------------------------------------------------------------- lexical helpers

    /// <summary>Splits on <paramref name="sep"/> outside parentheses (strings are tokens, so they never contain it).</summary>
    public static List<string> SplitTopLevel(string s, char sep = ',')
    {
        var r = new List<string>();
        var depth = 0;
        var b = new StringBuilder();
        foreach (var c in s ?? "")
        {
            if (c == '(') depth++;
            else if (c == ')') depth--;
            if (c == sep && depth == 0)
            {
                r.Add(b.ToString().Trim());
                b.Clear();
                continue;
            }
            b.Append(c);
        }
        r.Add(b.ToString().Trim());
        return r;
    }

    /// <summary>Index of the ')' closing the '(' at <paramref name="open"/> (0-based), or -1.</summary>
    public static int MatchParen(string s, int open)
    {
        var depth = 0;
        for (var i = open; i < s.Length; i++)
        {
            if (s[i] == '(') depth++;
            else if (s[i] == ')' && --depth == 0) return i;
        }
        return -1;
    }

    /// <summary>VB6 type of a type-declaration character (<c>$ % &amp; ! # @</c>), or "".</summary>
    public static string SuffixType(char c)
    {
        switch (c)
        {
            case '$': return "String";
            case '%': return "Integer";
            case '&': return "Long";
            case '!': return "Single";
            case '#': return "Double";
            case '@': return "Currency";
            default: return "";
        }
    }

    /// <summary>Removes a trailing type character from an identifier; returns the implied VB6 type ("" if none).</summary>
    public static string StripSuffix(ref string name)
    {
        name = Trim(name);
        if (name.Length < 2) return "";
        var t = SuffixType(name[name.Length - 1]);
        if (t != "" && Regex.IsMatch(name.Substring(0, name.Length - 1), "^" + Id + "$")) name = name.Substring(0, name.Length - 1);
        else t = "";
        return t;
    }

    /// <summary>Removes type characters from identifiers and numeric literals in an expression / statement.</summary>
    public static string StripTypeSuffixes(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        // date literals (#10:30 PM#) are not suffixed names
        var dates = new List<string>();
        s = Regex.Replace(s, DateLiteralPattern, m =>
        {
            dates.Add(m.Value);
            return "" + (dates.Count - 1) + "";
        });
        // identifier suffix: not followed by an identifier char (keeps RS!Field bang access), not "&H" literals
        s = Regex.Replace(s, "(?<![A-Za-z0-9_&])(" + Id + ")[$%&!#@](?![A-Za-z0-9_(]|H[0-9A-Fa-f])", "$1");
        s = Regex.Replace(s, "(?<![A-Za-z0-9_&])(" + Id + ")\\$(?=\\()", "$1"); // Left$( … )
        // numeric literal suffix (1#, 2&, 3!, 4@, 5%)
        s = Regex.Replace(s, "(?<![A-Za-z_0-9#/:&.])([0-9]+(\\.[0-9]+)?)[%&!@#](?![A-Za-z0-9_#/])", "$1");
        return Regex.Replace(s, "([0-9]+)", m => dates[int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)]);
    }

    /// <summary>VB6 date/time literal: #m/d/yyyy#, #h:mm[:ss] [AM|PM]#, or both.</summary>
    public const string DateLiteralPattern = "#([0-9]{1,4}[/-][0-9]{1,2}[/-][0-9]{1,4}( +[0-9]{1,2}:[0-9]{2}(:[0-9]{2})?( ?[AaPp][Mm])?)?|[0-9]{1,2}:[0-9]{2}(:[0-9]{2})?( ?[AaPp][Mm])?)#";

    /// <summary>VB6 &amp;H / &amp;O literal to C#, honouring VB typing (&amp;HFFFF is Integer -1, &amp;HFFFF&amp; is 65535).</summary>
    public static string ConvertRadixLiteral(string s)
    {
        var m = Regex.Match(Trim(s), "^&([HhOo])([0-9A-Fa-f]+)([&%]?)$");
        if (!m.Success) return null;
        var hex = char.ToUpperInvariant(m.Groups[1].Value[0]) == 'H';
        long v;
        try { v = System.Convert.ToInt64(m.Groups[2].Value, hex ? 16 : 8); }
        catch (Exception) { return null; }
        var asLong = m.Groups[3].Value == "&" || v > 0xFFFF;
        if (asLong && v > 0x7FFFFFFF && v <= 0xFFFFFFFF) v -= 0x100000000;
        else if (!asLong && v > 0x7FFF) v -= 0x10000;
        if (v < 0) return v.ToString(CultureInfo.InvariantCulture);
        return hex ? "0x" + v.ToString("X", CultureInfo.InvariantCulture) : v.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>VB6 date/time literals (#m/d/yyyy#, #h:mm:ss AM#, both) to an invariant-culture parse.</summary>
    public static string ConvertDateLiterals(string s)
    {
        return Regex.Replace(s, DateLiteralPattern, m => "DateTime.Parse(\"" + m.Groups[1].Value + "\", System.Globalization.CultureInfo.InvariantCulture)");
    }

    /// <summary>Regex matching VB6 time literals (they contain ':' and must survive statement splitting).</summary>
    public const string TimeLiteralPattern = "#[0-9/ -]*[0-9]{1,2}:[0-9]{2}(:[0-9]{2})?( ?[AaPp][Mm])?#";

    /// <summary>C# label for a VB6 label or line number (C# labels cannot start with a digit).</summary>
    public static string LabelName(string s)
    {
        s = Trim(s);
        return Regex.IsMatch(s, "^[0-9]+$") ? "L" + s : s;
    }

    // ---------------------------------------------------------------- conditional compilation

    public static bool IsDirective(string t) => Regex.IsMatch(Trim(t), "^#(If|ElseIf|Else|End If|Const)\\b", RegexOptions.IgnoreCase);

    /// <summary>
    /// Conditional-compilation constants of the file being converted: the project's (VBP CondComp) overlaid with the
    /// module's #Const values. Names are case-insensitive, as in VB6.
    /// </summary>
    private static Dictionary<string, object> ppConsts = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

    /// <summary>C# symbol of a VB6 constant: C# symbols are case-sensitive, VB6 ones are not.</summary>
    private static readonly Dictionary<string, string> ppNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static string SymbolName(string name) => ppNames.TryGetValue(name, out var n) ? n : name;

    /// <summary>
    /// Starts converting a file: collects its #Const values (on top of the project's) and returns the #define / #undef
    /// lines that must open the C# file. VB6 #Const is private to its module, as a C# #define is to its file.
    /// </summary>
    public static string BeginFile(string vbSource, IDictionary<string, string> projectConstants = null)
    {
        ResetFileOptions();
        ClassesConverter.Current = null;
        OptionExplicit = string.IsNullOrEmpty(vbSource) || Regex.IsMatch(vbSource, "(?mi)^Option Explicit");
        FileSource = vbSource ?? "";
        ppConsts = ProjectConstantValues(projectConstants);
        var header = new StringBuilder();
        var defined = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in (vbSource ?? "").Replace("\r\n", "\n").Split('\n'))
        {
            var m = Regex.Match(StripComment(raw), "^\\s*#Const\\s+(" + Id + ")\\s*=\\s*(.+)$", RegexOptions.IgnoreCase);
            if (!m.Success) continue;
            var name = m.Groups[1].Value;
            var v = Evaluate(m.Groups[2].Value) ?? 0.0;
            ppConsts[name] = v;
            if (!ppNames.ContainsKey(name)) ppNames[name] = name;
            var on = IsTrue(v);
            if (defined.TryGetValue(name, out var was) && was != on)
            {
                header.Append("// TODO: VB6 #Const " + name + " changes value within the module (C# symbols are file-wide)\r\n");
            }
            defined[name] = on;
        }
        foreach (var d in defined) header.Append((d.Value ? "#define " : "#undef ") + SymbolName(d.Key) + "\r\n");
        PragmaConverter.BeginFile(vbSource); // project / file-level pragmas
        return header.ToString();
    }

    /// <summary>Evaluated project constants (VBP CondComp, "Name = value").</summary>
    public static Dictionary<string, object> ProjectConstantValues(IDictionary<string, string> projectConstants)
    {
        var r = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (projectConstants == null) return r;
        foreach (var c in projectConstants)
        {
            ppNames[c.Key] = c.Key;
            ppConsts = r; // later constants may use earlier ones
            r[c.Key] = Evaluate(c.Value) ?? 0.0;
        }
        return r;
    }

    /// <summary>Project constants that are true, for the C# project's DefineConstants.</summary>
    public static List<string> ProjectSymbols(IDictionary<string, string> projectConstants)
    {
        var r = new List<string>();
        foreach (var c in ProjectConstantValues(projectConstants))
        {
            if (IsTrue(c.Value)) r.Add(c.Key);
        }
        return r;
    }

    private static string StripComment(string l)
    {
        var q = false;
        for (var i = 0; i < l.Length; i++)
        {
            if (l[i] == '"') q = !q;
            else if (l[i] == '\'' && !q) return l.Substring(0, i);
        }
        return l;
    }

    /// <summary>#If/#ElseIf/#Else/#End If/#Const to the C# preprocessor.</summary>
    public static string ConvertDirective(string t)
    {
        t = Trim(StripComment(Trim(t)));
        Match m;
        if ((m = Regex.Match(t, "^#If (.*) Then$", RegexOptions.IgnoreCase)).Success) return "#if " + PreprocessorCondition(m.Groups[1].Value);
        if ((m = Regex.Match(t, "^#ElseIf (.*) Then$", RegexOptions.IgnoreCase)).Success) return "#elif " + PreprocessorCondition(m.Groups[1].Value);
        if (Regex.IsMatch(t, "^#Else$", RegexOptions.IgnoreCase)) return "#else";
        if (Regex.IsMatch(t, "^#End ?If$", RegexOptions.IgnoreCase)) return "#endif";
        if ((m = Regex.Match(t, "^#Const\\s+(" + Id + ")", RegexOptions.IgnoreCase)).Success)
        {
            return "// VB6 " + t + " (#define / #undef " + SymbolName(m.Groups[1].Value) + " at the top of the file)";
        }
        return "// TODO: VB6 directive not converted: " + t;
    }

    /// <summary>
    /// A live C# condition when the VB6 one is boolean over symbols (so it can still be toggled through the C# symbols);
    /// otherwise the VB6 condition evaluated now against the known constants.
    /// </summary>
    private static string PreprocessorCondition(string c)
    {
        c = Trim(c);
        var vb = Evaluate(c);
        var live = LiveCondition(c, out var liveTruth);
        if (live != null && vb != null && liveTruth == IsTrue(vb)) return live;
        if (vb == null) return "false // TODO: VB6 condition not evaluated: " + c;
        return (IsTrue(vb) ? "true" : "false") + " // VB6: " + c;
    }

    /// <summary>Translates Not/And/Or over symbols and "Symbol = True|False|0|n"; also returns its value for the current constants.</summary>
    private static string LiveCondition(string c, out bool truth)
    {
        truth = false;
        var toks = Tokenize(c);
        if (toks == null) return null;
        var cs = new StringBuilder();
        var check = new StringBuilder(); // the same condition over the symbols' truth values, in VB syntax
        for (var i = 0; i < toks.Count; i++)
        {
            var k = toks[i];
            if (k == "(" || k == ")")
            {
                cs.Append(k);
                check.Append(k);
            }
            else if (Eq(k, "Not"))
            {
                cs.Append("!");
                check.Append(" Not ");
            }
            else if (Eq(k, "And") || Eq(k, "Or"))
            {
                cs.Append(Eq(k, "And") ? " && " : " || ");
                check.Append(" " + k + " ");
            }
            else if (Regex.IsMatch(k, "^" + Id + "$") && !IsKeyword(k))
            {
                var sym = Eq(k, "Win32") ? "true" : Eq(k, "Win16") ? "false" : SymbolName(k);
                var value = Eq(k, "Win32") ? -1.0 : Eq(k, "Win16") ? 0.0 : ToNumber(ppConsts.TryGetValue(k, out var v) ? v : 0.0);
                var positive = true;
                if (i + 2 < toks.Count && (toks[i + 1] == "=" || toks[i + 1] == "<>"))
                { // Symbol = literal: a symbol test only when the constant is 0 or that literal
                    var lit = toks[i + 2];
                    double l;
                    if (Eq(lit, "True")) l = -1;
                    else if (Eq(lit, "False")) l = 0;
                    else if (!double.TryParse(lit, NumberStyles.Float, CultureInfo.InvariantCulture, out l)) return null;
                    if (l != 0 && value != 0 && value != l) return null;
                    positive = (l != 0) == (toks[i + 1] == "=");
                    i += 2;
                }
                cs.Append((positive ? "" : "!") + sym);
                check.Append((positive ? "" : " Not ") + (value != 0 ? "-1" : "0"));
            }
            else
            {
                return null;
            }
        }
        var r = Evaluate(check.ToString());
        if (r == null) return null;
        truth = IsTrue(r);
        return cs.ToString();
    }

    private static bool Eq(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static bool IsKeyword(string k)
    {
        switch (k.ToLowerInvariant())
        {
            case "not": case "and": case "or": case "xor": case "eqv": case "imp": case "mod": case "true": case "false": return true;
            default: return false;
        }
    }

    private static bool IsTrue(object v) => v is string s ? s != "" : ToNumber(v) != 0;

    private static double ToNumber(object v)
    {
        if (v is double d) return d;
        return double.TryParse(v as string, NumberStyles.Float, CultureInfo.InvariantCulture, out var r) ? r : 0;
    }

    private static List<string> Tokenize(string s)
    {
        var r = new List<string>();
        var re = new Regex("\\G\\s*(&[Hh][0-9A-Fa-f]+&?|&[Oo][0-7]+&?|[0-9]+(\\.[0-9]+)?|\"([^\"]|\"\")*\"|" + Id + "|<>|<=|>=|[-+*/\\\\^&=<>()])");
        var pos = 0;
        while (pos < s.Length)
        {
            if (s.Substring(pos).Trim() == "") break;
            var m = re.Match(s, pos);
            if (!m.Success) return null;
            r.Add(m.Groups[1].Value);
            pos = m.Index + m.Length;
        }
        return r;
    }

    /// <summary>
    /// Evaluates a VB6 conditional-compilation expression (numbers, strings, constants, all operators) with VB6 semantics:
    /// True is -1, logical operators are bitwise, an undefined constant is Empty (0). Null when not an expression.
    /// </summary>
    public static object Evaluate(string expr)
    {
        var toks = Tokenize(expr ?? "");
        if (toks == null || toks.Count == 0) return null;
        var p = 0;
        try
        {
            var v = ParseImp(toks, ref p);
            return p == toks.Count ? v : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool Next(List<string> t, ref int p, string k)
    {
        if (p < t.Count && Eq(t[p], k))
        {
            p++;
            return true;
        }
        return false;
    }

    private static long L(object v) => (long)Math.Round(ToNumber(v), MidpointRounding.ToEven);

    private static object Bool(bool b) => b ? -1.0 : 0.0;

    private static object ParseImp(List<string> t, ref int p)
    {
        var a = ParseEqv(t, ref p);
        while (Next(t, ref p, "Imp")) { var b = ParseEqv(t, ref p); a = (double)(~L(a) | L(b)); }
        return a;
    }

    private static object ParseEqv(List<string> t, ref int p)
    {
        var a = ParseXor(t, ref p);
        while (Next(t, ref p, "Eqv")) { var b = ParseXor(t, ref p); a = (double)~(L(a) ^ L(b)); }
        return a;
    }

    private static object ParseXor(List<string> t, ref int p)
    {
        var a = ParseOr(t, ref p);
        while (Next(t, ref p, "Xor")) { var b = ParseOr(t, ref p); a = (double)(L(a) ^ L(b)); }
        return a;
    }

    private static object ParseOr(List<string> t, ref int p)
    {
        var a = ParseAnd(t, ref p);
        while (Next(t, ref p, "Or")) { var b = ParseAnd(t, ref p); a = (double)(L(a) | L(b)); }
        return a;
    }

    private static object ParseAnd(List<string> t, ref int p)
    {
        var a = ParseNot(t, ref p);
        while (Next(t, ref p, "And")) { var b = ParseNot(t, ref p); a = (double)(L(a) & L(b)); }
        return a;
    }

    private static object ParseNot(List<string> t, ref int p)
    {
        if (Next(t, ref p, "Not")) return (double)~L(ParseNot(t, ref p));
        return ParseCompare(t, ref p);
    }

    private static object ParseCompare(List<string> t, ref int p)
    {
        var a = ParseConcat(t, ref p);
        while (p < t.Count && (t[p] == "=" || t[p] == "<>" || t[p] == "<" || t[p] == ">" || t[p] == "<=" || t[p] == ">="))
        {
            var op = t[p++];
            var b = ParseConcat(t, ref p);
            var cmp = a is string || b is string ? string.CompareOrdinal(a as string ?? ToNumber(a).ToString(CultureInfo.InvariantCulture), b as string ?? ToNumber(b).ToString(CultureInfo.InvariantCulture)) : ToNumber(a).CompareTo(ToNumber(b));
            a = Bool(op == "=" ? cmp == 0 : op == "<>" ? cmp != 0 : op == "<" ? cmp < 0 : op == ">" ? cmp > 0 : op == "<=" ? cmp <= 0 : cmp >= 0);
        }
        return a;
    }

    private static object ParseConcat(List<string> t, ref int p)
    {
        var a = ParseAdd(t, ref p);
        while (Next(t, ref p, "&")) { var b = ParseAdd(t, ref p); a = Text(a) + Text(b); }
        return a;
    }

    private static string Text(object v) => v as string ?? ToNumber(v).ToString(CultureInfo.InvariantCulture);

    private static object ParseAdd(List<string> t, ref int p)
    {
        var a = ParseMod(t, ref p);
        while (p < t.Count && (t[p] == "+" || t[p] == "-"))
        {
            var op = t[p++];
            var b = ParseMod(t, ref p);
            a = op == "+" && (a is string || b is string) ? Text(a) + Text(b) : op == "+" ? ToNumber(a) + ToNumber(b) : ToNumber(a) - ToNumber(b);
        }
        return a;
    }

    private static object ParseMod(List<string> t, ref int p)
    {
        var a = ParseIntDiv(t, ref p);
        while (Next(t, ref p, "Mod")) { var b = ParseIntDiv(t, ref p); a = (double)(L(a) % L(b)); }
        return a;
    }

    private static object ParseIntDiv(List<string> t, ref int p)
    {
        var a = ParseMul(t, ref p);
        while (Next(t, ref p, "\\")) { var b = ParseMul(t, ref p); a = (double)(L(a) / L(b)); }
        return a;
    }

    private static object ParseMul(List<string> t, ref int p)
    {
        var a = ParseUnary(t, ref p);
        while (p < t.Count && (t[p] == "*" || t[p] == "/"))
        {
            var op = t[p++];
            var b = ParseUnary(t, ref p);
            if (op == "/" && ToNumber(b) == 0) throw new DivideByZeroException(); // VB6 error, not Infinity
            a = op == "*" ? ToNumber(a) * ToNumber(b) : ToNumber(a) / ToNumber(b);
        }
        return a;
    }

    private static object ParseUnary(List<string> t, ref int p)
    {
        if (Next(t, ref p, "-")) return -ToNumber(ParseUnary(t, ref p));
        if (Next(t, ref p, "+")) return ToNumber(ParseUnary(t, ref p));
        return ParsePow(t, ref p);
    }

    private static object ParsePow(List<string> t, ref int p)
    {
        var a = ParseAtom(t, ref p);
        while (Next(t, ref p, "^")) { var b = ParseAtom(t, ref p); a = Math.Pow(ToNumber(a), ToNumber(b)); }
        return a;
    }

    private static object ParseAtom(List<string> t, ref int p)
    {
        var k = t[p++];
        if (k == "(")
        {
            var v = ParseImp(t, ref p);
            if (!Next(t, ref p, ")")) throw new FormatException(")");
            return v;
        }
        if (k.StartsWith("\"")) return k.Substring(1, k.Length - 2).Replace("\"\"", "\"");
        if (k.StartsWith("&"))
        {
            var r = ConvertRadixLiteral(k);
            return r.StartsWith("0x") ? (double)System.Convert.ToInt64(r.Substring(2), 16) : double.Parse(r, CultureInfo.InvariantCulture);
        }
        if (char.IsDigit(k[0])) return double.Parse(k, CultureInfo.InvariantCulture);
        if (Eq(k, "True") || Eq(k, "Win32")) return -1.0; // VB6 is always Win32
        if (Eq(k, "False") || Eq(k, "Win16")) return 0.0;
        if (Regex.IsMatch(k, "^" + Id + "$") && !IsKeyword(k)) return ppConsts.TryGetValue(k, out var c) ? c : 0.0; // undefined: Empty
        throw new FormatException(k);
    }

    // ---------------------------------------------------------------- error handling

    /// <summary>Error-handling state of the procedure being converted (VB6 On Error is procedure-scoped).</summary>
    public sealed class ErrorScope
    {
        public enum Modes { None, ResumeNext, GoTo }
        public Modes Mode = Modes.None;
        public string Handler = "";
        public bool TryOpen;
        public int TryInd;
        public string TryHandler = "";
        /// <summary>The procedure has line numbers: Erl reports the last one passed (vbErl).</summary>
        public bool Erl;
        /// <summary>The GoTo handler resumes (Resume / Resume Next): statements are guarded one by one, the handler is a local function.</summary>
        public bool Resumable;

        public string ProjectError() => SetProjectError + "(" + CatchVar + (Erl ? ", vbErl" : "") + ");";

        public string OpenTry(ref int ind)
        {
            TryOpen = true;
            TryInd = ind;
            TryHandler = Handler;
            ind = ind + spIndent;
            return SSpace(TryInd) + "try {" + vbCrLf;
        }

        public string CloseTry(ref int ind)
        {
            if (!TryOpen) return "";
            TryOpen = false;
            ind = TryInd;
            var i = SSpace(ind + spIndent);
            return SSpace(ind) + "} catch (Exception " + CatchVar + ") {" + vbCrLf
                + i + ProjectError() + vbCrLf
                + i + "goto " + TryHandler + ";" + vbCrLf
                + SSpace(ind) + "}" + vbCrLf;
        }

        /// <summary>Closes the protected block if it is open at the current nesting level (a nested one closes at its block end).</summary>
        public string CloseTryHere(ref int ind) => TryOpen && ind == TryInd + spIndent ? CloseTry(ref ind) : "";
    }

    /// <summary>Wraps a statement so a failure sets Err and execution continues (On Error Resume Next).</summary>
    public static string WrapResumeNext(string stmt, int ind, ErrorScope scope = null)
    {
        var i = SSpace(ind);
        return i + "try {" + vbCrLf
            + SSpace(ind + spIndent) + Trim(stmt) + vbCrLf
            + i + "} catch (Exception " + CatchVar + ") { " + (scope ?? new ErrorScope()).ProjectError() + " }";
    }

    /// <summary>Name of the local function a resumable error handler becomes.</summary>
    public static string HandlerFunction(string label) => "vbHandler_" + label;

    /// <summary>
    /// A statement under a resumable On Error GoTo handler: on failure the handler (a local function) runs and its code
    /// says how to go on: 0 Resume Next (continue), 1 Resume (retry), -1 leave the procedure, k Resume label k.
    /// </summary>
    public static string WrapResumable(string stmt, int ind, ErrorScope scope, ProcedurePlan plan, string exitStatement, ref int retries)
    {
        var i = SSpace(ind);
        var j = SSpace(ind + spIndent);
        var h = plan.Handlers[scope.Handler];
        var retry = "";
        var cases = new List<string> { "case -1: " + exitStatement };
        if (h.Retry)
        {
            retries++;
            retry = "vbRetry" + retries;
            cases.Insert(0, "case 1: goto " + retry + ";");
        }
        for (var k = 0; k < h.ResumeLabels.Count; k++) cases.Add("case " + (k + 2) + ": goto " + h.ResumeLabels[k] + ";");
        return (retry == "" ? "" : i + retry + ":" + vbCrLf)
            + i + "try {" + vbCrLf
            + j + Trim(stmt) + vbCrLf
            + i + "} catch (Exception " + CatchVar + ") {" + vbCrLf
            + j + scope.ProjectError() + vbCrLf
            + j + "switch (" + HandlerFunction(scope.Handler) + "()) { " + string.Join(" ", cases) + " }" + vbCrLf
            + i + "}";
    }

    /// <summary>A resumable error handler: whether it retries (Resume) and the labels it resumes at.</summary>
    public sealed class HandlerInfo
    {
        public bool Retry;
        public readonly List<string> ResumeLabels = new List<string>();
    }

    /// <summary>
    /// What a procedure needs before it is converted line by line: GoSub routines and resumable handlers (converted to
    /// local functions), jump targets (they end such sections) and line numbers (for Erl).
    /// </summary>
    public sealed class ProcedurePlan
    {
        public readonly HashSet<string> GoSubTargets = new HashSet<string>();
        public readonly Dictionary<string, HandlerInfo> Handlers = new Dictionary<string, HandlerInfo>();
        public readonly HashSet<string> JumpTargets = new HashSet<string>();
        public bool HasLineNumbers;
        public bool HasErrorHandling;

        /// <summary>A label whose code becomes a local function (GoSub routine, resumable handler).</summary>
        public bool IsRouted(string label) => GoSubTargets.Contains(label) || Handlers.ContainsKey(label);

        public static ProcedurePlan Scan(IEnumerable<string> lines)
        {
            var plan = new ProcedurePlan();
            var list = new List<string>();
            foreach (var raw in lines) list.Add(Trim(StripComment(raw)));
            var handlerLabels = new HashSet<string>();
            foreach (var t in list)
            {
                Match m;
                if (Regex.IsMatch(t, "^L[0-9]+:$")) plan.HasLineNumbers = true;
                if ((m = Regex.Match(t, "^On (Local )?Error GoTo (.+)$")).Success)
                {
                    plan.HasErrorHandling = true;
                    var target = Trim(m.Groups[2].Value);
                    if (target != "0" && target != "-1") handlerLabels.Add(LabelName(target));
                }
                else if (LMatch(t, "On Error ") || LMatch(t, "On Local Error ")) plan.HasErrorHandling = true;
                else if ((m = Regex.Match(t, "^GoSub (" + Id + "|[0-9]+)$")).Success) plan.GoSubTargets.Add(LabelName(m.Groups[1].Value));
                else if ((m = Regex.Match(t, "^On .+ GoSub (.+)$")).Success)
                {
                    foreach (var l in SplitTopLevel(m.Groups[1].Value)) if (l != "") plan.GoSubTargets.Add(LabelName(l));
                }
                if ((m = Regex.Match(t, "^(?:GoTo|Resume) (" + Id + "|[0-9]+)$")).Success && m.Groups[1].Value != "Next" && m.Groups[1].Value != "0")
                {
                    plan.JumpTargets.Add(LabelName(m.Groups[1].Value));
                }
                if ((m = Regex.Match(t, "^On .+ GoTo (.+)$")).Success && !LMatch(t, "On Error") && !LMatch(t, "On Local Error"))
                {
                    foreach (var l in SplitTopLevel(m.Groups[1].Value)) if (l != "") plan.JumpTargets.Add(LabelName(l));
                }
            }
            // a handler section runs from its label to the next jump target / routed label, or the procedure end
            foreach (var h in handlerLabels)
            {
                var start = list.IndexOf(h + ":");
                if (start < 0) continue;
                var info = new HandlerInfo();
                var resumes = false;
                for (var i = start + 1; i < list.Count; i++)
                {
                    var t = list[i];
                    var label = Regex.Match(t, "^(" + Id + "):$");
                    if (label.Success && (plan.JumpTargets.Contains(label.Groups[1].Value) || handlerLabels.Contains(label.Groups[1].Value) || plan.GoSubTargets.Contains(label.Groups[1].Value))) break;
                    if (Regex.IsMatch(t, "^End (Sub|Function|Property)$")) break;
                    if (t == "Resume" || t == "Resume 0")
                    {
                        resumes = true;
                        info.Retry = true;
                    }
                    else if (t == "Resume Next")
                    {
                        resumes = true;
                    }
                    else if (LMatch(t, "Resume ") && !info.ResumeLabels.Contains(LabelName(Mid(t, 8))))
                    {
                        info.ResumeLabels.Add(LabelName(Mid(t, 8)));
                    }
                    else if (LMatch(t, "GoTo ") && !info.ResumeLabels.Contains(LabelName(Mid(t, 6))))
                    { // leaving the handler with GoTo jumps like Resume label (was taken for a retry)
                        info.ResumeLabels.Add(LabelName(Mid(t, 6)));
                    }
                }
                if (resumes) plan.Handlers[h] = info; // only Resume / Resume Next need the local-function form
            }
            return plan;
        }
    }


    /// <summary>
    /// VB6's <c>Err</c> object is a parameterless method in Microsoft.VisualBasic, so a member access on it needs the
    /// call: <c>Err.Number</c> → <c>Err().Number</c>, <c>Err.Raise 5</c> → <c>Err().Raise(5)</c>. Applies wherever a
    /// member of <c>Err</c> is referenced — in an expression and in the name of a statement call alike. Only a
    /// standalone <c>Err</c> is rewritten, never the tail of another name (<c>myErr.Text</c>, <c>obj.Err.X</c>).
    /// </summary>
    public static string IntrinsicMember(string s) =>
        s == "" ? s : Regex.Replace(s, @"(?<![A-Za-z0-9_.])Err\.", "Err().");

    /// <summary>On Error ... statement: updates <paramref name="scope"/>, returns the code to emit.</summary>
    public static string ConvertOnError(string t, ErrorScope scope, ref int ind)
    {
        var target = Trim(Regex.Replace(Trim(t), "^On (Local )?Error ", ""));
        var o = "";
        if (target == "GoTo -1")
        {
            return SSpace(ind) + "Err().Clear(); // On Error GoTo -1";
        }
        // leaving the current mode: a protected block open at this level ends here
        o = o + scope.CloseTryHere(ref ind);
        if (target == "Resume Next")
        {
            scope.Mode = ErrorScope.Modes.ResumeNext;
            scope.Handler = "";
        }
        else if (target == "GoTo 0")
        {
            scope.Mode = ErrorScope.Modes.None;
            scope.Handler = "";
        }
        else if (LMatch(target, "GoTo "))
        {
            scope.Mode = ErrorScope.Modes.GoTo;
            scope.Handler = LabelName(Mid(target, 6));
        }
        else
        {
            return o + SSpace(ind) + "// TODO: VB6 On Error not converted: " + Trim(t);
        }
        return o + SSpace(ind) + "Err().Clear(); // On Error " + target;
    }

    /// <summary>Resume / Resume Next / Resume label.</summary>
    public static string ConvertResume(string t, int ind)
    {
        var target = Trim(Mid(Trim(t), 7));
        var i = SSpace(ind);
        if (target == "" || target == "0")
        {
            return i + "Err().Clear(); // TODO: VB6 Resume (retry the failing statement) has no C# equivalent";
        }
        if (target == "Next")
        {
            return i + "Err().Clear(); // TODO: VB6 Resume Next (continue after the failing statement) has no C# equivalent";
        }
        return i + "Err().Clear();" + vbCrLf + i + "goto " + LabelName(target) + ";";
    }

    // ---------------------------------------------------------------- jumps

    /// <summary>On expr GoTo a, b, ... / On expr GoSub ...</summary>
    public static string ConvertOnGoTo(string t, int ind)
    {
        var m = Regex.Match(Trim(t), "^On (.+) (GoTo|GoSub) (.+)$");
        if (!m.Success) return null;
        var gosub = m.Groups[2].Value == "GoSub"; // GoSub routines are local functions
        var r = SSpace(ind) + "switch (Conversions.ToInteger(" + ConvertValue(m.Groups[1].Value) + ")) {";
        var n = 0;
        foreach (var lbl in SplitTopLevel(m.Groups[3].Value))
        {
            n++;
            if (lbl != "") r = r + " case " + n + ": " + (gosub ? LabelName(lbl) + "(); break;" : "goto " + LabelName(lbl) + ";");
        }
        return r + " }";
    }

    // ---------------------------------------------------------------- Select Case

    private static bool IsLiteral(string s)
    {
        s = Trim(s);
        return Regex.IsMatch(s, "^-?[0-9]+(\\.[0-9]+)?$") || LMatch(s, ConverterUtils.deStringTokenBase) && Regex.IsMatch(s, "^" + Id + "$")
            || Regex.IsMatch(s, "^-?&[HhOo][0-9A-Fa-f]+&?$");
    }

    /// <summary>One Case comparison; C# has no relational operators on strings, so a string operand compares as VB6 does.</summary>
    private static string CaseCompare(string v, string op, string raw)
    {
        var value = ConvertValue(raw);
        if (op == "==" || op == "!=" || OperandType(raw) != "String") return v + " " + op + " " + value;
        return (OptionCompareText ? "TextCompare(" : "string.CompareOrdinal(") + v + ", " + value + ") " + op + " 0";
    }

    /// <summary>Case list to C# labels: constants become <c>case x:</c>; Is / To / non-constant items a guarded pattern.</summary>
    public static string ConvertCaseLabels(string list)
    {
        var items = SplitTopLevel(list);
        var simple = true;
        foreach (var it in items)
        {
            if (!IsLiteral(it)) simple = false;
        }
        if (simple)
        {
            var r = "";
            foreach (var it in items) r = r + "case " + ConvertValue(it) + ": ";
            return Trim(r);
        }
        const string v = "vbCase_";
        var conds = new List<string>();
        foreach (var it in items)
        {
            Match m;
            if ((m = Regex.Match(it, "^Is *(<>|<=|>=|=|<|>) *(.+)$")).Success)
            {
                var op = m.Groups[1].Value == "=" ? "==" : m.Groups[1].Value == "<>" ? "!=" : m.Groups[1].Value;
                conds.Add(CaseCompare(v, op, m.Groups[2].Value));
            }
            else if ((m = Regex.Match(it, "^(.+) To (.+)$")).Success)
            {
                conds.Add("(" + CaseCompare(v, ">=", m.Groups[1].Value) + " && " + CaseCompare(v, "<=", m.Groups[2].Value) + ")");
            }
            else
            {
                conds.Add(v + " == " + ConvertValue(it));
            }
        }
        return "case var " + v + " when " + string.Join(" || ", conds) + ":";
    }

    // ---------------------------------------------------------------- arrays

    /// <summary>Element count of one VB6 dimension ("ub" or "lb To ub"): VB arrays include the upper bound.</summary>
    public static string DimCount(string dim, out string lower)
    {
        lower = "";
        var m = Regex.Match(Trim(dim), "^(.+) To (.+)$");
        var ub = Trim(dim);
        if (m.Success)
        {
            lower = Trim(m.Groups[1].Value);
            ub = Trim(m.Groups[2].Value);
        }
        if (Regex.IsMatch(ub, "^-?[0-9]+$")) return (long.Parse(ub, CultureInfo.InvariantCulture) + 1).ToString(CultureInfo.InvariantCulture);
        return ConvertValue(ub) + " + 1";
    }

    /// <summary>ReDim [Preserve] a(dims) [As T], ... for T[] / T[,] (ReDim helper) and VB6Array (lower bound kept).</summary>
    public static string ConvertReDim(string t, int ind)
    {
        var s = Trim(Mid(Trim(t), 6));
        var preserve = false;
        if (LMatch(s, "Preserve "))
        {
            preserve = true;
            s = Trim(Mid(s, 10));
        }
        var r = "";
        foreach (var part in SplitTopLevel(s))
        {
            var name = Regex.Match(part, "^" + Id).Value;
            var open = part.IndexOf('(');
            var close = open < 0 ? -1 : MatchParen(part, open);
            if (name == "" || close < 0)
            {
                r = r + SSpace(ind) + "// TODO: VB6 ReDim not converted: " + part + vbCrLf;
                continue;
            }
            var dims = SplitTopLevel(part.Substring(open + 1, close - open - 1));
            var v = SubParam(name);
            var counts = new List<string>();
            var lbs = new List<string>();
            var ubs = new List<string>();
            foreach (var d in dims)
            {
                counts.Add(DimBounds(d, out var lb, out var ub));
                lbs.Add(lb);
                ubs.Add(ub);
            }
            var keep = preserve ? ", true" : "";
            if (v.vb6Array && dims.Count == 1)
            {
                r = r + SSpace(ind) + name + ".ReDim(" + lbs[0] + ", " + ubs[0] + keep + ");" + vbCrLf;
            }
            else if (dims.Count <= 2)
            {
                var todo = lbs.Exists(lb => lb != "0") ? " // TODO: VB6 lower bound " + string.Join(", ", lbs) + " (a zero-based array sized to the upper bound)" : "";
                r = r + SSpace(ind) + name + " = ReDim(" + name + ", " + string.Join(", ", counts) + keep + ");" + todo + vbCrLf;
            }
            else
            {
                var asType = Trim(part.Substring(close + 1));
                var cs = ConvertDataType(LMatch(asType, "As ") ? Trim(Mid(asType, 4)) : v.asType == "" ? "Variant" : v.asType);
                r = r + SSpace(ind) + name + " = new " + cs + "[" + string.Join(", ", counts) + "];"
                    + (preserve ? " // TODO: VB6 ReDim Preserve of a " + dims.Count + "-dimensional array" : "") + vbCrLf;
            }
            SubParamAssign(name);
        }
        return TrimEnd(r);
    }

    /// <summary>Erase a, b: dynamic arrays are released, fixed ones get default elements again.</summary>
    public static string ConvertErase(string t, int ind)
    {
        var r = "";
        foreach (var name in SplitTopLevel(Trim(Mid(Trim(t), 7))))
        {
            if (name == "") continue;
            var v = SubParam(name);
            var dynamicArr = v.asArray == "-1";
            var rank = v.asArray == "-1" || v.asArray == "" ? 1 : SplitTopLevel(v.asArray).Count;
            string stmt;
            if (v.vb6Array) stmt = name + ".Erase(" + (dynamicArr ? "false" : "true") + ");";
            else if (dynamicArr) stmt = name + " = null;";
            else if (rank == 2) stmt = name + " = ReDim(" + name + ", " + name + ".GetLength(0), " + name + ".GetLength(1));";
            else stmt = name + " = ReDim(" + name + ", " + name + ".Length);";
            r = r + SSpace(ind) + stmt + vbCrLf;
        }
        return TrimEnd(r);
    }

    private static string TrimEnd(string s) => s.TrimEnd('\r', '\n');

    // ---------------------------------------------------------------- string statements

    /// <summary>Mid(target, start[, length]) = value / MidB.</summary>
    public static string ConvertMidStatement(string t, int ind)
    {
        var m = Regex.Match(Trim(t), "^MidB?\\$?\\s*\\(");
        if (!m.Success) return null;
        var s = Trim(t);
        var open = s.IndexOf('(');
        var close = MatchParen(s, open);
        if (close < 0) return null;
        var rest = Trim(s.Substring(close + 1));
        if (!LMatch(rest, "=")) return null;
        var args = SplitTopLevel(s.Substring(open + 1, close - open - 1));
        if (args.Count < 2) return null;
        SubParamAssign(Regex.Match(args[0], "^" + Id).Value);
        var r = "MidStmt(ref " + ConvertValue(args[0]) + ", " + ConvertValue(args[1]);
        if (args.Count > 2) r = r + ", " + ConvertValue(args[2]);
        return SSpace(ind) + r + ", " + ConvertValue(Mid(rest, 2)) + ");";
    }

    /// <summary>LSet / RSet target = value (pads or truncates to the target's current length).</summary>
    public static string ConvertLRSet(string t, int ind)
    {
        var m = Regex.Match(Trim(t), "^(LSet|RSet) (.+?) = (.+)$");
        if (!m.Success) return null;
        var target = ConvertValue(m.Groups[2].Value);
        SubParamAssign(Regex.Match(m.Groups[2].Value, "^" + Id).Value);
        return SSpace(ind) + target + " = " + m.Groups[1].Value + "(" + ConvertValue(m.Groups[3].Value) + ", Len(" + target + "));";
    }

    // ---------------------------------------------------------------- file I/O (Microsoft.VisualBasic.FileSystem)

    private static string FileNum(string s) => ConvertValue(Trim(s).TrimStart('#'));

    /// <summary>VB6 file statements to Microsoft.VisualBasic.FileSystem calls; null when <paramref name="t"/> is none.</summary>
    public static string ConvertFileStatement(string t, int ind)
    {
        t = Trim(t);
        var i = SSpace(ind);
        Match m;

        if ((m = Regex.Match(t, "^Open (.+?)(?: For (Input|Output|Append|Binary|Random))?(?: Access (Read Write|Read|Write))?(?: (Shared|Lock Read Write|Lock Read|Lock Write))? As #?(.+?)(?: Len *= *(.+))?$")).Success)
        {
            var mode = m.Groups[2].Value == "" ? "Random" : m.Groups[2].Value;
            var r = "FileOpen(" + FileNum(m.Groups[5].Value) + ", " + ConvertValue(m.Groups[1].Value) + ", OpenMode." + mode;
            var access = m.Groups[3].Value == "" ? "Default" : m.Groups[3].Value == "Read Write" ? "ReadWrite" : m.Groups[3].Value;
            var share = m.Groups[4].Value == "" ? "Default" : Replace(m.Groups[4].Value, " ", "");
            if (share == "LockReadWrite" || share == "LockRead" || share == "LockWrite" || share == "Shared" || access != "Default" || m.Groups[6].Value != "")
            {
                r = r + ", OpenAccess." + access + ", OpenShare." + share;
                if (m.Groups[6].Value != "") r = r + ", " + ConvertValue(m.Groups[6].Value);
            }
            return i + r + ");";
        }
        if (t == "Close" || t == "Reset") return i + (t == "Close" ? "FileClose" : "Reset") + "();";
        if (LMatch(t, "Close ") && Regex.IsMatch(t, "^Close #?[^=]+$"))
        {
            var nums = new List<string>();
            foreach (var n in SplitTopLevel(Mid(t, 7))) nums.Add(FileNum(n));
            return i + "FileClose(" + string.Join(", ", nums) + ");";
        }
        if ((m = Regex.Match(t, "^(Print|Write) #([^,]+)(?:,(.*))?$")).Success)
        {
            var list = Trim(m.Groups[3].Value);
            var newLine = !(Right(list, 1) == ";" || Right(list, 1) == ",");
            if (!newLine) list = Trim(Left(list, Len(list) - 1));
            var args = m.Groups[1].Value == "Print" ? PrintArgs(list) : WriteArgs(list);
            var fn = m.Groups[1].Value == "Print" ? (newLine ? "PrintLine" : "Print") : (newLine ? "WriteLine" : "Write");
            return i + fn + "(" + FileNum(m.Groups[2].Value) + (args == "" ? "" : ", " + args) + ");";
        }
        if ((m = Regex.Match(t, "^Line Input #([^,]+), *(.+)$")).Success)
        {
            SubParamAssign(Regex.Match(m.Groups[2].Value, "^" + Id).Value);
            return i + ConvertValue(m.Groups[2].Value) + " = LineInput(" + FileNum(m.Groups[1].Value) + ");";
        }
        if ((m = Regex.Match(t, "^Input #([^,]+), *(.+)$")).Success)
        {
            var r = "";
            foreach (var v in SplitTopLevel(m.Groups[2].Value))
            {
                SubParamAssign(Regex.Match(v, "^" + Id).Value);
                r = r + (r == "" ? "" : vbCrLf + i) + "Input(" + FileNum(m.Groups[1].Value) + ", ref " + ConvertValue(v) + ");";
            }
            return i + r;
        }
        if ((m = Regex.Match(t, "^(Get|Put) #?([^,]+), *([^,]*), *(.+)$")).Success)
        {
            var rec = Trim(m.Groups[3].Value);
            var v = m.Groups[4].Value;
            if (m.Groups[1].Value == "Get")
            {
                SubParamAssign(Regex.Match(v, "^" + Id).Value);
                return i + "FileGet(" + FileNum(m.Groups[2].Value) + ", ref " + ConvertValue(v) + (rec == "" ? "" : ", " + ConvertValue(rec)) + ");";
            }
            return i + "FilePut(" + FileNum(m.Groups[2].Value) + ", " + ConvertValue(v) + (rec == "" ? "" : ", " + ConvertValue(rec)) + ");";
        }
        if ((m = Regex.Match(t, "^Seek #?([^,]+), *(.+)$")).Success)
        {
            return i + "Seek(" + FileNum(m.Groups[1].Value) + ", " + ConvertValue(m.Groups[2].Value) + ");";
        }
        if ((m = Regex.Match(t, "^(Lock|Unlock) #?([^,]+)(?:, *(.+))?$")).Success)
        {
            var r = m.Groups[1].Value + "(" + FileNum(m.Groups[2].Value);
            var range = Trim(m.Groups[3].Value);
            Match rm;
            if (range == "") { }
            else if ((rm = Regex.Match(range, "^(.*) *To (.+)$")).Success)
            {
                r = r + ", " + (Trim(rm.Groups[1].Value) == "" ? "1" : ConvertValue(rm.Groups[1].Value)) + ", " + ConvertValue(rm.Groups[2].Value);
            }
            else r = r + ", " + ConvertValue(range);
            return i + r + ");";
        }
        if ((m = Regex.Match(t, "^Width #([^,]+), *(.+)$")).Success)
        {
            return i + "FileWidth(" + FileNum(m.Groups[1].Value) + ", " + ConvertValue(m.Groups[2].Value) + ");";
        }
        if ((m = Regex.Match(t, "^Name (.+) As (.+)$")).Success)
        {
            return i + "Rename(" + ConvertValue(m.Groups[1].Value) + ", " + ConvertValue(m.Groups[2].Value) + ");";
        }
        return null;
    }

    /// <summary>Print # list: ',' moves to the next print zone (separate argument), ';' concatenates.</summary>
    private static string PrintArgs(string list)
    {
        if (list == "") return "";
        var zones = new List<string>();
        foreach (var zone in SplitTopLevel(list, ','))
        {
            var parts = new List<string>();
            foreach (var p in SplitTopLevel(zone, ';'))
            {
                if (p == "") continue;
                var v = Regex.Replace(p, "^Spc\\(", "SPC(");
                v = Regex.Replace(v, "^Tab\\(", "TAB(");
                parts.Add(v == p ? ConvertValue(p) : v.Substring(0, 4) + ConvertValue(v.Substring(4, v.Length - 5)) + ")");
            }
            zones.Add(parts.Count == 0 ? "\"\"" : parts.Count == 1 ? parts[0] : "string.Concat(" + string.Join(", ", parts) + ")");
        }
        return string.Join(", ", zones);
    }

    private static string WriteArgs(string list)
    {
        if (list == "") return "";
        var r = new List<string>();
        foreach (var p in SplitTopLevel(Replace(list, ";", ","))) r.Add(ConvertValue(p));
        return string.Join(", ", r);
    }

    // ---------------------------------------------------------------- expressions

    private static readonly string[] LogicalOps = { " And ", " Or ", " Xor ", " Eqv ", " Imp " };

    /// <summary>
    /// Parenthesizes the operand of a top-level Not: VB6 Not binds looser than comparisons
    /// ("Not a = b" is "Not (a = b)", "Not x Is Nothing" is "Not (x Is Nothing)"), up to the next logical operator.
    /// </summary>
    public static string GroupNot(string s)
    {
        var depth = 0;
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] == '(') depth++;
            else if (s[i] == ')') depth--;
            if (depth != 0 || string.CompareOrdinal(s, i, "Not ", 0, 4) != 0 || i > 0 && s[i - 1] != ' ') continue;
            var j = i + 4;
            var d = 0;
            for (; j < s.Length; j++)
            {
                if (s[j] == '(') d++;
                else if (s[j] == ')') d--;
                else if (d == 0 && Array.Exists(LogicalOps, op => string.CompareOrdinal(s, j, op, 0, op.Length) == 0)) break;
            }
            var operand = s.Substring(i + 4, j - i - 4).Trim();
            if (SplitTopLevel(operand, ' ').Count < 2) continue; // a single term needs no grouping
            var grouped = "Not (" + operand + ")";
            s = s.Substring(0, i) + grouped + s.Substring(j);
            i = i + grouped.Length - 1;
        }
        return s;
    }

    /// <summary>Debug.Print list: ';' concatenates, ',' is a tab; a trailing separator suppresses the new line.</summary>
    public static string ConvertDebugPrint(string list)
    {
        list = Trim(list);
        var newLine = !(Right(list, 1) == ";" || Right(list, 1) == ",");
        var nextZone = Right(list, 1) == ","; // a trailing "," still moves to the next print zone (was dropped)
        if (!newLine) list = Trim(Left(list, Len(list) - 1));
        var parts = new List<string>();
        foreach (var zone in SplitTopLevel(list, ','))
        {
            foreach (var p in SplitTopLevel(zone, ';'))
            {
                if (p != "") parts.Add(ConvertValue(p));
            }
            parts.Add("\"\\t\"");
        }
        if (!nextZone) parts.RemoveAt(parts.Count - 1);
        var arg = parts.Count == 0 ? "" : parts.Count == 1 ? parts[0] : "string.Concat(" + string.Join(", ", parts) + ")";
        return "Console." + (newLine ? "WriteLine" : "Write") + "(" + arg + ")";
    }

    /// <summary>[object.]Line / PSet / Circle with the VB6 graphics syntax ("(x1, y1)-(x2, y2), color, BF") to method calls.</summary>
    public static string ConvertGraphicsStatement(string t, int ind)
    {
        var m = Regex.Match(Trim(t), "^(?:(" + Id + "(?:\\." + Id + ")*)\\.)?(Line|PSet|Circle) *(Step *)?(.*)$");
        if (!m.Success) return null;
        var target = m.Groups[1].Value == "" ? "" : ConvertValue(m.Groups[1].Value) + ".";
        var verb = m.Groups[2].Value;
        var rest = Trim(m.Groups[4].Value);
        var todo = m.Groups[3].Value != "" ? " // TODO: VB6 Step (relative coordinates)" : "";
        var args = new List<string>();
        if (verb == "Line")
        {
            if (LMatch(rest, "("))
            {
                var close = MatchParen(rest, 0);
                if (close < 0) return null;
                foreach (var a in SplitTopLevel(rest.Substring(1, close - 1))) args.Add(ConvertValue(a));
                rest = Trim(rest.Substring(close + 1));
            }
            else
            { // Line -(x2, y2): from the current position
                args.Add(target + "CurrentX");
                args.Add(target + "CurrentY");
            }
            if (!LMatch(rest, "-")) return null;
            rest = Trim(Mid(rest, 2));
            if (LMatch(rest, "Step"))
            {
                todo = " // TODO: VB6 Step (relative coordinates)";
                rest = Trim(Mid(rest, 5));
            }
        }
        if (!LMatch(rest, "(")) return null;
        var end = MatchParen(rest, 0);
        if (end < 0) return null;
        foreach (var a in SplitTopLevel(rest.Substring(1, end - 1))) args.Add(ConvertValue(a));
        rest = Trim(rest.Substring(end + 1));
        if (LMatch(rest, ","))
        {
            foreach (var a in SplitTopLevel(Mid(rest, 2)))
            {
                if (a == "B" || a == "BF")
                {
                    if (args.Count == 4) args.Add("0"); // no color
                    args.Add("true");
                    if (a == "BF") todo = todo + " // TODO: VB6 BF (filled box)";
                }
                else
                {
                    if (a == "") todo = todo + " // TODO: VB6 omitted argument";
                    args.Add(a == "" ? "default" : ConvertValue(a));
                }
            }
        }
        return SSpace(ind) + target + verb + "(" + string.Join(", ", args) + ");" + todo;
    }

    // ---------------------------------------------------------------- misc statements

    /// <summary>Statements with a fixed translation; null when <paramref name="t"/> is none of them.</summary>
    public static string ConvertSimpleStatement(string t, int ind)
    {
        t = Trim(t);
        var i = SSpace(ind);
        Match m;
        if (t == "Stop") return i + "System.Diagnostics.Debugger.Break();";
        if (t == "End") return i + "Environment.Exit(0); // VB6 End";
        if (LMatch(t, "Attribute ")) return i + "// " + t;
        if ((m = Regex.Match(t, "^Error (.+)$")).Success) return i + "Err().Raise(" + ConvertValue(m.Groups[1].Value) + ");";
        if ((m = Regex.Match(t, "^(Date|Time)\\$? = (.+)$")).Success)
        {
            return i + "DateAndTime." + (m.Groups[1].Value == "Date" ? "Today" : "TimeOfDay") + " = " + ConvertValue(m.Groups[2].Value) + ";";
        }
        if (t == "Return") return i + "// TODO: VB6 GoSub Return not supported";
        if (LMatch(t, "GoSub ")) return i + "// TODO: VB6 GoSub not supported: " + t;
        return null;
    }
}
