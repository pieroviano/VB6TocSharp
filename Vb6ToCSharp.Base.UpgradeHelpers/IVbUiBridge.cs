namespace Vb6ToCSharp.UpgradeHelpers;

/// <summary>
/// The UI stack a converted program runs on (WinForms or WPF), for the VB6 statements the language runtime
/// cannot implement on its own. Each UI package registers one with
/// <see cref="VbRuntime.RegisterUi(IVbUiBridge, int)"/>; the highest priority active bridge wins.
/// </summary>
public interface IVbUiBridge
{
    /// <summary>This stack is the one the program is running on (WPF: an <c>Application</c> exists).</summary>
    bool IsActive { get; }

    /// <summary>Processes the pending UI messages; answers the number of open forms.</summary>
    int DoEvents();

    /// <summary>VB6 <c>Load form</c>: creates the form without showing it; false when the object is not of this stack.</summary>
    bool Load(object form);

    /// <summary>VB6 <c>Unload form</c>: closes it; false when the object is not of this stack.</summary>
    bool Unload(object form);
}
