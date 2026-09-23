using Vb6ToCSharp.UpgradeHelpers;

namespace Vb6ToCSharp.Tests.Fixtures;

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