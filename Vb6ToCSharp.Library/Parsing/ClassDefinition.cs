using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic;
using Vb6ToCSharp.ItemConversion;

namespace Vb6ToCSharp.Parsing;

/// <summary>Facts about one class module, read from its VB6 source.</summary>
public sealed class ClassDefinition
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

    public static ClassDefinition Scan(string name, string source)
    {
        var m = new ClassDefinition { Name = name, Source = source ?? "" };
        var src = m.Source;
        m.Implements.AddRange(ClassesConverter.ImplementsOf(src));
        m.Exposed = ProjectGroup.IsExposed(src);
        m.HasInitialize = Regex.IsMatch(src, "(?m)^\\s*(?:Private |Public )?Sub\\s+Class_Initialize\\s*\\(");
        m.HasTerminate = Regex.IsMatch(src, "(?m)^\\s*(?:Private |Public )?Sub\\s+Class_Terminate\\s*\\(");
        m.Predeclared = Regex.IsMatch(src, "(?m)^\\s*Attribute\\s+VB_PredeclaredId\\s*=\\s*True");
        foreach (Match x in Regex.Matches(src, "(?m)^\\s*Attribute\\s+(" + ClassesConverter.Id + ")\\.VB_UserMemId\\s*=\\s*(-?[0-9]+)"))
        {
            if (x.Groups[2].Value == "0") m.DefaultMember = x.Groups[1].Value;
            if (x.Groups[2].Value == "-4") m.EnumMember = x.Groups[1].Value;
        }
        if (m.EnumMember != null)
        {
            var e = Regex.Match(src, "(?m)Set\\s+" + m.EnumMember + "\\s*=\\s*(" + ClassesConverter.Id + ")\\.(?:\\[_NewEnum\\]|_NewEnum)");
            if (e.Success) m.EnumSource = e.Groups[1].Value;
        }
        if (m.DefaultMember != null)
        {
            var d = Regex.Match(src, "(?m)^\\s*(?:Public |Friend )?(?:Property Get|Function)\\s+" + m.DefaultMember + "\\s*\\(([^)]*)\\)(?:\\s*As\\s+(" + ClassesConverter.Id + "(?:\\." + ClassesConverter.Id + ")?))?");
            if (d.Success)
            {
                m.DefaultParams = Strings.Trim(d.Groups[1].Value);
                m.DefaultType = d.Groups[2].Success ? d.Groups[2].Value : "Variant";
            }
            m.DefaultHasSetter = Regex.IsMatch(src, "(?m)^\\s*(?:Public |Friend )?Property (?:Let|Set)\\s+" + m.DefaultMember + "\\s*\\([^)]*,");
        }
        foreach (Match x in Regex.Matches(src, "(?m)^\\s*(?:Public )?Event\\s+(" + ClassesConverter.Id + ")")) m.Events.Add(x.Groups[1].Value);
        foreach (Match x in Regex.Matches(src, "(?m)^\\s*(?:Public |Friend )?Property (?:Let|Set)\\s+(" + ClassesConverter.Id + ")\\s*\\([^)]*,")) m.ParameterizedSetters.Add(x.Groups[1].Value);
        foreach (Match x in Regex.Matches(src, "(?m)^\\s*(?:Public |Friend )?(?:Static )?(?:Sub|Function)\\s+(" + ClassesConverter.Id + ")")) m.Methods.Add(x.Groups[1].Value);
        return m;
    }
}