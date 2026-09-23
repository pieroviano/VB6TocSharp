using System;
using System.Data;

namespace Extras.Model;

public class RecordsetField
{
    public const int adSmallInt = 2; //	Integer	SmallInt
    public const int adInteger = 3; //	AutoNumber
    public const int adSingle = 4; //	Single	Real
    public const int adDouble = 5; //	Double	Float	Float
    public const int adCurrency = 6; //	Currency	Money
    public const int adDate = 7; //	Date	DateTime
    public const int adIDispatch = 9; //
    public const int adBoolean = 11; //	YesNo	Bit
    public const int adVariant = 12; //	 	Sql_Variant (SQL Server 2000 +)	VarChar2
    public const int adDecimal = 14; //	 	 	Decimal *
    public const int adUnsignedTinyInt = 17; //	Byte	TinyInt
    public const int adBigInt = 20; //	 	BigInt (SQL Server 2000 +)
    public const int adGUID = 72; //	ReplicationID (Access 97 (OLEDB)), (Access 2000 (OLEDB))	UniqueIdentifier (SQL Server 7.0 +)
    public const int adWChar = 130; //	 	NChar (SQL Server 7.0 +)
    public const int adChar = 129; //	 	Char	Char
    public const int adNumeric = 131; //	Decimal (Access 2000 (OLEDB))	Decimal
    public const int adBinary = 128; //	 	Binary
    public const int adDBTimeStamp = 135; //	DateTime (Access 97 (ODBC))	DateTime
    public const int adVarChar = 200; //	Text (Access 97)	VarChar	VarChar
    public const int adLongVarChar = 201; //	Memo (Access 97)
    public const int adVarWChar = 202; //	Text (Access 2000 (OLEDB))	NVarChar (SQL Server 7.0 +)	NVarChar2
    public const int adLongVarWChar = 203; //	Memo (Access 2000 (OLEDB))
    public const int adVarBinary = 204; //	ReplicationID (Access 97)	VarBinary
    public const int adLongVarBinary = 205; //	OLEObject	Image	Long Raw *

    private DataRow Row = null;
    public dynamic Name = "";
    public int Size = 0;


    public RecordsetField(DataRow Row, dynamic Name)
    {
        this.Row = Row;
        this.Name = Name;
    }

    public dynamic Value
    {
        get => Row[Name];
        set => Row[Name] = value;
    }

    public Type Type => Row.Table.Columns[Name].DataType;

    // private string TypeName
    // {
    //     get
    //     {
    //         switch (Type)
    //         {
    //             case adBinary: return "adBinary(" + Size + ")";
    //             case adBoolean: return "adBoolean";
    //             case adChar: return "adChar(" + Size + ")";
    //             case adCurrency: return "adCurrency";
    //             case adDBTimeStamp: return "adDBTimeStamp";
    //             case adDouble: return "adDouble";
    //             case adInteger: return "adInteger";
    //             case adLongVarBinary: return "adLongVarBinary";
    //             case adLongVarChar: return "adLongVarChar";
    //             case adLongVarWChar: return "adLongVarWChar";
    //             case adNumeric: return "adNumeric";
    //             case adSingle: return "adSingle";
    //             case adSmallInt: return "adSmallInt";
    //             //case adTinyInt: return  "adTinyInt";
    //             case adUnsignedTinyInt: return "adUnsignedTinyInt";
    //             case adVarBinary: return "adVarBinary (" + Size + ")";
    //             case adVarChar: return "adVarChar (" + Size + ")";
    //             case adVarWChar: return "adVarWChar(" + Size + ")";
    //             case adWChar: return "adWChar(" + Size + ")";
    //             default: return "UnKnown Field Type: " + Name + ", " + Type;
    //         }
    //     }
    // }
}