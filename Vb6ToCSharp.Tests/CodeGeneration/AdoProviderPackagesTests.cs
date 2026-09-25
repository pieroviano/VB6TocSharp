using System.IO;
using Vb6ToCSharp.CodeGeneration;
using Vb6ToCSharp.Parsing;
using Vb6ToCSharp.Parsing.Model;
using Vb6ToCSharp.Tests.Fixtures;

namespace Vb6ToCSharp.Tests.CodeGeneration;

/// <summary>
/// The ADO.NET client a converted project needs: which provider its connection strings name, and which package
/// that provider resolves to.
/// </summary>
public class AdoProviderPackagesTests : IClassFixture<ConverterFixture>
{
    private readonly ConverterFixture fixture;

    public AdoProviderPackagesTests(ConverterFixture fixture) => this.fixture = fixture;

    [Theory]
    [InlineData("MSOLEDBSQL", "Microsoft.Data.SqlClient")]
    [InlineData("sqloledb", "Microsoft.Data.SqlClient")] // an OLE DB ProgID is not case-sensitive
    [InlineData("Microsoft.Jet.OLEDB.4.0", "System.Data.OleDb")]
    [InlineData("MSDASQL", "System.Data.Odbc")]
    [InlineData("Npgsql", "Npgsql")]
    [InlineData("OraOLEDB.Oracle", "Oracle.ManagedDataAccess.Core")]
    [InlineData("SQLite", "Microsoft.Data.Sqlite")]
    [InlineData("Firebird", "FirebirdSql.Data.FirebirdClient")]
    public void Package_MapsAProviderTokenOntoItsClient(string token, string package) =>
        Assert.Equal(package, AdoProviderPackages.Package(token).Split(';')[0]);

    [Theory]
    [InlineData("")]
    [InlineData("SomeoneElses.Provider")]
    public void Package_RefusesAnUnknownToken(string token) => Assert.Equal("", AdoProviderPackages.Package(token));

    [Fact]
    public void Package_PinsAVersionForEveryProvider() =>
        Assert.NotEqual("", AdoProviderPackages.Package("MSOLEDBSQL").Split(';')[1]);

    [Fact]
    public void ProvidersIn_ReadsTheProviderOfAConnectionString() =>
        Assert.Equal(new[] { "MSOLEDBSQL" },
            AdoProviderPackages.ProvidersIn(
                "cn.ConnectionString = \"Provider=MSOLEDBSQL;Data Source=.;Integrated Security=SSPI;\""));

    [Fact]
    public void ProvidersIn_ReadsEveryProviderTheProjectUses()
    {
        var providers = AdoProviderPackages.ProvidersIn(
            "a = \"Provider=SQLOLEDB;...\"\r\nb = \"Provider = Microsoft.Jet.OLEDB.4.0;...\"");

        Assert.Equal(new[] { "SQLOLEDB", "Microsoft.Jet.OLEDB.4.0" }, providers);
    }

    /// <summary>An ODBC connection string names no provider: ADO read it through MSDASQL.</summary>
    [Theory]
    [InlineData("cn.Open \"Driver={SQL Server};Server=.;\"")]
    [InlineData("cn.Open \"DSN=Payroll;UID=sa;\"")]
    public void ProvidersIn_TakesADriverOrDsnForOdbc(string source) =>
        Assert.Equal(new[] { "MSDASQL" }, AdoProviderPackages.ProvidersIn(source));

    [Theory]
    [InlineData("")]
    [InlineData("s = \"no connection string here\"")]
    [InlineData("s = \"Provider=\" & PROVIDER_NAME")] // assembled at run time: there is no token to read
    public void ProvidersIn_FindsNothingWhereThereIsNothing(string source) =>
        Assert.Empty(AdoProviderPackages.ProvidersIn(source));

    [Fact]
    public void Providers_ReadsTheSourcesOfTheProject()
    {
        var dir = TestUtil.TempDir();
        File.WriteAllText(Path.Combine(dir, "modDb.bas"),
            "cn.ConnectionString = \"Provider=SQLOLEDB;Data Source=.;\"\r\n");

        var providers = AdoProviderPackages.Providers(Vbp(dir, "Module=modDb; modDb.bas"));

        Assert.Equal(new[] { "SQLOLEDB" }, providers);
    }

    /// <summary>A VB6 application of that age kept its connection string in an INI file beside the executable.</summary>
    [Fact]
    public void Providers_ReadsTheIniFilesBesideTheProject()
    {
        var dir = TestUtil.TempDir();
        File.WriteAllText(Path.Combine(dir, "App.ini"),
            "[Database]\r\nConnection=Provider=MSOLEDBSQL;Data Source=.;Initial Catalog=Payroll;\r\n");

        var providers = AdoProviderPackages.Providers(Vbp(dir));

        Assert.Equal(new[] { "MSOLEDBSQL" }, providers);
    }

    [Fact]
    public void Packages_TurnsTheProvidersIntoClientPackages()
    {
        var dir = TestUtil.TempDir();
        File.WriteAllText(Path.Combine(dir, "App.ini"), "Connection=Provider=Npgsql;Host=db;\r\n");

        var packages = AdoProviderPackages.Packages(Vbp(dir));

        Assert.Single(packages);
        Assert.StartsWith("Npgsql;", packages[0]);
    }

    [Fact]
    public void References_EmitsThePackageReference()
    {
        var dir = TestUtil.TempDir();
        File.WriteAllText(Path.Combine(dir, "App.ini"), "Connection=Provider=MSOLEDBSQL;Data Source=.;\r\n");

        var xml = AdoProviderPackages.References(Vbp(dir), "\r\n");

        Assert.Contains("<PackageReference Include=\"Microsoft.Data.SqlClient\" Version=\"", xml);
    }

    /// <summary>A connection string built at run time leaves nothing to find; the INI setting is the way in.</summary>
    [Fact]
    public void References_AsksForTheProviderWhenItFindsNone()
    {
        var dir = TestUtil.TempDir();

        var xml = AdoProviderPackages.References(Vbp(dir), "\r\n");

        Assert.Contains("TODO", xml);
        Assert.Contains(ProjectConfigurationParser.iniKeyDbProvider, xml);
        Assert.DoesNotContain("<PackageReference", xml);
    }

    private static ProjectInfo Vbp(string folder, params string[] lines)
    {
        var path = Path.Combine(folder, "Sample.vbp");
        File.WriteAllText(path, "Type=Exe\r\n" + string.Join("\r\n", lines) + "\r\n");
        return ProjectInfo.Load(path);
    }
}
