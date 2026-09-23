namespace Vb6ToCSharp.ItemConversion;

public class Variable
{
    public string name = "";
    public string asType = "";
    public string asArray = "";
    public bool param = false;
    public bool retVal = false;
    public bool assigned = false;
    public bool used = false;
    public bool assignedBeforeUsed = false;
    public bool usedBeforeAssigned = false;
    public bool vb6Array = false; // a VB6Array<T> (non-zero lower bound), not a T[]
    public string fixedLen = ""; // String * n: the length expression
}