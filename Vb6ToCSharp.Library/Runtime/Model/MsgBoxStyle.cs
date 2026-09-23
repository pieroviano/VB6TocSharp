using System;

namespace Vb6ToCSharp.Runtime.Model;

/// <summary>The VB6 <c>MsgBox</c> button/icon flags (the <c>vbOKCancel</c>… constants).</summary>
[Flags]
public enum MsgBoxStyle
{
    ApplicationModal = 0,
    OkOnly = 0,
    OkCancel = 1,
    AbortRetryIgnore = 2,
    YesNoCancel = 3,
    YesNo = 4,
    RetryCancel = 5,
    Critical = 16,
    Question = 32,
    Exclamation = 48,
    Information = 64,
    DefaultButton1 = 0,
    DefaultButton2 = 256,
    DefaultButton3 = 512,
    SystemModal = 4096,
    MsgBoxHelp = 16384,
    MsgBoxSetForeground = 65536,
    MsgBoxRight = 524288,
    MsgBoxRtlReading = 1048576
}
