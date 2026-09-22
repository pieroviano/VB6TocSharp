using System.Windows;
using Vb6ToCSharp.Modules;
using static Microsoft.VisualBasic.Constants;
using static Microsoft.VisualBasic.FileSystem;
using static Microsoft.VisualBasic.Interaction;
using static Microsoft.VisualBasic.Strings;
using static Vb6ToCSharp.Modules.ModConfig;


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
        ModConfig.hush = true;
        txtVBPFile.Text = ModConfig.VbpFile;
        txtOutput.Text = ModConfig.OutputFolder();
        txtAssemblyName.Text = ModConfig.AssemblyName();
        ModConfig.hush = false;
    }

    private void cmdCancel_Click(object sender, RoutedEventArgs e) { Unload(); }

    private void cmdOK_Click(object sender, RoutedEventArgs e)
    {
        ModConfig.SaveSettings(txtVBPFile.Text, txtOutput.Text, txtAssemblyName.Text);
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