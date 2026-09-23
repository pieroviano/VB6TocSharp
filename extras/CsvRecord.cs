using System;
using System.Collections.Generic;
using System.Linq;
using static Extras.CsvHandler;

namespace Extras;

// Concrete on purpose: a plain CsvRecord declares no fields and is addressed by position
// (see CsvRecord.md), while a subclass declares its fields with [RecordField].
public class CsvRecord : FieldInfoListSource
{
    protected List<string> extraFields = new List<string>();

    public CsvRecord() { }
    public CsvRecord(string line) { FromLine(line); }

    public string HeaderLine(bool Commented = false, bool addNL = false)
    {
        var S = "";
        if (Commented) S += "# ";
        foreach (var f in FieldInfoList()) S += ProtectCsv(recordName(f)) + ",";
        if (S.Length > 0 && S[S.Length - 1] == ',') S = S.Substring(0, S.Length - 1);
        if (addNL) S += "\n";
        return S;
    }

    protected string getFieldByIndex(int i)
    {
        if (i < 0) throw new ArgumentOutOfRangeException(nameof(i), i, "A field index cannot be negative.");
        var f = thisField(i);
        if (f != null) return "" + f.GetValue(this);
        var extraIdx = i - FieldInfoListCount();
        if (extraIdx < extraFields.Count) return extraFields[extraIdx];
        return "";
    }

    protected void setFieldByIndex(int i, string value)
    {
        if (i < 0) throw new ArgumentOutOfRangeException(nameof(i), i, "A field index cannot be negative.");
        var f = thisField(i);
        if (f != null)
            f.SetValue(this, value);
        else
        {
            var extraIdx = i - FieldInfoListCount();
            while (extraIdx >= extraFields.Count) extraFields.Add("");
            extraFields[extraIdx] = value;
        }
    }

    new public string this[int i]
    {
        get => getFieldByIndex(i);
        set => setFieldByIndex(i, value);
    }


    // The documented way to render a record (CsvRecord.md).
    public override string ToString() { return ToLine(); }

    public string ToLine()
    { return CsvLine(FieldInfoList().Select(f => "" + f.GetValue(this)).Concat(extraFields).ToArray()); }

    public void FromLine(string line)
    {
        var i = 0;
        foreach (var f in FieldInfoList()) f.SetValue(this, CsvField(line, i++));
        extraFields = new List<string>();
        // Undeclared trailing fields keep their values, so ToLine can write them back unchanged.
        for (i = FieldInfoListCount(); i < CsvFieldCount(line); i++) extraFields.Add(CsvField(line, i));
    }

    public static List<T> FromCsvFile<T>(string csvContents) where T : CsvRecord, new()
    {
        var res = new List<T>();
        var header = new T().HeaderLine();
        var firstDataLine = true;
        foreach (var l in CsvRecords(csvContents))
        {
            if (l == "") continue;
            if (l[0] == '#') continue;
            // A leading line that is exactly this record's header - as ToCsvFile writes it - is not data.
            if (firstDataLine)
            {
                firstDataLine = false;
                if (l == header) continue;
            }
            var item = new T();
            item.FromLine(l);
            res.Add(item);
        }
        return res;
    }

    public static string ToCsvFile<T>(List<T> lines, bool addHeader = false) where T : CsvRecord, new()
    {
        var res = "";
        if (lines.Count == 0) return res;
        if (addHeader) res += lines[0].HeaderLine() + "\r\n";

        foreach (var l in lines) res += l.ToLine() + "\r\n";
        return res;
    }
}