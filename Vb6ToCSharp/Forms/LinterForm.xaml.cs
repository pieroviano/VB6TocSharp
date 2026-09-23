using System.Windows;
using Vb6ToCSharp.CodeErrors;
using Vb6ToCSharp.Parsing;
using Vb6ToCSharp.UI;
using static Microsoft.VisualBasic.Strings;
using static Vb6ToCSharp.Runtime.RuntimeExtension;


namespace Vb6ToCSharp.Forms;

public partial class LinterForm : Window
{
    private static LinterForm _instance;
    public static LinterForm Instance { set => _instance = null; get => _instance ??= new LinterForm(); }
    public static void Load() { if (_instance == null) { dynamic a = LinterForm.Instance; } }
    public static void Unload() { if (_instance != null) Instance.Close(); _instance = null; }
    public LinterForm() { InitializeComponent(); }


    // Option Explicit //Right Justify


    private void Form_Load(object sender, RoutedEventArgs e)
    {
        txtVBPFile.Text = ProjectConfigurationParser.VbpFile;
        txtFile.Text = "";
    }

    private void cmdClose_Click(object sender, RoutedEventArgs e) { VBCloseFile(null); }

    private void cmdBrowseVBPFile_Click(object sender, RoutedEventArgs e) { BrowseDialog.BrowseFile(this, txtVBPFile, BrowseDialog.ProjectFilter); }

    private void cmdBrowseFile_Click(object sender, RoutedEventArgs e) { BrowseDialog.BrowseFile(this, txtFile, BrowseDialog.LintFilter); }

    private void cmdLint_Click(object sender, RoutedEventArgs e)
    {
        fraConfig.IsEnabled = false;
        var results = QuickLint.LintFileOrProject(txtFile.Text);
        fraConfig.IsEnabled = true;

        txtResults.Text = IIf(results == "", "Done.", results);
    }
}