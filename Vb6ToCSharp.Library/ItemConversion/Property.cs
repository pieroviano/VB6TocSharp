namespace Vb6ToCSharp.ItemConversion;

public class Property
{
    public string name = "";
    public bool asPublic = false;
    public string asType = "";
    public bool asFunc = false;
    public string getter = "";
    public string setter = "";
    public string origArgName = "";
    public string funcArgs = "";
    public string origProto = "";
    public string getArgs = ""; // VB6 parameters of Property Get
    public string letArgs = ""; // VB6 parameters of Property Let / Set (the last one is the value)
}