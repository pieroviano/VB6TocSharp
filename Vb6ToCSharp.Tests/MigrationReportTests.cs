using System.IO;
using Vb6ToCSharp.ItemConversion;
using Vb6ToCSharp.Parsing;
using Vb6ToCSharp.Tests.Infrastructure;

namespace Vb6ToCSharp.Tests;

/// <summary>Migration report of what a conversion left to review.</summary>
public partial class ConverterTests
{
    [Fact]
    public void Report_ListsItemsByCategoryAndFile()
    {
        var dir = TestUtil.TempDir();
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "Modules"));
            File.WriteAllText(Path.Combine(dir, "Modules", "modA.cs"),
                "class A {\r\n  // TODO: VB6 Resume (retry the failing statement) has no C# equivalent\r\n  int x; // TODO: VB6 lower bound 1\r\n  // VB6: n = Weird | Syntax\r\n}\r\n");
            File.WriteAllText(Path.Combine(dir, "Clean.cs"), "class B { }\r\n");
            var issues = MigrationReport.Collect(dir);
            Assert.Equal(3, issues.Count);
            Assert.Equal("Modules/modA.cs", issues[0].File);
            Assert.Equal(2, issues[0].Line);
            Assert.Equal("Error handling", issues[0].Category);
            Assert.Equal("Arrays", issues[1].Category);
            Assert.Equal("Kept as VB6", issues[2].Category);
            var md = MigrationReport.Render(issues, 2, "prj");
            Assert.Contains("2 C# files, 3 items to review.", md);
            Assert.Contains("| Error handling | 1 |", md);
            Assert.Contains("| [Modules/modA.cs:3](Modules/modA.cs#L3) | Arrays | VB6 lower bound 1 |", md);
            Assert.Contains("Weird \\| Syntax", md); // table cells escape pipes
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void ConvertProject_WritesTheReport()
    {
        CleanOut();
        CaptureNotify(() => TestUtil.WithTimeout(() => { CodeConverter.ConvertProject(ProjectConfigurationParser.VbpFile); return 0; }, 60000));
        var report = Out(fixture, MigrationReport.ReportFile);
        Assert.True(File.Exists(report));
        Assert.StartsWith("# Migration report: prj", File.ReadAllText(report));
    }
}
