namespace Vb6ToCSharp.Parsing;

/// <summary>A binary resource extracted from the .frx.</summary>
public sealed class FormResource
{
    public string Name { get; set; } = "";
    public byte[] Data { get; set; } = new byte[0];
    public FrxBlobKind Kind { get; set; }
}