using static Microsoft.VisualBasic.Constants;
using static Microsoft.VisualBasic.Strings;
using static Vb6ToCSharp.Runtime.RuntimeExtension;
using static Vb6ToCSharp.Parsing.ProjectConfigurationParser;
using static Vb6ToCSharp.Parsing.ProjectFiles;
using static Vb6ToCSharp.Infrastructure.TextFiles;
using static Vb6ToCSharp.CodeConversion.ConversionUtility;
using Vb6ToCSharp.CodeConversion.Model;
using Vb6ToCSharp.Parsing.Model;
using Vb6ToCSharp.Runtime;

namespace Vb6ToCSharp.CodeGeneration;

public static class UsingEverything
{
    // Option Explicit
    private static string everything = "";
    private static string everythingKey;


    public static string UseEverything(string packageName = "")
    {
        var name = "";

        dynamic l = null;

        var e = "";
        var r = "";
        var n = vbCrLf;
        var m = "";

        if (packageName != "")
        {
            //    R = R & N & "package " & PackagePrefix & PackageName & ";"
            r = r + n + "";
        }

        var key = Ui + "|" + VbpFile + "|" + AssemblyName(); // per project and UI (was per UI only: a second project reused the first one's)
        if (everything == "" || everythingKey != key)
        {
            everythingKey = key;
            // what the generated project references: .NET Framework, Microsoft.VisualBasic, UpgradeHelpers (ADODB only when used)
            e = e + m + "using System.Runtime.InteropServices;";
            e = e + n + "using Microsoft.VisualBasic;";
            e = e + n + "using Microsoft.VisualBasic.CompilerServices;"; // Conversions: VB6 implicit conversions
            e = e + n + "using static Vb6ToCSharp.UpgradeHelpers.VbRuntime;"; // ReDim, NewArray, FixedLen, MidStmt, DoEvents, Load / Unload...
            e = e + n + "using static System.Math;";
            e = e + n + "using static Microsoft.VisualBasic.Constants;";
            e = e + n + "using static Microsoft.VisualBasic.Conversion;";
            e = e + n + "using static Microsoft.VisualBasic.DateAndTime;";
            e = e + n + "using static Microsoft.VisualBasic.FileSystem;";
            e = e + n + "using static Microsoft.VisualBasic.Financial;";
            e = e + n + "using static Microsoft.VisualBasic.Information;";
            e = e + n + "using static Microsoft.VisualBasic.Interaction;";
            e = e + n + "using static Microsoft.VisualBasic.Strings;";
            e = e + n + "using static Microsoft.VisualBasic.VBMath;";
            if (VbpFile != "" && SupportFiles.UsesAdo(ProjectInfo.Load(VbpFile)))
            {
                e = e + n + "using ADODB;";
            }
            e = e + n + "using System;";
            e = e + n + "using System.Collections.Generic;";
            e = e + n + "using System.Linq;";
            e = e + n + "using System.Text;";
            e = e + n + "using System.Threading.Tasks;";
            if (Ui == UiTarget.WinForms)
            {
                // WPF namespaces would make Button, TextBox, Label, Application… ambiguous
                e = e + n + "using System.Drawing;";
                e = e + n + "using System.Windows.Forms;";
                e = e + n + "using Vb6ToCSharp.UpgradeHelpers;";
                e = e + n + "using Vb6ToCSharp.UpgradeHelpers.Arrays;";
                e = e + n + "using Vb6ToCSharp.UpgradeHelpers.Dialogs;";
                e = e + n + "using Vb6ToCSharp.UpgradeHelpers.Interop;";
                e = e + n + "using Vb6ToCSharp.UpgradeHelpers.Model;";
                e = e + n + "using Vb6ToCSharp.UpgradeHelpers.WinForms.Controls;";
                e = e + n + "using Vb6ToCSharp.UpgradeHelpers.WinForms.Helpers;";
                // CommonDialog also exists in System.Windows.Forms: in converted code it is the VB6 one
                e = e + n + "using CommonDialog = Vb6ToCSharp.UpgradeHelpers.Dialogs.CommonDialog;";
            }
            else
            {
                e = e + n + "using System.Windows;";
                e = e + n + "using System.Windows.Controls;";
                e = e + n + "using System.Windows.Data;";
                e = e + n + "using System.Windows.Documents;";
                e = e + n + "using System.Windows.Input;";
                e = e + n + "using System.Windows.Media;";
                e = e + n + "using System.Windows.Media.Imaging;";
                e = e + n + "using System.Windows.Shapes;";
                e = e + n + "using Vb6ToCSharp.UpgradeHelpers;";
                e = e + n + "using Vb6ToCSharp.UpgradeHelpers.Arrays;";
                e = e + n + "using Vb6ToCSharp.UpgradeHelpers.Dialogs;";
                e = e + n + "using Vb6ToCSharp.UpgradeHelpers.Interop;";
                e = e + n + "using Vb6ToCSharp.UpgradeHelpers.Model;";
                e = e + n + "using Vb6ToCSharp.UpgradeHelpers.Wpf.Controls;";
                e = e + n + "using Vb6ToCSharp.UpgradeHelpers.Wpf.Helpers;";
                // Model and Wpf.Helpers both declare Vb6Color: in a WPF project it is the Media one
                e = e + n + "using Vb6Color = Vb6ToCSharp.UpgradeHelpers.Wpf.Helpers.Vb6Color;";
            }

            e = e + n;

            if (VbpForms(VbpFile) != "")
            { // the namespace exists only when there are forms
                e = e + n + "using " + AssemblyName() + ".Forms;";
            }
            if (VbpUserControls(VbpFile) != "")
            {
                e = e + n + "using " + AssemblyName() + ".UserControls;";
            }

            var path = FilePath(VbpFile);
            foreach (var iterL in Split(VbpModules(VbpFile), vbCrLf))
            {
                l = iterL;
                if (l != "")
                {
                    name = ModuleName(ReadEntireFile(path + l));
                    e = e + n + "using static " + packagePrefix + name + ";";
                }
            }
            foreach (var iterL in Split(VbpForms(VbpFile), vbCrLf))
            {
                l = iterL;
                if (l != "")
                {
                    name = ModuleName(ReadEntireFile(path + l));
                    e = e + n + "using static " + AssemblyName() + ".Forms." + name + ";";
                }
            }
            //    For Each L In Split(VBPClasses(vbpFile), vbCrLf)  ' controls?
            //      If L <> "" Then
            //        Name = ModuleName(ReadEntireFile(Path & L))
            //        E = E & N & "using " & PackagePrefix & Name & ";"
            //      End If
            //    Next
            everything = e;
        }

        r = everything + n + r;
        var usingEverything = r;
        return usingEverything;
    }
}