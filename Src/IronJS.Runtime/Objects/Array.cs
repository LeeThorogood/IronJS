using System;
using System.Collections.Generic;
using System.Linq;

namespace IronJS.Runtime {
  public class ArrayObject : CommonObject {
    public const UInt32 DenseMaxIndex = 2147483646u;
    public const UInt32 DenseMaxSize = 2147483647u;

    private Descriptor[] dense;
    private SparseArray sparse;
    private Boolean isDense;
    private UInt32 length;

    public ArrayObject(Environment env, UInt32 length)
      : base(env, env.Maps.Array, env.Prototypes.Array) {

      this.length = length;

      if (length > 0x20000) {
        sparse = new SparseArray();
        isDense = false;
      } else {
        dense = new Descriptor[length];
        isDense = true;
      }
      Put("length", length, DescriptorAttrs.DontEnum);
    }

    private void ResizeDense(UInt32 newCapacity) {
      UInt32 capacity = (UInt32)dense.Length;
      newCapacity = newCapacity == 0 ? 2 : newCapacity;

      UInt32 copyLength = newCapacity < capacity ? newCapacity : capacity;

      Descriptor[] newDense = new Descriptor[newCapacity];
      Array.Copy(dense, newDense, copyLength);
      dense = newDense;
    }

    public Descriptor[] Dense { get => dense; set => dense = value; }

    public SparseArray Sparse { get => sparse; }

    public override UInt32 Length {
      get => length;
      set {
        length = value;
        Put("length", value, DescriptorAttrs.DontEnum);
      }
    }

    public override String ClassName { get => "Array"; }

    public Boolean IsDense { get => isDense; }

    public override void GetAllIndexProperties(IDictionary<UInt32, BoxedValue> dict, UInt32 l) {
      if (isDense) {
        Int32 length = (Int32)this.length;

        for (Int32 i = 0; i < length; i++)
          if ((i < l) && dense[i].HasValue && !dict.ContainsKey((UInt32)i))
            dict.Add((UInt32)i, dense[i].Value);

      } else {
        //FIXME:  Was this function supposed to use the `l` parameter or the `this.length` to limit the entries?
        sparse.GetAllIndexProperties(dict, length);
      }
    }

    internal Boolean HasIndex(UInt32 index) {
      if (index >= length) return false;

      if (!isDense) return sparse.storage.ContainsKey(index);

      return index < dense.Length && dense[index].HasValue;
    }

    private void PutLength(UInt32 newLength) {
      if (isDense)  //INFO: Shouldn't we just clip or grow the dense array to the proper value when the user asks us to?
      {
        while (newLength < length)  //FIXME: What if we are larger than the dense array, shouldn't we grow the array?
        {
          length--;
          if (length < dense.Length) {
            dense[length].Value = new BoxedValue();
            dense[length].HasValue = false;
          }
        }
      } else {
        sparse.PutLength(newLength, length);
      }

      length = newLength;
      base.Put("length", newLength);
    }

    internal void PutLength(Double newLength) {
      UInt32 length = (UInt32)newLength;
      if (newLength < 0.0 || (length != newLength)) {
        Env.RaiseRangeError<Object>("Invalid array length");
        return;
      }
      PutLength(length);
    }

    public override void Put(UInt32 index, BoxedValue value) {
      if (index == UInt32.MaxValue) {
        base.Put(index.ToString(), value);
        return;
      }

      if (isDense) {
        UInt32 denseLength = (UInt32)dense.Length;

        if (index < denseLength) {
          dense[index].Value = value;
          dense[index].HasValue = true;
          if (index >= length) Length = index + 1;
          return;
        } else if (index < (denseLength + 10)) {
          // We're above the currently allocated dense size
          // but not far enough above to switch to sparse
          // so we expand the dense array
          ResizeDense(denseLength * 2 + 10);
          dense[index].Value = value;
          dense[index].HasValue = true;
          Length = index + 1;
          return;
        } else {
          // Switch to sparse array
          sparse = SparseArray.OfDense(dense);
          dense = null;
          isDense = false;

          // Fall through to the sparse handling below.
        }
      }

      sparse.Put(index, value);
      if (index >= length)
        Length = index + 1;  //INFO: I changed this from setting the field to setting the property, like above.  This means that it stores the value on the base object as well.
    }

    public override void Put(UInt32 index, Double value) => Put(index, BoxedValue.Box(value));

    public override void Put(UInt32 index, Object value, UInt32 tag)
      => Put(index, BoxedValue.Box(value, tag));

    public override void Put(String name, BoxedValue value) {
      if (name == "length") {
        PutLength(TypeConverter.ToNumber(value));
        SetAttrs("length", DescriptorAttrs.DontEnum); //TODO: Shouldn't `PutLength` keep the `DontEnum` flag?
        return;
      }

      if (TypeConverter.TryToIndex(name, out UInt32 index)) {  //TODO: I changed this to use TryToIndex, but that forgoes checking that `index.ToString() == name`, which may be necessary.
        Put(index, value);
        return;
      }

      base.Put(name, value);
    }

    public override void Put(String name, Double value) {
      if (name == "length") {
        PutLength(TypeConverter.ToNumber(value));
        SetAttrs("length", DescriptorAttrs.DontEnum); //TODO: Shouldn't `PutLength` keep the `DontEnum` flag?
        return;
      }

      if (TypeConverter.TryToIndex(name, out UInt32 index))  //TODO: I changed this to use TryToIndex, but that forgoes checking that `index.ToString() == name`, which may be necessary.
      {
        Put(index, value);
        return;
      }

      base.Put(name, value);
    }

    public override void Put(String name, Object value, UInt32 tag) {
      BoxedValue boxed = BoxedValue.Box(value, tag);

      if (name == "length") {
        PutLength(TypeConverter.ToNumber(boxed));
        SetAttrs("length", DescriptorAttrs.DontEnum); //TODO: Shouldn't `PutLength` keep the `DontEnum` flag?
        return;
      }

      if (TypeConverter.TryToIndex(name, out UInt32 index)) { //TODO: I changed this to use TryToIndex, but that forgoes checking that `index.ToString() == name`, which may be necessary.
        Put(index, boxed);
        return;
      }

      base.Put(name, boxed);
    }

    public override BoxedValue Get(String name) {
      if (UInt32.TryParse(name, out UInt32 index)) return Get(index);

      if (string.Equals(name, "length")) return BoxedValue.Box(length);

      return base.Get(name);
    }

    public override BoxedValue Get(UInt32 index) {
      if (index == UInt32.MaxValue) return base.Get(index.ToString());

      if (HasIndex(index)) {
        if (isDense) return dense[index].Value;
        else return sparse.Get(index);
      }

      return Prototype.Get(index);

      //TODO: This was ported from the following F# code, but I simplified it.  It may have broken due to the simplification.
      ////let ii = Int32 index
      ////if isDense && ii >= 0 && ii < dense.Length && dense.[ii].HasValue then
      ////    dense.[ii].Value
      ////else
      ////    if index = UInt32.MaxValue then
      ////        base.Get(string index)
      ////    else
      ////        if x.HasIndex(index) then
      ////            if isDense 
      ////                then dense.[Int32 index].Value
      ////                else sparse.Get(index)
      ////        else
      ////            x.Prototype.Get(index)
    }

    public override Boolean Has(String name) {
      if (UInt32.TryParse(name, out UInt32 index)) return Has(index);

      return base.Has(name);
    }

    public override Boolean Has(UInt32 index) {
      if (index == UInt32.MaxValue) return base.Has(index.ToString());

      return HasIndex(index) || Prototype.Has(index);
    }

    public override Boolean HasOwn(String name) {
      if (UInt32.TryParse(name, out UInt32 index)) return HasOwn(index);

      return base.HasOwn(name);
    }

    public override Boolean HasOwn(UInt32 index) {
      if (index == UInt32.MaxValue) return base.HasOwn(index.ToString());

      return HasIndex(index);
    }

    public override Boolean Delete(String name) {
      if (UInt32.TryParse(name, out UInt32 index)) return Delete(index);

      return base.Delete(name);
    }

    public override Boolean Delete(UInt32 index) {
      if (index == UInt32.MaxValue) return base.Delete(index.ToString());

      if (HasIndex(index)) {
        if (isDense) {
          //TODO: This was creating a new boxed value and pushing it into the existing descriptor, along with a `false` for has value.
          //  I changed it to a zero-initialized descriptor for speed and clarity.
          dense[index] = default;
        } else {
          return sparse.Remove(index);
        }
      }

      return false;
    }

    public class SparseArray {
      internal SortedDictionary<UInt32, BoxedValue> storage = new SortedDictionary<UInt32, BoxedValue>();

      public SortedDictionary<UInt32, BoxedValue> Members { get => storage; }

      public void Put(UInt32 index, BoxedValue value) { storage[index] = value; }

      public Boolean Has(UInt32 index) => storage.ContainsKey(index);

      public BoxedValue Get(UInt32 index) => storage[index];

      public Boolean TryGet(UInt32 index, out BoxedValue value)
        => storage.TryGetValue(index, out value);

      public Boolean Remove(UInt32 index) => storage.Remove(index);

      public void PutLength(UInt32 newLength, UInt32 length) {
        if (newLength >= length) return;

        foreach (UInt32 key in storage.Keys.ToList()) {
          if (key >= newLength) storage.Remove(key);
        }
      }

      public void Shift() {
        storage.Remove(0);

        foreach (UInt32 key in storage.Keys) {
          BoxedValue value = storage[key];
          storage.Remove(key);
          storage.Add(key, value); //FIXME: Isn't this supposed to subtract one from the key?  This probably isn't caught by our tests, because they don't trigger sparse arrays.
        }
      }

      public void Reverse(UInt32 length) {
        SortedDictionary<UInt32, BoxedValue> newStorage =
          new SortedDictionary<UInt32, BoxedValue>();

        foreach (KeyValuePair<UInt32, BoxedValue> kvp in storage) {
          newStorage.Add(length - kvp.Key - 1, kvp.Value);  //FIXME: What do we do when length > max(key)?  Does this just allow negative indices?
        }

        storage = newStorage;
      }

      public void Sort(Comparison<BoxedValue> comparison) {
        BoxedValue[] sorted = storage.Values.ToArray();
        Array.Sort(sorted, comparison);

        storage.Clear();
        for (UInt32 i = 0; i < sorted.Length; i++) {
          storage.Add(i, sorted[i]);
        }
      }

      public void Unshift(BoxedValue[] args) {
        UInt32 length = (UInt32)args.Length;

        SortedDictionary<UInt32, BoxedValue> newStorage =
          new SortedDictionary<UInt32, BoxedValue>();

        foreach (KeyValuePair<UInt32, BoxedValue> kvp in storage) {
          newStorage.Add(kvp.Key + length, kvp.Value);
        }

        for (UInt32 i = 0; i < length; i++) {
          newStorage.Add(i, args[i]);
        }

        storage = newStorage;
      }

      public void GetAllIndexProperties(IDictionary<UInt32, BoxedValue> dict, UInt32 length) {
        foreach (KeyValuePair<UInt32, BoxedValue> kvp in storage) {
          if ((kvp.Key < length) && !dict.ContainsKey(kvp.Key)) {
            dict.Add(kvp.Key, kvp.Value);
          }
        }
      }

      public static SparseArray OfDense(Descriptor[] values) {
        SparseArray sparse = new SparseArray();

        for (Int32 i = 0; i < values.Length; i++) {
          if (values[i].HasValue) {
            UInt32 num = (UInt32)i;
            sparse.storage[num] = values[i].Value;
          }
        }

        return sparse;
      }
    }
  }
}
