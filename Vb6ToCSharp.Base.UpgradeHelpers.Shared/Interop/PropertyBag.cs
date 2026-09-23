using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Vb6ToCSharp.UpgradeHelpers.Internal;

namespace Vb6ToCSharp.UpgradeHelpers.Interop;

/// <summary>
/// VB6 PropertyBag (UserControl <c>ReadProperties</c> / <c>WriteProperties</c>). Names are case-insensitive.
/// <see cref="Contents"/> uses a small tagged binary format (no BinaryFormatter) supporting null, string, bool,
/// char, all numeric types, DateTime and byte[].
/// </summary>
public class PropertyBag
{
    private const uint Magic = 0x42505642; // "BVPB"
    private const byte FormatVersion = 1;

    private readonly Dictionary<string, object> _values = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Bag pre-filled with <c>name, value, name, value, …</c> (designer-persisted values).</summary>
    public static PropertyBag FromPairs(params object[] nameValuePairs)
    {
        var bag = new PropertyBag();
        if (nameValuePairs == null) return bag;
        if (nameValuePairs.Length % 2 != 0)
            throw new ArgumentException("Expected name/value pairs.", nameof(nameValuePairs));
        for (var i = 0; i < nameValuePairs.Length; i += 2)
        {
            if (nameValuePairs[i] is not string name || name.Length == 0)
                throw new ArgumentException($"Element {i} is not a property name.", nameof(nameValuePairs));
            bag._values[name] = nameValuePairs[i + 1];
        }
        return bag;
    }

    /// <summary>Stored value, or <paramref name="defaultValue"/> when <paramref name="name"/> was not written.</summary>
    public object ReadProperty(string name, object defaultValue = null) =>
        _values.TryGetValue(name, out var v) ? v : defaultValue;

    /// <summary>Stores <paramref name="value"/>; like VB6 a value equal to <paramref name="defaultValue"/> is not stored.</summary>
    public void WriteProperty(string name, object value, object defaultValue = null)
    {
        if (string.IsNullOrEmpty(name)) throw new ArgumentException("Property name required.", nameof(name));
        if (VbCompare.ValueEquals(value, defaultValue)) _values.Remove(name);
        else _values[name] = value;
    }

    /// <summary>Serialized form of the bag (VB6 <c>Contents</c>); setting it replaces all values.</summary>
    public byte[] Contents
    {
        get
        {
            using var ms = new MemoryStream();
            using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
            {
                w.Write(Magic);
                w.Write(FormatVersion);
                w.Write(_values.Count);
                foreach (var kv in _values)
                {
                    w.Write(kv.Key);
                    WriteValue(w, kv.Key, kv.Value);
                }
            }
            return ms.ToArray();
        }
        set
        {
            var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (value is { Length: > 0 })
            {
                try
                {
                    using var r = new BinaryReader(new MemoryStream(value), Encoding.UTF8);
                    if (r.ReadUInt32() != Magic) throw new InvalidDataException("Not a PropertyBag stream.");
                    var version = r.ReadByte();
                    if (version != FormatVersion) throw new InvalidDataException($"Unsupported PropertyBag version {version}.");
                    var count = r.ReadInt32();
                    if (count < 0) throw new InvalidDataException("Negative property count.");
                    for (var i = 0; i < count; i++)
                    {
                        var name = r.ReadString();
                        values[name] = ReadValue(r);
                    }
                }
                catch (EndOfStreamException e)
                {
                    throw new InvalidDataException("Truncated PropertyBag stream.", e);
                }
            }
            _values.Clear();
            foreach (var kv in values) _values[kv.Key] = kv.Value;
        }
    }

    private enum Tag : byte
    {
        Null, String, Boolean, Byte, SByte, Int16, UInt16, Int32, UInt32, Int64, UInt64, Single, Double, Decimal,
        DateTime, Char, Bytes,
    }

    private static void WriteValue(BinaryWriter w, string name, object value)
    {
        switch (value)
        {
            case null: w.Write((byte)Tag.Null); break;
            case DBNull: w.Write((byte)Tag.Null); break;
            case string s: w.Write((byte)Tag.String); w.Write(s); break;
            case bool b: w.Write((byte)Tag.Boolean); w.Write(b); break;
            case byte b: w.Write((byte)Tag.Byte); w.Write(b); break;
            case sbyte b: w.Write((byte)Tag.SByte); w.Write(b); break;
            case short n: w.Write((byte)Tag.Int16); w.Write(n); break;
            case ushort n: w.Write((byte)Tag.UInt16); w.Write(n); break;
            case int n: w.Write((byte)Tag.Int32); w.Write(n); break;
            case uint n: w.Write((byte)Tag.UInt32); w.Write(n); break;
            case long n: w.Write((byte)Tag.Int64); w.Write(n); break;
            case ulong n: w.Write((byte)Tag.UInt64); w.Write(n); break;
            case float n: w.Write((byte)Tag.Single); w.Write(n); break;
            case double n: w.Write((byte)Tag.Double); w.Write(n); break;
            case decimal n: w.Write((byte)Tag.Decimal); w.Write(n); break;
            case DateTime d: w.Write((byte)Tag.DateTime); w.Write(d.ToBinary()); break;
            case char c: w.Write((byte)Tag.Char); w.Write((ushort)c); break;
            case byte[] bytes: w.Write((byte)Tag.Bytes); w.Write(bytes.Length); w.Write(bytes); break;
            case Enum e: WriteValue(w, name, Convert.ChangeType(e, e.GetTypeCode())); break;
            default:
                throw new NotSupportedException($"Property '{name}': type {value.GetType()} cannot be persisted.");
        }
    }

    private static object ReadValue(BinaryReader r)
    {
        var tag = (Tag)r.ReadByte();
        return tag switch
        {
            Tag.Null => null,
            Tag.String => r.ReadString(),
            Tag.Boolean => r.ReadBoolean(),
            Tag.Byte => r.ReadByte(),
            Tag.SByte => r.ReadSByte(),
            Tag.Int16 => r.ReadInt16(),
            Tag.UInt16 => r.ReadUInt16(),
            Tag.Int32 => r.ReadInt32(),
            Tag.UInt32 => r.ReadUInt32(),
            Tag.Int64 => r.ReadInt64(),
            Tag.UInt64 => r.ReadUInt64(),
            Tag.Single => r.ReadSingle(),
            Tag.Double => r.ReadDouble(),
            Tag.Decimal => r.ReadDecimal(),
            Tag.DateTime => DateTime.FromBinary(r.ReadInt64()),
            Tag.Char => (char)r.ReadUInt16(),
            Tag.Bytes => ReadBytes(r),
            _ => throw new InvalidDataException($"Unknown value tag {(byte)tag}."),
        };
    }

    private static byte[] ReadBytes(BinaryReader r)
    {
        var length = r.ReadInt32();
        if (length < 0) throw new InvalidDataException("Negative byte array length.");
        var bytes = r.ReadBytes(length);
        if (bytes.Length != length) throw new EndOfStreamException();
        return bytes;
    }
}
