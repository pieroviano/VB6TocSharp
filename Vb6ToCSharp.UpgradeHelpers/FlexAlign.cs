using System.Collections.Generic;

namespace Vb6ToCSharp.UpgradeHelpers;

/// <summary>MSFlexGrid <c>AlignmentSettings</c> (ColAlignment / CellAlignment values).</summary>
public static class FlexAlign
{
    public const int flexAlignLeftTop = 0;
    public const int flexAlignLeftCenter = 1;
    public const int flexAlignLeftBottom = 2;
    public const int flexAlignCenterTop = 3;
    public const int flexAlignCenterCenter = 4;
    public const int flexAlignCenterBottom = 5;
    public const int flexAlignRightTop = 6;
    public const int flexAlignRightCenter = 7;
    public const int flexAlignRightBottom = 8;
    public const int flexAlignGeneral = 9;

    /// <summary>Horizontal part: 0 left, 1 center, 2 right (general → left).</summary>
    internal static int Horizontal(int alignment) => alignment is >= 0 and <= 8 ? alignment / 3 : 0;

    /// <summary>Vertical part: 0 top, 1 center, 2 bottom (general → center).</summary>
    internal static int Vertical(int alignment) => alignment is >= 0 and <= 8 ? alignment % 3 : 1;

    internal static int Combine(int horizontal, int vertical) => horizontal * 3 + vertical;
}

/// <summary>Parsed MSFlexGrid <c>FormatString</c>: <c>"|&lt;Col1|&gt;Col2|^Col3;|Row1|Row2"</c>.</summary>
internal sealed class FlexFormat
{
    /// <summary>Column header texts (entry i → column i of row 0).</summary>
    public List<string> ColumnTexts { get; } = new();

    /// <summary>Column alignments (null = not specified).</summary>
    public List<int?> ColumnAlignments { get; } = new();

    /// <summary>Row header texts (entry i → row i of column 0); empty when there is no ';' part.</summary>
    public List<string> RowTexts { get; } = new();

    public static FlexFormat Parse(string format)
    {
        var result = new FlexFormat();
        if (string.IsNullOrEmpty(format)) return result;
        var semi = format.IndexOf(';');
        var cols = semi >= 0 ? format.Substring(0, semi) : format;
        var rows = semi >= 0 ? format.Substring(semi + 1) : null;
        if (cols.Length > 0)
        {
            foreach (var entry in cols.Split('|'))
            {
                int? align = null;
                var text = entry;
                if (text.Length > 0)
                {
                    align = text[0] switch
                    {
                        '<' => FlexAlign.flexAlignLeftCenter,
                        '^' => FlexAlign.flexAlignCenterCenter,
                        '>' => FlexAlign.flexAlignRightCenter,
                        _ => null,
                    };
                    if (align.HasValue) text = text.Substring(1);
                }
                result.ColumnTexts.Add(text);
                result.ColumnAlignments.Add(align);
            }
        }
        if (!string.IsNullOrEmpty(rows)) result.RowTexts.AddRange(rows.Split('|'));
        return result;
    }
}
