namespace Vb6ToCSharp.UpgradeHelpers.Tests.Fixtures;

/// <summary>A VB6 <c>Type ... End Type</c> as the converter emits it: fixed-length string, VB6 array.</summary>
internal struct Rec : IVbStruct
{
    public string Name;
    public int[] Values;

    public void Initialize()
    {
        Name = VbRuntime.FixedLen("", 3);
        Values = VbRuntime.NewArray<int>(2);
    }
}
