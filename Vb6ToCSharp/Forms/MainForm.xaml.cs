using System.Windows;
using Vb6ToCSharp.Runtime;
using Vb6ToCSharp.UI;
using static Microsoft.VisualBasic.Constants;
using static Microsoft.VisualBasic.FileSystem;
using static Microsoft.VisualBasic.Interaction;
using static Vb6ToCSharp.Parsing.ProjectConfigurationParser;
using static Vb6ToCSharp.ItemConversion.CodeConverter;
using static Vb6ToCSharp.Parsing.ProjectFiles;
using static Vb6ToCSharp.Modules.ModRefScan;
using static Vb6ToCSharp.Modules.ModSupportFiles;
using static Vb6ToCSharp.Modules.ModUtils;
using static Vb6ToCSharp.Runtime.RuntimeExtension;
using Vb6ToCSharp.Parsing;

namespace Vb6ToCSharp.Forms;

public partial class MainForm : Window
{
    private static MainForm _instance;
    public static MainForm Instance { set => _instance = null;
        get => _instance ??= new MainForm();
    }
    public static void Load() { if (_instance == null) { dynamic a = MainForm.Instance; } }
    public static void Unload() { if (_instance != null) Instance.Close(); _instance = null; }
    public MainForm() { InitializeComponent(); }


    // Option Explicit //Right Justify
    public int pMax = 0;


    private void cmdAll_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfigValid(allowGroup: true)) // a project group (.vbg) converts as a whole
        {
            return;

        }
        IsWorking();
        ConvertProject(txtSrc.Text);
        IsWorking(true);
    }

    private void cmdClasses_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfigValid())
        {
            return;

        }
        IsWorking();
        ConvertFileList(FilePath(txtSrc.Text), VbpClasses(txtSrc.Text));
        IsWorking(true);
    }

    private void cmdConfig_Click(object sender, RoutedEventArgs e)
    {
        ConfigForm.Instance.Show(1);
        ProjectConfigurationParser.LoadSettings();
    }

    private void cmdExit_Click(object sender, RoutedEventArgs e) { Unload(); }

    private void cmdBrowseSrc_Click(object sender, RoutedEventArgs e) { BrowseDialog.BrowseFile(this, txtSrc, BrowseDialog.ProjectFilter); }

    private void cmdBrowseFile_Click(object sender, RoutedEventArgs e) { BrowseDialog.BrowseFile(this, txtFile, BrowseDialog.SourceFilter); }

    private void cmdFile_Click(object sender, RoutedEventArgs e)
    {
        if (txtFile.Text == "")
        {
            MsgBox("Enter a file in the box.", vbExclamation, "No File Entered");
            return;

        }
        if (!ConfigValid())
        {
            return;

        }
        IsWorking();
        var success = ConvertFile(txtFile.Text);
        IsWorking(true);
        if (success)
        {
            MsgBox("Converted " + txtFile.Text + ".");
        }
    }

    private void cmdForms_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfigValid())
        {
            return;

        }
        IsWorking();
        ConvertFileList(FilePath(txtSrc.Text), VbpForms(txtSrc.Text));
        IsWorking(true);
    }

    private void cmdModules_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfigValid())
        {
            return;

        }
        IsWorking();
        ConvertFileList(FilePath(txtSrc.Text), VbpModules(txtSrc.Text));
        IsWorking(true);
    }

    private bool ConfigValid(bool allowGroup = false)
    {
        var error = ProjectConfigurationParser.ValidateSettings(allowGroup);
        if (error != "")
        {
            MsgBox(error, vbExclamation, "Configuration");
            return false;

        }

        return true;
    }

    private void IsWorking(bool done = false)
    {
        txtFile.IsEnabled = done;
        cmdConfig.IsEnabled = done;
        cmdLint.IsEnabled = done;
        cmdFile.IsEnabled = done;
        cmdAll.IsEnabled = done;
        cmdClasses.IsEnabled = done;
        cmdExit.IsEnabled = done;
        cmdForms.IsEnabled = done;
        cmdModules.IsEnabled = done;
        txtSrc.IsEnabled = done;
        cmdBrowseSrc.IsEnabled = done;
        cmdBrowseFile.IsEnabled = done;
        cmdScan.IsEnabled = done;
        cmdSupport.IsEnabled = done;
        MousePointer = CInt(IIf(done, Cursors.Default, Cursors.Hourglass));
    }

    public string Prg(int val = -1, int max = -1, string cap = "#")
    {
        var prg = "";
        // TODO (not supported): On Error Resume Next
        if (max >= 0)
        {
            pMax = max;
        }
        lblPrg.Content = IIf(prg == "#", "", cap);
        if (pMax != 0)
        {
            shpPrg.Width = (double)val / pMax * shpPrgBack.Width;
        }
        shpPrg.Visibility = val >= 0 ? Visibility.Visible : Visibility.Hidden;
        lblPrg.Visibility = shpPrg.Visibility;
        return prg;
    }

    private void cmdLint_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfigValid())
        {
            return;

        }
        LinterForm.Instance.Show((int)DialogType.Modal);
    }

    private void cmdScan_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfigValid())
        {
            return;

        }
        IsWorking();
        ScanRefs();
        IsWorking(true);
    }

    private void cmdSupport_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfigValid())
        {
            return;

        }
        if (MsgBox("Generate Project files?", vbYesNo) == vbYes)
        {
            CreateProjectFile(VbpFile);
        }
        if (MsgBox("Generate Support files?", vbYesNo) == vbYes)
        {
            CreateProjectSupportFiles();
        }
    }

    private void Form_Load(object sender, RoutedEventArgs e)
    {
        ProjectConfigurationParser.hush = true;
        ProjectConfigurationParser.LoadSettings();
        ProjectConfigurationParser.hush = false;
        txtSrc.Text = VbpFile;
    }
}