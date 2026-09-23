# Csv Record Class

A convenience handler for processing Csv Records.

See Also:  

 - [FieldInfoListSource.cs](FieldInfoListSource.cs)
 - [FixedWidthRecord.md](FixedWidthRecord.md)

## Example Usage

```c#
    public class PayComm : CsvRecord
    {
        [RecordField]
        public string Salesman = "";
        [RecordField]
        public string Lease = "";
        [RecordField]
        public string Name = "";
        [RecordField]
        public string Style = "";
        [RecordField]
        public string Landed = "";
        [RecordField]
        public string Sell = "";
        [RecordField]
        public string GM = "";
        [RecordField]
        public string SaleGM = "";
        [RecordField]
        public string Rate = "";
        [RecordField]
        public string Sales = "";
        [RecordField]
        public string Extra = "";
        [RecordField]
        public string Split = "";
        [RecordField]
        public string MargRec = "";

        public PayComm() { }
        public PayComm(
            string salesman = "", string lease = "", string name = "", string style = "", string landed = "",
            string sell = "", string gM = "", string saleGM = "", string rate = "", string sales = "",
            string extra = "", string split = "", string margRec = ""
            )
        {
            Salesman = salesman;
            Lease = lease;
            Name = name;
            Style = style;
            Landed = landed;
            Sell = sell;
            GM = gM;
            SaleGM = saleGM;
            Rate = rate;
            Sales = sales;
            Extra = extra;
            Split = split;
            MargRec = margRec;
        }
    }
```

## Field Config

The RecordField contains the following properties;

- public string name = "";
- public string type = "";
- public int max = 0;
- public int order = 0;

```c#
[RecordField(max = 4)]
public string RDP = "";
```

## Operations

### With field definitions

```c#
PayComm record = new PayComm();
record.Salesman = "field1";
record["Lease"] = "field2 \"with quotes\"";
record.Style = "Field4";
string CsvLine = record.ToString();       // ==> field1,"field2 ""with quotes""",,Field4, ...

PayComm recordCopy = new PayComm();
recordCopy.FromLine(CsvLine);

// File IO is up to you.
List<PayComm> fileContents = CsvRecord.FromCsvFile<PayComm>(csvFileContents);
string newCsvFileContents = CsvRecord.ToCsvFile(fileContents, addHeader: true);
```

- `ToString()` and `ToLine()` render the record; `FromLine()` reads one back.
- `HeaderLine()` lists the field names - the `name` the attribute declares, else the member name.
  `FromCsvFile` skips a leading header line and any line starting with `#`.
- Fields beyond the declared ones are kept as they were read, and written back unchanged.
- A newline inside a quoted field is part of the field, on the way out and on the way back in.

### Without field definitions

```c#
CsvRecord record = new CsvRecord();
record[0] = "field1";
record[1] = "field2 \"with quotes\"";
record[3] = "Field4";
string CsvLine = record.ToString();       // ==> field1,"field2 ""with quotes""",,Field4
```
