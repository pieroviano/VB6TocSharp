using System.Runtime.ExceptionServices;

namespace Vb6ToCSharp.UpgradeHelpers.Tests.Infrastructure;

/// <summary>Runs test code on a dedicated STA thread (WinForms/WPF objects) and rethrows its exception.</summary>
internal static class Sta
{
    public static void Run(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                error = e;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}
