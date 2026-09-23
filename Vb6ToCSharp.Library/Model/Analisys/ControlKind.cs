namespace Vb6ToCSharp.Analisys;

/// <summary>How a VB6 control is emitted.</summary>
public enum ControlKind
{
    /// <summary>Visual control without children.</summary>
    Standard,
    /// <summary>Visual control hosting children (Frame, PictureBox…).</summary>
    Container,
    /// <summary>Top-level designer root (Form, MDIForm, UserControl).</summary>
    Root,
    /// <summary>Component without UI (Timer, CommonDialog, ImageList).</summary>
    NonVisual,
    Menu,
    Line,
    Shape,
    /// <summary>SSTab: children are split into tab pages.</summary>
    TabbedContainer,
    /// <summary>ActiveX control hosted through AxHost.</summary>
    Hosted,
    /// <summary>A UserControl of the converted project.</summary>
    ProjectUserControl,
    /// <summary>No .NET equivalent: placeholder + TODO.</summary>
    Placeholder,
}