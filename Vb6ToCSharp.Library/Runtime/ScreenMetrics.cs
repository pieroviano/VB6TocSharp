using System.Windows;

namespace Vb6ToCSharp.Runtime;

public class ScreenMetrics
{
    public FrameworkElement ActiveControl;
    public int Width => (int)SystemParameters.PrimaryScreenWidth;
    public int Height => (int)SystemParameters.PrimaryScreenHeight;
}