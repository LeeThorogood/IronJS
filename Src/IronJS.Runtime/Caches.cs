using System;
using System.Collections.Generic;
using System.Linq;

namespace IronJS.Runtime.Caches {
  public class LimitCache<K, V> where K : IEquatable<K> where V : class {
    readonly Int32 size;
    List<Tuple<K, V>> storage = new List<Tuple<K, V>>();

    public LimitCache(Int32 halfSize) => size = halfSize * 2;

    public V Lookup(K key, Func<V> value) {
      Tuple<K, V> result = storage.FirstOrDefault(x => x.Item1.Equals(key));

      if (result == null) {
        result = Tuple.Create(key, value());
        storage.Add(result);

        if (storage.Count > size) storage = storage.Take(size).ToList();
      }

      return result.Item2;
    }
  }

  public class SplayCache<K, V> where K : IComparable<K> {
    readonly SplayTree<K, V> storage = new SplayTree<K, V>();

    public V Lookup(K key, Func<V> value) {

      if (!storage.TryGetValue(key, out V cached)) {
        storage[key] = cached = value();

        if (storage.Count > 1024) storage.Trim(10);
      }

      return cached;
    }
  }

  public class WeakCache<K, V> {
    readonly Dictionary<K, WeakReference> storage = new Dictionary<K, WeakReference>();

    public V Lookup(K key, Func<V> value) {

      if (storage.TryGetValue(key, out WeakReference cached) && cached.Target is V v) return v;

      V newValue = value();
      storage[key] = new WeakReference(newValue);
      return newValue;
    }
  }
}
