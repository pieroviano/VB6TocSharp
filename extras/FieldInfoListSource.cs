using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Extras.Model;

namespace Extras;

public abstract class FieldInfoListSource
{
    private static Dictionary<string, List<FieldInfo>> fieldInfoList = new Dictionary<string, List<FieldInfo>>();

    protected List<FieldInfo> FieldInfoList()
    {
        var n = GetType().Name;
        if (!fieldInfoList.ContainsKey(n))
        {
            var l =
                fieldInfoList[n] = GetType().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                    .ToList()
                    .FindAll(f => f.GetCustomAttribute<RecordField>() != null);
            fieldInfoList[n].Sort((a, b) => (a.GetCustomAttribute<RecordField>() == null ? 0 : a.GetCustomAttribute<RecordField>().order) - (b.GetCustomAttribute<RecordField>() == null ? 0 : b.GetCustomAttribute<RecordField>().order));
        }
        return fieldInfoList[n];
    }
    protected int FieldInfoListCount() { return FieldInfoList().Count; }
    protected FieldInfo thisField(int i)
    {
        if (i >= 0 || i < FieldInfoListCount()) return FieldInfoList()[i];
        return null;
    }
    protected FieldInfo thisField(string i)
    {
        foreach (var f in FieldInfoList())
            if (f.Name.Equals(i, StringComparison.OrdinalIgnoreCase)) return f;
        return null;
    }

    protected RecordField thisFieldMod(int i) { return thisField(i)?.GetCustomAttribute<RecordField>(); }
    protected RecordField thisFieldMod(string i) { return thisField(i)?.GetCustomAttribute<RecordField>(); }

    public string this[string i]
    {
        get => "" + thisField(i).GetValue(this);
        set => thisField(i).SetValue(this, value);
    }

    public string this[int i]
    {
        get => "" + thisField(i).GetValue(this);
        set => thisField(i).SetValue(this, value);
    }
}