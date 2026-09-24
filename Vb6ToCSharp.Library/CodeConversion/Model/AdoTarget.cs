namespace Vb6ToCSharp.CodeConversion.Model;

/// <summary>What a converted project references for ADO (Microsoft ActiveX Data Objects).</summary>
public enum AdoTarget
{
    /// <summary>The managed replacement, the Standard.AdoDb package: no COM interop, so it also builds off Windows.</summary>
    Package,

    /// <summary>The COM type library itself, as a &lt;COMReference&gt; (Windows and Visual Studio's MSBuild only).</summary>
    Com
}
