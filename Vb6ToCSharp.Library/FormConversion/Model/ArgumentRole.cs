namespace Vb6ToCSharp.FormConversion.Model;

/// <summary>Meaning of a VB6 event parameter, used to bind it to the .NET event arguments.</summary>
public enum ArgumentRole
{
    Other,
    KeyAscii,
    KeyCode,
    KeyShift,
    MouseShift,
    Button,
    X,
    Y,
    Cancel,
    CancelEdit,
    UnloadMode,
    Node,
    Item,
    Column,
    ToolButton,
    Panel,
    PreviousTab,
    ItemIndex,
    Date,
    NewString,
    PropBag
}