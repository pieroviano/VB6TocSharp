using System.Globalization;
using System.IO;
using System.Threading;
using Vb6ToCSharp.Modules;
using static Vb6ToCSharp.VbExtension;

namespace Vb6ToCSharp.Tests;

public class Vb6StringSemanticsTests
{
    [Fact] public void Replace_EmptyOrNull_IsEmptyString() { Assert.Equal("", Replace("", "a", "b")); Assert.Equal("", Replace(null!, "a", "b")); }
    [Fact] public void Replace_Replaces() => Assert.Equal("xbx", Replace("aba", "a", "x"));
    [Fact] public void Replace_StartAndCount_FollowVb() { Assert.Equal("bx", Replace("aba", "a", "x", 2)); Assert.Equal("xba", Replace("aba", "a", "x", 1, 1)); }
    [Fact] public void Split_Empty_IsEmptyArray() { Assert.Empty(Split("", ",")); Assert.Empty(Split(null!, ",")); }
    [Fact] public void Split_Splits() => Assert.Equal(new[] { "a", "", "b" }, Split("a,,b", ","));

    [Fact] public void CountLines_Empty_IsZero() => Assert.Equal(0, ModTextFiles.CountLines("", false, ""));
    [Fact] public void CDbl_Date_IsOleDate() => Assert.Equal(2.5, CDbl(new DateTime(1900, 1, 1, 12, 0, 0)));
    [Fact] public void IsIde_WithoutDebugger_DoesNotBreak() => Assert.Equal(System.Diagnostics.Debugger.IsAttached, TestUtil.WithTimeout(() => ModUtils.IsIde()));

    [Fact]
    public void WriteFile_UnwritablePath_ReturnsFalse()
    {
        var f = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing-dir", "f.txt");
        Assert.False(ModTextFiles.WriteFile(f, "x"));
    }
}

public class ModShellTests
{
    [Fact]
    public void RunCmdToOutput_CapturesStdout()
    {
        string err = null!;
        var o = TestUtil.WithTimeout(() => ModShell.RunCmdToOutput("echo hello", out err), 20000);
        Assert.Equal("hello", o.Trim());
        Assert.Equal("", err);
    }

    [Fact]
    public void RunCmdToOutput_CapturesStderr()
    {
        string err = null!;
        var o = TestUtil.WithTimeout(() => ModShell.RunCmdToOutput("echo oops 1>&2", out err), 20000);
        Assert.Equal("", o.Trim());
        Assert.Equal("oops", err.Trim());
    }

    [Fact]
    public void ShellAndWait_WaitsForExit()
    {
        var f = Path.Combine(TestUtil.TempDir(), "done.txt");
        TestUtil.WithTimeout(() => { ModShell.ShellAndWait("cmd /c echo x> \"" + f + "\"", ModShell.EnSw.EnSwHide); return 0; }, 20000);
        Assert.True(File.Exists(f));
    }

    [Theory]
    [InlineData("\"C:\\a b\\x.exe\" -q", "C:\\a b\\x.exe", "-q")]
    [InlineData("cmd /c dir", "cmd", "/c dir")]
    [InlineData("notepad", "notepad", "")]
    [InlineData("  \"x.exe\"", "x.exe", "")]
    public void SplitCommandLine_SeparatesExecutable(string cl, string exe, string args)
    {
        ModShell.SplitCommandLine(cl, out var e, out var a);
        Assert.Equal(exe, e);
        Assert.Equal(args, a);
    }

    [Fact]
    public void TempFile_IsUniqueAndCultureInvariant()
    {
        var old = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("it-IT");
            var notes = new List<string>();
            var oldNotify = ModUtils.Notify;
            ModUtils.Notify = notes.Add;
            try
            {
                var t = ModShell.TempFile();
                Assert.StartsWith(AppDomain.CurrentDomain.BaseDirectory, t);
                Assert.EndsWith(".tmp", t);
                Assert.DoesNotContain(",", Path.GetFileName(t));
                Assert.DoesNotContain("\\\\", t);
                Assert.False(File.Exists(t)); // the write test cleans up
                Assert.Empty(notes);
            }
            finally { ModUtils.Notify = oldNotify; }
        }
        finally { Thread.CurrentThread.CurrentCulture = old; }
    }

    [Fact]
    public void GitCmd_RunsGit()
    {
        string err = null!;
        if (!ModShell.RunCmdToOutput("where git", out err).Contains("git")) return; // git not installed: nothing to test
        var o = TestUtil.WithTimeout(() => ModGit.GitCmd("git --version", true, true), 20000);
        Assert.StartsWith("git version", o.Trim());
    }
}
