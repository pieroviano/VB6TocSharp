using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using Vb6ToCSharp.UpgradeHelpers.Interop;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;

namespace Vb6ToCSharp.UpgradeHelpers.Wpf.Helpers;

/// <summary>WPF implementation of VB6 <c>DoEvents</c>, <c>Load</c> and <c>Unload</c>.</summary>
public sealed class WpfUiBridge : IVbUiBridge
{
    /// <summary>Ahead of WinForms: a WPF program also carries the WinForms package (the common dialogs).</summary>
    private const int WpfPriority = 10;

    /// <summary>Registers this stack and its OCX conversions with the language runtime when the package is loaded.</summary>
#pragma warning disable CA2255 // registering the UI stack when the package is loaded is what a module initializer is for
    [ModuleInitializer]
    internal static void Register()
    {
        VbRuntime.RegisterUi(new WpfUiBridge(), WpfPriority);
        OcxHelper.AddConverter(new WpfOcxValueConverter());
    }
#pragma warning restore CA2255

    /// <summary>A WPF <c>Application</c> is running.</summary>
    public bool IsActive => Application.Current != null;

    public int DoEvents()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
        return Application.Current?.Windows.Count ?? 0;
    }

    /// <summary>A WPF window exists as soon as it is constructed: VB6 <c>Load</c> has nothing left to do.</summary>
    public bool Load(object form) => form is Window;

    public bool Unload(object form)
    {
        if (form is not Window w) return false;
        w.Close();
        return true;
    }
}

/// <summary>OCX design-time values (VB6 OLE colors) as WPF <see cref="MediaColor"/> and <see cref="MediaBrush"/>.</summary>
public sealed class WpfOcxValueConverter : IOcxValueConverter
{
    public bool TryConvert(object value, Type type, out object result)
    {
        if (type == typeof(MediaColor))
        {
            result = Vb6Color.ToColor(OcxHelper.ToOleColor(value));
            return true;
        }
        if (typeof(MediaBrush).IsAssignableFrom(type))
        {
            result = Vb6Color.ToBrush(OcxHelper.ToOleColor(value));
            return true;
        }
        result = null;
        return false;
    }
}
