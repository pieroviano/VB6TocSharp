namespace Vb6ToCSharp.Parsing.Model;

/// <summary>A reference to data stored in the form's .frx file (<c>"frmX.frx":01A2</c>; <c>$"..."</c> marks a string).</summary>
public sealed class FrxRef
{
    public string File { get; }
    public int Offset { get; }
    public bool IsString { get; }

    public FrxRef(string file, int offset, bool isString)
    {
        File = file;
        Offset = offset;
        IsString = isString;
    }
}