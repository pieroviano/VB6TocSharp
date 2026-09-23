using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Resources;
using Vb6ToCSharp.Parsing;

namespace Vb6ToCSharp.ItemConversion;

/// <summary>Writes form resources as a typed .resx (Icon / Image), like the WinForms designer.</summary>
public static class ResxWriter
{
    public static void Write(string path, IEnumerable<FormResource> resources)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        using var w = new ResXResourceWriter(path);
        foreach (var r in resources)
        {
            using var ms = new MemoryStream(r.Data);
            if (r.Kind == FrxBlobKind.Icon)
            {
                using var icon = new Icon(ms);
                w.AddResource(r.Name, icon);
            }
            else
            {
                using var img = Image.FromStream(ms);
                w.AddResource(r.Name, img);
            }
        }
        w.Generate();
    }

    /// <summary>Writes each resource's bytes to <c>root\Name</c> (WPF resource files).</summary>
    public static void WriteFiles(string root, IEnumerable<FormResource> resources)
    {
        foreach (var r in resources)
        {
            var p = Path.Combine(root, r.Name);
            Directory.CreateDirectory(Path.GetDirectoryName(p) ?? root);
            File.WriteAllBytes(p, r.Data);
        }
    }
}
