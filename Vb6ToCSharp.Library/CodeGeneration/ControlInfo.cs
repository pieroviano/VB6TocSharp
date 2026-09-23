using Vb6ToCSharp.ItemConversion;

namespace Vb6ToCSharp.CodeGeneration;

public sealed class ControlInfo
{
    public string VbType { get; set; } = "";
    public ControlKind Kind { get; set; }
    /// <summary>Fully qualified WinForms type.</summary>
    public string WinForms { get; set; } = "";
    /// <summary>XAML element (visual) or C# type (non-visual) for WPF.</summary>
    public string Wpf { get; set; } = "";
    /// <summary>VB6 default property (<c>Text1 = "x"</c> means <c>Text1.Text</c>).</summary>
    public string DefaultProperty { get; set; } = "Caption";
    /// <summary>Type library of a hosted ActiveX (<c>MSCommLib</c>).</summary>
    public string Library { get; set; } = "";

    public string Type(UiTarget ui) => ui == UiTarget.WinForms ? WinForms : Wpf;
    public bool IsContainer => Kind is ControlKind.Container or ControlKind.Root or ControlKind.TabbedContainer;
}