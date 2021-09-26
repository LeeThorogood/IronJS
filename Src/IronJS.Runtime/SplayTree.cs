using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace IronJS.Runtime {
  public class SplayTree<TKey, TValue> : IDictionary<TKey, TValue> where TKey : IComparable<TKey> {
    private SplayTreeNode root;
    private Int32 count;
    private Int32 version = 0;

    public void Add(TKey key, TValue value) => Set(key, value, throwOnExisting: true);

    public void Add(KeyValuePair<TKey, TValue> item)
     => Set(item.Key, item.Value, throwOnExisting: true);

    private void Set(TKey key, TValue value, Boolean throwOnExisting) {
      if (count == 0) {
        version++;
        root = new SplayTreeNode(key, value);
        count = 1;
        return;
      }

      Splay(key);

      Int32 c = key.CompareTo(root.Key);
      if (c == 0) {
        if (throwOnExisting) {
          throw new ArgumentException("An item with the same key already exists in the tree.");
        }

        version++;
        root.Value = value;
        return;
      }

      SplayTreeNode n = new SplayTreeNode(key, value);
      if (c < 0) {
        n.LeftChild = root.LeftChild;
        n.RightChild = root;
        root.LeftChild = null;
      } else {
        n.RightChild = root.RightChild;
        n.LeftChild = root;
        root.RightChild = null;
      }

      root = n;
      count++;
      Splay(key);
      version++;
    }

    public void Clear() {
      root = null;
      count = 0;
      version++;
    }

    public Boolean ContainsKey(TKey key) {
      if (count == 0) return false;

      Splay(key);

      return key.CompareTo(root.Key) == 0;
    }

    public Boolean Contains(KeyValuePair<TKey, TValue> item) {
      if (count == 0) return false;

      Splay(item.Key);

      return item.Key.CompareTo(root.Key) == 0 &&
        (ReferenceEquals(root.Value, item.Value) ||
          (item.Value is object && item.Value.Equals(root.Value)));
    }

    private void Splay(TKey key) {
      SplayTreeNode l, r, t, y, header;
      l = r = header = new SplayTreeNode(default, default);
      t = root;
      while (true) {
        Int32 c = key.CompareTo(t.Key);
        if (c < 0) {
          if (t.LeftChild == null) break;
          if (key.CompareTo(t.LeftChild.Key) < 0) {
            y = t.LeftChild;
            t.LeftChild = y.RightChild;
            y.RightChild = t;
            t = y;
            if (t.LeftChild == null) break;
          }
          r.LeftChild = t;
          r = t;
          t = t.LeftChild;
        } else if (c > 0) {
          if (t.RightChild == null) break;
          if (key.CompareTo(t.RightChild.Key) > 0) {
            y = t.RightChild;
            t.RightChild = y.LeftChild;
            y.LeftChild = t;
            t = y;
            if (t.RightChild == null) break;
          }
          l.RightChild = t;
          l = t;
          t = t.RightChild;
        } else {
          break;
        }
      }
      l.RightChild = t.LeftChild;
      r.LeftChild = t.RightChild;
      t.LeftChild = header.RightChild;
      t.RightChild = header.LeftChild;
      root = t;
    }

    public Boolean Remove(TKey key) {
      if (count == 0) return false;

      Splay(key);

      if (key.CompareTo(root.Key) != 0) return false;

      if (root.LeftChild == null) {
        root = root.RightChild;
      } else {
        SplayTreeNode swap = root.RightChild;
        root = root.LeftChild;
        Splay(key);
        root.RightChild = swap;
      }

      version++;
      count--;
      return true;
    }

    public Boolean TryGetValue(TKey key, out TValue value) {
      if (count == 0) {
        value = default;
        return false;
      }

      Splay(key);
      if (key.CompareTo(root.Key) != 0) {
        value = default;
        return false;
      }

      value = root.Value;
      return true;
    }

    public TValue this[TKey key] {
      get {
        if (count == 0) throw new KeyNotFoundException("The key was not found in the tree.");

        Splay(key);
        if (key.CompareTo(root.Key) != 0)
          throw new KeyNotFoundException("The key was not found in the tree.");

        return root.Value;
      }
      set => Set(key, value, throwOnExisting: false);
    }

    public Int32 Count { get => count; }

    public Boolean IsReadOnly { get => false; }

    public Boolean Remove(KeyValuePair<TKey, TValue> item) {
      if (count == 0) return false;

      Splay(item.Key);

      if (item.Key.CompareTo(root.Key) == 0 &&
          (ReferenceEquals(root.Value, item.Value) ||
          (item.Value is object && item.Value.Equals(root.Value))))
        return false;

      if (root.LeftChild == null) {
        root = root.RightChild;
      } else {
        SplayTreeNode swap = root.RightChild;
        root = root.LeftChild;
        Splay(item.Key);
        root.RightChild = swap;
      }

      version++;
      count--;
      return true;
    }

    public void Trim(Int32 depth) {
      if (depth < 0)
        throw new ArgumentOutOfRangeException("depth", "The trim depth must not be negative.");

      if (count == 0) return;

      if (depth == 0) {
        Clear();
      } else {
        Int32 prevCount = count;
        count = Trim(root, depth - 1);
        if (prevCount != count) version++;
      }
    }

    private Int32 Trim(SplayTreeNode node, Int32 depth) {
      if (depth == 0) {
        node.LeftChild = null;
        node.RightChild = null;
        return 1;
      } else {
        Int32 count = 1;

        if (node.LeftChild != null) count += Trim(node.LeftChild, depth - 1);

        if (node.RightChild != null) count += Trim(node.RightChild, depth - 1);

        return count;
      }
    }

    public ICollection<TKey> Keys {
      get => new TiedList<TKey>(this, version, AsList(node => node.Key));
    }

    public ICollection<TValue> Values {
      get => new TiedList<TValue>(this, version, AsList(node => node.Value));
    }

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, Int32 arrayIndex) {
      AsList(node => new KeyValuePair<TKey, TValue>(node.Key, node.Value))
        .CopyTo(array, arrayIndex);
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
      => new TiedList<KeyValuePair<TKey, TValue>>(this, version,
        AsList(node => new KeyValuePair<TKey, TValue>(node.Key, node.Value))).GetEnumerator();

    private IList<TEnumerator> AsList<TEnumerator>(Func<SplayTreeNode, TEnumerator> selector) {
      if (root == null) return new TEnumerator[0];

      List<TEnumerator> result = new List<TEnumerator>(count);
      PopulateList(root, result, selector);
      return result;
    }

    private void PopulateList<TEnumerator>(SplayTreeNode node,
                                           List<TEnumerator> list,
                                           Func<SplayTreeNode, TEnumerator> selector) {

      if (node.LeftChild != null) PopulateList(node.LeftChild, list, selector);
      list.Add(selector(node));
      if (node.RightChild != null) PopulateList(node.RightChild, list, selector);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private sealed class SplayTreeNode {
      public readonly TKey Key;

      public TValue Value;
      public SplayTreeNode LeftChild;
      public SplayTreeNode RightChild;

      public SplayTreeNode(TKey key, TValue value) {
        Key = key;
        Value = value;
      }
    }

    [DebuggerDisplay("Count = {Count}")]
    private sealed class TiedList<T> : IList<T> {
      private readonly SplayTree<TKey, TValue> tree;
      private readonly Int32 version;
      private readonly IList<T> backingList;

      public TiedList(SplayTree<TKey, TValue> tree, Int32 version, IList<T> backingList) {
        this.tree = tree ?? throw new ArgumentNullException("tree");
        this.version = version;
        this.backingList = backingList ?? throw new ArgumentNullException("backingList");
      }

      public Int32 IndexOf(T item) {
        if (tree.version != version)
          throw new InvalidOperationException("The collection has been modified.");
        return backingList.IndexOf(item);
      }

      public void Insert(Int32 index, T item) {
        throw new NotSupportedException();
      }

      public void RemoveAt(Int32 index) {
        throw new NotSupportedException();
      }

      public T this[Int32 index] {
        get {
          if (tree.version != version)
            throw new InvalidOperationException("The collection has been modified.");
          return backingList[index];
        }
        set {
          throw new NotSupportedException();
        }
      }

      public void Add(T item) {
        throw new NotSupportedException();
      }

      public void Clear() {
        throw new NotSupportedException();
      }

      public Boolean Contains(T item) {
        if (tree.version != version)
          throw new InvalidOperationException("The collection has been modified.");
        return backingList.Contains(item);
      }

      public void CopyTo(T[] array, Int32 arrayIndex) {
        if (tree.version != version)
          throw new InvalidOperationException("The collection has been modified.");
        backingList.CopyTo(array, arrayIndex);
      }

      public Int32 Count { get => tree.count; }

      public Boolean IsReadOnly { get => true; }

      public Boolean Remove(T item) {
        throw new NotSupportedException();
      }

      public IEnumerator<T> GetEnumerator() {
        if (tree.version != version)
          throw new InvalidOperationException("The collection has been modified.");

        foreach (var item in backingList) {
          yield return item;
          if (tree.version != version)
            throw new InvalidOperationException("The collection has been modified.");
        }
      }

      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
  }
}
