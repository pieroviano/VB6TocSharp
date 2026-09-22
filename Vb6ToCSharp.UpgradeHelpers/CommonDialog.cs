using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using Wf = System.Windows.Forms;
using static Vb6ToCSharp.UpgradeHelpers.CommonDialogConstants;

namespace Vb6ToCSharp.UpgradeHelpers;

/// <summary>
/// VB6 CommonDialog control (MSComDlg) over the WinForms common dialogs; usable from WinForms and WPF.
/// Every <c>ShowXxx</c> maps the VB6 properties/<see cref="Flags"/> onto a fresh .NET dialog, shows it and maps
/// the results back. When <see cref="CancelError"/> is true a cancelled dialog throws
/// <see cref="CommonDialogCancelException"/> (error 32755).
/// </summary>
[DesignerCategory("Code")]
public class CommonDialog : Component
{
    private const string DefaultFontName = "Microsoft Sans Serif";

    public string FileName { get; set; } = "";

    /// <summary>File name without path of the last selected file.</summary>
    public string FileTitle { get; private set; } = "";

    /// <summary>VB6 filter (<c>"Text|*.txt|All|*.*"</c>), same syntax as .NET.</summary>
    public string Filter { get; set; } = "";

    /// <summary>1-based selected filter.</summary>
    public int FilterIndex { get; set; }

    public string InitDir { get; set; } = "";
    public string DialogTitle { get; set; } = "";
    public string DefaultExt { get; set; } = "";

    /// <summary>cdlOFN* / cdlCC* / cdlCF* / cdlPD* flags; updated with the dialog's results.</summary>
    public int Flags { get; set; }

    public bool CancelError { get; set; }

    /// <summary>OLE color (<c>&amp;H00BBGGRR</c>).</summary>
    public int Color { get; set; }

    public string FontName { get; set; } = "";
    public float FontSize { get; set; }
    public bool FontBold { get; set; }
    public bool FontItalic { get; set; }
    public bool FontUnderline { get; set; }
    public bool FontStrikethru { get; set; }

    public short Copies { get; set; } = 1;
    public int FromPage { get; set; }
    public int ToPage { get; set; }

    /// <summary>Font size limits (cdlCFLimitSize) or printer page limits.</summary>
    public int Min { get; set; }

    public int Max { get; set; }

    /// <summary>Kept for source compatibility; the .NET print dialog always edits the selected printer settings.</summary>
    public bool PrinterDefault { get; set; } = true;

    public string HelpFile { get; set; } = "";

    /// <summary>cdlHelp* command used by <see cref="ShowHelp"/>.</summary>
    public int HelpCommand { get; set; }

    /// <summary>Topic id for cdlHelpContext / cdlHelpContextPopup.</summary>
    public int HelpContext { get; set; }

    /// <summary>Keyword for cdlHelpKey / cdlHelpPartialKey.</summary>
    public string HelpKey { get; set; } = "";

    /// <summary>Owner window of the dialogs (null = active window).</summary>
    public Wf.IWin32Window Owner { get; set; }

    /// <summary>Printer settings chosen by the last <see cref="ShowPrinter"/>.</summary>
    public PrinterSettings PrinterSettings { get; private set; }

    /// <summary>Test seam: shows a configured dialog and returns its result.</summary>
    internal Func<Wf.CommonDialog, Wf.IWin32Window, Wf.DialogResult> ShowDialogHandler { get; set; } =
        (dialog, owner) => owner == null ? dialog.ShowDialog() : dialog.ShowDialog(owner);

    /// <summary>Test seam: invokes WinHelp/HTML help.</summary>
    internal Action<Wf.Control, string, Wf.HelpNavigator, object> ShowHelpHandler { get; set; } =
        (parent, file, navigator, parameter) =>
        {
            if (parameter == null) Wf.Help.ShowHelp(parent, file, navigator);
            else Wf.Help.ShowHelp(parent, file, navigator, parameter);
        };

    public void ShowOpen()
    {
        using var d = new Wf.OpenFileDialog();
        ConfigureFileDialog(d);
        d.Multiselect = Has(cdlOFNAllowMultiselect);
        d.CheckFileExists = Has(cdlOFNFileMustExist);
        d.ShowReadOnly = !Has(cdlOFNHideReadOnly);
        d.ReadOnlyChecked = Has(cdlOFNReadOnly);
        if (!Show(d)) return;
        SetFlag(cdlOFNReadOnly, d.ReadOnlyChecked);
        var names = d.FileNames;
        if (d.Multiselect && names.Length > 1)
        {
            // Explorer-style multiselect: directory, then file names, NUL separated.
            var dir = Path.GetDirectoryName(names[0]) ?? "";
            FileName = string.Join("\0", new[] { dir }.Concat(names.Select(Path.GetFileName)));
            FileTitle = "";
        }
        else
        {
            AcceptFile(d);
        }
        FilterIndex = d.FilterIndex;
    }

    public void ShowSave()
    {
        using var d = new Wf.SaveFileDialog();
        ConfigureFileDialog(d);
        d.OverwritePrompt = Has(cdlOFNOverwritePrompt);
        d.CreatePrompt = Has(cdlOFNCreatePrompt);
        d.CheckFileExists = Has(cdlOFNFileMustExist);
        if (!Show(d)) return;
        AcceptFile(d);
        FilterIndex = d.FilterIndex;
    }

    public void ShowColor()
    {
        using var d = new Wf.ColorDialog
        {
            FullOpen = Has(cdlCCFullOpen),
            AllowFullOpen = !Has(cdlCCPreventFullOpen),
            ShowHelp = Has(cdlCCHelpButton),
            AnyColor = true,
        };
        if (Has(cdlCCRGBInit)) d.Color = Vb6Color.ToColor(Color);
        if (!Show(d)) return;
        Color = Vb6Color.FromColor(d.Color);
    }

    public void ShowFont()
    {
        var style = FontStyle.Regular;
        if (FontBold) style |= FontStyle.Bold;
        if (FontItalic) style |= FontStyle.Italic;
        if (FontUnderline) style |= FontStyle.Underline;
        if (FontStrikethru) style |= FontStyle.Strikeout;
        using var initial = CreateFont(string.IsNullOrEmpty(FontName) ? DefaultFontName : FontName,
            FontSize > 0 ? FontSize : 8.25f, style);
        using var d = new Wf.FontDialog
        {
            Font = initial,
            ShowEffects = Has(cdlCFEffects),
            ShowColor = Has(cdlCFEffects),
            ShowApply = Has(cdlCFApply),
            ShowHelp = Has(cdlCFHelpButton),
            FixedPitchOnly = Has(cdlCFFixedPitchOnly),
            FontMustExist = Has(cdlCFForceFontExist),
            AllowVectorFonts = !Has(cdlCFNoVectorFonts),
            AllowSimulations = !Has(cdlCFNoSimulations),
            AllowScriptChange = !Has(cdlCFANSIOnly),
        };
        if (Has(cdlCFEffects)) d.Color = Vb6Color.ToColor(Color);
        if (Has(cdlCFLimitSize))
        {
            d.MinSize = Math.Max(0, Min);
            d.MaxSize = Math.Max(0, Max);
        }
        if (!Show(d)) return;
        var f = d.Font;
        FontName = f.Name;
        FontSize = f.SizeInPoints;
        FontBold = f.Bold;
        FontItalic = f.Italic;
        FontUnderline = f.Underline;
        FontStrikethru = f.Strikeout;
        if (Has(cdlCFEffects)) Color = Vb6Color.FromColor(d.Color);
    }

    public void ShowPrinter()
    {
        var settings = new PrinterSettings
        {
            Copies = Copies > 0 ? Copies : (short)1,
            Collate = Has(cdlPDCollate),
            PrintToFile = Has(cdlPDPrintToFile),
        };
        if (Max > 0)
        {
            settings.MinimumPage = Math.Max(0, Min);
            settings.MaximumPage = Max;
        }
        if (FromPage > 0) settings.FromPage = FromPage;
        if (ToPage > 0) settings.ToPage = ToPage;
        settings.PrintRange = Has(cdlPDSelection) ? PrintRange.Selection
            : Has(cdlPDPageNums) ? PrintRange.SomePages
            : PrintRange.AllPages;
        using var d = new Wf.PrintDialog
        {
            PrinterSettings = settings,
            AllowSelection = !Has(cdlPDNoSelection),
            AllowSomePages = !Has(cdlPDNoPageNums),
            AllowPrintToFile = !Has(cdlPDDisablePrintToFile) && !Has(cdlPDHidePrintToFile),
            ShowHelp = Has(cdlPDHelpButton),
            ShowNetwork = !Has(cdlPDNoNetworkButton),
            UseEXDialog = true,
        };
        if (!Show(d)) return;
        var s = d.PrinterSettings;
        PrinterSettings = s;
        Copies = s.Copies;
        FromPage = s.FromPage;
        ToPage = s.ToPage;
        SetFlag(cdlPDSelection, s.PrintRange == PrintRange.Selection);
        SetFlag(cdlPDPageNums, s.PrintRange == PrintRange.SomePages);
        SetFlag(cdlPDCollate, s.Collate);
        SetFlag(cdlPDPrintToFile, s.PrintToFile);
    }

    public void ShowHelp()
    {
        if (string.IsNullOrEmpty(HelpFile)) return;
        var parent = Owner as Wf.Control ?? (Owner != null ? Wf.Control.FromHandle(Owner.Handle) : null);
        switch (HelpCommand)
        {
            case cdlHelpQuit:
                return;
            case cdlHelpContext:
            case cdlHelpContextPopup:
                ShowHelpHandler(parent, HelpFile, Wf.HelpNavigator.TopicId, HelpContext);
                break;
            case cdlHelpKey:
                ShowHelpHandler(parent, HelpFile, Wf.HelpNavigator.KeywordIndex, HelpKey);
                break;
            case cdlHelpPartialKey:
                ShowHelpHandler(parent, HelpFile, Wf.HelpNavigator.Index, HelpKey);
                break;
            case cdlHelpContents:
                ShowHelpHandler(parent, HelpFile, Wf.HelpNavigator.TableOfContents, null);
                break;
            default:
                ShowHelpHandler(parent, HelpFile, Wf.HelpNavigator.TableOfContents, null);
                break;
        }
    }

    /// <summary>Normalizes a VB6 filter to .NET: trailing '|' removed, dangling description dropped.</summary>
    internal static string NormalizeFilter(string filter)
    {
        if (string.IsNullOrEmpty(filter)) return "";
        var parts = filter.TrimEnd('|').Split('|');
        var count = parts.Length - parts.Length % 2;
        return string.Join("|", parts.Take(count));
    }

    private void ConfigureFileDialog(Wf.FileDialog d)
    {
        d.Filter = NormalizeFilter(Filter);
        d.FilterIndex = FilterIndex > 0 ? FilterIndex : 1;
        d.InitialDirectory = InitDir ?? "";
        d.Title = DialogTitle ?? "";
        d.DefaultExt = DefaultExt ?? "";
        d.AddExtension = !string.IsNullOrEmpty(DefaultExt);
        d.FileName = FileName ?? "";
        d.CheckPathExists = Has(cdlOFNPathMustExist);
        d.RestoreDirectory = Has(cdlOFNNoChangeDir);
        d.ValidateNames = !Has(cdlOFNNoValidate);
        d.DereferenceLinks = !Has(cdlOFNNoDereferenceLinks);
        d.ShowHelp = Has(cdlOFNHelpButton);
    }

    private void AcceptFile(Wf.FileDialog d)
    {
        FileName = d.FileName;
        FileTitle = string.IsNullOrEmpty(d.FileName) ? "" : Path.GetFileName(d.FileName);
        var ext = (DefaultExt ?? "").TrimStart('.');
        SetFlag(cdlOFNExtensionDifferent, ext.Length > 0 &&
            !string.Equals(Path.GetExtension(FileName).TrimStart('.'), ext, StringComparison.OrdinalIgnoreCase));
    }

    private bool Show(Wf.CommonDialog d)
    {
        var result = ShowDialogHandler(d, Owner);
        if (result == Wf.DialogResult.OK) return true;
        if (CancelError) throw new CommonDialogCancelException();
        return false;
    }

    private static Font CreateFont(string name, float size, FontStyle style)
    {
        try
        {
            return new Font(name, size, style);
        }
        catch (ArgumentException)
        {
            return new Font(DefaultFontName, size, style);
        }
    }

    private bool Has(int flag) => (Flags & flag) != 0;

    private void SetFlag(int flag, bool on) => Flags = on ? Flags | flag : Flags & ~flag;
}
