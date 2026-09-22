using System;

namespace Vb6ToCSharp.UpgradeHelpers;

/// <summary>VB6 run-time error raised by control array operations.</summary>
public class ControlArrayException : Exception
{
    public const int ElementNotFoundNumber = 340;
    public const int AlreadyLoadedNumber = 360;
    public const int DesignTimeUnloadNumber = 362;

    public ControlArrayException(int number, string message) : base(message)
    {
        Number = number;
        HResult = unchecked((int)0x800A0000) | number;
    }

    /// <summary>VB6 <c>Err.Number</c>.</summary>
    public int Number { get; }

    internal static ControlArrayException ElementNotFound(int index) =>
        new(ElementNotFoundNumber, $"Control array element '{index}' doesn't exist");

    internal static ControlArrayException AlreadyLoaded() =>
        new(AlreadyLoadedNumber, "Object already loaded");

    internal static ControlArrayException DesignTimeUnload() =>
        new(DesignTimeUnloadNumber, "Can't unload controls created at design time");
}
