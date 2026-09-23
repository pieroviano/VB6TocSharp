using System;
using System.IO;
using Vb6ToCSharp.CodeConversion;
using Vb6ToCSharp.Infrastructure;
using Vb6ToCSharp.Parsing;

namespace Vb6ToCSharp.Tests.Fixtures;

/// <summary>
/// Points ModConfig (INI next to the test assembly) at a tiny temp VB6 project so ModRefScan can scan it.
/// </summary>
public sealed class ConverterFixture : IDisposable
{
    public readonly string Dir = TestUtil.TempDir();
    private readonly string ini = ProjectConfigurationParser.IniFile();
    private readonly string refs = AppDomain.CurrentDomain.BaseDirectory + "\\refs.txt";
    private readonly string? iniBackup;

    public ConverterFixture()
    {
        iniBackup = File.Exists(ini) ? File.ReadAllText(ini) : null;
        var vbp = Path.Combine(Dir, "prj.vbp");
        File.WriteAllText(vbp, "Type=Exe\r\nForm=frmA.frm\r\nModule=modA; modA.bas\r\n");
        File.WriteAllText(Path.Combine(Dir, "frmA.frm"),
            "VERSION 5.00\r\n" +
            "Begin VB.Form frmA \r\n" +
            "   Caption         =   \"Hello\"\r\n" +
            "   ClientHeight    =   3000\r\n" +
            "   ClientWidth     =   4000\r\n" +
            "   Begin VB.CommandButton cmdOK \r\n" +
            "      Caption         =   \"OK\"\r\n" +
            "      Height          =   495\r\n" +
            "      Left            =   120\r\n" +
            "      TabIndex        =   0\r\n" +
            "      Top             =   120\r\n" +
            "      Width           =   1215\r\n" +
            "   End\r\n" +
            "End\r\n" +
            "Attribute VB_Name = \"frmA\"\r\n" +
            "Attribute VB_GlobalNameSpace = False\r\n" +
            "Option Explicit\r\n\r\n" +
            "Private Sub cmdOK_Click()\r\n  Unload Me\r\nEnd Sub\r\n");
        File.WriteAllText(Path.Combine(Dir, "modA.bas"),
            "Attribute VB_Name = \"modA\"\r\nOption Explicit\r\n\r\n" +
            "Public Function Twice(ByVal X As Long) As Long\r\n  Twice = X * 2\r\nEnd Function\r\n\r\n" +
            "Public Function Add2(ByVal A As Long, ByVal B As Long) As Long\r\n  Add2 = A + B\r\nEnd Function\r\n");
        IniInterop.IniWrite(ProjectConfigurationParser.iniSectionSettings, ProjectConfigurationParser.iniKeyVbpFile, vbp, ini);
        IniInterop.IniWrite(ProjectConfigurationParser.iniSectionSettings, ProjectConfigurationParser.iniKeyOutputFolder, Path.Combine(Dir, "out") + "\\",
            ini);
        IniInterop.IniWrite(ProjectConfigurationParser.iniSectionSettings, ProjectConfigurationParser.iniKeyAssemblyName, "TestAsm", ini);
        ProjectConfigurationParser.LoadSettings(true);
        RefScanner.ScanRefs();
    }

    public void Dispose()
    {
        if (iniBackup == null) File.Delete(ini);
        else File.WriteAllText(ini, iniBackup);
        ProjectConfigurationParser.LoadSettings(true);
        try
        {
            File.Delete(refs);
        }
        catch
        {
        }

        try
        {
            Directory.Delete(Dir, true);
        }
        catch
        {
        }
    }
}
