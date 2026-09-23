using System;

namespace Vb6ToCSharp.UpgradeHelpers.Interop;

/// <summary>
/// Converts a VB6 design-time value to a type only a UI package knows (WPF <c>Color</c>, <c>Brush</c>).
/// UI packages register theirs with <see cref="OcxHelper.AddConverter"/>.
/// </summary>
public interface IOcxValueConverter
{
    /// <summary>Converts <paramref name="value"/> to <paramref name="type"/>; false when the type is not its own.</summary>
    bool TryConvert(object value, Type type, out object result);
}
