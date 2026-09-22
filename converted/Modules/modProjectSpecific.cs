using static Microsoft.VisualBasic.Strings;
using static Vb6ToCSharp.Modules.ModRegEx;
using static Vb6ToCSharp.Modules.ModUtils;
using static Vb6ToCSharp.VbExtension;


namespace Vb6ToCSharp.Modules;

static class ModProjectSpecific
{
    // Option Explicit


    public static string ProjectSpecificPostCodeLineConvert(string str)
    {
        var s = str;
        //  If IsInStr(S, "!C == null") Then Stop
        // Some patterns we dont use or didn't catch in lint...
        if (IsInStr(s, "DisposeDA"))
        {
            s = Replace(s, "DisposeDA", "// DisposeDA");
        }
        if (IsInStr(s, "MousePointer = vbNormal"))
        {
            s = Replace(s, "MousePointer = vbNormal", "MousePointer = vbDefault");
        }

        // We use decimal, not double
        if (IsInStr(s, "Val("))
        {
            s = Replace(s, "Val( ", "ValD(");
        }

        // Bad pattern combination
        if (RegExTest(s, "\\(!" + patToken + " == null\\)"))
        {
            s = Replace(s, "!", "", 1);
            s = Replace(s, "==", "!=", 1);
        }

        // False ref entries...
        if (IsInStr(s, "IsIn("))
        {
            s = Replace(s, "ref ", "");
        }
        if (IsInStr(s, "POMode("))
        {
            s = Replace(s, "ref ", "");
        }
        if (IsInStr(s, "OrderMode("))
        {
            s = Replace(s, "ref ", "");
        }
        if (IsInStr(s, "InvenMode("))
        {
            s = Replace(s, "ref ", "");
        }
        if (IsInStr(s, "ReportsMode("))
        {
            s = Replace(s, "ref ", "");
        }
        if (IsInStr(s, "SetButtonImage("))
        {
            s = Replace(s, "ref ", "");
            s = Replace(s, ".DefaultProperty", "");
        }
        if (IsInStr(s, "EnableFrame"))
        {
            s = Replace(s, "ref ", "");
        }
        s = Replace(s, " && BackupType.", " & BackupType.");

        // Common Mistake Functions...
        if (IsInStr(s, "StoreSettings."))
        {
            s = Replace(s, "StoreSettings.", "StoreSettings().");
        }

        // etc
        if (IsInStr(s, ".hwnd"))
        {
            s = Replace(s, ".hwnd", ".hWnd()");
        }
        if (IsInStr(s, "SetCustomFrame"))
        {
            s = "";
        }
        if (IsInStr(s, "RemoveCustomFrame"))
        {
            s = "";
        }
        s = Replace(s, "VbMsgBoxResult", "MsgBoxResult");

        const string tokenBreak = "[ ,)]";
        s = RegExReplace(s, "InventFolder(" + tokenBreak + ")", "InventFolder()$1");
        s = RegExReplace(s, "PXFolder(" + tokenBreak + ")", "InventFolder()$1");
        s = RegExReplace(s, "FXFolder(" + tokenBreak + ")", "InventFolder()$1");
        s = RegExReplace(s, "InventFolder(" + tokenBreak + ")", "InventFolder()$1");
        s = RegExReplace(s, "IsDevelopment(" + tokenBreak + ")", "IsDevelopment()$1");

        var projectSpecificPostCodeLineConvert = s;
        return projectSpecificPostCodeLineConvert;
    }
}