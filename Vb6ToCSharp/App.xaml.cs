using System.Windows;
using Vb6ToCSharp.ItemConversion;

namespace Vb6ToCSharp;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    // VB6 project Startup="frm": show the form's default instance
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        ConversionUtility.Progress = (val, max, cap) => Forms.MainForm.Instance.Prg(val, max, cap);
        Forms.MainForm.Instance.Show();
    }
}