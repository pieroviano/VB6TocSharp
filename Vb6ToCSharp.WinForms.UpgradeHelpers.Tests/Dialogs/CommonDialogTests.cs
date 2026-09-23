using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;
using Vb6ToCSharp.UpgradeHelpers.Dialogs;
using CommonDialog = Vb6ToCSharp.UpgradeHelpers.Dialogs.CommonDialog;
using static Vb6ToCSharp.UpgradeHelpers.Dialogs.CommonDialogConstants;
using Vb6ToCSharp.UpgradeHelpers.Tests.Fixtures;

namespace Vb6ToCSharp.UpgradeHelpers.Tests.Dialogs;

public class CommonDialogTests
{
    private static CommonDialog WithHandler<TDialog>(Func<TDialog, DialogResult> handler) where TDialog : System.Windows.Forms.CommonDialog =>
        new() { ShowDialogHandler = (d, _) => handler((TDialog)d) };

    [Fact]
    public void ShowOpen_MapsPropertiesAndFlags()
    {
        Sta.Run(() =>
        {
            OpenFileDialog? seen = null;
            var cd = WithHandler<OpenFileDialog>(d =>
            {
                seen = d;
                Assert.True(d.Multiselect);
                Assert.True(d.CheckFileExists);
                Assert.True(d.CheckPathExists);
                Assert.False(d.ShowReadOnly);
                Assert.True(d.RestoreDirectory);
                Assert.Equal("Text|*.txt|All|*.*", d.Filter);
                Assert.Equal(2, d.FilterIndex);
                Assert.Equal(@"C:\init", d.InitialDirectory);
                Assert.Equal("Pick one", d.Title);
                Assert.Equal("txt", d.DefaultExt);
                Assert.True(d.AddExtension);
                d.FileName = @"C:\data\report.dat";
                d.FilterIndex = 1;
                return DialogResult.OK;
            });
            cd.Filter = "Text|*.txt|All|*.*";
            cd.FilterIndex = 2;
            cd.InitDir = @"C:\init";
            cd.DialogTitle = "Pick one";
            cd.DefaultExt = ".txt";
            cd.Flags = cdlOFNAllowMultiselect | cdlOFNFileMustExist | cdlOFNHideReadOnly | cdlOFNNoChangeDir | cdlOFNPathMustExist;

            cd.ShowOpen();

            Assert.NotNull(seen);
            Assert.Equal(@"C:\data\report.dat", cd.FileName);
            Assert.Equal("report.dat", cd.FileTitle);
            Assert.Equal(1, cd.FilterIndex);
            Assert.NotEqual(0, cd.Flags & cdlOFNExtensionDifferent);
        });
    }

    [Fact]
    public void ShowOpen_DefaultFlags_DoNotValidateExistence()
    {
        Sta.Run(() =>
        {
            var cd = WithHandler<OpenFileDialog>(d =>
            {
                Assert.False(d.Multiselect);
                Assert.False(d.CheckFileExists);
                Assert.True(d.ShowReadOnly);
                Assert.False(d.RestoreDirectory);
                d.FileName = @"C:\a.txt";
                return DialogResult.OK;
            });
            cd.DefaultExt = "txt";
            cd.ShowOpen();
            Assert.Equal(0, cd.Flags & cdlOFNExtensionDifferent);
        });
    }

    [Fact]
    public void ShowSave_OverwriteAndCreatePrompt()
    {
        Sta.Run(() =>
        {
            var cd = WithHandler<SaveFileDialog>(d =>
            {
                Assert.True(d.OverwritePrompt);
                Assert.True(d.CreatePrompt);
                d.FileName = @"C:\out\x.txt";
                return DialogResult.OK;
            });
            cd.Flags = cdlOFNOverwritePrompt | cdlOFNCreatePrompt;
            cd.ShowSave();
            Assert.Equal("x.txt", cd.FileTitle);

            var plain = WithHandler<SaveFileDialog>(d =>
            {
                Assert.False(d.OverwritePrompt);
                return DialogResult.OK;
            });
            plain.ShowSave();
        });
    }

    [Fact]
    public void Cancel_WithCancelError_ThrowsVbError32755()
    {
        Sta.Run(() =>
        {
            var cd = WithHandler<OpenFileDialog>(_ => DialogResult.Cancel);
            cd.CancelError = true;
            var e = Assert.Throws<CommonDialogCancelException>(() => cd.ShowOpen());
            Assert.Equal(32755, e.Number);
            Assert.Equal(32755, e.ErrorCode);
            Assert.Equal("Cancel was selected.", e.Message);
        });
    }

    [Fact]
    public void Cancel_WithoutCancelError_KeepsValues()
    {
        Sta.Run(() =>
        {
            var cd = WithHandler<SaveFileDialog>(d =>
            {
                d.FileName = @"C:\ignored.txt";
                return DialogResult.Cancel;
            });
            cd.FileName = "orig.txt";
            cd.ShowSave();
            Assert.Equal("orig.txt", cd.FileName);
        });
    }

    [Fact]
    public void ShowColor_UsesInitialColorOnlyWithRgbInit()
    {
        Sta.Run(() =>
        {
            var cd = WithHandler<ColorDialog>(d =>
            {
                Assert.Equal(Color.FromArgb(255, 0, 0).ToArgb(), d.Color.ToArgb());
                Assert.True(d.FullOpen);
                Assert.False(d.AllowFullOpen == false);
                d.Color = Color.FromArgb(0, 0, 255);
                return DialogResult.OK;
            });
            cd.Color = 0x0000FF;
            cd.Flags = cdlCCRGBInit | cdlCCFullOpen;
            cd.ShowColor();
            Assert.Equal(0xFF0000, cd.Color);

            var noInit = WithHandler<ColorDialog>(d =>
            {
                Assert.Equal(Color.Black.ToArgb(), d.Color.ToArgb());
                Assert.False(d.AllowFullOpen);
                return DialogResult.OK;
            });
            noInit.Color = 0x0000FF;
            noInit.Flags = cdlCCPreventFullOpen;
            noInit.ShowColor();
        });
    }

    [Fact]
    public void ShowFont_MapsFontAndEffects()
    {
        Sta.Run(() =>
        {
            var cd = WithHandler<FontDialog>(d =>
            {
                Assert.Equal("Arial", d.Font.Name);
                Assert.Equal(12f, d.Font.SizeInPoints, 1);
                Assert.True(d.Font.Bold);
                Assert.False(d.Font.Italic);
                Assert.True(d.ShowEffects);
                Assert.True(d.ShowColor);
                Assert.Equal(8, d.MinSize);
                Assert.Equal(20, d.MaxSize);
                Assert.Equal(Color.FromArgb(255, 0, 0).ToArgb(), d.Color.ToArgb());
                d.Font = new Font("Times New Roman", 10f, FontStyle.Italic | FontStyle.Underline);
                d.Color = Color.FromArgb(0, 255, 0);
                return DialogResult.OK;
            });
            cd.FontName = "Arial";
            cd.FontSize = 12;
            cd.FontBold = true;
            cd.Color = 0x0000FF;
            cd.Min = 8;
            cd.Max = 20;
            cd.Flags = cdlCFBoth | cdlCFEffects | cdlCFLimitSize;
            cd.ShowFont();

            Assert.Equal("Times New Roman", cd.FontName);
            Assert.Equal(10f, cd.FontSize, 1);
            Assert.False(cd.FontBold);
            Assert.True(cd.FontItalic);
            Assert.True(cd.FontUnderline);
            Assert.False(cd.FontStrikethru);
            Assert.Equal(0x00FF00, cd.Color);
        });
    }

    [Fact]
    public void ShowPrinter_MapsPageRangeAndCopies()
    {
        Sta.Run(() =>
        {
            var cd = WithHandler<PrintDialog>(d =>
            {
                var s = d.PrinterSettings;
                Assert.Equal(3, s.Copies);
                Assert.Equal(2, s.FromPage);
                Assert.Equal(5, s.ToPage);
                Assert.Equal(1, s.MinimumPage);
                Assert.Equal(9, s.MaximumPage);
                Assert.Equal(PrintRange.SomePages, s.PrintRange);
                Assert.True(s.Collate);
                Assert.False(d.AllowSelection);
                Assert.True(d.AllowSomePages);
                s.Copies = 2;
                s.PrintRange = PrintRange.AllPages;
                s.Collate = false;
                return DialogResult.OK;
            });
            cd.Copies = 3;
            cd.FromPage = 2;
            cd.ToPage = 5;
            cd.Min = 1;
            cd.Max = 9;
            cd.Flags = cdlPDPageNums | cdlPDNoSelection | cdlPDCollate;
            cd.ShowPrinter();

            Assert.Equal((short)2, cd.Copies);
            Assert.Equal(0, cd.Flags & cdlPDPageNums);
            Assert.Equal(0, cd.Flags & cdlPDCollate);
            Assert.NotNull(cd.PrinterSettings);
        });
    }

    [Theory]
    [InlineData(cdlHelpContext, HelpNavigator.TopicId)]
    [InlineData(cdlHelpKey, HelpNavigator.KeywordIndex)]
    [InlineData(cdlHelpPartialKey, HelpNavigator.Index)]
    [InlineData(cdlHelpContents, HelpNavigator.TableOfContents)]
    public void ShowHelp_MapsCommand(int command, HelpNavigator expected)
    {
        var calls = new List<(string File, HelpNavigator Nav, object? Param)>();
        var cd = new CommonDialog
        {
            HelpFile = "app.chm",
            HelpCommand = command,
            HelpContext = 42,
            HelpKey = "printing",
            ShowHelpHandler = (_, file, nav, param) => calls.Add((file, nav, param)),
        };
        cd.ShowHelp();
        var call = Assert.Single(calls);
        Assert.Equal("app.chm", call.File);
        Assert.Equal(expected, call.Nav);
        if (command == cdlHelpContext) Assert.Equal(42, call.Param);
        if (command == cdlHelpKey) Assert.Equal("printing", call.Param);
    }

    [Fact]
    public void ShowHelp_QuitOrNoFile_DoesNothing()
    {
        var called = false;
        var cd = new CommonDialog { HelpCommand = cdlHelpQuit, HelpFile = "x.chm", ShowHelpHandler = (_, _, _, _) => called = true };
        cd.ShowHelp();
        cd.HelpFile = "";
        cd.HelpCommand = cdlHelpContents;
        cd.ShowHelp();
        Assert.False(called);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("Text|*.txt", "Text|*.txt")]
    [InlineData("Text|*.txt|", "Text|*.txt")]
    [InlineData("Text|*.txt|Dangling", "Text|*.txt")]
    public void NormalizeFilter(string? vb, string expected) => Assert.Equal(expected, CommonDialog.NormalizeFilter(vb!));
}
