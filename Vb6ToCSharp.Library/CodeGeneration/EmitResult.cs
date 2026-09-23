using System;
using System.Collections.Generic;
using Vb6ToCSharp.Parsing;

namespace Vb6ToCSharp.CodeGeneration;

/// <summary>Output of a UI emitter.</summary>
public sealed class EmitResult
{
    /// <summary>Designer.cs (WinForms) or XAML (WPF).</summary>
    public string Designer { get; set; } = "";
    /// <summary>Members added to the code file (WPF: non-visual fields, extras method).</summary>
    public string CodeMembers { get; set; } = "";
    /// <summary>Statements run in the constructor right after InitializeComponent.</summary>
    public string ConstructorCode { get; set; } = "";
    public List<FormResource> Resources { get; } = new();
    /// <summary>Type libraries of controls hosted through AxHost.</summary>
    public HashSet<string> HostedLibraries { get; } = new(StringComparer.OrdinalIgnoreCase);
    public bool UsesPowerPacks { get; set; }
    public bool UsesWindowsFormsHost { get; set; }
}