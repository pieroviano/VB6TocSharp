using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Extras.Model;

namespace Extras;

public abstract class FieldInfoListSource
{
    // Keyed by the type itself: record types in different namespaces may share a simple name.
    private static readonly ConcurrentDictionary<Type, List<FieldInfo>> fieldInfoList = new ConcurrentDictionary<Type, List<FieldInfo>>();

    protected List<FieldInfo> FieldInfoList()
    {
        return fieldInfoList.GetOrAdd(GetType(), t =>
        {
            var l = t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                .ToList()
                .FindAll(f => f.GetCustomAttribute<RecordField>() != null);
            l.Sort((a, b) => a.GetCustomAttribute<RecordField>().order - b.GetCustomAttribute<RecordField>().order);
            return l;
        });
    }
    protected int FieldInfoListCount() { return FieldInfoList().Count; }
    protected FieldInfo thisField(int i)
    {
        if (i >= 0 && i < FieldInfoListCount()) return FieldInfoList()[i];
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

    // Indexing a field the record does not declare is a programming error: name it, instead of
    // throwing a NullReferenceException from inside the accessor.
    private FieldInfo requiredField(string i)
    {
        return thisField(i) ?? throw new ArgumentException("No record field named '" + i + "' on " + GetType().Name + ".", nameof(i));
    }

    private FieldInfo requiredField(int i)
    {
        return thisField(i) ?? throw new ArgumentOutOfRangeException(nameof(i), i, GetType().Name + " declares " + FieldInfoListCount() + " record fields.");
    }

    public string this[string i]
    {
        get => "" + requiredField(i).GetValue(this);
        set => requiredField(i).SetValue(this, value);
    }

    public string this[int i]
    {
        get => "" + requiredField(i).GetValue(this);
        set => requiredField(i).SetValue(this, value);
    }
}