using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using static Microsoft.VisualBasic.Constants;
using static Microsoft.VisualBasic.Strings;
using static Vb6ToCSharp.Modules.ModConfig;
using static Vb6ToCSharp.Modules.ModConvert;
using static Vb6ToCSharp.Modules.ModConvertUtils;
using static Vb6ToCSharp.Modules.ModProjectFiles;
using static Vb6ToCSharp.Modules.ModSubTracking;
using static Vb6ToCSharp.Modules.ModUtils;
using static Vb6ToCSharp.Modules.ModVb6ToCs;


namespace Vb6ToCSharp.Modules;

/// <summary>
/// VB6 class-module semantics (as VB Migration Partner converts them): Class_Initialize / Class_Terminate, Implements,
/// default members (VB_UserMemId = 0), NewEnum (VB_UserMemId = -4), VB_PredeclaredId default instances, events.
/// </summary>
public static class ModConvertClasses
{
    private const string Id = "[A-Za-z_][A-Za-z0-9_]*";

    /// <summary>Facts about one class module, read from its VB6 source.</summary>
    public sealed class ClassModel
    {
        public string Name = "";
        public readonly List<string> Implements = new List<string>();
        public bool HasInitialize;
        public bool HasTerminate;
        public bool Predeclared;
        public string DefaultMember;
        public string DefaultParams = ""; // VB6 parameters of the default member ("" = none)
        public string DefaultType = "Variant";
        public bool DefaultHasSetter;
        public string EnumMember;
        public string EnumSource; // the collection whose enumerator NewEnum returns
        public readonly List<string> Events = new List<string>();
        public readonly HashSet<string> ParameterizedSetters = new HashSet<string>();
        /// <summary>Public / Friend Subs and Functions: "obj.Name" without parentheses calls them.</summary>
        public readonly HashSet<string> Methods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public string Source = "";
        /// <summary>Attribute VB_Exposed = True: other projects see the class (public in an ActiveX project).</summary>
        public bool Exposed;

        public static ClassModel Scan(string name, string source)
        {
            var m = new ClassModel { Name = name, Source = source ?? "" };
            var src = m.Source;
            m.Implements.AddRange(ImplementsOf(src));
            m.Exposed = ModProjectGroup.IsExposed(src);
            m.HasInitialize = Regex.IsMatch(src, "(?m)^\\s*(?:Private |Public )?Sub\\s+Class_Initialize\\s*\\(");
            m.HasTerminate = Regex.IsMatch(src, "(?m)^\\s*(?:Private |Public )?Sub\\s+Class_Terminate\\s*\\(");
            m.Predeclared = Regex.IsMatch(src, "(?m)^\\s*Attribute\\s+VB_PredeclaredId\\s*=\\s*True");
            foreach (Match x in Regex.Matches(src, "(?m)^\\s*Attribute\\s+(" + Id + ")\\.VB_UserMemId\\s*=\\s*(-?[0-9]+)"))
            {
                if (x.Groups[2].Value == "0") m.DefaultMember = x.Groups[1].Value;
                if (x.Groups[2].Value == "-4") m.EnumMember = x.Groups[1].Value;
            }
            if (m.EnumMember != null)
            {
                var e = Regex.Match(src, "(?m)Set\\s+" + m.EnumMember + "\\s*=\\s*(" + Id + ")\\.(?:\\[_NewEnum\\]|_NewEnum)");
                if (e.Success) m.EnumSource = e.Groups[1].Value;
            }
            if (m.DefaultMember != null)
            {
                var d = Regex.Match(src, "(?m)^\\s*(?:Public |Friend )?(?:Property Get|Function)\\s+" + m.DefaultMember + "\\s*\\(([^)]*)\\)(?:\\s*As\\s+(" + Id + "(?:\\." + Id + ")?))?");
                if (d.Success)
                {
                    m.DefaultParams = Trim(d.Groups[1].Value);
                    m.DefaultType = d.Groups[2].Success ? d.Groups[2].Value : "Variant";
                }
                m.DefaultHasSetter = Regex.IsMatch(src, "(?m)^\\s*(?:Public |Friend )?Property (?:Let|Set)\\s+" + m.DefaultMember + "\\s*\\([^)]*,");
            }
            foreach (Match x in Regex.Matches(src, "(?m)^\\s*(?:Public )?Event\\s+(" + Id + ")")) m.Events.Add(x.Groups[1].Value);
            foreach (Match x in Regex.Matches(src, "(?m)^\\s*(?:Public |Friend )?Property (?:Let|Set)\\s+(" + Id + ")\\s*\\([^)]*,")) m.ParameterizedSetters.Add(x.Groups[1].Value);
            foreach (Match x in Regex.Matches(src, "(?m)^\\s*(?:Public |Friend )?(?:Static )?(?:Sub|Function)\\s+(" + Id + ")")) m.Methods.Add(x.Groups[1].Value);
            return m;
        }
    }

    /// <summary>The class module being converted (null for standard modules and forms).</summary>
    public static ClassModel Current;

    private static string registryProject;
    private static readonly Dictionary<string, ClassModel> classes = new Dictionary<string, ClassModel>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> implemented = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Reads the project's class modules once (per .vbp): their names, attributes, events, and which are used as interfaces.</summary>
    private static void EnsureRegistry()
    {
        if (registryProject == VbpFile) return;
        registryProject = VbpFile;
        classes.Clear();
        implemented.Clear();
        try
        {
            var folder = FilePath(VbpFile);
            foreach (var f in Split(VbpClasses(VbpFile), vbCrLf))
            {
                if (Trim(f) == "" || !System.IO.File.Exists(folder + f)) continue;
                var src = System.IO.File.ReadAllText(folder + f);
                var model = ClassModel.Scan(ModuleName(src), src);
                classes[model.Name] = model;
            }
            foreach (var f in Split(VbpClasses(VbpFile) + vbCrLf + VbpForms(VbpFile) + vbCrLf + VbpUserControls(VbpFile), vbCrLf))
            {
                if (Trim(f) == "" || !System.IO.File.Exists(folder + f)) continue;
                foreach (var i in ImplementsOf(System.IO.File.ReadAllText(folder + f))) implemented.Add(i);
            }
            var project = ModConvert.ProjectInfo();
            // the exposed classes of the projects this one references are known as well (New, default members, For Each...)
            foreach (var r in ModProjectGroup.ReferencedProjects(project))
            {
                foreach (var src in SourcesOf(r, VbpClasses(r.Path)))
                {
                    var model = ClassModel.Scan(ModuleName(src), src);
                    if (model.Exposed && !classes.ContainsKey(model.Name)) classes[model.Name] = model;
                }
            }
            // a class another group project implements is an interface, even when no class of this project implements it
            foreach (var r in ModProjectGroup.ReferencingProjects(project))
            {
                foreach (var src in SourcesOf(r, VbpClasses(r.Path) + vbCrLf + VbpForms(r.Path) + vbCrLf + VbpUserControls(r.Path)))
                {
                    foreach (var i in ImplementsOf(src)) implemented.Add(i);
                }
            }
        }
        catch (Exception)
        {
            // no project: only the converted file is known
        }
    }

    /// <summary>Interfaces a VB6 source implements; <c>Implements Lib.IShape</c> gives <c>IShape</c> when Lib is one of our VB6 projects.</summary>
    private static IEnumerable<string> ImplementsOf(string source)
    {
        foreach (Match x in Regex.Matches(source ?? "", "(?m)^\\s*Implements\\s+(" + Id + "(?:\\." + Id + ")?)"))
        {
            yield return ModProjectGroup.StripProjectQualifier(x.Groups[1].Value);
        }
    }

    /// <summary>Sources of files (a .vbp list) of another project.</summary>
    private static IEnumerable<string> SourcesOf(FormConversion.VbpInfo project, string files)
    {
        foreach (var f in Split(files, vbCrLf))
        {
            if (Trim(f) == "") continue;
            var path = System.IO.Path.Combine(project.Folder, Trim(f));
            if (System.IO.File.Exists(path)) yield return System.IO.File.ReadAllText(path);
        }
    }

    /// <summary>Forgets the project classes read so far (a new conversion run: sources may have changed).</summary>
    public static void ResetCaches() => registryProject = null;

    /// <summary>Registers a class (the one being converted, or one known only from a test).</summary>
    public static void Register(ClassModel model)
    {
        EnsureRegistry();
        classes[model.Name] = model;
        foreach (var i in model.Implements) implemented.Add(i);
    }

    /// <summary>The class model of <paramref name="name"/> (a <c>Lib.CFoo</c> project qualifier is ignored), or null.</summary>
    private static ClassModel Find(string name)
    {
        EnsureRegistry();
        if (name == null) return null;
        return classes.TryGetValue(name, out var c) || classes.TryGetValue(ModProjectGroup.StripProjectQualifier(name), out c) ? c : null;
    }

    public static bool IsProjectClass(string name) => Find(name) != null;

    /// <summary>A project class with Class_Terminate (it is IDisposable).</summary>
    public static bool HasTerminate(string name) => Find(name)?.HasTerminate == true;

    /// <summary>A Sub / Function of the project class (or interface) <paramref name="typeName"/>.</summary>
    public static bool IsMethod(string typeName, string member) => Find(typeName)?.Methods.Contains(member) == true;

    public static bool IsPredeclared(string name) => Find(name)?.Predeclared == true;

    /// <summary>A class used with Implements somewhere in the project.</summary>
    public static bool IsInterface(string name)
    {
        EnsureRegistry();
        return name != null && (implemented.Contains(name) || implemented.Contains(ModProjectGroup.StripProjectQualifier(name)));
    }

    /// <summary>obj(i) indexes: VB Collection, or a class whose default member takes parameters.</summary>
    public static bool HasIndexedDefault(string typeName)
    {
        EnsureRegistry();
        if (typeName == "Collection") return true;
        var c = Find(typeName);
        return c != null && c.DefaultMember != null && c.DefaultParams != "";
    }

    /// <summary>A Property Let / Set with parameters in the project: "obj.Name(i) = v" calls set_Name(i, v).</summary>
    public static bool IsParameterizedSetter(string name)
    {
        EnsureRegistry();
        if (Current != null && Current.ParameterizedSetters.Contains(name)) return true;
        foreach (var c in classes.Values)
        {
            if (c.ParameterizedSetters.Contains(name)) return true;
        }
        return false;
    }

    /// <summary>The interface a member implements (IFoo_Bar implements IFoo.Bar when the class Implements IFoo), or null.</summary>
    public static string ImplementedInterface(string member)
    {
        if (Current == null || member == null) return null;
        foreach (var i in Current.Implements)
        {
            if (member.StartsWith(i + "_", StringComparison.Ordinal) && member.Length > i.Length + 1) return i;
        }
        return null;
    }

    /// <summary>Class declaration line (attributes, base interfaces) of <paramref name="model"/>.</summary>
    public static string ClassDeclaration(ClassModel model)
    {
        var bases = new List<string>(model.Implements);
        if (model.HasTerminate) bases.Add("IDisposable");
        if (model.EnumSource != null) bases.Add("System.Collections.IEnumerable");
        // with parameters the default member is an indexer, which already makes the type's DefaultMember (C# forbids both)
        var attr = model.DefaultMember != null && model.DefaultParams == "" ? "[System.Reflection.DefaultMember(\"" + model.DefaultMember + "\")]" + vbCrLf : "";
        return attr + ModProjectGroup.TypeModifier(model.Exposed) + " class " + model.Name + (bases.Count > 0 ? " : " + string.Join(", ", bases) : "") + " {";
    }

    /// <summary>Members VB6 provides implicitly: constructor, Dispose / finalizer, default instance, indexer, enumerator.</summary>
    public static string ClassMembers(ClassModel model)
    {
        var n = vbCrLf;
        var r = new StringBuilder();
        if (model.HasInitialize)
        {
            r.Append("public " + model.Name + "() {" + n + "  Class_Initialize(); // VB6 Class_Initialize" + n + "}" + n);
        }
        if (model.HasTerminate)
        { // VB6 Class_Terminate runs when the last reference goes: Dispose (deterministic) or the finalizer
            r.Append("private bool vbTerminated;" + n);
            r.Append("public void Dispose() {" + n + "  if (vbTerminated) return;" + n + "  vbTerminated = true;" + n + "  GC.SuppressFinalize(this);" + n + "  Class_Terminate();" + n + "}" + n);
            r.Append("~" + model.Name + "() {" + n + "  if (!vbTerminated) { vbTerminated = true; Class_Terminate(); }" + n + "}" + n);
        }
        if (model.Predeclared)
        {
            r.Append("private static " + model.Name + " vbDefaultInstance;" + n);
            r.Append("/// <summary>VB6 default instance (VB_PredeclaredId).</summary>" + n);
            r.Append("public static " + model.Name + " instance { get => vbDefaultInstance ?? (vbDefaultInstance = new " + model.Name + "()); set => vbDefaultInstance = value; }" + n);
        }
        if (model.DefaultMember != null && model.DefaultParams != "")
        { // obj(i): the default member with parameters is the indexer
            var parms = new List<string>();
            var names = new List<string>();
            foreach (var a in ModConvertStatements.SplitTopLevel(model.DefaultParams))
            {
                if (Trim(a) == "") continue;
                var c = ConvertParameter(a, true);
                if (LMatch(c, "ref ") || LMatch(c, "out ")) c = Mid(c, 5);
                var eq = InStr(c, "=");
                if (eq > 0) c = Trim(Left(c, eq - 1));
                parms.Add(c);
                names.Add(SplitWord(c, -1));
            }
            var type = ConvertDataType(model.DefaultType);
            // the indexer's metadata name must differ from the default member's method (both would be "Item")
            r.Append("[System.Runtime.CompilerServices.IndexerName(\"Default" + model.DefaultMember + "\")]" + n);
            r.Append("public " + type + " this[" + string.Join(", ", parms) + "] { get { return " + model.DefaultMember + "(" + string.Join(", ", names) + "); }"
                     + (model.DefaultHasSetter ? " set { set_" + model.DefaultMember + "(" + string.Join(", ", names) + ", value); }" : "") + " }" + n);
        }
        if (model.EnumSource != null)
        { // For Each over the class: NewEnum
            r.Append("public System.Collections.IEnumerator GetEnumerator() {" + n + "  return ((System.Collections.IEnumerable)" + model.EnumSource + ").GetEnumerator();" + n + "}" + n);
        }
        return r.ToString();
    }

    /// <summary>
    /// A class used through Implements whose procedures are all empty (a VB6 "interface class") becomes a C# interface;
    /// null when the class has code (it stays a class).
    /// </summary>
    public static string InterfaceDeclaration(ClassModel model)
    {
        var lines = Split(Replace(model.Source, vbLf, ""), vbCr);
        var members = new List<string>();
        var props = new Dictionary<string, string[]>(); // name -> type, get, set
        var header = new Regex("^(?:(Public |Private |Friend )?)(?:Static )?(Sub|Function|Property Get|Property Let|Property Set) (" + Id + ")");
        InitDeString();
        for (var i = 0; i < lines.Length; i++)
        {
            var line = DeString(DeComment(lines[i], true));
            var h = header.Match(line);
            if (!h.Success)
            {
                var field = Regex.Match(line, "^Public (" + Id + ")(\\(\\))? As (" + Id + ")");
                if (field.Success && field.Groups[1].Value != "Const")
                {
                    members.Add("  " + ConvertDataType(field.Groups[3].Value) + (field.Groups[2].Success ? "[]" : "") + " " + field.Groups[1].Value + " { get; set; }");
                }
                continue;
            }
            // the body must be empty
            var j = i + 1;
            for (; j < lines.Length; j++)
            {
                var b = Trim(DeComment(lines[j], true));
                if (Regex.IsMatch(b, "^End (Sub|Function|Property)")) break;
                if (b != "" && !LMatch(b, "Attribute ")) return null;
            }
            i = j;
            if (h.Groups[1].Value == "Private ") continue;
            var kind = h.Groups[2].Value;
            var name = h.Groups[3].Value;
            if (LMatch(kind, "Property"))
            {
                var args = Regex.Match(line, name + "\\s*\\(([^)]*)\\)(?:\\s*As\\s+(" + Id + "))?");
                var type = kind == "Property Get" && args.Groups[2].Success ? args.Groups[2].Value : "";
                if (kind != "Property Get")
                {
                    var last = ModConvertStatements.SplitTopLevel(args.Groups[1].Value);
                    var lastArg = last[last.Count - 1];
                    var asIdx = lastArg.IndexOf(" As ", StringComparison.Ordinal);
                    type = asIdx > 0 ? Trim(lastArg.Substring(asIdx + 4)) : "Variant";
                }
                if (!props.TryGetValue(name, out var pr)) props[name] = pr = new[] { type, "", "" };
                if (pr[0] == "" || pr[0] == "Variant") pr[0] = type;
                if (kind == "Property Get") pr[1] = "get; ";
                else pr[2] = "set; ";
                continue;
            }
            var ret = "";
            var sig = ConvertPrototype(line, ref ret, false, ref ret);
            sig = Trim(NextBy(sig, "{"));
            sig = Regex.Replace(sig, "^(public |private |internal )*(static )?", "");
            sig = Replace(sig, "_UNUSED", "");
            members.Add("  " + sig + ";");
        }
        foreach (var p in props)
        {
            members.Add("  " + ConvertDataType(p.Value[0] == "" ? "Variant" : p.Value[0]) + " " + p.Key + " { " + p.Value[1] + p.Value[2] + "}");
        }
        return ReString(ModProjectGroup.TypeModifier(model.Exposed) + " interface " + model.Name + " {" + vbCrLf + string.Join(vbCrLf, members) + vbCrLf + "}", true);
    }

    /// <summary>
    /// WithEvents variable: a property that moves the module's handlers (name_Event procedures) from the old object to the new one.
    /// </summary>
    public static string WithEventsProperty(string mods, string cType, string vbType, string name, int ind)
    {
        var handlers = new List<string>();
        foreach (Match x in Regex.Matches(ModConvertStatements.FileSource, "(?m)^\\s*(?:Private |Public |Friend )?Sub\\s+" + name + "_(" + Id + ")\\s*\\(")) handlers.Add(x.Groups[1].Value);
        var project = IsProjectClass(vbType);
        string Hook(string op)
        {
            var r = "";
            foreach (var h in handlers) r = r + " _" + name + "." + (project ? "event" : "") + h + " " + op + " " + name + "_" + h + ";";
            return r;
        }
        var i = SSpace(ind);
        var todo = project || handlers.Count == 0 ? "" : " // TODO: check the handler signatures against the events of " + vbType;
        return i + Replace(mods, "public ", "private ") + cType + " _" + name + ";" + vbCrLf
               + i + mods + cType + " " + name + " {" + todo + vbCrLf
               + i + "  get => _" + name + ";" + vbCrLf
               + i + "  set {" + vbCrLf
               + (handlers.Count == 0 ? "" : i + "    if (_" + name + " != null) {" + Hook("-=") + " }" + vbCrLf)
               + i + "    _" + name + " = value;" + vbCrLf
               + (handlers.Count == 0 ? "" : i + "    if (_" + name + " != null) {" + Hook("+=") + " }" + vbCrLf)
               + i + "  }" + vbCrLf
               + i + "}" + vbCrLf;
    }
}
