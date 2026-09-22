using System.Windows;
using Vb6ToCSharp.Modules;
using static Microsoft.VisualBasic.Constants;
using static Microsoft.VisualBasic.FileSystem;
using static Microsoft.VisualBasic.Interaction;
using static Microsoft.VisualBasic.Strings;
using static Vb6ToCSharp.Modules.ModConfig;


namespace Vb6ToCSharp.Forms;

public partial class FrmConfig : Window
{
    private static FrmConfig _instance;
    public static FrmConfig Instance { set => _instance = null; get => _instance ??= new FrmConfig();
    }
    public static void Load() { if (_instance == null) { dynamic a = FrmConfig.Instance; } }
    public static void Unload() { if (_instance != null) Instance.Close(); _instance = null; }
    public FrmConfig() { InitializeComponent(); }


    // Option Explicit //Right Justify


    private void Form_Load(object sender, RoutedEventArgs e) { Form_Load(); }
    private void Form_Load()
    {
        ModConfig.hush = true;
        txtVBPFile.Text = ModConfig.VbpFile;
        txtOutput.Text = ModConfig.OutputFolder();
        txtAssemblyName.Text = ModConfig.AssemblyName();
        ModConfig.hush = false;
    }

    private void cmdCancel_Click(object sender, RoutedEventArgs e) { cmdCancel_Click(); }
    private void cmdCancel_Click()
    {
        Unload();
    }

    private void cmdOK_Click(object sender, RoutedEventArgs e) { cmdOK_Click(); }
    private void cmdOK_Click()
    {
        ModIni.IniWrite(iniSectionSettings, iniKeyVbpFile, txtVBPFile.Text, IniFile());
        ModIni.IniWrite(iniSectionSettings, iniKeyOutputFolder, txtOutput.Text, IniFile());
        ModIni.IniWrite(iniSectionSettings, iniKeyAssemblyName, txtAssemblyName.Text, IniFile());
        ModConfig.LoadSettings(true);
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

    private void txtVBPFile_GotFocus(object sender, RoutedEventArgs e) { txtVBPFile_GotFocus(); }
    private void txtVBPFile_GotFocus()
    {
        txtVBPFile.SelectionStart = 0;
        txtVBPFile.SelectionLength = Len(txtVBPFile.Text);
    }

    private void txtOutput_GotFocus(object sender, RoutedEventArgs e) { txtOutput_GotFocus(); }
    private void txtOutput_GotFocus()
    {
        txtOutput.SelectionStart = 0;
        txtOutput.SelectionLength = Len(txtOutput.Text);
    }

    private void txtAssemblyName_GotFocus(object sender, RoutedEventArgs e) { txtAssemblyName_GotFocus(); }
    private void txtAssemblyName_GotFocus()
    {
        txtAssemblyName.SelectionStart = 0;
        txtAssemblyName.SelectionLength = Len(txtAssemblyName.Text);
    }


}