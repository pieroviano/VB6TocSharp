using System.Windows.Controls;

namespace Vb6ToCSharp.UI;

public class TreeViewItemObject : TreeViewItem
{
    private string Key;
    private readonly string Text;

    public TreeViewItemObject(string Text = "", string key = "")
    {
        this.Text = Text;
        Header = Text;
        Key = key;
    }

    public new string ToString()
    {
        return Text;
    }

    public TreeViewItem getContainer(TreeView tv)
    {
        return tv.ItemContainerGenerator.ContainerFromItem(this) as TreeViewItem;
    }

    public string getKey()
    {
        return Key;
    }

    public string getValue()
    {
        return Key;
    }

    public void setKey(string s)
    {
        Key = s;
    }

    public void setValue(string s)
    {
        Key = s;
    }
}