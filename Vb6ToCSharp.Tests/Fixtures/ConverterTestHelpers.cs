using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Vb6ToCSharp.CodeConversion;

namespace Vb6ToCSharp.Tests.Fixtures;

/// <summary>Conversion helpers shared by the converter test classes.</summary>
internal static class ConverterTestHelpers
{
    /// <summary>Full pipeline (SanitizeCode splits single-line Ifs, ':' lists and line numbers before ConvertSub).</summary>
    internal static string Segment(string vb) => TestUtil.WithTimeout(() => CodeConverter.ConvertCodeSegment(vb.Replace("\r\n", "\n").Replace("\n", "\r\n"), true), 30000);

    /// <summary>The generated members are syntactically valid C#.</summary>
    internal static void AssertParses(string members)
    {
        var tree = CSharpSyntaxTree.ParseText("class C {\r\n" + members + "\r\n}");
        var errors = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors) + "\n" + members);
    }

internal static string Sub(params string[] body) => "Public Sub T()\r\n" + string.Join("\r\n", body) + "\r\nEnd Sub";

internal static string Convert(string vb) => TestUtil.WithTimeout(() => CodeConverter.ConvertSub(vb, true), 20000);

internal static string Out(ConverterFixture f, string rel) => Path.Combine(f.Dir, "out", rel);

internal static List<string> CaptureNotify(Action a)
    {
        var got = new List<string>();
        var oldNotify = ConversionUtility.Notify;
        var oldProgress = ConversionUtility.Progress;
        ConversionUtility.Notify = got.Add;
        ConversionUtility.Progress = (_, _, _) => { };
        try
        {
            a();
        }
        finally
        {
            ConversionUtility.Notify = oldNotify;
            ConversionUtility.Progress = oldProgress;
        }

        return got;
    }

    /// <summary>Sets the constants of the "file" being converted; an empty source resets them.</summary>
    internal static string Begin(string vb = "", Dictionary<string, string>? project = null) => StatementsConverter.BeginFile(vb.Replace("\n", "\r\n"), project);

internal const string CompileUsings =
        "using System;\r\nusing System.Runtime.InteropServices;\r\nusing Microsoft.VisualBasic;\r\nusing Microsoft.VisualBasic.CompilerServices;\r\n" +
        "using static Microsoft.VisualBasic.Strings;\r\nusing static Microsoft.VisualBasic.Information;\r\nusing static Microsoft.VisualBasic.Interaction;\r\n" +
        "using static Microsoft.VisualBasic.Conversion;\r\nusing static Microsoft.VisualBasic.FileSystem;\r\nusing static Microsoft.VisualBasic.DateAndTime;\r\n" +
        "using static System.Math;\r\nusing Vb6ToCSharp.UpgradeHelpers;\r\n" +
        // the same UpgradeHelpers namespaces UsingEverything emits into every converted file
        "using Vb6ToCSharp.UpgradeHelpers.Arrays;\r\nusing Vb6ToCSharp.UpgradeHelpers.Dialogs;\r\n" +
        "using Vb6ToCSharp.UpgradeHelpers.Interop;\r\nusing Vb6ToCSharp.UpgradeHelpers.Model;\r\n" +
        "using static Vb6ToCSharp.UpgradeHelpers.VbRuntime;\r\n";

    /// <summary>The generated members compile (types and conversions, not only syntax) against the VB runtime and the helpers.</summary>
    internal static void AssertCompiles(string members) => AssertCompilesTop("public static class M {\r\n" + members + "\r\n}");

    /// <summary>Top-level declarations (classes, interfaces) compile against the VB runtime and the helpers.</summary>
    internal static void AssertCompilesTop(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(CompileUsings + code);
        var refs = new[] { typeof(object), typeof(Microsoft.VisualBasic.Strings), typeof(Vb6ToCSharp.UpgradeHelpers.VbRuntime), typeof(System.Linq.Enumerable) }
            .Select(t => MetadataReference.CreateFromFile(t.Assembly.Location))
            .Concat(new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location.Replace("mscorlib.dll", "System.dll")) });
        var comp = CSharpCompilation.Create("check", new[] { tree }, refs, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var errors = comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors) + "\n" + code);
    }

    /// <summary>Globals + procedures of one module, with its per-file options.</summary>
    internal static string Module(string globals, string code)
    {
        Begin();
        try
        {
            var g = TestUtil.WithTimeout(() => CodeConverter.ConvertGlobals(globals.Replace("\r\n", "\n").Replace("\n", "\r\n"), true), 30000);
            return g + "\r\n" + Segment(code);
        }
        finally { Begin(); }
    }

    /// <summary>Deletes the fixture's output folder so a test starts from nothing.</summary>
    internal static void CleanOut(ConverterFixture f)
    {
        var o = Path.Combine(f.Dir, "out");
        if (Directory.Exists(o)) Directory.Delete(o, true);
    }
}
