using Vb6ToCSharp.UpgradeHelpers;

namespace Vb6ToCSharp.UpgradeHelpers.Tests;

/// <summary>
/// The UI statements of <see cref="VbRuntime"/> are answered by the UI package's bridge: the highest priority
/// active one (WPF before WinForms), and Load / Unload by the first stack that recognizes the form.
/// </summary>
public class VbUiBridgeTests : IDisposable
{
    public VbUiBridgeTests() => VbRuntime.ResetUi();

    public void Dispose() => VbRuntime.ResetUi();

    /// <summary>One fake stack per form type: the runtime keeps one bridge per implementation type.</summary>
    private sealed class Bridge<TForm> : IVbUiBridge
    {
        public Bridge(bool active, int forms)
        {
            IsActive = active;
            Forms = forms;
        }

        private int Forms { get; }
        public bool IsActive { get; }
        public int Loaded { get; private set; }
        public int Unloaded { get; private set; }
        public int DoEvents() => Forms;
        public bool Load(object form) => form is TForm && ++Loaded > 0;
        public bool Unload(object form) => form is TForm && ++Unloaded > 0;
    }

    private sealed class WpfForm { }

    private sealed class WinFormsForm { }

    [Fact]
    public void DoEvents_WithoutUiPackage_IsZero() => Assert.Equal(0, VbRuntime.DoEvents());

    [Fact]
    public void DoEvents_AnswersTheActiveStack_HighestPriorityFirst()
    {
        VbRuntime.RegisterUi(new Bridge<WinFormsForm>(true, 1));
        VbRuntime.RegisterUi(new Bridge<WpfForm>(true, 2), 10);
        Assert.Equal(2, VbRuntime.DoEvents());
    }

    [Fact]
    public void DoEvents_SkipsAStackThatIsNotRunning()
    {
        VbRuntime.RegisterUi(new Bridge<WinFormsForm>(true, 1));
        VbRuntime.RegisterUi(new Bridge<WpfForm>(false, 2), 10); // no WPF Application
        Assert.Equal(1, VbRuntime.DoEvents());
    }

    [Fact]
    public void LoadAndUnload_GoToTheStackThatKnowsTheForm()
    {
        var winForms = new Bridge<WinFormsForm>(true, 0);
        var wpf = new Bridge<WpfForm>(false, 0);
        VbRuntime.RegisterUi(winForms);
        VbRuntime.RegisterUi(wpf, 10);

        VbRuntime.Load(new WinFormsForm());
        VbRuntime.Unload(new WpfForm()); // an inactive stack still owns its own windows

        Assert.Equal(1, winForms.Loaded);
        Assert.Equal(0, winForms.Unloaded);
        Assert.Equal(1, wpf.Unloaded);
    }

    [Fact]
    public void RegisterUi_IgnoresASecondBridgeOfTheSameKind()
    {
        VbRuntime.RegisterUi(new Bridge<WinFormsForm>(true, 3));
        VbRuntime.RegisterUi(new Bridge<WinFormsForm>(true, 4));
        Assert.Equal(3, VbRuntime.DoEvents());
    }
}
