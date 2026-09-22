using static Microsoft.VisualBasic.Constants;
using static Microsoft.VisualBasic.Strings;
using static Vb6ToCSharp.Modules.ModConfig;
using static Vb6ToCSharp.Modules.ModProjectFiles;
using static Vb6ToCSharp.Modules.ModTextFiles;
using static Vb6ToCSharp.Modules.ModUtils;


namespace Vb6ToCSharp.Modules;

static class ModUsingEverything
{
    // Option Explicit
    private static string everything = "";
    private const string vb6Compat = "Microsoft.VisualBasic.Compatibility.VB6";


    public static string UsingEverything(string packageName = "")
    {
        string list = "";
        string name = "";

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

        if (everything == "")
        {
            e = e + m + "using VB6 = " + vb6Compat + ";";
            e = e + n + "using System.Runtime.InteropServices;";
            e = e + n + "using static VBExtension;";
            e = e + n + "using static VBConstants;";
            e = e + n + "using Microsoft.VisualBasic;";

            e = e + n + "using System;";
            e = e + n + "using System.Windows;";
            e = e + n + "using System.Windows.Controls;";
            e = e + n + "using static System.DateTime;";
            e = e + n + "using static System.Math;";

            e = e + n + "using static Microsoft.VisualBasic.Globals;";
            e = e + n + "using static Microsoft.VisualBasic.Collection;";
            e = e + n + "using static Microsoft.VisualBasic.Constants;";
            e = e + n + "using static Microsoft.VisualBasic.Conversion;";
            e = e + n + "using static Microsoft.VisualBasic.DateAndTime;";
            e = e + n + "using static Microsoft.VisualBasic.ErrObject;";
            e = e + n + "using static Microsoft.VisualBasic.FileSystem;";
            e = e + n + "using static Microsoft.VisualBasic.Financial;";
            e = e + n + "using static Microsoft.VisualBasic.Information;";
            e = e + n + "using static Microsoft.VisualBasic.Interaction;";
            e = e + n + "using static Microsoft.VisualBasic.Strings;";
            e = e + n + "using static Microsoft.VisualBasic.VBMath;";
            e = e + n + "using System.Collections.Generic;";

            e = e + n + "using static Microsoft.VisualBasic.PowerPacks.Printing.Compatibility.VB6.ColorConstants;";
            e = e + n + "using static Microsoft.VisualBasic.PowerPacks.Printing.Compatibility.VB6.DrawStyleConstants;";
            e = e + n + "using static Microsoft.VisualBasic.PowerPacks.Printing.Compatibility.VB6.FillStyleConstants;";
            e = e + n + "using static Microsoft.VisualBasic.PowerPacks.Printing.Compatibility.VB6.GlobalModule;";
            e = e + n + "using static Microsoft.VisualBasic.PowerPacks.Printing.Compatibility.VB6.Printer;";
            e = e + n + "using static Microsoft.VisualBasic.PowerPacks.Printing.Compatibility.VB6.PrinterCollection;";
            e = e + n + "using static Microsoft.VisualBasic.PowerPacks.Printing.Compatibility.VB6.PrinterObjectConstants;";
            e = e + n + "using static Microsoft.VisualBasic.PowerPacks.Printing.Compatibility.VB6.ScaleModeConstants;";
            e = e + n + "using static Microsoft.VisualBasic.PowerPacks.Printing.Compatibility.VB6.SystemColorConstants;";
            e = e + n + "using ADODB;";

            e = e + n + "using System;";
            e = e + n + "using System.Collections.Generic;";
            e = e + n + "using System.Linq;";
            e = e + n + "using System.Text;";
            e = e + n + "using System.Threading.Tasks;";
            e = e + n + "using System.Windows;";
            e = e + n + "using System.Windows.Controls;";
            e = e + n + "using System.Windows.Data;";
            e = e + n + "using System.Windows.Documents;";
            e = e + n + "using System.Windows.Input;";
            e = e + n + "using System.Windows.Media;";
            e = e + n + "using System.Windows.Media.Imaging;";
            e = e + n + "using System.Windows.Shapes;";

            e = e + n;

            e = e + n + "using " + AssemblyName() + ".Forms;";

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