namespace Vb6ToCSharp.UpgradeHelpers.Interop;

/// <summary>
/// A constant a type library publishes globally, which VB6 treats as a Long: it flows into an enum parameter and
/// into an Integer one alike. Converted projects declare such constants as a value type implementing this
/// interface, so late-bound calls (<see cref="VbRuntime.ComInvoke"/>) can pass the plain number to COM.
/// </summary>
public interface IVbLibraryConstant
{
    /// <summary>The constant's numeric value, as VB6 sees it.</summary>
    int Value { get; }
}
