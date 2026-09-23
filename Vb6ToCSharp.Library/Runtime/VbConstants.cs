using Vb6ToCSharp.Runtime.Model;

namespace Vb6ToCSharp.Runtime;

/// <summary>
/// The VB6 intrinsic constants, replacing <c>Microsoft.VisualBasic.Constants</c> for this
/// converter's own code. Same names, same values, same types.
/// </summary>
public static class VbConstants
{
    public const string vbCr = "\r";
    public const string vbCrLf = "\r\n";
    public const string vbLf = "\n";
    public const string vbNewLine = "\r\n";
    public const string vbNullChar = "\0";
    public const string vbNullString = "";
    public const string vbTab = "\t";
    public const string vbBack = "\b";
    public const string vbFormFeed = "\f";
    public const string vbVerticalTab = "\v";

    public const CompareMethod vbBinaryCompare = CompareMethod.Binary;
    public const CompareMethod vbTextCompare = CompareMethod.Text;

    public const TriState vbFalse = TriState.False;
    public const TriState vbTrue = TriState.True;
    public const TriState vbUseDefault = TriState.UseDefault;

    public const FileAttribute vbNormal = FileAttribute.Normal;
    public const FileAttribute vbReadOnly = FileAttribute.ReadOnly;
    public const FileAttribute vbHidden = FileAttribute.Hidden;
    public const FileAttribute vbSystem = FileAttribute.System;
    public const FileAttribute vbVolume = FileAttribute.Volume;
    public const FileAttribute vbDirectory = FileAttribute.Directory;
    public const FileAttribute vbArchive = FileAttribute.Archive;

    public const MsgBoxStyle vbOKOnly = MsgBoxStyle.OkOnly;
    public const MsgBoxStyle vbOKCancel = MsgBoxStyle.OkCancel;
    public const MsgBoxStyle vbAbortRetryIgnore = MsgBoxStyle.AbortRetryIgnore;
    public const MsgBoxStyle vbYesNoCancel = MsgBoxStyle.YesNoCancel;
    public const MsgBoxStyle vbYesNo = MsgBoxStyle.YesNo;
    public const MsgBoxStyle vbRetryCancel = MsgBoxStyle.RetryCancel;
    public const MsgBoxStyle vbCritical = MsgBoxStyle.Critical;
    public const MsgBoxStyle vbQuestion = MsgBoxStyle.Question;
    public const MsgBoxStyle vbExclamation = MsgBoxStyle.Exclamation;
    public const MsgBoxStyle vbInformation = MsgBoxStyle.Information;
    public const MsgBoxStyle vbDefaultButton1 = MsgBoxStyle.DefaultButton1;
    public const MsgBoxStyle vbDefaultButton2 = MsgBoxStyle.DefaultButton2;
    public const MsgBoxStyle vbDefaultButton3 = MsgBoxStyle.DefaultButton3;
    public const MsgBoxStyle vbApplicationModal = MsgBoxStyle.ApplicationModal;
    public const MsgBoxStyle vbSystemModal = MsgBoxStyle.SystemModal;

    public const MsgBoxResult vbOK = MsgBoxResult.Ok;
    public const MsgBoxResult vbCancel = MsgBoxResult.Cancel;
    public const MsgBoxResult vbAbort = MsgBoxResult.Abort;
    public const MsgBoxResult vbRetry = MsgBoxResult.Retry;
    public const MsgBoxResult vbIgnore = MsgBoxResult.Ignore;
    public const MsgBoxResult vbYes = MsgBoxResult.Yes;
    public const MsgBoxResult vbNo = MsgBoxResult.No;

    public const int vbObjectError = -2147221504;
}
