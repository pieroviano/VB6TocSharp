using System;
using static Microsoft.VisualBasic.Constants;
using static Microsoft.VisualBasic.FileSystem;
using static Microsoft.VisualBasic.Strings;
using static Vb6ToCSharp.VbExtension;
using static Vb6ToCSharp.Modules.ModConfig;
using static Vb6ToCSharp.Modules.ModProjectFiles;
using static Vb6ToCSharp.Modules.ModTextFiles;
using static Vb6ToCSharp.Modules.ModUtils;


namespace Vb6ToCSharp.Modules;

public static class ModSupportFiles
{
    // Option Explicit


    public static bool CreateProjectSupportFiles()
    {
        var createProjectSupportFiles = false;

        var s = ApplicationXaml();
        var f = "application.xaml";
        WriteOut(f, s, "");

        s = VbExtensionClass();
        f = "VBExtension.cs";
        WriteOut(f, s, "");

        s = VbaConstantsClass();
        f = "VBConstants.cs";
        WriteOut(f, s, "");

        s = AppConfigFile();
        f = "App.config";
        WriteOut(f, s, "");

        s = AppXamlCsFile();
        f = "App.xaml.cs";
        WriteOut(f, s, "");

        GeneratePropertiesFiles();
        return createProjectSupportFiles;
    }

    public static bool GeneratePropertiesFiles()
    {
        var generatePropertiesFiles = false;

        var s = OutputFolder();
        s = s + "Properties\\";
        if (Dir(s, vbDirectory) == "")
        {
            System.IO.Directory.CreateDirectory(s);
        }


        WriteOut("Properties\\Settings.settings", SettingsSettingsFile(), "Properties");
        WriteOut("Properties\\Settings.Designer.cs", SettingsDesignerCsFile(), "Properties");
        WriteOut("Properties\\AssemblyInfo.cs", AssemblyInfoFile(), "Properties");
        WriteOut("Properties\\Resources.resx", ResourcesResxFile(), "Properties");
        WriteOut("Properties\\Resources.Designer.cs", ResourcesDesignerCsFile(), "Properties");

        ResourcesResxFile();
        return generatePropertiesFiles;
    }

    public static string ApplicationXaml()
    {
        var r = "";
        var m = "";
        var n = vbCrLf;

        r = r + m + "<Application x:Class=\"Application\" ";
        r = r + n + "xmlns = \"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" ";
        r = r + n + "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" ";
        r = r + n + "xmlns:local=\"clr-namespace:" + AssemblyName() + "\" ";
        r = r + n + "StartupUri=\"MainWindow.xaml\"> ";
        r = r + n + "  <Application.Resources>";
        r = r + n + "  </Application.Resources>";
        r = r + n + "</Application>";

        var applicationXaml = r;
        return applicationXaml;
    }

    public static string CreateProjectFile(string vbpFile)
    {
        dynamic l = null;

        var s = "";
        var m = "";
        var n = vbCrLf;


        s = s + m + "<?xml version=\"1.0\" encoding=\"utf-8\"?>";
        s = s + n + "<Project ToolsVersion=\"15.0\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">";
        s = s + n + "  <Import Project=\"$(MSBuildExtensionsPath)\\$(MSBuildToolsVersion)\\Microsoft.Common.props\" Condition=\"Exists('$(MSBuildExtensionsPath)\\$(MSBuildToolsVersion)\\Microsoft.Common.props')\" />";
        s = s + n + "  <PropertyGroup>";
        s = s + n + "    <Configuration Condition=\" '$(Configuration)' == '' \">Debug</Configuration>";
        s = s + n + "    <Platform Condition=\" '$(Platform)' == '' \">AnyCPU</Platform>";
        s = s + n + "    <ProjectGuid>{92F75129-0EC1-47BA-85A7-E47F9EB140FD}</ProjectGuid>";
        s = s + n + "    <OutputType>WinExe</OutputType>";
        s = s + n + "    <RootNamespace>" + AssemblyName() + "</RootNamespace>";
        s = s + n + "    <AssemblyName>" + AssemblyName() + "</AssemblyName>";
        s = s + n + "    <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>";
        s = s + n + "    <FileAlignment>512</FileAlignment>";
        s = s + n + "    <ProjectTypeGuids>{60dc8134-eba5-43b8-bcc9-bb4bc16c2548};{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}</ProjectTypeGuids>";
        s = s + n + "    <WarningLevel>4</WarningLevel>";
        s = s + n + "    <AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>";
        s = s + n + "    <Deterministic>true</Deterministic>";
        s = s + n + "  </PropertyGroup>";
        s = s + n + "  <PropertyGroup Condition=\" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' \">";
        s = s + n + "    <PlatformTarget>AnyCPU</PlatformTarget>";
        s = s + n + "    <DebugSymbols>true</DebugSymbols>";
        s = s + n + "    <DebugType>full</DebugType>";
        s = s + n + "    <Optimize>false</Optimize>";
        s = s + n + "    <OutputPath>bin\\Debug\\</OutputPath>";
        // VB6 conditional compilation arguments that are true become C# symbols
        var vbSymbols = string.Concat(ModConvertStatements.ProjectSymbols(Vb6ToCSharp.FormConversion.VbpInfo.Load(vbpFile).CondComp).ConvertAll(c => ";" + c));
        s = s + n + "    <DefineConstants>DEBUG;TRACE" + vbSymbols + "</DefineConstants>";
        s = s + n + "    <ErrorReport>prompt</ErrorReport>";
        s = s + n + "    <WarningLevel>4</WarningLevel>";
        s = s + n + "  </PropertyGroup>";
        s = s + n + "  <PropertyGroup Condition=\" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' \">";
        s = s + n + "    <PlatformTarget>AnyCPU</PlatformTarget>";
        s = s + n + "    <DebugType>pdbonly</DebugType>";
        s = s + n + "    <Optimize>true</Optimize>";
        s = s + n + "    <OutputPath>bin\\Release\\</OutputPath>";
        s = s + n + "    <DefineConstants>TRACE" + vbSymbols + "</DefineConstants>";
        s = s + n + "    <ErrorReport>prompt</ErrorReport>";
        s = s + n + "    <WarningLevel>4</WarningLevel>";
        s = s + n + "  </PropertyGroup>";
        s = s + n + "  <ItemGroup>";
        s = s + n + "    <Reference Include=\"Microsoft.VisualBasic\" />";
        s = s + n + "    <Reference Include=\"Microsoft.VisualBasic.Compatibility\" />";
        s = s + n + "    <Reference Include=\"Microsoft.VisualBasic.Compatibility.Data\" />";
        s = s + n + "    <Reference Include=\"Microsoft.VisualBasic.PowerPacks, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a, processorArchitecture=MSIL\" />";
        s = s + n + "    <Reference Include=\"Microsoft.VisualBasic.PowerPacks.Vs, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a, processorArchitecture=MSIL\" />";
        s = s + n + "    <Reference Include=\"System\" />";
        s = s + n + "    <Reference Include=\"System.Data\" />";
        s = s + n + "    <Reference Include=\"System.Drawing\" />";
        s = s + n + "    <Reference Include=\"System.Xml\" />";
        s = s + n + "    <Reference Include=\"Microsoft.CSharp\" />";
        s = s + n + "    <Reference Include=\"System.Core\" />";
        s = s + n + "    <Reference Include=\"System.Xml.Linq\" />";
        s = s + n + "    <Reference Include=\"System.Data.DataSetExtensions\" />";
        s = s + n + "    <Reference Include=\"System.Net.Http\" />";
        s = s + n + "    <Reference Include=\"System.Xaml\">";
        s = s + n + "      <RequiredTargetFramework>4.0</RequiredTargetFramework>";
        s = s + n + "    </Reference>";
        s = s + n + "    <Reference Include=\"WindowsBase\" />";
        s = s + n + "    <Reference Include=\"PresentationCore\" />";
        s = s + n + "    <Reference Include=\"PresentationFramework\" />";
        s = s + n + "  </ItemGroup>";
        s = s + n + "  <ItemGroup>";
        // the runtime of converted code (VB6 arrays, UDTs, fixed-length strings, controls), like VB Migration Partner's library
        var runtime = typeof(ModSupportFiles).Assembly.GetName().Version;
        s = s + n + "    <PackageReference Include=\"Net4x.Vb6ToCSharp.UpgradeHelpers\" Version=\"" + runtime.Major + "." + runtime.Minor + ".*\" />";
        s = s + n + "  </ItemGroup>";
        s = s + n + "  <ItemGroup>";
        s = s + n + "    <ApplicationDefinition Include=\"Application.xaml\">";
        s = s + n + "      <Generator>MSBuild:Compile</Generator>";
        s = s + n + "      <SubType>Designer</SubType>";
        s = s + n + "    </ApplicationDefinition>";
        s = s + n + "    <Compile Include=\"App.xaml.cs\">";
        s = s + n + "      <DependentUpon>App.xaml</DependentUpon>";
        s = s + n + "      <SubType>Code</SubType>";
        s = s + n + "    </Compile>";

        foreach (var iterL in Split(VbpForms(vbpFile), vbCrLf))
        {
            l = iterL;
            if (l == "")
            {
                goto SkipForm;
            }
            s = s + n + "    <Page Include=\"" + OutputSubFolder(l) + ChgExt(l, ".xaml") + "\">";
            s = s + n + "      <SubType>Designer</SubType>";
            s = s + n + "      <Generator>MSBuild:Compile</Generator>";
            s = s + n + "    </Page>";
            s = s + n + "    <Compile Include=\"" + OutputSubFolder(l) + ChgExt(l, ".xaml.cs") + "\">";
            s = s + n + "      <DependentUpon>" + ChgExt(l, ".xaml") + "</DependentUpon>";
            s = s + n + "      <SubType>Code</SubType>";
            s = s + n + "    </Compile>";
            SkipForm:;
        }


        s = s + n + "    <Compile Include=\"VBExtension.cs\" />";
        s = s + n + "    <Compile Include=\"VBConstants.cs\" />";
        foreach (var iterL in Split(VbpClasses(vbpFile) + vbCrLf + VbpModules(vbpFile), vbCrLf))
        {
            l = iterL;
            if (l == "")
            {
                goto SkipClass;
            }
            s = s + n + "    <Compile Include=\"" + OutputSubFolder(l) + ChgExt(l, ".cs") + "\" />";
            SkipClass:;
        }

        s = s + n + "  </ItemGroup>";
        s = s + n + "  <ItemGroup>";
        s = s + n + "    <Compile Include=\"Properties\\AssemblyInfo.cs\">";
        s = s + n + "      <SubType>Code</SubType>";
        s = s + n + "    </Compile>";
        s = s + n + "    <Compile Include=\"Properties\\Resources.Designer.cs\">";
        s = s + n + "      <AutoGen>True</AutoGen>";
        s = s + n + "      <DesignTime>True</DesignTime>";
        s = s + n + "      <DependentUpon>Resources.resx</DependentUpon>";
        s = s + n + "    </Compile>";
        s = s + n + "    <Compile Include=\"Properties\\Settings.Designer.cs\">";
        s = s + n + "      <AutoGen>True</AutoGen>";
        s = s + n + "      <DependentUpon>Settings.settings</DependentUpon>";
        s = s + n + "      <DesignTimeSharedInput>True</DesignTimeSharedInput>";
        s = s + n + "    </Compile>";
        s = s + n + "    <EmbeddedResource Include=\"Properties\\Resources.resx\">";
        s = s + n + "      <Generator>ResXFileCodeGenerator</Generator>";
        s = s + n + "      <LastGenOutput>Resources.Designer.cs</LastGenOutput>";
        s = s + n + "    </EmbeddedResource>";
        s = s + n + "    <None Include=\"Properties\\Settings.settings\">";
        s = s + n + "      <Generator>SettingsSingleFileGenerator</Generator>";
        s = s + n + "      <LastGenOutput>Settings.Designer.cs</LastGenOutput>";
        s = s + n + "    </None>";
        s = s + n + "  </ItemGroup>";
        s = s + n + "  <ItemGroup>";
        s = s + n + "    <None Include=\"App.config\" />";
        s = s + n + "  </ItemGroup>";
        s = s + n + "  <ItemGroup>";
        s = s + n + "    <COMReference Include=\"ADODB\">";
        s = s + n + "      <Guid>{B691E011-1797-432E-907A-4D8C69339129}</Guid>";
        s = s + n + "      <VersionMajor>6</VersionMajor>";
        s = s + n + "      <VersionMinor>1</VersionMinor>";
        s = s + n + "      <Lcid>0</Lcid>";
        s = s + n + "      <WrapperTool>tlbimp</WrapperTool>";
        s = s + n + "      <Isolated>False</Isolated>";
        s = s + n + "      <EmbedInteropTypes>True</EmbedInteropTypes>";
        s = s + n + "    </COMReference>";
        s = s + n + "  </ItemGroup>";
        s = s + n + "  <Import Project=\"$(MSBuildToolsPath)\\Microsoft.CSharp.targets\" />";
        s = s + n + "</Project>";

        var createProjectFile = s;

        WriteOut(ChgExt(TFileName(vbpFile), ".csproj"), s);
        return createProjectFile;
    }

    public static string VbExtensionClass()
    {
        var vbExtensionClass = ReadEntireFile(AppDomain.CurrentDomain.BaseDirectory + "\\\\VBExtension.cs");
        return vbExtensionClass;
    }

    public static string VbaConstantsClass()
    {
        var vbaConstantsClass = ReadEntireFile(AppDomain.CurrentDomain.BaseDirectory + "\\\\VBConstants.cs");
        return vbaConstantsClass;
    }

    public static string AppConfigFile()
    {
        var r = "";
        var m = "";
        var n = vbCrLf;

        r = r + m + "<?xml version='1.0' encoding='utf-8'?>";
        r = r + n + "<configuration>";
        r = r + n + "    <startup>";
        r = r + n + "        <supportedRuntime version='v4.0' sku='.NETFramework,Version=v4.7.2'/>";
        r = r + n + "    </startup>";
        r = r + n + "</configuration>";

        var appConfigFile = r;
        return appConfigFile;
    }

    public static string AppXamlCsFile()
    {
        var r = "";
        var m = "";
        var n = vbCrLf;

        r = r + m + "using System;";
        r = r + n + "using System.Collections.Generic;";
        r = r + n + "using System.Configuration;";
        r = r + n + "using System.Data;";
        r = r + n + "using System.Linq;";
        r = r + n + "using System.Threading.Tasks;";
        r = r + n + "using System.Windows;";
        r = r + n + "";
        r = r + n + "namespace " + AssemblyName();
        r = r + n + "{";
        r = r + n + "  /// <summary>";
        r = r + n + "  /// Interaction logic for App.xaml";
        r = r + n + "  /// </summary>";
        r = r + n + "  public partial class App : Application";
        r = r + n + "    {";
        r = r + n + "    }";
        r = r + n + "}";
        r = r + n + "";

        var appXamlCsFile = r;
        return appXamlCsFile;
    }

    public static string SettingsSettingsFile()
    {
        var r = "";
        var m = "";
        var n = vbCrLf;

        r = r + m + "<?xml version='1.0' encoding='utf-8'?>";
        r = r + n + "<SettingsFile xmlns='uri:settings' CurrentProfile='(Default)'>";
        r = r + n + "  <Profiles>";
        r = r + n + "    <Profile Name='(Default)' />";
        r = r + n + "  </Profiles>";
        r = r + n + "  <Settings />";
        r = r + n + "</SettingsFile>";

        var settingsSettingsFile = r;
        return settingsSettingsFile;
    }

    public static string SettingsDesignerCsFile()
    {
        var r = "";
        var m = "";
        var n = vbCrLf;

        r = r + m + "//------------------------------------------------------------------------------";
        r = r + n + "// <auto-generated>";
        r = r + n + "//     This code was generated by a tool.";
        r = r + n + "//     Runtime Version:4.0.30319.42000";
        r = r + n + "//";
        r = r + n + "//     Changes to this file may cause incorrect behavior and will be lost if";
        r = r + n + "//     the code is regenerated.";
        r = r + n + "// </auto-generated>";
        r = r + n + "//------------------------------------------------------------------------------";
        r = r + n + "";
        r = r + n + "namespace " + AssemblyName() + ".Properties {";
        r = r + n + "";
        r = r + n + "";
        r = r + n + "    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]";
        r = r + n + "    [global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator\", \"15.9.0.0\")]";
        r = r + n + "    internal sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase {";
        r = r + n + "";
        r = r + n + "        private static Settings defaultInstance = ((Settings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));";
        r = r + n + "";
        r = r + n + "        public static Settings Default {";
        r = r + n + "            get {";
        r = r + n + "                return defaultInstance;";
        r = r + n + "            }";
        r = r + n + "        }";
        r = r + n + "    }";
        r = r + n + "}";

        var settingsDesignerCsFile = r;
        return settingsDesignerCsFile;
    }

    public static string AssemblyInfoFile()
    {
        var r = "";
        var m = "";
        var n = vbCrLf;

        r = r + m + "using System.Reflection;";
        r = r + n + "using System.Resources;";
        r = r + n + "using System.Runtime.CompilerServices;";
        r = r + n + "using System.Runtime.InteropServices;";
        r = r + n + "using System.Windows;";
        r = r + n + "";
        r = r + n + "// General Information about an assembly is controlled through the following";
        r = r + n + "// set of attributes. Change these attribute values to modify the information";
        r = r + n + "// associated with an assembly.";
        r = r + n + "[assembly: AssemblyTitle(\"" + AssemblyName() + "\")]";
        r = r + n + "[assembly: AssemblyDescription(\"\")]";
        r = r + n + "[assembly: AssemblyConfiguration(\"\")]";
        r = r + n + "[assembly: AssemblyCompany(\"\")]";
        r = r + n + "[assembly: AssemblyProduct(\"" + AssemblyName() + "\")]";
        r = r + n + "[assembly: AssemblyCopyright(\"Copyright " + DateTime.Now.Year +"\")]";
        r = r + n + "[assembly: AssemblyTrademark(\"\")]";
        r = r + n + "[assembly: AssemblyCulture(\"\")]";
        r = r + n + "";
        r = r + n + "// Setting ComVisible to false makes the types in this assembly not visible";
        r = r + n + "// to COM components.  If you need to access a type in this assembly from";
        r = r + n + "// COM, set the ComVisible attribute to true on that type.";
        r = r + n + "[assembly: ComVisible(false)]";
        r = r + n + "";
        r = r + n + "//In order to begin building localizable applications, set";
        r = r + n + "//<UICulture>CultureYouAreCodingWith</UICulture> in your .csproj file";
        r = r + n + "//inside a <PropertyGroup>.  For example, if you are using US english";
        r = r + n + "//in your source files, set the <UICulture> to en-US.  Then uncomment";
        r = r + n + "//the NeutralResourceLanguage attribute below.  Update the \"en-US\" in";
        r = r + n + "//the line below to match the UICulture setting in the project file.";
        r = r + n + "";
        r = r + n + "//[assembly: NeutralResourcesLanguage(\"en-US\", UltimateResourceFallbackLocation.Satellite)]";
        r = r + n + "";
        r = r + n + "";
        r = r + n + "[assembly: ThemeInfo(";
        r = r + n + "  ResourceDictionaryLocation.None, //where theme specific resource dictionaries are located";
        r = r + n + "//(used if a resource is not found in the page,";
        r = r + n + "// or application resource dictionaries)";
        r = r + n + "ResourceDictionaryLocation.SourceAssembly //where the generic resource dictionary is located";
        r = r + n + "                                              //(used if a resource is not found in the page,";
        r = r + n + "                                              // app, or any theme specific resource dictionaries)";
        r = r + n + ")]";
        r = r + n + "";
        r = r + n + "";
        r = r + n + "// Version information for an assembly consists of the following four values:";
        r = r + n + "//";
        r = r + n + "//      Major Version";
        r = r + n + "//      Minor Version";
        r = r + n + "//      Build Number";
        r = r + n + "//      Revision";
        r = r + n + "//";
        r = r + n + "// You can specify all the values or you can default the Build and Revision Numbers";
        r = r + n + "// by using the '*' as shown below:";
        r = r + n + "// [assembly: AssemblyVersion(\"1.0.*\")]";
        r = r + n + "[assembly: AssemblyVersion(\"1.0.0.0\")]";
        r = r + n + "[assembly: AssemblyFileVersion(\"1.0.0.0\")]";

        var assemblyInfoFile = r;
        return assemblyInfoFile;
    }

    public static string ResourcesResxFile()
    {
        var r = "";
        var n = vbCrLf;


        r = r + n + "<?xml version='1.0' encoding='utf-8'?>";
        r = r + n + "<root>";
        r = r + n + "  <!--";
        r = r + n + "    Microsoft ResX Schema";
        r = r + n + "";
        r = r + n + "    Version 2.0";
        r = r + n + "";
        r = r + n + "    The primary goals of this format is to allow a simple XML format";
        r = r + n + "    that is mostly human readable. The generation and parsing of the";
        r = r + n + "    various data types are done through the TypeConverter classes";
        r = r + n + "    associated with the data types.";
        r = r + n + "";
        r = r + n + "    Example:";
        r = r + n + "";
        r = r + n + "    ... ado.net/XML headers & schema ...";
        r = r + n + "    <resheader name='resmimetype'>text/microsoft-resx</resheader>";
        r = r + n + "    <resheader name='version'>2.0</resheader>";
        r = r + n + "    <resheader name='reader'>System.Resources.ResXResourceReader, System.Windows.Forms, ...</resheader>";
        r = r + n + "    <resheader name='writer'>System.Resources.ResXResourceWriter, System.Windows.Forms, ...</resheader>";
        r = r + n + "    <data name='Name1'><value>this is my long string</value><comment>this is a comment</comment></data>";
        r = r + n + "    <data name='Color1' type='System.Drawing.Color, System.Drawing'>Blue</data>";
        r = r + n + "    <data name='Bitmap1' mimetype='application/x-microsoft.net.object.binary.base64'>";
        r = r + n + "        <value>[base64 mime encoded serialized .NET Framework object]</value>";
        r = r + n + "    </data>";
        r = r + n + "    <data name='Icon1' type='System.Drawing.Icon, System.Drawing' mimetype='application/x-microsoft.net.object.bytearray.base64'>";
        r = r + n + "        <value>[base64 mime encoded string representing a byte array form of the .NET Framework object]</value>";
        r = r + n + "        <comment>This is a comment</comment>";
        r = r + n + "    </data>";
        r = r + n + "";
        r = r + n + "    There are any number of 'resheader' rows that contain simple";
        r = r + n + "    name/value pairs.";
        r = r + n + "";
        r = r + n + "    Each data row contains a name, and value. The row also contains a";
        r = r + n + "    type or mimetype. Type corresponds to a .NET class that support";
        r = r + n + "    text/value conversion through the TypeConverter architecture.";
        r = r + n + "    Classes that don't support this are serialized and stored with the";
        r = r + n + "    mimetype set.";
        r = r + n + "";
        r = r + n + "    The mimetype is used for serialized objects, and tells the";
        r = r + n + "    ResXResourceReader how to depersist the object. This is currently not";
        r = r + n + "    extensible. For a given mimetype the value must be set accordingly:";
        r = r + n + "";
        r = r + n + "    Note - application/x-microsoft.net.object.binary.base64 is the format";
        r = r + n + "    that the ResXResourceWriter will generate, however the reader can";
        r = r + n + "    read any of the formats listed below.";
        r = r + n + "";
        r = r + n + "    mimetype: application/x-microsoft.net.object.binary.base64";
        r = r + n + "    value   : The object must be serialized with";
        r = r + n + "            : System.Serialization.Formatters.Binary.BinaryFormatter";
        r = r + n + "            : and then encoded with base64 encoding.";
        r = r + n + "";
        r = r + n + "    mimetype: application/x-microsoft.net.object.soap.base64";
        r = r + n + "    value   : The object must be serialized with";
        r = r + n + "            : System.Runtime.Serialization.Formatters.Soap.SoapFormatter";
        r = r + n + "            : and then encoded with base64 encoding.";
        r = r + n + "";
        r = r + n + "    mimetype: application/x-microsoft.net.object.bytearray.base64";
        r = r + n + "    value   : The object must be serialized into a byte array";
        r = r + n + "            : using a System.ComponentModel.TypeConverter";
        r = r + n + "            : and then encoded with base64 encoding.";
        r = r + n + "    -->";
        r = r + n + "  <xsd:schema id='root' xmlns='' xmlns:xsd='http://www.w3.org/2001/XMLSchema' xmlns:msdata='urn:schemas-microsoft-com:xml-msdata'>";
        r = r + n + "    <xsd:element name='root' msdata:IsDataSet='true'>";
        r = r + n + "      <xsd:complexType>";
        r = r + n + "        <xsd:choice maxOccurs='unbounded'>";
        r = r + n + "          <xsd:element name='metadata'>";
        r = r + n + "            <xsd:complexType>";
        r = r + n + "              <xsd:sequence>";
        r = r + n + "                <xsd:element name='value' type='xsd:string' minOccurs='0' />";
        r = r + n + "              </xsd:sequence>";
        r = r + n + "              <xsd:attribute name='name' type='xsd:string' />";
        r = r + n + "              <xsd:attribute name='type' type='xsd:string' />";
        r = r + n + "              <xsd:attribute name='mimetype' type='xsd:string' />";
        r = r + n + "            </xsd:complexType>";
        r = r + n + "          </xsd:element>";
        r = r + n + "          <xsd:element name='assembly'>";
        r = r + n + "            <xsd:complexType>";
        r = r + n + "              <xsd:attribute name='alias' type='xsd:string' />";
        r = r + n + "              <xsd:attribute name='name' type='xsd:string' />";
        r = r + n + "            </xsd:complexType>";
        r = r + n + "          </xsd:element>";
        r = r + n + "          <xsd:element name='data'>";
        r = r + n + "            <xsd:complexType>";
        r = r + n + "              <xsd:sequence>";
        r = r + n + "                <xsd:element name='value' type='xsd:string' minOccurs='0' msdata:Ordinal='1' />";
        r = r + n + "                <xsd:element name='comment' type='xsd:string' minOccurs='0' msdata:Ordinal='2' />";
        r = r + n + "              </xsd:sequence>";
        r = r + n + "              <xsd:attribute name='name' type='xsd:string' msdata:Ordinal='1' />";
        r = r + n + "              <xsd:attribute name='type' type='xsd:string' msdata:Ordinal='3' />";
        r = r + n + "              <xsd:attribute name='mimetype' type='xsd:string' msdata:Ordinal='4' />";
        r = r + n + "            </xsd:complexType>";
        r = r + n + "          </xsd:element>";
        r = r + n + "          <xsd:element name='resheader'>";
        r = r + n + "            <xsd:complexType>";
        r = r + n + "              <xsd:sequence>";
        r = r + n + "                <xsd:element name='value' type='xsd:string' minOccurs='0' msdata:Ordinal='1' />";
        r = r + n + "              </xsd:sequence>";
        r = r + n + "              <xsd:attribute name='name' type='xsd:string' use='required' />";
        r = r + n + "            </xsd:complexType>";
        r = r + n + "          </xsd:element>";
        r = r + n + "        </xsd:choice>";
        r = r + n + "      </xsd:complexType>";
        r = r + n + "    </xsd:element>";
        r = r + n + "  </xsd:schema>";
        r = r + n + "  <resheader name='resmimetype'>";
        r = r + n + "    <value>text/microsoft-resx</value>";
        r = r + n + "  </resheader>";
        r = r + n + "  <resheader name='version'>";
        r = r + n + "    <value>2.0</value>";
        r = r + n + "  </resheader>";
        r = r + n + "  <resheader name='reader'>";
        r = r + n + "    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>";
        r = r + n + "  </resheader>";
        r = r + n + "  <resheader name='writer'>";
        r = r + n + "    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>";
        r = r + n + "  </resheader>";
        r = r + n + "</root>End Function";

        var resourcesResxFile = r;
        return resourcesResxFile;
    }

    public static string ResourcesDesignerCsFile()
    {
        var r = "";
        var n = vbCrLf;

        r = r + n + "//------------------------------------------------------------------------------";
        r = r + n + "// <auto-generated>";
        r = r + n + "//     This code was generated by a tool.";
        r = r + n + "//     Runtime Version:4.0.30319.42000";
        r = r + n + "//";
        r = r + n + "//     Changes to this file may cause incorrect behavior and will be lost if";
        r = r + n + "//     the code is regenerated.";
        r = r + n + "// </auto-generated>";
        r = r + n + "//------------------------------------------------------------------------------";
        r = r + n + "";
        r = r + n + "namespace " + AssemblyName() + ".Properties {";
        r = r + n + "    using System;";
        r = r + n + "";
        r = r + n + "";
        r = r + n + "    /// <summary>";
        r = r + n + "    ///   A strongly-typed resource class, for looking up localized strings, etc.";
        r = r + n + "    /// </summary>";
        r = r + n + "    // This class was auto-generated by the StronglyTypedResourceBuilder";
        r = r + n + "    // class via a tool like ResGen or Visual Studio.";
        r = r + n + "    // To add or remove a member, edit your .ResX file then rerun ResGen";
        r = r + n + "    // with the /str option, or rebuild your VS project.";
        r = r + n + "    [global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"System.Resources.Tools.StronglyTypedResourceBuilder\", \"15.0.0.0\")]";
        r = r + n + "    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]";
        r = r + n + "    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]";
        r = r + n + "    internal class Resources {";
        r = r + n + "";
        r = r + n + "        private static global::System.Resources.ResourceManager resourceMan;";
        r = r + n + "";
        r = r + n + "        private static global::System.Globalization.CultureInfo resourceCulture;";
        r = r + n + "";
        r = r + n + "        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute(\"Microsoft.Performance\", \"CA1811:AvoidUncalledPrivateCode\")]";
        r = r + n + "        internal Resources() {";
        r = r + n + "        }";
        r = r + n + "";
        r = r + n + "        /// <summary>";
        r = r + n + "        ///   Returns the cached ResourceManager instance used by this class.";
        r = r + n + "        /// </summary>";
        r = r + n + "        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]";
        r = r + n + "        internal static global::System.Resources.ResourceManager ResourceManager {";
        r = r + n + "            get {";
        r = r + n + "                if (object.ReferenceEquals(resourceMan, null)) {";
        r = r + n + "                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager(\"WinCDS.Properties.Resources\", typeof(Resources).Assembly);";
        r = r + n + "                    resourceMan = temp;";
        r = r + n + "                }";
        r = r + n + "                return resourceMan;";
        r = r + n + "            }";
        r = r + n + "        }";
        r = r + n + "";
        r = r + n + "        /// <summary>";
        r = r + n + "        ///   Overrides the current thread's CurrentUICulture property for all";
        r = r + n + "        ///   resource lookups using this strongly typed resource class.";
        r = r + n + "        /// </summary>";
        r = r + n + "        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]";
        r = r + n + "        internal static global::System.Globalization.CultureInfo Culture {";
        r = r + n + "            get {";
        r = r + n + "                return resourceCulture;";
        r = r + n + "            }";
        r = r + n + "            set {";
        r = r + n + "                resourceCulture = value;";
        r = r + n + "            }";
        r = r + n + "        }";
        r = r + n + "    }";
        r = r + n + "}";

        var resourcesDesignerCsFile = r;
        return resourcesDesignerCsFile;
    }
}