using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Vb6ToCSharp.FormConversion;

/// <summary>Kind of a binary .frx blob, sniffed from its magic bytes.</summary>
public enum FrxBlobKind { Unknown, Bmp, Gif, Jpeg, Png, Icon, Cursor, Wmf, Emf }

/// <summary>
/// Reads the binary side-file of a form. The layout is undocumented; the reader accepts the variants VB6 writes:
/// blobs <c>"lt\0\0" + u32 size + data</c> or <c>u32 size + data</c>; strings <c>u8 len</c>, <c>FF + u16 len</c> or <c>u32 len</c>;
/// <c>List</c> as <c>u16 count</c> + (<c>u16 len</c> + bytes)*; <c>ItemData</c> as <c>u16 count</c> + i32*.
/// </summary>
public sealed class FrxReader
{
    private static readonly Dictionary<string, FrxReader> cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly byte[] data;

    public FrxReader(byte[] data) => this.data = data ?? new byte[0];

    /// <summary>Reader for a file (cached per path), or null when it does not exist.</summary>
    public static FrxReader Open(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        lock (cache)
        {
            if (!cache.TryGetValue(path, out var r)) cache[path] = r = new FrxReader(File.ReadAllBytes(path));
            return r;
        }
    }

    public static void ClearCache()
    {
        lock (cache) cache.Clear();
    }

    private bool Fits(int off, long len) => off >= 0 && len >= 0 && off + len <= data.Length;
    private ushort U16(int off) => Fits(off, 2) ? BitConverter.ToUInt16(data, off) : (ushort)0;
    private uint U32(int off) => Fits(off, 4) ? BitConverter.ToUInt32(data, off) : 0;

    private byte[] Slice(int off, long len)
    {
        if (!Fits(off, 0)) return new byte[0];
        len = Math.Min(len, data.Length - off);
        var r = new byte[len];
        Array.Copy(data, off, r, 0, len);
        return r;
    }

    /// <summary>A picture/icon/binary property value.</summary>
    public byte[] ReadBlob(int offset)
    {
        int start;
        long len;
        if (Fits(offset, 8) && data[offset] == (byte)'l' && data[offset + 1] == (byte)'t' && data[offset + 2] == 0 && data[offset + 3] == 0)
        {
            len = U32(offset + 4);
            start = offset + 8;
        }
        else
        {
            len = U32(offset);
            start = offset + 4;
        }
        if (!Fits(start, len)) len = Math.Max(0, data.Length - start);
        // some writers put a few header bytes before the image itself
        for (var k = 0; k <= 16 && k < len; k++)
        {
            if (Sniff(data, start + k) != FrxBlobKind.Unknown) return Slice(start + k, len - k);
        }
        return Slice(start, len);
    }

    /// <summary>A long string property (<c>$"x.frx":0000</c>, e.g. multi-line Text).</summary>
    public string ReadString(int offset)
    {
        if (!Fits(offset, 1)) return "";
        var b = data[offset];
        if (b == 0xFF && Fits(offset, 3))
        {
            return Ansi(offset + 3, U16(offset + 1));
        }
        if (Fits(offset, 4))
        {
            var u = U32(offset);
            // a u32 length has zero high bytes; read as u8 + text that would put NULs inside a 3+ char text
            if (u > 0 && data[offset + 3] == 0 && data[offset + 2] == 0 && (data[offset + 1] == 0 || b >= 3) && Fits(offset + 4, u))
            {
                return Ansi(offset + 4, u);
            }
        }
        return Ansi(offset + 1, b);
    }

    /// <summary>ListBox/ComboBox <c>List</c>.</summary>
    public List<string> ReadList(int offset)
    {
        var res = new List<string>();
        var n = U16(offset);
        var p = offset + 2;
        for (var k = 0; k < n && Fits(p, 2); k++)
        {
            var len = U16(p);
            res.Add(Ansi(p + 2, len));
            p += 2 + len;
        }
        return res;
    }

    /// <summary>ListBox/ComboBox <c>ItemData</c>.</summary>
    public List<int> ReadItemData(int offset)
    {
        var res = new List<int>();
        var n = U16(offset);
        for (var k = 0; k < n && Fits(offset + 2 + k * 4, 4); k++)
        {
            res.Add(BitConverter.ToInt32(data, offset + 2 + k * 4));
        }
        return res;
    }

    private string Ansi(int off, long len) => Fits(off, len) ? Encoding.Default.GetString(data, off, (int)len) : "";

    public static FrxBlobKind Sniff(byte[] b, int o = 0)
    {
        bool At(params byte[] m)
        {
            if (o + m.Length > b.Length) return false;
            for (var k = 0; k < m.Length; k++) if (b[o + k] != m[k]) return false;
            return true;
        }
        if (At(0x42, 0x4D)) return FrxBlobKind.Bmp;
        if (At(0x47, 0x49, 0x46, 0x38)) return FrxBlobKind.Gif;
        if (At(0xFF, 0xD8, 0xFF)) return FrxBlobKind.Jpeg;
        if (At(0x89, 0x50, 0x4E, 0x47)) return FrxBlobKind.Png;
        if (At(0x00, 0x00, 0x01, 0x00)) return FrxBlobKind.Icon;
        if (At(0x00, 0x00, 0x02, 0x00)) return FrxBlobKind.Cursor;
        if (At(0xD7, 0xCD, 0xC6, 0x9A)) return FrxBlobKind.Wmf;
        if (At(0x01, 0x00, 0x00, 0x00) && o + 44 <= b.Length && b[o + 40] == 0x20 && b[o + 41] == 0x45 && b[o + 42] == 0x4D && b[o + 43] == 0x46) return FrxBlobKind.Emf;
        return FrxBlobKind.Unknown;
    }

    public static string Extension(FrxBlobKind k) => k switch
    {
        FrxBlobKind.Bmp => ".bmp",
        FrxBlobKind.Gif => ".gif",
        FrxBlobKind.Jpeg => ".jpg",
        FrxBlobKind.Png => ".png",
        FrxBlobKind.Icon => ".ico",
        FrxBlobKind.Cursor => ".cur",
        FrxBlobKind.Wmf => ".wmf",
        FrxBlobKind.Emf => ".emf",
        _ => ".bin",
    };
}
