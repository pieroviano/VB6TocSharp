using System.IO;
using System.Linq;
using System.Text;
using Vb6ToCSharp.CodeGeneration;
using Vb6ToCSharp.Parsing;
using Vb6ToCSharp.Tests.Infrastructure;

namespace Vb6ToCSharp.Tests;

public class FormModelTests
{
    internal const string Frm =
        "VERSION 5.00\r\n" +
        "Object = \"{831FDD16-0C5C-11D2-A9FC-0000F8754DA1}#2.0#0\"; \"MSCOMCTL.OCX\"\r\n" +
        "Begin VB.Form frmMain \r\n" +
        "   Caption         =   \"Main \"\"Window\"\"\"\r\n" +
        "   ClientHeight    =   3000\r\n" +
        "   ClientWidth     =   4500\r\n" +
        "   BeginProperty Font \r\n" +
        "      Name            =   \"Tahoma\"\r\n" +
        "      Size            =   9.75\r\n" +
        "      Weight          =   700\r\n" +
        "   EndProperty\r\n" +
        "   Icon            =   \"frmMain.frx\":0000\r\n" +
        "   StartUpPosition =   2  'CenterScreen\r\n" +
        "   Begin VB.CommandButton cmd \r\n" +
        "      Caption         =   \"A 'quoted' apostrophe\"\r\n" +
        "      Index           =   1\r\n" +
        "      Left            =   120\r\n" +
        "   End\r\n" +
        "   Begin VB.CommandButton cmd \r\n" +
        "      Index           =   0\r\n" +
        "   End\r\n" +
        "   Begin MSComctlLib.StatusBar sb \r\n" +
        "      BeginProperty Panels {8E3867A5-8586-11D1-B16A-00C0F0283628} \r\n" +
        "         NumPanels       =   2\r\n" +
        "         BeginProperty Panel1 {8E3867AB-8586-11D1-B16A-00C0F0283628} \r\n" +
        "            Text            =   \"Ready\"\r\n" +
        "         EndProperty\r\n" +
        "         BeginProperty Panel2 {8E3867AB-8586-11D1-B16A-00C0F0283628} \r\n" +
        "            AutoSize        =   1\r\n" +
        "         EndProperty\r\n" +
        "      EndProperty\r\n" +
        "   End\r\n" +
        "   Begin VB.Frame fra \r\n" +
        "      BackColor       =   &H8000000F&\r\n" +
        "      Visible         =   0   'False\r\n" +
        "      Begin VB.TextBox txt \r\n" +
        "         Text            =   $\"frmMain.frx\":0010\r\n" +
        "      End\r\n" +
        "   End\r\n" +
        "   Begin VB.Menu mnuFile \r\n" +
        "      Caption         =   \"&File\"\r\n" +
        "      Begin VB.Menu mnuExit \r\n" +
        "         Caption         =   \"E&xit\"\r\n" +
        "         Shortcut        =   ^Q\r\n" +
        "      End\r\n" +
        "   End\r\n" +
        "End\r\n" +
        "Attribute VB_Name = \"frmMain\"\r\n" +
        "Attribute VB_GlobalNameSpace = False\r\n" +
        "Option Explicit\r\n" +
        "Private Sub Form_Load()\r\nEnd Sub\r\n";

    private static FormControlFile Parse() => FrmParser.Parse(Frm);

    [Fact]
    public void Parse_ReadsTreeAttributesAndCode()
    {
        var f = Parse();
        Assert.Equal("frmMain", f.Name);
        Assert.Equal("VB.Form", f.Root.Type);
        Assert.Equal(new[] { "cmd", "cmd", "sb", "fra", "mnuFile" }, f.Root.Children.Select(c => c.Name));
        Assert.Same(f.Root, f.Find("fra").Parent);
        Assert.Equal("txt", f.Find("fra").Children.Single().Name);
        Assert.StartsWith("Option Explicit", f.Code);
        Assert.Contains("Form_Load", f.Code);
        Assert.DoesNotContain("Attribute", f.Code);
    }

    [Fact]
    public void Parse_ReadsObjects()
    {
        var o = Parse().Objects.Single();
        Assert.Equal("{831FDD16-0C5C-11D2-A9FC-0000F8754DA1}", o.Guid);
        Assert.Equal(2, o.VersionMajor);
        Assert.Equal(0, o.VersionMinor);
        Assert.Equal("MSCOMCTL.OCX", o.File);
    }

    [Fact]
    public void Properties_UnquoteAndStripComments()
    {
        var root = Parse().Root;
        Assert.Equal("Main \"Window\"", root.Text("Caption"));
        Assert.Equal(2, root.Num("StartUpPosition"));
        Assert.Equal("A 'quoted' apostrophe", root.Children[0].Text("Caption"));
        Assert.False(Parse().Find("fra").Bool("Visible", true));
    }

    [Fact]
    public void Properties_NestedGroupsGetDottedNames()
    {
        var root = Parse().Root;
        Assert.Equal("Tahoma", root.Text("Font.Name"));
        Assert.Equal(9.75, root.Num("Font.Size"));
        var sb = Parse().Find("sb");
        Assert.Equal("Ready", sb.Text("Panels.Panel1.Text"));
        Assert.Equal(new[] { "Panel1", "Panel2" }, sb.SubGroups("Panels"));
    }

    [Fact]
    public void Arrays_IndexAndMemberName()
    {
        var f = Parse();
        var cmd = f.Root.Children[0];
        Assert.Equal(1, cmd.Index);
        Assert.Equal("_cmd_1", cmd.MemberName);
        Assert.Equal("sb", f.Find("sb").MemberName);
        Assert.Contains("cmd", f.ControlArrays);
        Assert.DoesNotContain("sb", f.ControlArrays);
    }

    [Fact]
    public void FrxRefs_AreParsed()
    {
        var f = Parse();
        var icon = f.Root.Get("Icon").Frx;
        Assert.Equal("frmMain.frx", icon.File);
        Assert.Equal(0, icon.Offset);
        Assert.False(icon.IsString);
        var text = f.Find("txt").Get("Text").Frx;
        Assert.Equal(0x10, text.Offset);
        Assert.True(text.IsString);
        Assert.Null(f.Root.Get("Caption").Frx);
    }

    [Theory]
    [InlineData("&H8000000F&", -2147483633)]
    [InlineData("&HFF&", 255)]
    [InlineData("-1  ", -1)]
    [InlineData("True", -1)]
    [InlineData("8.25", 8.25)]
    [InlineData("", 7)]
    public void VbValue_ToNumber(string raw, double expected) => Assert.Equal(expected, StringConvert.ToNumber(raw, 7));

    [Fact]
    public void ParseFile_SetsPathAndResolvesFrx()
    {
        var dir = TestUtil.TempDir();
        var p = Path.Combine(dir, "frmMain.frm");
        File.WriteAllText(p, Frm, Encoding.Default);
        var f = FrmParser.ParseFile(p);
        Assert.Equal(Path.Combine(dir, "frmMain.frx"), f.FrxPath(f.Root.Get("Icon").Frx));
    }

    [Fact]
    public void ParseUserControlAndMdi()
    {
        var uc = FrmParser.Parse("VERSION 5.00\r\nBegin VB.UserControl ucX \r\n   ScaleMode = 3\r\nEnd\r\nAttribute VB_Name = \"ucX\"\r\n");
        Assert.True(uc.IsUserControl);
        var mdi = FrmParser.Parse("VERSION 5.00\r\nBegin VB.MDIForm frmMdi \r\nEnd\r\nAttribute VB_Name = \"frmMdi\"\r\n");
        Assert.True(mdi.IsMdiForm);
        var child = FrmParser.Parse("VERSION 5.00\r\nBegin VB.Form frmC \r\n   MDIChild = -1  'True\r\nEnd\r\n");
        Assert.True(child.IsMdiChild);
    }
}