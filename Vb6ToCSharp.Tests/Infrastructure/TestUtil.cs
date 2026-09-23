using System.IO;
using System.Threading;

namespace Vb6ToCSharp.Tests.Infrastructure;

internal static class TestUtil
{
    /// <summary>Runs <paramref name="f"/> on a worker and fails instead of hanging the run.</summary>
    public static T WithTimeout<T>(Func<T> f, int ms = 5000)
    {
        T result = default!;
        Exception? error = null;
        var t = new Thread(() =>
        {
            try { result = f(); }
            catch (Exception e) { error = e; }
        }) { IsBackground = true };
        t.Start();
        Assert.True(t.Join(ms), "call did not return (suspected infinite loop)");
        if (error != null) throw new Exception("call threw", error);
        return result;
    }

    public static string TempDir()
    {
        var d = Path.Combine(Path.GetTempPath(), "Vb6ToCSharp.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        return d;
    }
}
