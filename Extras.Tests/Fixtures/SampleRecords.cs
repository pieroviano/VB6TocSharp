using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Extras.Model;

namespace Extras.Tests.Fixtures;

/// <summary>Plain <see cref="FieldInfoListSource"/> subclass exposing the protected surface.</summary>
public class SampleFieldSource : FieldInfoListSource
{
    [RecordField(max: 4)] public string Alpha = "";
    [RecordField(max: 3)] public string Beta = "";
    public string NotAField = "";

    public List<string> Names() => FieldInfoList().Select(f => f.Name).ToList();
    public int DeclaredCount() => FieldInfoListCount();
    public FieldInfo FieldAt(int i) => thisField(i);
    public FieldInfo FieldNamed(string n) => thisField(n);
    public RecordField ModAt(int i) => thisFieldMod(i);
    public RecordField ModNamed(string n) => thisFieldMod(n);
}

/// <summary>A source with no annotated fields at all.</summary>
public class EmptyFieldSource : FieldInfoListSource
{
    public string Ignored = "";

    public int DeclaredCount() => FieldInfoListCount();
    public FieldInfo FieldAt(int i) => thisField(i);
}

/// <summary>Fixed width record: Name(4) City(3) Code(2).</summary>
public class SampleFixedRecord : FixedWidthRecord
{
    [RecordField(max: 4)] public string Name = "";
    [RecordField(max: 3)] public string City = "";
    [RecordField(max: 2)] public string Code = "";
}

/// <summary>Fixed width record that wraps itself in a start marker and a terminator.</summary>
public class WrappedFixedRecord : FixedWidthRecord
{
    [RecordField(max: 3)] public string Abc = "";

    public override string RecordStart => "<";
    public override string RecordTerminator => ">";
}

/// <summary>Csv record: Name City Code.</summary>
public class SampleCsvRecord : CsvRecord
{
    [RecordField(max: 10)] public string Name = "";
    [RecordField(max: 10)] public string City = "";
    [RecordField(max: 10)] public string Code = "";

    public SampleCsvRecord() { }
    public SampleCsvRecord(string line) : base(line) { }

    public List<string> ExtraValues => extraFields;
    public int DeclaredCount() => FieldInfoListCount();
}
