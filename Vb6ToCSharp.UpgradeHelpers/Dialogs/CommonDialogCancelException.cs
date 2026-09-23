using System.Runtime.InteropServices;

namespace Vb6ToCSharp.UpgradeHelpers.Dialogs;

/// <summary>VB6 error 32755 raised by <see cref="CommonDialog"/> when <c>CancelError</c> is set and the user cancels.</summary>
public class CommonDialogCancelException : COMException
{
    public CommonDialogCancelException() : base("Cancel was selected.", CommonDialogConstants.cdlCancel)
    {
    }

    /// <summary>VB6 <c>Err.Number</c> (32755).</summary>
    public int Number => CommonDialogConstants.cdlCancel;
}
