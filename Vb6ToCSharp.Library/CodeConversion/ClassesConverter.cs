using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Vb6ToCSharp.CodeGeneration;
using Vb6ToCSharp.Parsing.Model;
using static Vb6ToCSharp.Runtime.VbConstants;
using static Vb6ToCSharp.Runtime.VbStrings;
using static Vb6ToCSharp.Parsing.ProjectConfigurationParser;
using static Vb6ToCSharp.CodeConversion.CodeConverter;
using static Vb6ToCSharp.CodeConversion.ConverterUtils;
using static Vb6ToCSharp.Parsing.ProjectFiles;
using static Vb6ToCSharp.Infrastructure.TextFiles;
using static Vb6ToCSharp.CodeConversion.ConversionUtility;
using static Vb6ToCSharp.CodeConversion.Vb6ToCsConverter;

namespace Vb6ToCSharp.CodeConversion;

/// <summary>
/// VB6 class-module semantics (as VB Migration Partner converts them): Class_Initialize / Class_Terminate, Implements,
/// default members (VB_UserMemId = 0), NewEnum (VB_UserMemId = -4), VB_PredeclaredId default instances, events.
/// </summary>
public static class ClassesConverter
{
    internal const string Id = "[A-Za-z_][A-Za-z0-9_]*";

    /// <summary>The class module being converted (null for standard modules and forms).</summary>
    public static ClassDefinition Current;

    private static string registryProject;
    private static readonly Dictionary<string, ClassDefinition> classes = new Dictionary<string, ClassDefinition>(StringComparer.OrdinalIgnoreCase);
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
                var src = ReadEntireFile(folder + f);
                var model = ClassDefinition.Scan(ModuleName(src), src);
                classes[model.Name] = model;
            }
            foreach (var f in Split(VbpClasses(VbpFile) + vbCrLf + VbpForms(VbpFile) + vbCrLf + VbpUserControls(VbpFile), vbCrLf))
            {
                if (Trim(f) == "" || !System.IO.File.Exists(folder + f)) continue;
                foreach (var i in ImplementsOf(ReadEntireFile(folder + f))) implemented.Add(i);
            }
            var project = CodeConverter.ProjectInfo();
            // the exposed classes of the projects this one references are known as well (New, default members, For Each...)
            foreach (var r in ProjectGroup.ReferencedProjects(project))
            {
                foreach (var src in SourcesOf(r, VbpClasses(r.Path)))
                {
                    var model = ClassDefinition.Scan(ModuleName(src), src);
                    if (model.Exposed && !classes.ContainsKey(model.Name)) classes[model.Name] = model;
                }
            }
            // a class another group project implements is an interface, even when no class of this project implements it
            foreach (var r in ProjectGroup.ReferencingProjects(project))
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
    internal static IEnumerable<string> ImplementsOf(string source)
    {
        foreach (Match x in Regex.Matches(source ?? "", "(?m)^\\s*Implements\\s+(" + Id + "(?:\\." + Id + ")?)"))
        {
            yield return ProjectGroup.StripProjectQualifier(x.Groups[1].Value);
        }
    }

    /// <summary>Sources of files (a .vbp list) of another project.</summary>
    private static IEnumerable<string> SourcesOf(ProjectInfo project, string files)
    {
        foreach (var f in Split(files, vbCrLf))
        {
            if (Trim(f) == "") continue;
            var path = System.IO.Path.Combine(project.Folder, Trim(f));
            if (System.IO.File.Exists(path)) yield return ReadEntireFile(path);
        }
    }

    /// <summary>Forgets the project classes read so far (a new conversion run: sources may have changed).</summary>
    public static void ResetCaches() => registryProject = null;

    /// <summary>Registers a class (the one being converted, or one known only from a test).</summary>
    public static void Register(ClassDefinition definition)
    {
        EnsureRegistry();
        classes[definition.Name] = definition;
        foreach (var i in definition.Implements) implemented.Add(i);
    }

    /// <summary>The class model of <paramref name="name"/> (a <c>Lib.CFoo</c> project qualifier is ignored), or null.</summary>
    private static ClassDefinition Find(string name)
    {
        EnsureRegistry();
        if (name == null) return null;
        return classes.TryGetValue(name, out var c) || classes.TryGetValue(ProjectGroup.StripProjectQualifier(name), out c) ? c : null;
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
        return name != null && (implemented.Contains(name) || implemented.Contains(ProjectGroup.StripProjectQualifier(name)));
    }

    /// <summary>obj(i) indexes: VB VbCollection, or a class whose default member takes parameters.</summary>
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

    /// <summary>Class declaration line (attributes, base interfaces) of <paramref name="definition"/>.</summary>
    public static string ClassDeclaration(ClassDefinition definition)
    {
        var bases = new List<string>(definition.Implements);
        if (definition.HasTerminate) bases.Add("IDisposable");
        if (definition.EnumSource != null) bases.Add("System.Collections.IEnumerable");
        // with parameters the default member is an indexer, which already makes the type's DefaultMember (C# forbids both)
        var attr = definition.DefaultMember != null && definition.DefaultParams == "" ? "[System.Reflection.DefaultMember(\"" + definition.DefaultMember + "\")]" + vbCrLf : "";
        return attr + ProjectGroup.TypeModifier(definition.Exposed) + " class " + definition.Name + (bases.Count > 0 ? " : " + string.Join(", ", bases) : "") + " {";
    }

    /// <summary>Members VB6 provides implicitly: constructor, Dispose / finalizer, default instance, indexer, enumerator.</summary>
    public static string ClassMembers(ClassDefinition definition)
    {
        var n = vbCrLf;
        var i1 = SSpace(spIndent);
        var r = new StringBuilder();
        if (definition.HasInitialize)
        {
            r.Append("public " + definition.Name + "() {" + n + i1 + "Class_Initialize(); // VB6 Class_Initialize" + n + "}" + n);
        }
        if (definition.HasTerminate)
        { // VB6 Class_Terminate runs when the last reference goes: Dispose (deterministic) or the finalizer
            r.Append("private bool vbTerminated;" + n);
            r.Append("public void Dispose() {" + n + i1 + "if (vbTerminated) return;" + n + i1 + "vbTerminated = true;" + n + i1 + "GC.SuppressFinalize(this);" + n + i1 + "Class_Terminate();" + n + "}" + n);
            r.Append("~" + definition.Name + "() {" + n + i1 + "if (!vbTerminated) { vbTerminated = true; Class_Terminate(); }" + n + "}" + n);
        }
        if (definition.Predeclared)
        {
            r.Append("private static " + definition.Name + " vbDefaultInstance;" + n);
            r.Append("/// <summary>VB6 default instance (VB_PredeclaredId).</summary>" + n);
            r.Append("public static " + definition.Name + " instance { get => vbDefaultInstance ?? (vbDefaultInstance = new " + definition.Name + "()); set => vbDefaultInstance = value; }" + n);
        }
        if (definition.DefaultMember != null && definition.DefaultParams != "")
        { // obj(i): the default member with parameters is the indexer
            var parms = new List<string>();
            var names = new List<string>();
            foreach (var a in StatementsConverter.SplitTopLevel(definition.DefaultParams))
            {
                if (Trim(a) == "") continue;
                var c = ConvertParameter(a, true);
                if (LMatch(c, "ref ") || LMatch(c, "out ")) c = Mid(c, 5);
                var eq = InStr(c, "=");
                if (eq > 0) c = Trim(Left(c, eq - 1));
                parms.Add(c);
                names.Add(SplitWord(c, -1));
            }
            var type = ConvertDataType(definition.DefaultType);
            // the indexer's metadata name must differ from the default member's method (both would be "Item")
            r.Append("[System.Runtime.CompilerServices.IndexerName(\"Default" + definition.DefaultMember + "\")]" + n);
            r.Append("public " + type + " this[" + string.Join(", ", parms) + "] { get { return " + definition.DefaultMember + "(" + string.Join(", ", names) + "); }"
                     + (definition.DefaultHasSetter ? " set { set_" + definition.DefaultMember + "(" + string.Join(", ", names) + ", value); }" : "") + " }" + n);
        }
        if (definition.EnumSource != null)
        { // For Each over the class: NewEnum
            r.Append("public System.Collections.IEnumerator GetEnumerator() {" + n + i1 + "return ((System.Collections.IEnumerable)" + definition.EnumSource + ").GetEnumerator();" + n + "}" + n);
        }
        return r.ToString();
    }

    /// <summary>
    /// A class used through Implements whose procedures are all empty (a VB6 "interface class") becomes a C# interface;
    /// null when the class has code (it stays a class).
    /// </summary>
    public static string InterfaceDeclaration(ClassDefinition definition)
    {
        var lines = Split(Replace(definition.Source, vbLf, ""), vbCr);
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
                    members.Add(SSpace(spIndent) + ConvertDataType(field.Groups[3].Value) + (field.Groups[2].Success ? "[]" : "") + " " + field.Groups[1].Value + " { get; set; }");
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
                    var last = StatementsConverter.SplitTopLevel(args.Groups[1].Value);
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
            members.Add(SSpace(spIndent) + sig + ";");
        }
        foreach (var p in props)
        {
            members.Add(SSpace(spIndent) + ConvertDataType(p.Value[0] == "" ? "Variant" : p.Value[0]) + " " + p.Key + " { " + p.Value[1] + p.Value[2] + "}");
        }
        return ReString(ProjectGroup.TypeModifier(definition.Exposed) + " interface " + definition.Name + " {" + vbCrLf + string.Join(vbCrLf, members) + vbCrLf + "}", true);
    }

    /// <summary>
    /// WithEvents variable: a property that moves the module's handlers (name_Event procedures) from the old object to the new one.
    /// </summary>
    public static string WithEventsProperty(string mods, string cType, string vbType, string name, int ind)
    {
        var handlers = new List<string>();
        foreach (Match x in Regex.Matches(StatementsConverter.FileSource, "(?m)^\\s*(?:Private |Public |Friend )?Sub\\s+" + name + "_(" + Id + ")\\s*\\(")) handlers.Add(x.Groups[1].Value);
        var project = IsProjectClass(vbType);
        string Hook(string op)
        {
            var r = "";
            foreach (var h in handlers) r = r + " _" + name + "." + (project ? "event" : "") + h + " " + op + " " + name + "_" + h + ";";
            return r;
        }
        var i = SSpace(ind);
        var i1 = i + SSpace(spIndent);
        var i2 = i + SSpace(spIndent * 2);
        var todo = project || handlers.Count == 0 ? "" : " // TODO: check the handler signatures against the events of " + vbType;
        return i + Replace(mods, "public ", "private ") + cType + " _" + name + ";" + vbCrLf
               + i + mods + cType + " " + name + " {" + todo + vbCrLf
               + i1 + "get => _" + name + ";" + vbCrLf
               + i1 + "set {" + vbCrLf
               + (handlers.Count == 0 ? "" : i2 + "if (_" + name + " != null) {" + Hook("-=") + " }" + vbCrLf)
               + i2 + "_" + name + " = value;" + vbCrLf
               + (handlers.Count == 0 ? "" : i2 + "if (_" + name + " != null) {" + Hook("+=") + " }" + vbCrLf)
               + i1 + "}" + vbCrLf
               + i + "}" + vbCrLf;
    }
}