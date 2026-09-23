
namespace Extras;

public abstract class FixedWidthRecord : FieldInfoListSource
{
    public string RecordStart => "";
    public string RecordTerminator => "";

    public new string ToString()
    {
        var s = RecordStart;
        foreach (var f in FieldInfoList())
        {
            var r = thisFieldMod(f.Name);
            var w = r.max;
            s += (f.GetValue(this).ToString() + new string(' ', w)).Substring(0, w);
        }
        s += RecordTerminator;

        return s;
    }

    public void fromString(string l)
    {
        foreach (var f in FieldInfoList())
        {
            var r = thisFieldMod(f.Name);
            var w = r.max;
            var v = l.Substring(0, w);
            l = l.Substring(w);
            f.SetValue(this, l);
        }
    }
}