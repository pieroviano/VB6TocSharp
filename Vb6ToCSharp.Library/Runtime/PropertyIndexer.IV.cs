namespace Vb6ToCSharp.Runtime;

public class PropertyIndexer<I, V>
{
    public delegate V getProperty(I idx);

    public delegate void setProperty(I idx, V value);

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

    public V this[I idx]
    {
        get => getter.Invoke(idx);
        set => setter.Invoke(idx, value);
    }

    public V getPropertyNoop(I idx)
    {
        return default;
    }

    public event getProperty getter;

    public void setPropertyNoop(I idx, V value)
    {
    }

    public event setProperty setter;
}