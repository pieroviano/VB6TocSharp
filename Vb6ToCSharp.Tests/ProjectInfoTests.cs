using Vb6ToCSharp.Parsing;
using Vb6ToCSharp.Tests.Infrastructure;

namespace Vb6ToCSharp.Tests;

public class ProjectInfoTests
{
    [Fact]
    public void Parse_ReadsProjectFacts()
    {
        var v = ProjectInfo.Parse(
            "Type=Exe\r\nForm=frmMain.frm\r\nForm=frmB.frm\r\nUserControl=ucX.ctl\r\nModule=modA; modA.bas\r\n" +
            "Object={831FDD16-0C5C-11D2-A9FC-0000F8754DA1}#2.0#0; MSCOMCTL.OCX\r\n" +
            "Object={831FDD16-0C5C-11D2-A9FC-0000F8754DA1}#2.0#0; MSCOMCTL.OCX\r\n" +
            "Startup=\"frmMain\"\r\nName=\"Proj1\"\r\n");
        Assert.Equal("Proj1", v.Name);
        Assert.Equal("frmMain", v.Startup);
        Assert.False(v.StartsWithSubMain);
        Assert.Equal(new[] { "frmMain.frm", "frmB.frm" }, v.Forms);
        Assert.Equal(new[] { "ucX.ctl" }, v.UserControls);
        Assert.Single(v.Objects);
    }

    [Fact]
    public void DesignerFiles_FindMdiAndUserControls()
    {
        var dir = TestUtil.TempDir();
        File.WriteAllText(Path.Combine(dir, "p.vbp"), "Form=frmMdi.frm\r\nUserControl=ucX.ctl\r\nStartup=\"Sub Main\"\r\n");
        File.WriteAllText(Path.Combine(dir, "frmMdi.frm"), "VERSION 5.00\r\nBegin VB.MDIForm frmMdi \r\nEnd\r\nAttribute VB_Name = \"frmMdi\"\r\n");
        File.WriteAllText(Path.Combine(dir, "ucX.ctl"), "VERSION 5.00\r\nBegin VB.UserControl ucX \r\nEnd\r\nAttribute VB_Name = \"ucX\"\r\n");
        var v = ProjectInfo.Load(Path.Combine(dir, "p.vbp"));
        Assert.True(v.StartsWithSubMain);
        Assert.Equal("frmMdi", v.MdiFormName);
        Assert.Contains("ucX", v.UserControlNames);
    }
}