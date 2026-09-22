using System.Windows;
using Vb6ToCSharp.Modules;
using static Microsoft.VisualBasic.Strings;
using static Vb6ToCSharp.VbExtension;


namespace Vb6ToCSharp.Forms;

public partial class FrmLinter : Window
{
    private static FrmLinter _instance;
    public static FrmLinter Instance { set => _instance = null;
        get => _instance ??= new FrmLinter();
    }
    public static void Load() { if (_instance == null) { dynamic a = FrmLinter.Instance; } }
    public static void Unload() { if (_instance != null) Instance.Close(); _instance = null; }
    public FrmLinter() { InitializeComponent(); }


    // Option Explicit //Right Justify


    private void Form_Load(object sender, RoutedEventArgs e) { Form_Load(); }
    private void Form_Load()
    {
        txtVBPFile.Text = ModConfig.VbpFile;
        txtFile.Text = "";
    }

    private void cmdClose_Click(object sender, RoutedEventArgs e) { cmdClose_Click(); }
    private void cmdClose_Click()
    {
        VBCloseFile(null); 
    }

    private void cmdLint_Click(object sender, RoutedEventArgs e) { cmdLint_Click(); }
    private void cmdLint_Click()
    {
        var results = "";


        fraConfig.IsEnabled = false;
        if (txtFile.Text == "")
        {
            results = ModQuickLint.Lint();
        }
        else
        {
            var file = txtFile.Text;
            if (InStr(file, "\\") == 0)
            {
                file = Left(txtVBPFile.Text, InStrRev(txtVBPFile.Text, "\\")) + file;
            }
            results = ModQuickLint.Lint(file);
        }
        fraConfig.IsEnabled = true;

        txtResults.Text = IIf(results == "", "Done.", results);
    }


}