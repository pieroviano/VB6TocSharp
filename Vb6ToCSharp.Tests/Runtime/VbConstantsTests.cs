using Vb6ToCSharp.Runtime.Model;
using static Vb6ToCSharp.Runtime.VbConstants;

namespace Vb6ToCSharp.Tests.Runtime;

/// <summary>
/// The VB6 intrinsic constants the converter's own code writes into its output, with the values
/// Microsoft.VisualBasic.Constants gives them.
/// </summary>
public class VbConstantsTests
{
    [Fact]
    public void LineAndCharacterConstants()
    {
        Assert.Equal("\r\n", vbCrLf);
        Assert.Equal("\r\n", vbNewLine);
        Assert.Equal("\r", vbCr);
        Assert.Equal("\n", vbLf);
        Assert.Equal("\t", vbTab);
        Assert.Equal("", vbNullString);
        Assert.Equal("\0", vbNullChar);
    }

    [Fact]
    public void EnumConstantsKeepTheirVbValues()
    {
        Assert.Equal(FileAttribute.Directory, vbDirectory);
        Assert.Equal(16, (int)vbDirectory);
        Assert.Equal(MsgBoxStyle.OkCancel, vbOKCancel);
        Assert.Equal(1, (int)vbOKCancel);
        Assert.Equal(MsgBoxStyle.Exclamation, vbExclamation);
        Assert.Equal(48, (int)vbExclamation);
        Assert.Equal(MsgBoxResult.Cancel, vbCancel);
        Assert.Equal(2, (int)vbCancel);
        Assert.Equal(MsgBoxResult.Yes, vbYes);
        Assert.Equal(TriState.False, vbFalse);
        Assert.Equal(CompareMethod.Text, vbTextCompare);
        Assert.Equal(-2147221504, vbObjectError);
    }
}
