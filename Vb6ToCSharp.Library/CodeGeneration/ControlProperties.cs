using Vb6ToCSharp.Convert;
using static Vb6ToCSharp.Parsing.ProjectConfigurationParser;

namespace Vb6ToCSharp.CodeGeneration;

public static class ControlProperties
{
    /// <summary>
    /// The .NET member a VB6 control property / method used in code maps to, for the UI target of the conversion
    /// (<paramref name="vProp"/> "" = the control's default property). <paramref name="cType"/> is the VB6 control class.
    /// </summary>
    public static string ConvertControlProperty(string srcUnused, string vProp, string cType)
    {
        return Ui == UiTarget.WinForms ? WinFormsMember(vProp, cType) : WpfMember(vProp, cType);
    }

    /// <summary>WinForms names (as VBUC / VB Migration Partner): list API from UpgradeHelpers.WinForms.ListHelper extensions.</summary>
    private static string WinFormsMember(string vProp, string cType)
    {
        switch (vProp)
        {
            case "Caption":
                return "Text";
            case "ListIndex":
                return "SelectedIndex";
            case "ListCount":
                return "Items.Count";
            case "List":
                return "GetList";
            case "ItemData":
                return "GetItemData";
            case "NewIndex":
                return "GetNewIndex()";
            case "SelStart":
                return "SelectionStart";
            case "SelLength":
                return "SelectionLength";
            case "SelText":
                return "SelectedText";
            case "SetFocus":
                return "Focus";
            case "Value":
                return cType == "VB.OptionButton" ? "Checked" : vProp;
            case "":
                switch (cType)
                {
                    case "VB.OptionButton":
                        return "Checked";
                    case "VB.PictureBox":
                    case "VB.Image":
                        return "Image";
                    case "VB.CommandButton":
                    case "VB.CheckBox":
                    case "VB.HScrollBar":
                    case "VB.VScrollBar":
                        return "Value";
                    default:
                        return "Text"; // TextBox, Label, Frame, ComboBox, ListBox
                }
            default:
                return vProp;
        }
    }

    private static string WpfMember(string vProp, string cType)
    {
        switch (vProp)
        {
            case "ListIndex":
                return "SelectedIndex";
            case "ListCount":
                return "Items.Count";
            case "Visible":
                return "Visibility";
            case "Enabled":
                return "IsEnabled";
            case "TabStop":
                return "IsTabStop";
            case "SelStart":
                return "SelectionStart";
            case "SelLength":
                return "SelectionLength";
            case "SetFocus":
                return "Focus";
            case "Caption":
                return cType == "VB.Form" ? "Title" : cType == "VB.Frame" ? "Header" : "Content"; // Label, CommandButton, CheckBox, OptionButton
            case "Value":
                return cType == "VB.CheckBox" || cType == "VB.OptionButton" ? "IsChecked" : cType == "MSComCtl2.DTPicker" ? "DisplayDate" : vProp;
            case "Text":
                return cType == "VB.ListBox" ? "SelectedItem.ToString()" : vProp;
            case "Default":
                return "IsDefault";
            case "Cancel":
                return "IsCancel";
            case "":
                switch (cType)
                {
                    case "VB.TextBox":
                    case "VB.ComboBox":
                        return "Text";
                    case "VB.PictureBox":
                    case "VB.Image":
                        return "Source";
                    case "VB.OptionButton":
                    case "VB.CheckBox":
                        return "IsChecked";
                    case "VB.Frame":
                        return "Header";
                    case "VB.Label":
                        return "Content";
                    default:
                        return "DefaultProperty";
                }
            default:
                return vProp;
        }
    }
}
