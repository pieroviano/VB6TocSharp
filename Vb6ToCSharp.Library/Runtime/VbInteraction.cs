using System.Windows;
using Vb6ToCSharp.Runtime.Model;

namespace Vb6ToCSharp.Runtime;

/// <summary>
/// The two VB6 <c>Interaction</c> functions this converter's own code calls, replacing
/// <c>Microsoft.VisualBasic.Interaction</c>. <see cref="IIf"/> keeps VB's signature (and so keeps
/// losing to <see cref="RuntimeExtension"/>'s typed overloads wherever those are in scope) and, as
/// in VB, both parts are evaluated before the call.
/// </summary>
public static class VbInteraction
{
    public static object IIf(bool Expression, object TruePart, object FalsePart) => Expression ? TruePart : FalsePart;

    /// <summary>
    /// VB's MsgBox. This is the one member of the shim that shows UI, as it did before — the
    /// converter's own git helper asks the developer to confirm.
    /// </summary>
    public static MsgBoxResult MsgBox(object Prompt, MsgBoxStyle Buttons = MsgBoxStyle.OkOnly, object Title = null)
    {
        var result = MessageBox.Show(
            Prompt == null ? "" : Prompt.ToString(),
            Title == null ? "" : Title.ToString(),
            Button(Buttons),
            Icon(Buttons));

        switch (result)
        {
            case MessageBoxResult.OK: return MsgBoxResult.Ok;
            case MessageBoxResult.Cancel: return MsgBoxResult.Cancel;
            case MessageBoxResult.Yes: return MsgBoxResult.Yes;
            case MessageBoxResult.No: return MsgBoxResult.No;
            default: return MsgBoxResult.Ok;
        }
    }

    private static MessageBoxButton Button(MsgBoxStyle style)
    {
        // The button set is the low nibble of the style.
        switch ((int)style & 0xF)
        {
            case (int)MsgBoxStyle.OkCancel: return MessageBoxButton.OKCancel;
            case (int)MsgBoxStyle.YesNoCancel: return MessageBoxButton.YesNoCancel;
            case (int)MsgBoxStyle.YesNo: return MessageBoxButton.YesNo;
            case (int)MsgBoxStyle.RetryCancel: return MessageBoxButton.OKCancel;
            case (int)MsgBoxStyle.AbortRetryIgnore: return MessageBoxButton.YesNoCancel;
            default: return MessageBoxButton.OK;
        }
    }

    private static MessageBoxImage Icon(MsgBoxStyle style)
    {
        if ((style & MsgBoxStyle.Exclamation) == MsgBoxStyle.Exclamation) return MessageBoxImage.Exclamation;
        if ((style & MsgBoxStyle.Information) == MsgBoxStyle.Information) return MessageBoxImage.Information;
        if ((style & MsgBoxStyle.Question) == MsgBoxStyle.Question) return MessageBoxImage.Question;
        if ((style & MsgBoxStyle.Critical) == MsgBoxStyle.Critical) return MessageBoxImage.Error;
        return MessageBoxImage.None;
    }
}
