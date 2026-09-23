using Vb6ToCSharp.CodeGeneration;

namespace Vb6ToCSharp.Analisys;

/// <summary>How a VB6 event maps to a .NET event.</summary>
public sealed class EventBinding
{
    public string VbEvent { get; set; } = "";
    /// <summary>.NET event (XAML attribute for WPF), or "" for <see cref="Special"/> events.</summary>
    public string NetEvent { get; set; } = "";
    public string Delegate { get; set; } = "System.EventHandler";
    public string Args { get; set; } = "System.EventArgs";
    /// <summary>VB6 parameters in order (without the control-array Index).</summary>
    public ArgumentRole[] Roles { get; set; } = new ArgumentRole[0];
    /// <summary>Statement run first in the adapter (e.g. ignore an OptionButton being cleared).</summary>
    public string Guard { get; set; } = "";
    /// <summary><c>ctor</c> (Initialize), <c>InitProperties</c>, <c>ReadProperties</c>, <c>WriteProperties</c>: no .NET event.</summary>
    public string Special { get; set; } = "";
    /// <summary>The handler is the VB method itself (UserControl events): no adapter.</summary>
    public bool Direct { get; set; }
}