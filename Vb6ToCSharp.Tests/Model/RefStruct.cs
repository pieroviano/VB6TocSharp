using Vb6ToCSharp.UpgradeHelpers;

namespace Vb6ToCSharp.Tests.Model;

internal sealed class RefStruct : IVbStruct
{
    public bool Initialized;
    public void Initialize() => Initialized = true;
}