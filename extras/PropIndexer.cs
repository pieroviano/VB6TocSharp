using System;

namespace WinCDS.Classes
{
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

    public class PropIndexer2<I, J, V>
    {
        public delegate void setProperty(I idx, J idx2, V value);
        public delegate V getProperty(I idx, J idx2);

        public event getProperty getter;
        public event setProperty setter;

        public PropIndexer2(getProperty g, setProperty s) { getter = g; setter = s; }
        public PropIndexer2(getProperty g) { getter = g; setter = setPropertyNoop; }
        public PropIndexer2() { getter = getPropertyNoop; setter = setPropertyNoop; }

        public void setPropertyNoop(I idx, J idx2, V value) { }
        public V getPropertyNoop(I idx, J idx2) { return default(V); }

        public V this[I idx, J idx2]
        {
            get => getter.Invoke(idx, idx2);
            set => setter.Invoke(idx, idx2, value);
        }
    }
}
