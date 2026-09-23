using System;

namespace Vb6ToCSharp.Runtime.Model;

/// <summary>The file attributes VB6's <c>Dir</c>/<c>SetAttr</c> take (the <c>vbNormal</c>… constants).</summary>
[Flags]
public enum FileAttribute
{
    Normal = 0,
    ReadOnly = 1,
    Hidden = 2,
    System = 4,
    Volume = 8,
    Directory = 16,
    Archive = 32
}
