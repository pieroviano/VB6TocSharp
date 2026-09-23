namespace Vb6ToCSharp.Convert;

/// <summary>A loop or Select Case open in the procedure being converted.</summary>
internal sealed class Breakable
{
    public string Kind = "";
    public string Label = "";
}