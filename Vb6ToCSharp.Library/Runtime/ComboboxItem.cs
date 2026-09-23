namespace Vb6ToCSharp.Runtime;

public class ComboboxItem
{
    public ComboboxItem(string vText)
    {
        Text = vText;
    }

    public ComboboxItem(string vText, int vValue)
    {
        Text = vText;
        Value = vValue;
    }

    public string Text { get; set; }
    public int Value { get; set; }

    public override string ToString()
    {
        return Text;
    }
}