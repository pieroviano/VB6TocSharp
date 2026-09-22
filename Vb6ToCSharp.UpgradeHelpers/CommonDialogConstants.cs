namespace Vb6ToCSharp.UpgradeHelpers;

/// <summary>VB6 MSComDlg constants (<c>Flags</c>, <c>HelpCommand</c>, cancel error).</summary>
public static class CommonDialogConstants
{
    public const int cdlCancel = 32755;

    // File open/save
    public const int cdlOFNReadOnly = 0x1;
    public const int cdlOFNOverwritePrompt = 0x2;
    public const int cdlOFNHideReadOnly = 0x4;
    public const int cdlOFNNoChangeDir = 0x8;
    public const int cdlOFNHelpButton = 0x10;
    public const int cdlOFNNoValidate = 0x100;
    public const int cdlOFNAllowMultiselect = 0x200;
    public const int cdlOFNExtensionDifferent = 0x400;
    public const int cdlOFNPathMustExist = 0x800;
    public const int cdlOFNFileMustExist = 0x1000;
    public const int cdlOFNCreatePrompt = 0x2000;
    public const int cdlOFNShareAware = 0x4000;
    public const int cdlOFNNoReadOnlyReturn = 0x8000;
    public const int cdlOFNNoLongNames = 0x40000;
    public const int cdlOFNExplorer = 0x80000;
    public const int cdlOFNNoDereferenceLinks = 0x100000;
    public const int cdlOFNLongNames = 0x200000;

    // Color
    public const int cdlCCRGBInit = 0x1;
    public const int cdlCCFullOpen = 0x2;
    public const int cdlCCPreventFullOpen = 0x4;
    public const int cdlCCHelpButton = 0x8;

    // Font
    public const int cdlCFScreenFonts = 0x1;
    public const int cdlCFPrinterFonts = 0x2;
    public const int cdlCFBoth = 0x3;
    public const int cdlCFHelpButton = 0x4;
    public const int cdlCFEffects = 0x100;
    public const int cdlCFApply = 0x200;
    public const int cdlCFANSIOnly = 0x400;
    public const int cdlCFNoVectorFonts = 0x800;
    public const int cdlCFNoSimulations = 0x1000;
    public const int cdlCFLimitSize = 0x2000;
    public const int cdlCFFixedPitchOnly = 0x4000;
    public const int cdlCFWYSIWYG = 0x8000;
    public const int cdlCFForceFontExist = 0x10000;
    public const int cdlCFScalableOnly = 0x20000;
    public const int cdlCFTTOnly = 0x40000;
    public const int cdlCFNoFaceSel = 0x80000;
    public const int cdlCFNoStyleSel = 0x100000;
    public const int cdlCFNoSizeSel = 0x200000;

    // Printer
    public const int cdlPDAllPages = 0x0;
    public const int cdlPDSelection = 0x1;
    public const int cdlPDPageNums = 0x2;
    public const int cdlPDNoSelection = 0x4;
    public const int cdlPDNoPageNums = 0x8;
    public const int cdlPDCollate = 0x10;
    public const int cdlPDPrintToFile = 0x20;
    public const int cdlPDPrintSetup = 0x40;
    public const int cdlPDNoWarning = 0x80;
    public const int cdlPDReturnDC = 0x100;
    public const int cdlPDReturnIC = 0x200;
    public const int cdlPDReturnDefault = 0x400;
    public const int cdlPDHelpButton = 0x800;
    public const int cdlPDUseDevModeCopies = 0x40000;
    public const int cdlPDDisablePrintToFile = 0x80000;
    public const int cdlPDHidePrintToFile = 0x100000;
    public const int cdlPDNoNetworkButton = 0x200000;

    // Help
    public const int cdlHelpContext = 0x1;
    public const int cdlHelpQuit = 0x2;
    public const int cdlHelpIndex = 0x3;
    public const int cdlHelpContents = 0x3;
    public const int cdlHelpHelpOnHelp = 0x4;
    public const int cdlHelpSetIndex = 0x5;
    public const int cdlHelpSetContents = 0x5;
    public const int cdlHelpContextPopup = 0x8;
    public const int cdlHelpForceFile = 0x9;
    public const int cdlHelpKey = 0x101;
    public const int cdlHelpCommandHelp = 0x102;
    public const int cdlHelpPartialKey = 0x105;
}
