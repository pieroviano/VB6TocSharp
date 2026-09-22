using System.Windows;
using Vb6ToCSharp.Modules;
using static Microsoft.VisualBasic.Constants;
using static Microsoft.VisualBasic.FileSystem;
using static Microsoft.VisualBasic.Interaction;
using static Vb6ToCSharp.Modules.ModConfig;
using static Vb6ToCSharp.Modules.ModConvert;
using static Vb6ToCSharp.Modules.ModProjectFiles;
using static Vb6ToCSharp.Modules.ModRefScan;
using static Vb6ToCSharp.Modules.ModSupportFiles;
using static Vb6ToCSharp.Modules.ModUtils;
using static Vb6ToCSharp.VbConstants;
using static Vb6ToCSharp.VbExtension;


namespace Vb6ToCSharp.Forms
{
    public partial class Frm : Window
    {
        private static Frm _instance;
        public static Frm Instance { set { _instance = null; } get { return _instance ?? (_instance = new Frm()); } }
        public static void Load() { if (_instance == null) { dynamic a = Frm.Instance; } }
        public static void Unload() { if (_instance != null) Instance.Close(); _instance = null; }
        public Frm() { InitializeComponent(); }


        // Option Explicit //Right Justify
        public int pMax = 0;


        private void cmdAll_Click(object sender, RoutedEventArgs e) { cmdAll_Click(); }
        private void cmdAll_Click()
        {
            if (!ConfigValid())
            {
                return;

            }
            IsWorking();
            ConvertProject(txtSrc.Text);
            IsWorking(true);
        }

        private void cmdClasses_Click(object sender, RoutedEventArgs e) { cmdClasses_Click(); }
        private void cmdClasses_Click()
        {
            if (!ConfigValid())
            {
                return;

            }
            IsWorking();
            ConvertFileList(FilePath(txtSrc.Text), VbpClasses(txtSrc.Text));
            IsWorking(true);
        }

        private void cmdConfig_Click(object sender, RoutedEventArgs e) { cmdConfig_Click(); }
        private void cmdConfig_Click()
        {
            FrmConfig.Instance.Show(1);
            ModConfig.LoadSettings();
        }

        private void cmdExit_Click(object sender, RoutedEventArgs e) { cmdExit_Click(); }
        private void cmdExit_Click()
        {
            Unload();
        }

        private void cmdFile_Click(object sender, RoutedEventArgs e) { cmdFile_Click(); }
        private void cmdFile_Click()
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

        private void cmdForms_Click(object sender, RoutedEventArgs e) { cmdForms_Click(); }
        private void cmdForms_Click()
        {
            if (!ConfigValid())
            {
                return;

            }
            IsWorking();
            ConvertFileList(FilePath(txtSrc.Text), VbpForms(txtSrc.Text));
            IsWorking(true);
        }

        private void cmdModules_Click(object sender, RoutedEventArgs e) { cmdModules_Click(); }
        private void cmdModules_Click()
        {
            if (!ConfigValid())
            {
                return;

            }
            IsWorking();
            ConvertFileList(FilePath(txtSrc.Text), VbpModules(txtSrc.Text));
            IsWorking(true);
        }

        private bool ConfigValid()
        {
            bool configValid = false;
            ModConfig.LoadSettings();

            if (Dir(ModConfig.VbpFile) == "")
            {
                MsgBox("Project file not found.  Perhaps do config first?", vbExclamation, "File Not Found");
                return configValid;

            }
            if (Dir(ModConfig.OutputFolder(), vbDirectory) == "")
            {
                MsgBox("Ouptut Folder not found.  Perhaps do config first?", vbExclamation, "Directory Not Found");
                return configValid;

            }
            if (ModConfig.AssemblyName() == "")
            {
                MsgBox("Assembly name not set.  Perhaps do config first?", vbExclamation, "Setting Not Found");
                return configValid;

            }
            configValid = true;
            return configValid;
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
            cmdScan.IsEnabled = done;
            cmdSupport.IsEnabled = done;
            MousePointer = IIf(done, vbDefault, vbHourglass);
        }

        public string Prg(int val = -1, int max = -1, string cap = "#")
        {
            string prg = "";
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

        private void cmdLint_Click(object sender, RoutedEventArgs e) { cmdLint_Click(); }
        private void cmdLint_Click()
        {
            if (!ConfigValid())
            {
                return;

            }
            FrmLinter.Instance.Show(vbModal);
        }

        private void cmdScan_Click(object sender, RoutedEventArgs e) { cmdScan_Click(); }
        private void cmdScan_Click()
        {
            if (!ConfigValid())
            {
                return;

            }
            IsWorking(false);
            ScanRefs();
            IsWorking(true);
        }

        private void cmdSupport_Click(object sender, RoutedEventArgs e) { cmdSupport_Click(); }
        private void cmdSupport_Click()
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

        private void Form_Load(object sender, RoutedEventArgs e) { Form_Load(); }
        private void Form_Load()
        {
            ModConfig.hush = true;
            ModConfig.LoadSettings();
            ModConfig.hush = false;
            txtSrc.Text = VbpFile;
        }


    }
}
