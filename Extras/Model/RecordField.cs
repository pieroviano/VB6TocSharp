using System.Runtime.CompilerServices;

namespace Extras.Model;

[System.AttributeUsage(System.AttributeTargets.Field)]
public class RecordField : System.Attribute
{
    public string name = "";
    public string type = "";
    public int max = 0;
    public int order = 0;

    public RecordField(string name = "", string type = "", int max = 0, [CallerLineNumber] int order = 0)
    {
        this.name = name;
        this.type = type;
        this.max = max;
        this.order = order;
    }
}