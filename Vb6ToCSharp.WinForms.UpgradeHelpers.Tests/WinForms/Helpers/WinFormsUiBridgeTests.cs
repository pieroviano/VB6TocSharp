using System.Windows.Forms;
using Vb6ToCSharp.UpgradeHelpers.Tests.Fixtures;
using Vb6ToCSharp.UpgradeHelpers.WinForms.Helpers;

namespace Vb6ToCSharp.UpgradeHelpers.Tests.WinForms.Helpers;

/// <summary>VB6 Load / Unload / DoEvents on the WinForms stack.</summary>
public class WinFormsUiBridgeTests
{
    /// <summary>
    /// VB6 <c>Load form</c> takes the WinForms stack and does not show the form. (WinForms creates the handle
    /// only when the control is visible, so the form stays uncreated until it is shown.)
    /// </summary>
    [Fact]
    public void Load_TakesTheFormWithoutShowingIt()
    {
        Sta.Run(() =>
        {
            using var form = new Form();
            Assert.True(new WinFormsUiBridge().Load(form));
            Assert.False(form.Visible);
        });
    }

    [Fact]
    public void Unload_ClosesTheForm()
    {
        Sta.Run(() =>
        {
            using var form = new Form();
            form.Show();
            Assert.True(new WinFormsUiBridge().Unload(form));
            Assert.False(form.Visible);
        });
    }

    [Fact]
    public void ObjectsOfAnotherStack_AreNotHandled()
    {
        var bridge = new WinFormsUiBridge();
        Assert.False(bridge.Load(new object()));
        Assert.False(bridge.Unload(new object()));
    }

    [Fact]
    public void DoEvents_AnswersTheOpenForms()
    {
        Sta.Run(() =>
        {
            using var form = new Form();
            Assert.Equal(0, new WinFormsUiBridge().DoEvents());
            form.Show();
            Assert.Equal(1, new WinFormsUiBridge().DoEvents());
            form.Close();
        });
    }

    /// <summary>WinForms answers whenever no other stack is running.</summary>
    [Fact]
    public void IsActive_IsAlwaysTrue() => Assert.True(new WinFormsUiBridge().IsActive);

    /// <summary>
    /// The language runtime reaches this stack with no registration call of its own: loading the package runs its
    /// module initializer, and the runtime loads the package when a VB6 UI statement is the first thing to need it.
    /// </summary>
    [Fact]
    public void ThePackage_RegistersItsStack()
    {
        Sta.Run(() =>
        {
            using var form = new Form();
            form.Show();
            Assert.Equal(1, VbRuntime.DoEvents());
            Assert.Equal(1, VbRuntime.RegisteredUiCount);
            VbRuntime.Unload(form);
            Assert.False(form.Visible);
        });
    }
}
