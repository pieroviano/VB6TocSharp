using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace Vb6ToCSharp.UpgradeHelpers.WinForms.Helpers;

/// <summary>WinForms implementation of VB6 <c>DoEvents</c>, <c>Load</c> and <c>Unload</c>.</summary>
public sealed class WinFormsUiBridge : IVbUiBridge
{
    /// <summary>Registers this stack with the language runtime when the package is loaded.</summary>
#pragma warning disable CA2255 // registering the UI stack when the package is loaded is what a module initializer is for
    [ModuleInitializer]
    internal static void Register() => VbRuntime.RegisterUi(new WinFormsUiBridge());
#pragma warning restore CA2255

    /// <summary>WinForms answers whenever no other stack is running (see the WPF bridge's priority).</summary>
    public bool IsActive => true;

    public int DoEvents()
    {
        Application.DoEvents();
        return Application.OpenForms.Count;
    }

    public bool Load(object form)
    {
        if (form is not Form f) return false;
        if (!f.IsHandleCreated) f.CreateControl();
        return true;
    }

    public bool Unload(object form)
    {
        if (form is not Form f) return false;
        f.Close();
        return true;
    }
}
