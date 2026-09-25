using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Vb6ToCSharp.Parsing.Model;
using static Vb6ToCSharp.Runtime.VbConstants;
using static Vb6ToCSharp.Runtime.VbStrings;
using static Vb6ToCSharp.Runtime.RuntimeExtension;
using static Vb6ToCSharp.Parsing.ProjectConfigurationParser;
using static Vb6ToCSharp.Parsing.ProjectFiles;
using static Vb6ToCSharp.CodeConversion.ConversionUtility;

namespace Vb6ToCSharp.CodeGeneration;

/// <summary>
/// The ADO.NET client a converted project needs beside the managed ADODB package.
/// <para>
/// ADO reached a database through the OLE DB provider its connection string named, and the provider came with the
/// operating system. <see cref="SupportFiles.AdoPackage"/> resolves its <c>DbProviderFactory</c> by name instead, so
/// it needs no client of its own - but the converted project does, or the very first <c>Connection.Open</c> fails
/// with "provider is not registered". Which client that is follows from the <c>Provider=</c> token, so the
/// conversion reads the connection strings the project carries and adds the matching package.
/// </para>
/// </summary>
public static class AdoProviderPackages
{
    /// <summary>
    /// The <c>Provider=</c> tokens ADO connection strings use, and the ADO.NET client package each one resolves to,
    /// as "token[,token...]=package;version". The tokens are the ones the managed ADODB package maps onto a factory,
    /// OLE DB ProgIDs included, because a converted connection string still names the OLE DB provider it always did.
    /// Overridable per token from the INI, as the control catalog is.
    /// </summary>
    private const string Catalog =
            "SQLOLEDB,SQLNCLI,SQLNCLI10,SQLNCLI11,MSOLEDBSQL,MSOLEDBSQL19,SQLClient,System.Data.SqlClient,Microsoft.Data.SqlClient=Microsoft.Data.SqlClient;7.*|"
          + "MSDAORA.OLEDB,Microsoft.Jet.OLEDB.4.0,Microsoft.Jet.OLEDB.3.51,Microsoft.ACE.OLEDB.12.0,Microsoft.ACE.OLEDB.16.0,OleDb,System.Data.OleDb=System.Data.OleDb;10.*|"
          + "MSDASQL,Odbc,System.Data.Odbc=System.Data.Odbc;10.*|"
          + "MySQLProv,MySQL,MySql.Data.MySqlClient,MySqlConnector=MySql.Data;9.*|"
          + "PostgreSQL,PostgreSQL OLE DB Provider,PGNP,Npgsql=Npgsql;10.*|"
          + "OraOLEDB.Oracle,MSDAORA,Oracle.ManagedDataAccess.Client,System.Data.OracleClient=Oracle.ManagedDataAccess.Core;23.*|"
          + "SQLite,SQLiteOLEDB,Microsoft.Data.Sqlite,System.Data.SQLite=Microsoft.Data.Sqlite;10.*|"
          + "Firebird,LCPI.IBProvider,FirebirdSql.Data.FirebirdClient=FirebirdSql.Data.FirebirdClient;10.*";

    /// <summary>A string is a connection string when it carries the keyword that names the driver.</summary>
    private const string ProviderKeyword = "(?:Provider|Data\\s*Provider)\\s*=\\s*([^;\"'&]+)";

    /// <summary>
    /// An ODBC connection string names no provider: ADO read it through MSDASQL, its own default, which is what
    /// <c>Driver=</c> (a DSN-less connection) and <c>DSN=</c> identify.
    /// </summary>
    private const string OdbcKeyword = "(?<![A-Za-z0-9_])(?:Driver|DSN)\\s*=";

    /// <summary>The ADO provider token the ODBC keywords stand for.</summary>
    private const string OdbcProvider = "MSDASQL";

    /// <summary>The packages the project's connection strings call for, as <c>PackageReference</c> lines.</summary>
    /// <param name="n">The line separator of the file being written.</param>
    public static string References(ProjectInfo projectInfo, string n)
    {
        var o = "";
        foreach (var package in Packages(projectInfo))
        {
            var name = SplitWord(package, 1, ";");
            var version = SplitWord(package, 2, ";");
            o = o + "    <PackageReference Include=\"" + name + "\" Version=\"" + version + "\" />" + n;
        }
        if (o != "")
        {
            return "    <!-- the ADO.NET client the connection strings ask for: " + SupportFiles.AdoPackage
                   + " resolves its factory by name, so the project has to carry it -->" + n + o;
        }
        return "    <!-- TODO: no ADO connection string found, so no ADO.NET client is referenced; name the provider"
               + " with [" + iniSectionSettings + "] " + iniKeyDbProvider + "=<Provider= token> -->" + n;
    }

    /// <summary>
    /// The client packages the project needs, "package;version" each, without repetition: the provider named in the
    /// INI if there is one, otherwise every provider its connection strings name - a project may use two engines.
    /// </summary>
    public static List<string> Packages(ProjectInfo projectInfo)
    {
        var packages = new List<string>();
        foreach (var token in Providers(projectInfo))
        {
            var package = Package(token);
            if (package == "")
            {
                Notify("Unknown ADO provider, no client package referenced: " + token);
                continue;
            }
            if (!packages.Contains(package)) packages.Add(package);
        }
        return packages;
    }

    /// <summary>The package a provider token resolves to, "package;version", or "" when neither table knows it.</summary>
    public static string Package(string token)
    {
        var t = Trim(token);
        if (t == "") return "";
        var ini = IniMap(iniSectionAdoProviders, t); // a project-specific provider, or another client for a known one
        if (ini != null) return IsInStr(ini, ";") ? ini : ini + ";*";
        foreach (var entry in Split(Catalog, "|"))
        {
            foreach (var known in Split(SplitWord(entry, 1, "="), ","))
            {
                if (LCase(Trim(known)) == LCase(t)) return SplitWord(entry, 2, "=");
            }
        }
        return "";
    }

    /// <summary>
    /// The provider tokens the project names: the INI setting wins, because a connection string assembled at run
    /// time or read from the registry cannot be found in the sources at all.
    /// </summary>
    public static List<string> Providers(ProjectInfo projectInfo)
    {
        var configured = Trim(IniMap(iniSectionSettings, iniKeyDbProvider) ?? "");
        if (configured != "") return new List<string>(Split(configured, ","));
        var found = new List<string>();
        foreach (var file in SourceFiles(projectInfo))
        {
            foreach (var token in ProvidersIn(ReadFileIfAny(file)))
            {
                if (!found.Contains(token)) found.Add(token);
            }
        }
        return found;
    }

    /// <summary>The provider tokens the connection strings in a piece of text name.</summary>
    public static List<string> ProvidersIn(string text)
    {
        var found = new List<string>();
        foreach (Match m in Regex.Matches(text ?? "", ProviderKeyword, RegexOptions.IgnoreCase))
        {
            var token = Trim(m.Groups[1].Value);
            // the VB6 source may build the string up: "Provider=" & PROVIDER - there is no token to read then
            if (token != "" && Package(token) != "" && !found.Contains(token)) found.Add(token);
        }
        if (found.Count == 0 && Regex.IsMatch(text ?? "", OdbcKeyword, RegexOptions.IgnoreCase))
        {
            found.Add(OdbcProvider);
        }
        return found;
    }

    /// <summary>
    /// Where a connection string can be read without running the program: the project's own sources, and the INI
    /// files beside the .vbp, which is where a VB6 application of that age kept it.
    /// </summary>
    private static IEnumerable<string> SourceFiles(ProjectInfo projectInfo)
    {
        var folder = projectInfo.Folder;
        if (folder == "") yield break;
        var vbp = projectInfo.Path;
        var list = VbpModules(vbp) + vbCrLf + VbpClasses(vbp) + vbCrLf + VbpForms(vbp) + vbCrLf + VbpUserControls(vbp);
        foreach (var name in Split(list, vbCrLf))
        {
            if (Trim(name) != "") yield return Path.Combine(folder, Trim(name));
        }
        foreach (var ini in SafeFiles(folder, "*.ini"))
        {
            yield return ini;
        }
    }

    private static string[] SafeFiles(string folder, string pattern)
    {
        try
        {
            return Directory.GetFiles(folder, pattern);
        }
        catch (IOException)
        {
            return new string[0];
        }
    }

    private static string ReadFileIfAny(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path, System.Text.Encoding.Default) : "";
        }
        catch (IOException)
        {
            return "";
        }
    }
}
