using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Vb6ToCSharp.CodeConversion;
using Vb6ToCSharp.CodeConversion.Model;
using Vb6ToCSharp.Parsing.Model;
using static Vb6ToCSharp.Runtime.VbConstants;
using static Vb6ToCSharp.Runtime.VbStrings;
using static Vb6ToCSharp.Parsing.ProjectConfigurationParser;
using static Vb6ToCSharp.Parsing.ProjectFiles;
using static Vb6ToCSharp.Infrastructure.TextFiles;
using static Vb6ToCSharp.CodeConversion.ConversionUtility;

namespace Vb6ToCSharp.CodeGeneration;

/// <summary>
/// The C# project around the converted files: an SDK-style .NET 10 Windows project (WinForms or WPF), its entry point
/// (startup form or Sub Main, as the .vbp says) and assembly attributes.
/// </summary>
public static class SupportFiles
{
    /// <summary>Warnings converted code produces by design (unreachable breaks after goto, unused labels / locals).</summary>
    private const string ConvertedCodeNoWarn = "CS0162;CS0164;CS0168;CS0219;CS0414;CS0649;CS0105";

    /// <summary>Entry point and assembly attributes of the converted project.</summary>
    public static bool CreateProjectSupportFiles()
    {
        var vbp = ProjectInfo.Load(VbpFile);
        var ok = vbp.IsLibrary || WriteOut("Program.cs", ProgramFile(vbp), ""); // a class library has no entry point
        ok = WriteOut("Properties\\AssemblyInfo.cs", AssemblyInfoFile(), "Properties") && ok;
        return ok;
    }

    /// <summary>The .csproj of the converted project (named after the .vbp).</summary>
    public static bool CreateProjectFile(string vbpFile)
    {
        return WriteOut(ChgExt(TFileName(vbpFile), ".csproj"), ProjectFile(ProjectInfo.Load(vbpFile)));
    }

    /// <summary>SDK-style project: sources are picked up from the output folder; forms per the UI target.</summary>
    public static string ProjectFile(ProjectInfo projectInfo)
    {
        var n = vbCrLf;
        var wpf = Ui == UiTarget.Wpf;
        var library = projectInfo.IsLibrary; // ActiveX DLL / OCX
        var symbols = string.Concat(StatementsConverter.ProjectSymbols(projectInfo.CondComp).ConvertAll(c => ";" + c)); // VB6 CondComp that are true
        var runtime = typeof(SupportFiles).Assembly.GetName().Version;
        var s = new StringBuilder();
        s.Append("<Project Sdk=\"Microsoft.NET.Sdk\">" + n);
        s.Append("  <PropertyGroup>" + n);
        s.Append("    <TargetFramework>net10.0-windows</TargetFramework>" + n);
        s.Append("    <OutputType>" + (library ? "Library" : "WinExe") + "</OutputType>" + n);
        s.Append("    <RootNamespace>" + AssemblyName() + "</RootNamespace>" + n);
        s.Append("    <AssemblyName>" + AssemblyName() + "</AssemblyName>" + n);
        s.Append("    <" + (wpf ? "UseWPF" : "UseWindowsForms") + ">true</" + (wpf ? "UseWPF" : "UseWindowsForms") + ">" + n);
        if (!library) s.Append("    <StartupObject>" + AssemblyName() + ".Program</StartupObject>" + n);
        s.Append("    <LangVersion>latest</LangVersion>" + n);
        s.Append("    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>" + n);
        s.Append("    <DefineConstants>$(DefineConstants)" + symbols + "</DefineConstants>" + n);
        s.Append("    <NoWarn>$(NoWarn);" + ConvertedCodeNoWarn + "</NoWarn>" + n);
        s.Append("  </PropertyGroup>" + n);
        s.Append("  <ItemGroup>" + n);
        // Microsoft.VisualBasic (MsgBox, Strings, FileSystem...) and Microsoft.CSharp (dynamic, for Object)
        // are part of the shared framework: no <Reference> needed
        s.Append("    <!-- the runtime of converted code (VB6 arrays, UDTs, fixed-length strings, controls), like VB Migration Partner's library -->" + n);
        s.Append("    <PackageReference Include=\"Net4x.Vb6ToCSharp.UpgradeHelpers\" Version=\"" + runtime.Major + "." + runtime.Minor + ".*\" />" + n);
        s.Append("  </ItemGroup>" + n);
        var projects = ProjectGroup.CSharpProjectReferences(projectInfo); // VB6 references to other projects of the group
        if (projects.Count > 0)
        {
            s.Append("  <ItemGroup>" + n);
            foreach (var p in projects) s.Append("    <ProjectReference Include=\"" + p + "\" />" + n);
            s.Append("  </ItemGroup>" + n);
        }
        if (UsesAdo(projectInfo))
        {
            s.Append("  <ItemGroup>" + n);
            s.Append("    <COMReference Include=\"ADODB\">" + n);
            s.Append("      <Guid>{B691E011-1797-432E-907A-4D8C69339129}</Guid>" + n);
            s.Append("      <VersionMajor>6</VersionMajor>" + n);
            s.Append("      <VersionMinor>1</VersionMinor>" + n);
            s.Append("      <Lcid>0</Lcid>" + n);
            s.Append("      <WrapperTool>tlbimp</WrapperTool>" + n);
            s.Append("      <Isolated>False</Isolated>" + n);
            s.Append("      <EmbedInteropTypes>True</EmbedInteropTypes>" + n);
            s.Append("    </COMReference>" + n);
            s.Append("  </ItemGroup>" + n);
        }
        s.Append("</Project>" + n);
        return s.ToString();
    }

    /// <summary>The project references ADO (Microsoft ActiveX Data Objects): converted code uses ADODB.</summary>
    public static bool UsesAdo(ProjectInfo projectInfo) => projectInfo.References.Exists(r => Regex.IsMatch(r, "ActiveX Data Objects|msado|\\{00000[0-9A-F]{3}-0000-0010-8000-00AA006D2EA4\\}", RegexOptions.IgnoreCase));

    /// <summary>
    /// Program.Main: VB6 starts with the startup form, or runs Sub Main and keeps going while forms are open.
    /// </summary>
    public static string ProgramFile(ProjectInfo projectInfo)
    {
        var n = vbCrLf;
        var wpf = Ui == UiTarget.Wpf;
        var main = SubMainModule(projectInfo);
        var s = new StringBuilder();
        s.Append("using System;" + n + n);
        s.Append("namespace " + AssemblyName() + n + "{" + n);
        s.Append("    /// <summary>Entry point of the converted VB6 project (" + (projectInfo.StartsWithSubMain ? "Sub Main" : "startup form " + projectInfo.Startup) + ").</summary>" + n);
        s.Append("    internal static class Program" + n + "    {" + n);
        s.Append("        [STAThread]" + n);
        s.Append("        private static void Main()" + n + "        {" + n);
        if (wpf)
        {
            s.Append("            var app = new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnLastWindowClose };" + n);
            if (!projectInfo.StartsWithSubMain) s.Append("            app.Run(" + AssemblyName() + ".Forms." + projectInfo.Startup + ".instance);" + n);
            else if (main != null) s.Append("            global::" + main + ".Main();" + n + "            if (app.Windows.Count > 0) app.Run();" + n);
        }
        else
        {
            s.Append("            System.Windows.Forms.Application.EnableVisualStyles();" + n);
            s.Append("            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);" + n);
            if (!projectInfo.StartsWithSubMain) s.Append("            System.Windows.Forms.Application.Run(" + AssemblyName() + ".Forms." + projectInfo.Startup + ".instance);" + n);
            else if (main != null)
            {
                s.Append("            global::" + main + ".Main();" + n);
                s.Append("            // VB6 keeps running while a form is loaded" + n);
                s.Append("            while (System.Windows.Forms.Application.OpenForms.Count > 0) System.Windows.Forms.Application.Run(System.Windows.Forms.Application.OpenForms[0]);" + n);
            }
        }
        s.Append("        }" + n + "    }" + n + "}" + n);
        return s.ToString();
    }

    /// <summary>The standard module declaring Sub Main, or null.</summary>
    public static string SubMainModule(ProjectInfo projectInfo)
    {
        var folder = FilePath(VbpFile);
        foreach (var f in Split(VbpModules(VbpFile), vbCrLf))
        {
            if (Trim(f) == "" || !System.IO.File.Exists(folder + f)) continue;
            var src = ReadEntireFile(folder + f);
            if (Regex.IsMatch(src, "(?mi)^\\s*(Public |Private )?Sub\\s+Main\\s*\\(")) return ModuleName(src);
        }
        return null;
    }

    public static string AssemblyInfoFile()
    {
        var n = vbCrLf;
        return "using System.Reflection;" + n
               + "using System.Runtime.InteropServices;" + n + n
               + "[assembly: AssemblyTitle(\"" + AssemblyName() + "\")]" + n
               + "[assembly: AssemblyProduct(\"" + AssemblyName() + "\")]" + n
               + "[assembly: AssemblyCopyright(\"Copyright " + DateTime.Now.Year + "\")]" + n
               + "[assembly: ComVisible(false)]" + n
               + "[assembly: AssemblyVersion(\"1.0.0.0\")]" + n
               + "[assembly: AssemblyFileVersion(\"1.0.0.0\")]" + n;
    }
}
