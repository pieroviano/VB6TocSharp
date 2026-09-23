using System;
using System.IO;
using Vb6ToCSharp.Tests.Fixtures;
using static Vb6ToCSharp.Parsing.ProjectFiles;

namespace Vb6ToCSharp.Tests.Parsing;

public class ProjectFilesTests : IDisposable
{
    private readonly string dir = TestUtil.TempDir();

    public void Dispose()
    {
        try { Directory.Delete(dir, true); } catch { /* best effort */ }
    }

    private string Vbp(string eol)
    {
        var p = Path.Combine(dir, Guid.NewGuid().ToString("N") + ".vbp");
        File.WriteAllText(p, string.Join(eol, new[]
        {
            "Type=Exe",
            "Module=modA; modA.bas",
            "Module=modB; modB.bas",
            "Form=frmA.frm",
            "Class=clsA; clsA.cls",
            "UserControl=ucA; ucA.ctl",
            "Startup=\"Sub Main\"",
            "",
        }));
        return p;
    }

    /// <summary>A .vbp written with any line ending lists the same files (a vbLf-only one listed none).</summary>
    [Theory]
    [InlineData("\r\n")]
    [InlineData("\n")]
    [InlineData("\r")]
    public void VbpLists_ReadAnyLineEnding(string eol)
    {
        var p = Vbp(eol);
        Assert.Equal("modA.bas\r\nmodB.bas", VbpModules(p));
        Assert.Equal("frmA.frm", VbpForms(p));
        Assert.Equal("clsA.cls", VbpClasses(p));
        Assert.Equal("clsA", VbpClasses(p, true));
        Assert.Equal("ucA.ctl", VbpUserControls(p));
    }
}
