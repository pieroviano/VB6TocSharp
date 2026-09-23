namespace Vb6ToCSharp.UpgradeHelpers.Tests.Infrastructure;

/// <summary>Temporary directory with files for list-box tests; removed on dispose.</summary>
internal sealed class TempDir : IDisposable
{
    public TempDir()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "vb6uh_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(string name, FileAttributes extra = 0)
    {
        var full = System.IO.Path.Combine(Path, name);
        System.IO.File.WriteAllText(full, name);
        if (extra != 0) System.IO.File.SetAttributes(full, System.IO.File.GetAttributes(full) | extra);
        return full;
    }

    public string Dir(string name) => Directory.CreateDirectory(System.IO.Path.Combine(Path, name)).FullName;

    public void Dispose()
    {
        try
        {
            foreach (var f in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
                System.IO.File.SetAttributes(f, FileAttributes.Normal);
            Directory.Delete(Path, true);
        }
        catch (IOException)
        {
        }
    }
}
