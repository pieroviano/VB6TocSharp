using System.Windows;
using Vb6ToCSharp.Parsing;
using static Vb6ToCSharp.Runtime.VbConstants;
using static Vb6ToCSharp.Runtime.VbFileSystem;
using static Vb6ToCSharp.Runtime.VbInteraction;
using static Vb6ToCSharp.Runtime.VbStrings;
using static Vb6ToCSharp.Parsing.ProjectConfigurationParser;
using Vb6ToCSharp.UI;


namespace Vb6ToCSharp.Forms;

public partial class ConfigForm : Window
{
    private static ConfigForm _instance;
    public static ConfigForm Instance { set => _instance = null; get => _instance ??= new ConfigForm(); }
    public static void Load() { if (_instance == null) { dynamic a = ConfigForm.Instance; } }
    public static void Unload() { if (_instance != null) Instance.Close(); _instance = null; }
    public ConfigForm() { InitializeComponent(); }

    private void Form_Load(object sender, RoutedEventArgs e)
    {
        ProjectConfigurationParser.hush = true;
        txtVBPFile.Text = ProjectConfigurationParser.VbpFile;
        txtOutput.Text = ProjectConfigurationParser.OutputFolder();
        txtAssemblyName.Text = ProjectConfigurationParser.AssemblyName();
        ProjectConfigurationParser.hush = false;
    }

    private void cmdCancel_Click(object sender, RoutedEventArgs e) { Unload(); }

    private void cmdBrowseVBPFile_Click(object sender, RoutedEventArgs e) { BrowseDialog.BrowseFile(this, txtVBPFile, BrowseDialog.ProjectFilter); }

    private void cmdBrowseOutput_Click(object sender, RoutedEventArgs e) { BrowseDialog.BrowseFolder(this, txtOutput); }

    private void cmdOK_Click(object sender, RoutedEventArgs e)
    {
        ProjectConfigurationParser.SaveSettings(txtVBPFile.Text, txtOutput.Text, txtAssemblyName.Text);
        Unload();
    }

    private void txtOutput_Validate(ref bool cancelUnused)
    {
        if (Dir(txtOutput.Text, vbDirectory) == "")
        {
            MsgBox("Output folder does not exist.  Please create to prevent errors.");
        }
    }

    private void txtVBPFile_Validate(ref bool cancelUnused)
    {
        if (Dir(txtVBPFile.Text) == "")
        {
            MsgBox("Project file does not exist.  Please give a valid project to prevent errors.");
        }
    }

    private void txtAssemblyName_Validate(ref bool cancelUnused)
    {
        if (txtAssemblyName.Text == "")
        {
            MsgBox("Please enter something for an assembly name.");
        }
    }

    private void txtVBPFile_GotFocus(object sender, RoutedEventArgs e)
    {
        txtVBPFile.SelectionStart = 0;
        txtVBPFile.SelectionLength = Len(txtVBPFile.Text);
    }

    private void txtOutput_GotFocus(object sender, RoutedEventArgs e)
    {
        txtOutput.SelectionStart = 0;
        txtOutput.SelectionLength = Len(txtOutput.Text);
    }

    private void txtAssemblyName_GotFocus(object sender, RoutedEventArgs e)
    {
        txtAssemblyName.SelectionStart = 0;
        txtAssemblyName.SelectionLength = Len(txtAssemblyName.Text);
    }
}