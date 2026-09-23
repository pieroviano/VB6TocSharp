namespace Vb6ToCSharp.UI;

public class PropertyIndexer<I, J, V>
{
    public delegate V getProperty(I idx, J idx2);

    public delegate void setProperty(I idx, J idx2, V value);

    public PropertyIndexer(getProperty g, setProperty s)
    {
        getter = g;
        setter = s;
    }

    public PropertyIndexer(getProperty g)
    {
        getter = g;
        setter = setPropertyNoop;
    }

    public PropertyIndexer()
    {
        getter = getPropertyNoop;
        setter = setPropertyNoop;
    }

    public V this[I idx, J idx2]
    {
        get => getter.Invoke(idx, idx2);
        set => setter.Invoke(idx, idx2, value);
    }

    public V getPropertyNoop(I idx, J idx2)
    {
        return default;
    }

    public event getProperty getter;

    public void setPropertyNoop(I idx, J idx2, V value)
    {
    }

    public event setProperty setter;
}