namespace Extras.Model;

public class PropIndexer<I, V>
{
    public delegate void setProperty(I idx, V value);
    public delegate V getProperty(I idx);

    public event getProperty getter;
    public event setProperty setter;

    public PropIndexer(getProperty g, setProperty s) { getter = g; setter = s; }
    public PropIndexer(getProperty g) { getter = g; setter = setPropertyNoop; }
    public PropIndexer() { getter = getPropertyNoop; setter = setPropertyNoop; }

    public void setPropertyNoop(I idx, V value) { }
    public V getPropertyNoop(I idx) { return default(V); }

    public V this[I idx]
    {
        get => getter.Invoke(idx);
        set => setter.Invoke(idx, value);
    }
}