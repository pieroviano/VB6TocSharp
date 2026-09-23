using Vb6ToCSharp.Parsing;
using Vb6ToCSharp.Parsing.Model;

namespace Vb6ToCSharp.Tests;

public class OcxInteropTests
{
    private static readonly OcxRef Comm = OcxRef.Parse("{648A5603-2C6E-101B-82B6-000000000014}#1.1#0; MSCOMM32.OCX");
    private static readonly OcxRef Custom = OcxRef.Parse("{11111111-2222-3333-4444-555555555555}#3.a#0; ACME.OCX");

    private static void WithLookup(Func<Guid, short, short, string?> f, Action a)
    {
        var old = OcxInterop.RegistryLookup;
        OcxInterop.ClearCache();
        OcxInterop.RegistryLookup = f;
        try { a(); }
        finally
        {
            OcxInterop.RegistryLookup = old;
            OcxInterop.ClearCache();
        }
    }

    [Fact]
    public void LibraryName_RegistryFirstThenKnownFile() =>
        WithLookup((g, ma, mi) => g == Guid.Parse("11111111-2222-3333-4444-555555555555") && ma == 3 && mi == 10 ? "AcmeLib" : null, () =>
        {
            Assert.Equal("AcmeLib", OcxInterop.LibraryName(Custom));
            Assert.Equal("MSCommLib", OcxInterop.LibraryName(Comm));
        });

    [Fact]
    public void ComReferences_OnlyHostedLibraries() =>
        WithLookup((_, _, _) => null, () =>
        {
            var x = OcxInterop.ComReferences(new[] { Comm, Custom }, new[] { "MSCommLib" });
            Assert.Contains("<COMReference Include=\"AxMSCommLib\">", x);
            Assert.Contains("<COMReference Include=\"MSCommLib\">", x);
            Assert.Contains("<WrapperTool>aximp</WrapperTool>", x);
            Assert.Contains("<VersionMinor>1</VersionMinor>", x);
            Assert.DoesNotContain("ACME", x);
        });

    [Fact]
    public void ComReferences_UnresolvedKeptWhenAHostedLibIsUnmatched() =>
        WithLookup((_, _, _) => null, () =>
        {
            var x = OcxInterop.ComReferences(new[] { Custom }, new[] { "AcmeLib" });
            Assert.Contains("Include=\"AxACME\"", x);
        });

    [Fact]
    public void ComReferences_NothingHosted_Empty() =>
        WithLookup((_, _, _) => null, () => Assert.Equal("", OcxInterop.ComReferences(new[] { Comm }, new string[0])));
}