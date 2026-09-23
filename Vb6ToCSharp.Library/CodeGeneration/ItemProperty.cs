using System;
using System.Globalization;
using Vb6ToCSharp.Parsing;

namespace Vb6ToCSharp.CodeGeneration;

/// <summary>One <c>Name = Value</c> line of a .frm/.ctl designer section; nested BeginProperty groups give dotted names.</summary>
public sealed class ItemProperty
{
    public string Name { get; }
    /// <summary>Value text as written, trailing <c>'comment</c> removed.</summary>
    public string Raw { get; }

    public ItemProperty(string name, string raw)
    {
        Name = name;
        Raw = raw;
    }

    public bool IsQuoted => Raw.Length >= 2 && Raw[0] == '"' && Raw[Raw.Length - 1] == '"';

    /// <summary>Value with quotes removed and doubled quotes collapsed.</summary>
    public string Text => IsQuoted ? Raw.Substring(1, Raw.Length - 2).Replace("\"\"", "\"") : Raw;

    public FrxRef Frx
    {
        get
        {
            var s = Raw;
            var isString = s.StartsWith("$", StringComparison.Ordinal);
            if (isString) s = s.Substring(1);
            if (!s.StartsWith("\"", StringComparison.Ordinal)) return null;
            var close = s.IndexOf('"', 1);
            if (close < 0 || close + 1 >= s.Length || s[close + 1] != ':') return null;
            var file = s.Substring(1, close - 1);
            var off = s.Substring(close + 2).Trim();
            return int.TryParse(off, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var offset)
                ? new FrxRef(file, offset, isString)
                : null;
        }
    }

    public double Number(double def = 0) => StringConvert.ToNumber(Raw, def);
    public bool Bool(bool def = false) => StringConvert.ToBool(Raw, def);
}