using System.IO;
using System.Linq;
using System.Text;
using Vb6ToCSharp.FormConversion;

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

    private static VbFormFile Parse() => FrmParser.Parse(Frm);

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
    public void VbValue_ToNumber(string raw, double expected) => Assert.Equal(expected, VbValue.ToNumber(raw, 7));

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

public class FrxReaderTests
{
    private static byte[] Bytes(params byte[] b) => b;

    [Fact]
    public void ReadBlob_LtHeader()
    {
        var img = new byte[] { 0x42, 0x4D, 1, 2, 3 };
        var data = new byte[] { (byte)'l', (byte)'t', 0, 0, 5, 0, 0, 0 }.Concat(img).ToArray();
        var blob = new FrxReader(data).ReadBlob(0);
        Assert.Equal(img, blob);
        Assert.Equal(FrxBlobKind.Bmp, FrxReader.Sniff(blob));
    }

    [Fact]
    public void ReadBlob_SizeHeaderAtOffset_SkipsPaddingBeforeMagic()
    {
        var data = new byte[] { 9, 9, 9, 10, 0, 0, 0, 0xAA, 0xBB, 0x00, 0x00, 0x01, 0x00, 7, 7, 7, 7 };
        var blob = new FrxReader(data).ReadBlob(3);
        Assert.Equal(FrxBlobKind.Icon, FrxReader.Sniff(blob));
        Assert.Equal(8, blob.Length);
    }

    [Fact]
    public void ReadString_Variants()
    {
        var enc = Encoding.Default;
        var u8 = new byte[] { 3 }.Concat(enc.GetBytes("abc")).ToArray();
        Assert.Equal("abc", new FrxReader(u8).ReadString(0));
        var ff = new byte[] { 0xFF, 4, 0 }.Concat(enc.GetBytes("wxyz")).ToArray();
        Assert.Equal("wxyz", new FrxReader(ff).ReadString(0));
        var u32 = new byte[] { 5, 0, 0, 0 }.Concat(enc.GetBytes("a\r\nbc")).ToArray();
        Assert.Equal("a\r\nbc", new FrxReader(u32).ReadString(0));
        var one = new byte[] { 1, (byte)'Z', 0, 0 };
        Assert.Equal("Z", new FrxReader(one).ReadString(0));
    }

    [Fact]
    public void ReadListAndItemData()
    {
        var enc = Encoding.Default;
        var list = new byte[] { 2, 0, 3, 0 }.Concat(enc.GetBytes("One")).Concat(new byte[] { 3, 0 }).Concat(enc.GetBytes("Two")).ToArray();
        Assert.Equal(new[] { "One", "Two" }, new FrxReader(list).ReadList(0));
        var data = new byte[] { 2, 0, 10, 0, 0, 0, 0xFF, 0xFF, 0xFF, 0xFF };
        Assert.Equal(new[] { 10, -1 }, new FrxReader(data).ReadItemData(0));
    }

    [Fact]
    public void Reads_AreBoundsSafe()
    {
        var r = new FrxReader(Bytes(0xFF));
        Assert.Equal("", r.ReadString(5));
        Assert.Empty(r.ReadList(0));
        Assert.Empty(r.ReadBlob(10));
    }

    [Theory]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38 }, FrxBlobKind.Gif, ".gif")]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, FrxBlobKind.Jpeg, ".jpg")]
    [InlineData(new byte[] { 0xD7, 0xCD, 0xC6, 0x9A }, FrxBlobKind.Wmf, ".wmf")]
    [InlineData(new byte[] { 0x00, 0x00, 0x02, 0x00 }, FrxBlobKind.Cursor, ".cur")]
    [InlineData(new byte[] { 1, 2, 3, 4 }, FrxBlobKind.Unknown, ".bin")]
    public void Sniff_KnownFormats(byte[] b, FrxBlobKind kind, string ext)
    {
        Assert.Equal(kind, FrxReader.Sniff(b));
        Assert.Equal(ext, FrxReader.Extension(kind));
    }
}

public class UnitsColorTests
{
    private static VbControl Ctl(string frm, string name) => FrmParser.Parse(frm).Find(name);

    private const string Twips = "Begin VB.Form f \r\n Begin VB.CommandButton b \r\n  Left = 150\r\n  Top = 300\r\n  Width = 1500\r\n  Height = 450\r\n End\r\nEnd\r\n";
    private const string Pixels = "Begin VB.Form f \r\n ScaleMode = 3\r\n Begin VB.Frame fr \r\n  Begin VB.CommandButton b \r\n   Left = 10\r\n   Width = 100\r\n  End\r\n End\r\nEnd\r\n";
    private const string User = "Begin VB.Form f \r\n ClientWidth = 3000\r\n ClientHeight = 1500\r\n ScaleMode = 0\r\n ScaleWidth = 100\r\n ScaleHeight = 50\r\n ScaleLeft = 10\r\n Begin VB.CommandButton b \r\n  Left = 20\r\n  Width = 10\r\n  Top = 5\r\n End\r\nEnd\r\n";

    [Fact]
    public void Twips_ToPixels()
    {
        var b = Ctl(Twips, "b");
        Assert.Equal(10, Units.PosPx(b, "Left"));
        Assert.Equal(20, Units.PosPx(b, "Top"));
        Assert.Equal(100, Units.SizePx(b, "Width"));
        Assert.Equal(30, Units.SizePx(b, "Height"));
    }

    [Fact]
    public void ScaleMode_InheritedThroughFrame()
    {
        var b = Ctl(Pixels, "b");
        Assert.Equal(10, Units.PosPx(b, "Left"));
        Assert.Equal(100, Units.SizePx(b, "Width"));
    }

    [Fact]
    public void ScaleMode_UserDefinedUsesScaleOriginAndClientSize()
    {
        var b = Ctl(User, "b");
        Assert.Equal(300, Units.PosTwips(b, "Left"));   // (20 - 10) * 3000/100
        Assert.Equal(300, Units.SizeTwips(b, "Width"));  // 10 * 30
        Assert.Equal(150, Units.PosTwips(b, "Top"));     // 5 * 1500/50
    }

    [Fact]
    public void SizeTwips_MissingUsesDefault() => Assert.Equal(99, Units.SizeTwips(Ctl(Twips, "b"), "Nope", 99));

    [Fact]
    public void Colors_SystemAndRgb()
    {
        Assert.Equal("System.Drawing.SystemColors.Control", VbColor.WinForms(VbColor.Parse("&H8000000F&")));
        Assert.Equal("System.Drawing.SystemColors.WindowText", VbColor.WinForms(VbColor.Parse("&H80000008&")));
        Assert.Equal("System.Drawing.Color.FromArgb(255, 128, 0)", VbColor.WinForms(VbColor.Parse("&H000080FF&")));
        Assert.Equal("#FF8000", VbColor.Xaml(VbColor.Parse("&H000080FF&")));
        Assert.Equal("{DynamicResource {x:Static SystemColors.ControlBrushKey}}", VbColor.Xaml(VbColor.Parse("&H8000000F&")));
        Assert.Null(VbColor.SystemName(0x000000FF));
    }
}

public class VbpInfoTests
{
    [Fact]
    public void Parse_ReadsProjectFacts()
    {
        var v = VbpInfo.Parse(
            "Type=Exe\r\nForm=frmMain.frm\r\nForm=frmB.frm\r\nUserControl=ucX.ctl\r\nModule=modA; modA.bas\r\n" +
            "Object={831FDD16-0C5C-11D2-A9FC-0000F8754DA1}#2.0#0; MSCOMCTL.OCX\r\n" +
            "Object={831FDD16-0C5C-11D2-A9FC-0000F8754DA1}#2.0#0; MSCOMCTL.OCX\r\n" +
            "Startup=\"frmMain\"\r\nName=\"Proj1\"\r\n");
        Assert.Equal("Proj1", v.Name);
        Assert.Equal("frmMain", v.Startup);
        Assert.False(v.StartsWithSubMain);
        Assert.Equal(new[] { "frmMain.frm", "frmB.frm" }, v.Forms);
        Assert.Equal(new[] { "ucX.ctl" }, v.UserControls);
        Assert.Single(v.Objects);
    }

    [Fact]
    public void DesignerFiles_FindMdiAndUserControls()
    {
        var dir = TestUtil.TempDir();
        File.WriteAllText(Path.Combine(dir, "p.vbp"), "Form=frmMdi.frm\r\nUserControl=ucX.ctl\r\nStartup=\"Sub Main\"\r\n");
        File.WriteAllText(Path.Combine(dir, "frmMdi.frm"), "VERSION 5.00\r\nBegin VB.MDIForm frmMdi \r\nEnd\r\nAttribute VB_Name = \"frmMdi\"\r\n");
        File.WriteAllText(Path.Combine(dir, "ucX.ctl"), "VERSION 5.00\r\nBegin VB.UserControl ucX \r\nEnd\r\nAttribute VB_Name = \"ucX\"\r\n");
        var v = VbpInfo.Load(Path.Combine(dir, "p.vbp"));
        Assert.True(v.StartsWithSubMain);
        Assert.Equal("frmMdi", v.MdiFormName);
        Assert.Contains("ucX", v.UserControlNames);
    }
}

public class OcxInteropTests
{
    private static readonly OcxRef Comm = OcxRef.Parse("{648A5603-2C6E-101B-82B6-000000000014}#1.1#0; MSCOMM32.OCX");
    private static readonly OcxRef Custom = OcxRef.Parse("{11111111-2222-3333-4444-555555555555}#3.a#0; ACME.OCX");

    private static void WithLookup(Func<Guid, short, short, string> f, Action a)
    {
        var old = OcxInterop.RegistryLookup;
        OcxInterop.ClearCache();
        OcxInterop.RegistryLookup = f;
        try { a(); }
        finally
        {
            OcxInterop.RegistryLookup = old;
            OcxInterop.ClearCache();
        }
    }

    [Fact]
    public void LibraryName_RegistryFirstThenKnownFile() =>
        WithLookup((g, ma, mi) => g == Guid.Parse("11111111-2222-3333-4444-555555555555") && ma == 3 && mi == 10 ? "AcmeLib" : null, () =>
        {
            Assert.Equal("AcmeLib", OcxInterop.LibraryName(Custom));
            Assert.Equal("MSCommLib", OcxInterop.LibraryName(Comm));
        });

    [Fact]
    public void ComReferences_OnlyHostedLibraries() =>
        WithLookup((_, _, _) => null, () =>
        {
            var x = OcxInterop.ComReferences(new[] { Comm, Custom }, new[] { "MSCommLib" });
            Assert.Contains("<COMReference Include=\"AxMSCommLib\">", x);
            Assert.Contains("<COMReference Include=\"MSCommLib\">", x);
            Assert.Contains("<WrapperTool>aximp</WrapperTool>", x);
            Assert.Contains("<VersionMinor>1</VersionMinor>", x);
            Assert.DoesNotContain("ACME", x);
        });

    [Fact]
    public void ComReferences_UnresolvedKeptWhenAHostedLibIsUnmatched() =>
        WithLookup((_, _, _) => null, () =>
        {
            var x = OcxInterop.ComReferences(new[] { Custom }, new[] { "AcmeLib" });
            Assert.Contains("Include=\"AxACME\"", x);
        });

    [Fact]
    public void ComReferences_NothingHosted_Empty() =>
        WithLookup((_, _, _) => null, () => Assert.Equal("", OcxInterop.ComReferences(new[] { Comm }, new string[0])));
}
