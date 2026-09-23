using System;
using System.Globalization;
using System.Linq;

namespace Vb6ToCSharp.Parsing;

/// <summary>An <c>Object={guid}#ver#lcid; file.ocx</c> reference.</summary>
public sealed class OcxRef
{
    public string Guid { get; }
    public string Version { get; }
    public string Lcid { get; }
    public string File { get; }

    public OcxRef(string guid, string version, string lcid, string file)
    {
        Guid = guid;
        Version = version;
        Lcid = lcid;
        File = file;
    }

    public int VersionMajor => int.TryParse(Version.Split('.')[0], out var v) ? v : 1;
    public int VersionMinor => Version.Contains('.') && int.TryParse(Version.Split('.')[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v) ? v : 0;

    /// <summary>Parses the value part of <c>Object=</c> (.vbp) or <c>Object = "..."; "..."</c> (.frm).</summary>
    public static OcxRef Parse(string value)
    {
        var parts = value.Split(';');
        var id = parts[0].Trim().Trim('"');
        var file = parts.Length > 1 ? parts[1].Trim().Trim('"') : "";
        var bits = id.Split('#');
        if (bits.Length < 1 || !bits[0].StartsWith("{", StringComparison.Ordinal)) return null;
        return new OcxRef(bits[0].ToUpperInvariant(), bits.Length > 1 ? bits[1] : "1.0", bits.Length > 2 ? bits[2] : "0", file);
    }
}