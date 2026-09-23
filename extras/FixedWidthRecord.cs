
using System;

namespace Extras;

public abstract class FixedWidthRecord : FieldInfoListSource
{
    public virtual string RecordStart => "";
    public virtual string RecordTerminator => "";

    public override string ToString()
    {
        var s = RecordStart;
        foreach (var f in FieldInfoList())
        {
            var r = thisFieldMod(f.Name);
            var w = r.max;
            // An unset field is as good as an empty one: pad it, do not throw.
            s += ("" + f.GetValue(this) + new string(' ', w)).Substring(0, w);
        }
        s += RecordTerminator;

        return s;
    }

    public void fromString(string l)
    {
        l = l ?? "";
        if (RecordStart.Length > 0 && l.StartsWith(RecordStart, StringComparison.Ordinal)) l = l.Substring(RecordStart.Length);
        if (RecordTerminator.Length > 0 && l.EndsWith(RecordTerminator, StringComparison.Ordinal)) l = l.Substring(0, l.Length - RecordTerminator.Length);

        foreach (var f in FieldInfoList())
        {
            var r = thisFieldMod(f.Name);
            var w = r.max;
            // A line shorter than the record leaves the remaining fields blank rather than throwing,
            // and each field is assigned its own slice - not what is left of the line.
            var v = (l + new string(' ', w)).Substring(0, w);
            l = l.Length > w ? l.Substring(w) : "";
            f.SetValue(this, v);
        }
    }
}