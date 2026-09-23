using System.Globalization;
using Vb6ToCSharp.CodeConversion;
using Vb6ToCSharp.Infrastructure;
using Vb6ToCSharp.Runtime.Model;
using Vb6ToCSharp.Tests.Infrastructure;

namespace Vb6ToCSharp.Tests;

public class ShellTests
{
    [Fact]
    public void RunCmdToOutput_CapturesStdout()
    {
        string err = null!;
        var o = TestUtil.WithTimeout(() => ShellHandler.RunCmdToOutput("echo hello", out err), 20000);
        Assert.Equal("hello", o.Trim());
        Assert.Equal("", err);
    }

    [Fact]
    public void RunCmdToOutput_CapturesStderr()
    {
        string err = null!;
        var o = TestUtil.WithTimeout(() => ShellHandler.RunCmdToOutput("echo oops 1>&2", out err), 20000);
        Assert.Equal("", o.Trim());
        Assert.Equal("oops", err.Trim());
    }

    [Fact]
    public void ShellAndWait_WaitsForExit()
    {
        var f = Path.Combine(TestUtil.TempDir(), "done.txt");
        TestUtil.WithTimeout(() => { ShellHandler.ShellAndWait("cmd /c echo x> \"" + f + "\"", ShowType.Hide); return 0; }, 20000);
        Assert.True(File.Exists(f));
    }

    [Theory]
    [InlineData("\"C:\\a b\\x.exe\" -q", "C:\\a b\\x.exe", "-q")]
    [InlineData("cmd /c dir", "cmd", "/c dir")]
    [InlineData("notepad", "notepad", "")]
    [InlineData("  \"x.exe\"", "x.exe", "")]
    public void SplitCommandLine_SeparatesExecutable(string cl, string exe, string args)
    {
        ShellHandler.SplitCommandLine(cl, out var e, out var a);
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
            var oldNotify = ConversionUtility.Notify;
            ConversionUtility.Notify = notes.Add;
            try
            {
                var t = ShellHandler.TempFile();
                Assert.StartsWith(AppDomain.CurrentDomain.BaseDirectory, t);
                Assert.EndsWith(".tmp", t);
                Assert.DoesNotContain(",", Path.GetFileName(t));
                Assert.DoesNotContain("\\\\", t);
                Assert.False(File.Exists(t)); // the write test cleans up
                Assert.Empty(notes);
            }
            finally { ConversionUtility.Notify = oldNotify; }
        }
        finally { Thread.CurrentThread.CurrentCulture = old; }
    }

    [Fact]
    public void GitCmd_RunsGit()
    {
        string err = null!;
        if (!ShellHandler.RunCmdToOutput("where git", out err).Contains("git")) return; // git not installed: nothing to test
        var o = TestUtil.WithTimeout(() => GitInteraction.GitCmd("git --version", true, true), 20000);
        Assert.StartsWith("git version", o.Trim());
    }
}