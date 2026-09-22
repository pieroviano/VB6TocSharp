namespace Vb6ToCSharp.Modules;

public static class ModControlProperties
{
    // Option Explicit


    public static string ConvertControlProperty(string srcUnused, string vProp, string cType)
    {
        var convertControlProperty =
            //If IsInStr(vProp, "SetF") Then Stop
            vProp;
        switch (vProp)
        {
            case "ListIndex":
                convertControlProperty = "SelectedIndex";
                break;
            case "Visible":
                convertControlProperty = "Visibility";
                break;
            case "Enabled":
                convertControlProperty = "IsEnabled";
                break;
            case "TabStop":
                convertControlProperty = "IsTabStop";
                break;
            case "SelStart":
                convertControlProperty = "SelectionStart";
                break;
            case "SelLength":
                convertControlProperty = "SelectionLength";
                break;
            case "Caption":
                if (cType == "VB.Label")
                {
                    convertControlProperty = "Content";
                }
                break;
            case "Value":
                if (cType == "VB.CheckBox")
                {
                    convertControlProperty = "IsChecked";
                }
                if (cType == "VB.OptionButton")
                {
                    convertControlProperty = "IsChecked";
                }
                if (cType == "MSComCtl2.DTPicker")
                {
                    convertControlProperty = "DisplayDate";
                }
                break;
            case "Text":
                if (cType == "VB.ListBox")
                {
                    convertControlProperty = "SelectedText.toString()";
                }
                break;
            case "ListCount":
                if (cType == "VB.ListBox")
                {
                    convertControlProperty = "Items.Count";
                }
                break;
            case "Default":
                convertControlProperty = "IsDefault";
                break;
            case "Cancel":
                convertControlProperty = "IsCancel";

                break;
            case "":
                switch (cType)
                {
                    case "VB.Caption":
                        convertControlProperty = "Content";
                        break;
                    case "VB.TextBox":
                        convertControlProperty = "Text";
                        break;
                    case "VB.ComboBox":
                        convertControlProperty = "Text";
                        break;
                    case "VB.PictureBox":
                        convertControlProperty = "Source";
                        break;
                    case "VB.Image":
                        convertControlProperty = "Source";
                        break;
                    case "VB.OptionButton":
                        convertControlProperty = "IsChecked";
                        break;
                    case "VB.CheckBox":
                        convertControlProperty = "IsChecked";
                        break;
                    case "VB.Frame":
                        convertControlProperty = "Content";
                        break;
                    case "VB.Label":
                        convertControlProperty = "Content";
                        break;
                    default:
                        convertControlProperty = "DefaultProperty";
                        break;
                }
                break;
        }
        return convertControlProperty;
    }
}