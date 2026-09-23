namespace Vb6ToCSharp.Parsing;

/// <summary>Kind of a binary .frx blob, sniffed from its magic bytes.</summary>
public enum FrxBlobKind
{
    Unknown,
    Bmp,
    Gif,
    Jpeg,
    Png,
    Icon,
    Cursor,
    Wmf,
    Emf
}