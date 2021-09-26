using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;

namespace IronJS.Runtime {
  public class CommonObject : DynamicObject {
    public Environment Env;
    public CommonObject Prototype;
    public Schema PropertySchema;
    public Descriptor[] Properties;

    public CommonObject(Environment env, Schema map, CommonObject prototype) {
      Env = env;
      Prototype = prototype;
      PropertySchema = map;
      Properties = new Descriptor[map.IndexMap.Count];
    }


    public CommonObject(Environment env, CommonObject prototype) {
      Env = env;
      Prototype = prototype;
      PropertySchema = env.Maps.Base;
      Properties = new Descriptor[env.Maps.Base.IndexMap.Count];
    }

    internal CommonObject(Environment env) {
      Env = env;
      Prototype = null;
      PropertySchema = null;
      Properties = null;
    }

    public override Boolean TryGetMember(GetMemberBinder binder, out Object result) {
      BoxedValue prop = Get(binder.Name);
      result = prop.UnboxObject();
      return true;
    }

    public override Boolean TryGetIndex(GetIndexBinder binder,
                                        Object[] indexes,
                                        out Object result) {

      if (indexes.Length != 1) {
        result = null;
        return false;
      }

      BoxedValue prop = Get(indexes[0].ToString());
      result = prop.UnboxObject();
      return true;
    }

    public override Boolean TrySetMember(SetMemberBinder binder, Object value) {
      Put(binder.Name, value);
      return true;
    }

    public override Boolean TrySetIndex(SetIndexBinder binder, Object[] indexes, Object value) {
      if (indexes.Length != 1)
        return false;

      Put(indexes[0].ToString(), value);
      return true;
    }

    public override Boolean TryInvokeMember(InvokeMemberBinder binder,
                                            Object[] args,
                                            out Object result) {

      Descriptor item = Find(binder.Name);
      if (item.HasValue) {
        BoxedValue box = item.Value;
        if (box.IsFunction) {
          FunctionObject func = box.Func;
          BoxedValue[] boxedArgs = args.Select(a => BoxedValue.Box(a)).ToArray();
          BoxedValue ret = func.Call(this, boxedArgs);
          result = ret.UnboxObject();
          return true;
        }
      }

      result = null;
      return false;
    }

    public override IEnumerable<String> GetDynamicMemberNames() {
      foreach (KeyValuePair<String, Int32> kvp in PropertySchema.IndexMap) {
        yield return kvp.Key;
      }
    }

    public override String ToString() {
      Descriptor item = Find("toString");
      if (item.HasValue) {
        BoxedValue box = item.Value;
        if (box.IsFunction) {
          FunctionObject func = box.Func;
          BoxedValue ret = func.Call(this);
          TypeConverter.ToString(ret);
        }
      }

      return base.ToString();
    }

    public virtual String ClassName { get => "Object"; }

    public String ClassType { get => GetType().Name; }

    public Dictionary<String, Object> Members {
      get {
        Dictionary<String, Object> dict = new Dictionary<String, Object>();
        foreach (KeyValuePair<String, Int32> kvp in PropertySchema.IndexMap) {
          if (Properties[kvp.Value].HasValue) {
            dict.Add(kvp.Key, Properties[kvp.Value].Value.ClrBoxed);
          }
        }

        return dict;
      }
    }

    public Boolean HasPrototype { get => Prototype != null; }

    public Int32 RequiredStorage { get => PropertySchema.IndexMap.Count; }

    // Default implementation of Length used for
    // all objects except ArrayObject
    public virtual UInt32 Length {
      get => TypeConverter.ToUInt32(Get("length"));
      set => Put("length", value);
    }

    public virtual void GetAllIndexProperties(IDictionary<UInt32, BoxedValue> dict, UInt32 length) {
      // UInt32 i = 0u;

      foreach (var kvp in PropertySchema.IndexMap) {
        if (UInt32.TryParse(kvp.Key, out UInt32 i) &&
            i < length &&
            !dict.ContainsKey(i) &&
            Properties[kvp.Value].HasValue)
          dict.Add(i, Properties[kvp.Value].Value);
      }

      if (Prototype != null) Prototype.GetAllIndexProperties(dict, length);
    }

    public T CastTo<T>() where T : CommonObject {
      if (this is T @object) return @object;

      return Env.RaiseTypeError<T>("Could not cast " + GetType().Name + " to " + typeof(T).Name);
    }

    public Boolean TryCastTo<T>(out T result) where T : CommonObject {
      if (this is T @object) {
        result = @object;
        return true;
      }

      result = null;
      return false;
    }

    public void CheckType<T>() where T : CommonObject {
      if (!(this is T)) {
        Env.RaiseTypeError<Object>(GetType().Name + " is not " + typeof(T).Name);
        return;
      }
    }

    /// <summary>
    /// Expands Object property storage
    /// </summary>
    public void ExpandStorage() {
      Descriptor[] newValues = new Descriptor[RequiredStorage * 2];

      if (Properties.Length > 0) Array.Copy(Properties, newValues, Properties.Length);

      Properties = newValues;
    }

    /// <summary>
    /// Creates an index for property named <paramref name="name"/>
    /// </summary>
    /// <param name="name">The property to be allocated.</param>
    internal Int32 CreateIndex(String name) {
      PropertySchema = PropertySchema.SubClass(name);

      if (RequiredStorage >= Properties.Length) ExpandStorage();

      return PropertySchema.IndexMap[name];
    }

    /// <summary>
    /// Finds a property in the prototype chain.
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public Descriptor Find(String name) => Find(this, name);

    private static Descriptor Find(CommonObject @this, String name) {
      while (@this != null) {
        if (@this.PropertySchema.IndexMap.TryGetValue(name, out Int32 index))
          return @this.Properties[index];

        @this = @this.Prototype;
      }

      return new Descriptor();
    }

    /// <summary>
    /// Can we put property named <paramref name="name"/>?
    /// </summary>
    /// <param name="name"></param>
    /// <param name="idx"></param>
    /// <returns></returns>
    public Boolean CanPut(String name, out Int32 idx) {
      if (PropertySchema.IndexMap.TryGetValue(name, out idx)) return Properties[idx].IsWritable;
      else {
        Boolean loop = true;
        CommonObject cobj = Prototype;

        while (loop && cobj != null) {
          if (cobj.PropertySchema.IndexMap.TryGetValue(name, out idx)) loop = false;
          else cobj = cobj.Prototype;
        }

        if (cobj == null || cobj.Properties[idx].IsWritable) {
          idx = CreateIndex(name);
          return true;
        }

        return false;
      }
    }

    public void SetAttrs(String name, UInt16 attrs) {
      if (PropertySchema.IndexMap.TryGetValue(name, out Int32 index))
        Properties[index].Attributes |= attrs;
    }

    public BoxedValue? TryCallMember(String name) {
      BoxedValue box = Get(name);
      if (box.IsFunction) return box.Func.Call(this);

      return null;
    }

    public BoxedValue CallMember(String name) => Get(name).Func.Call(this);

    //----------------------------------------------------------------------------
    // These methods are the core Put/Get/Has/Delete methods for property access
    //----------------------------------------------------------------------------

    public virtual void Put(String name, BoxedValue value) {
      if (CanPut(name, out Int32 index)) {
        Properties[index].Value = value;
        Properties[index].HasValue = true;
      }
    }

    public virtual void Put(String name, Object value, UInt32 tag) {
      if (CanPut(name, out Int32 index)) {
        Properties[index].Value.Clr = value;
        Properties[index].Value.Tag = tag;
        Properties[index].HasValue = true;
      }
    }

    public virtual void Put(String name, Double value) {
      if (CanPut(name, out Int32 index)) {
        Properties[index].Value.Number = value;
        Properties[index].HasValue = true;
      }
    }

    public virtual BoxedValue Get(String name) {
      Descriptor descriptor = Find(name);
      if (descriptor.HasValue) return descriptor.Value;
      return Undefined.Boxed;
    }

    public virtual Boolean Has(String name) => Find(name).HasValue;

    public virtual Boolean HasOwn(String name)
      => PropertySchema.IndexMap.TryGetValue(name, out Int32 index) && Properties[index].HasValue;

    public virtual Boolean Delete(String name) {
      if (!PropertySchema.IndexMap.TryGetValue(name, out Int32 index)) return true;

      if ((Properties[index].Attributes & DescriptorAttrs.DontDelete) == DescriptorAttrs.None) {
        PropertySchema = PropertySchema.Delete(name);
        Properties[index] = new Descriptor();
        return true;
      }

      return false;
    }

    //----------------------------------------------------------------------------

    public virtual void Put(UInt32 index, BoxedValue value) => Put(index.ToString(), value);

    public virtual void Put(UInt32 index, Object value, UInt32 tag)
      => Put(index.ToString(), value, tag);

    public virtual void Put(UInt32 index, Double value) => Put(index.ToString(), value);

    public virtual BoxedValue Get(UInt32 index) => Get(index.ToString());

    public virtual Boolean Has(UInt32 index) => Has(index.ToString());

    public virtual Boolean HasOwn(UInt32 index) => HasOwn(index.ToString());

    public virtual Boolean Delete(UInt32 index) => Delete(index.ToString());

    public virtual BoxedValue DefaultValue(DefaultValueHint hint) {
      BoxedValue? val;

      switch (hint) {
        case DefaultValueHint.String:
          val = TryCallMember("toString");
          if (val != null && val.Value.IsPrimitive) return val.Value;

          val = TryCallMember("valueOf");
          if (val != null && val.Value.IsPrimitive) return val.Value;

          return Env.RaiseTypeError<BoxedValue>("Could not get the default value.");

        default:
          val = TryCallMember("valueOf");
          if (val != null && val.Value.IsPrimitive) return val.Value;

          val = TryCallMember("toString");
          if (val != null && val.Value.IsPrimitive) return val.Value;

          return Env.RaiseTypeError<BoxedValue>("Could not get the default value.");
      }
    }

    public void Put(String name, Boolean value)
      => Put(name, value ? TaggedBools.True : TaggedBools.False);

    public void Put(String name, Object value) => Put(name, value, TypeTags.Clr);

    public void Put(String name, String value) => Put(name, value, TypeTags.String);

    public void Put(String name, Undefined value) => Put(name, value, TypeTags.Undefined);

    public void Put(String name, CommonObject value) => Put(name, value, TypeTags.Object);

    public void Put(String name, FunctionObject value) => Put(name, value, TypeTags.Function);

    public void Put(UInt32 index, Boolean value)
      => Put(index, value ? TaggedBools.True : TaggedBools.False);

    public void Put(UInt32 index, Object value) => Put(index, value, TypeTags.Clr);

    public void Put(UInt32 index, String value) => Put(index, value, TypeTags.String);

    public void Put(UInt32 index, Undefined value) => Put(index, value, TypeTags.Undefined);

    public void Put(UInt32 index, CommonObject value) => Put(index, value, TypeTags.Object);

    public void Put(UInt32 index, FunctionObject value) => Put(index, value, TypeTags.Function);

    public void Put(String name, BoxedValue value, UInt16 attrs) {
      Put(name, value);
      SetAttrs(name, attrs);
    }

    public void Put(String name, Boolean value, UInt16 attrs) {
      Put(name, value);
      SetAttrs(name, attrs);
    }

    public void Put(String name, Double value, UInt16 attrs) {
      Put(name, value);
      SetAttrs(name, attrs);
    }

    public void Put(String name, Object value, UInt16 attrs) {
      Put(name, value);
      SetAttrs(name, attrs);
    }

    public void Put(String name, String value, UInt16 attrs) {
      Put(name, value);
      SetAttrs(name, attrs);
    }

    public void Put(String name, Undefined value, UInt16 attrs) {
      Put(name, value);
      SetAttrs(name, attrs);
    }

    public void Put(String name, CommonObject value, UInt16 attrs) {
      Put(name, value);
      SetAttrs(name, attrs);
    }

    public void Put(String name, FunctionObject value, UInt16 attrs) {
      Put(name, value);
      SetAttrs(name, attrs);
    }

    //----------------------------------------------------------------------------
    // Put methods for setting indexes to BoxedValues
    //----------------------------------------------------------------------------

    public void Put(BoxedValue index, BoxedValue value) {
      if (TypeConverter.TryToIndex(index, out UInt32 @int)) Put(@int, value);
      else Put(TypeConverter.ToString(index), value);
    }

    public void Put(Boolean index, BoxedValue value) => Put(TypeConverter.ToString(index), value);

    public void Put(Double index, BoxedValue value) {
      if (TypeConverter.TryToIndex(index, out UInt32 parsed)) Put(parsed, value);
      else Put(TypeConverter.ToString(index), value);
    }

    public void Put(Object index, BoxedValue value) {
      String s = TypeConverter.ToString(index);
      if (TypeConverter.TryToIndex(s, out UInt32 parsed)) Put(parsed, value);
      else Put(s, value);
    }

    public void Put(Undefined _, BoxedValue value) => Put("undefined", value);

    public void Put(CommonObject index, BoxedValue value) {
      String @string = TypeConverter.ToString(index);
      if (TypeConverter.TryToIndex(@string, out UInt32 parsed)) Put(parsed, value);
      else Put(@string, value);
    }

    //----------------------------------------------------------------------------
    // Put methods for setting indexes to doubles
    //----------------------------------------------------------------------------

    public void Put(BoxedValue index, Double value) {
      if (TypeConverter.TryToIndex(index, out UInt32 @int)) Put(@int, value);
      else Put(TypeConverter.ToString(index), value);
    }

    public void Put(Boolean index, Double value) => Put(TypeConverter.ToString(index), value);

    public void Put(Double index, Double value) {
      if (TypeConverter.TryToIndex(index, out UInt32 parsed)) Put(parsed, value);
      else Put(TypeConverter.ToString(index), value);
    }

    public void Put(Object index0, Double value) {
      String index = TypeConverter.ToString(index0);
      if (TypeConverter.TryToIndex(index, out UInt32 parsed)) Put(parsed, value);
      else Put(index, value);
    }

    public void Put(Undefined _, Double value) => Put("undefined", value);

    public void Put(CommonObject index, Double value) {
      String @string = TypeConverter.ToString(index);
      if (TypeConverter.TryToIndex(@string, out UInt32 parsed)) Put(parsed, value);
      else Put(@string, value);
    }

    //----------------------------------------------------------------------------
    // Put methods for setting indexes to doubles
    //----------------------------------------------------------------------------

    public void Put(BoxedValue index, Object value, UInt32 tag) {
      if (TypeConverter.TryToIndex(index, out UInt32 @int)) Put(@int, value, tag);
      else Put(TypeConverter.ToString(index), value, tag);
    }

    public void Put(Boolean index, Object value, UInt32 tag)
      => Put(TypeConverter.ToString(index), value, tag);

    public void Put(Double index, Object value, UInt32 tag) {
      if (TypeConverter.TryToIndex(index, out UInt32 parsed)) Put(parsed, value, tag);
      else Put(TypeConverter.ToString(index), value, tag);
    }

    public void Put(Object index, Object value, UInt32 tag) {
      String @string = TypeConverter.ToString(index);
      if (TypeConverter.TryToIndex(@string, out UInt32 parsed)) Put(parsed, value, tag);
      else Put(@string, value, tag);
    }

    public void Put(Undefined _, Object value, UInt32 tag) => Put("undefined", value, tag);

    public void Put(CommonObject index, Object value, UInt32 tag) {
      String @string = TypeConverter.ToString(index);
      if (TypeConverter.TryToIndex(@string, out UInt32 parsed)) Put(parsed, value, tag);
      else Put(@string, value, tag);
    }

    //----------------------------------------------------------------------------
    // Overloaded .Get methods that convert their argument into either a String
    // or uint32 and forwards the call to the correct .Get method
    //----------------------------------------------------------------------------

    public BoxedValue Get(BoxedValue index) {
      if (TypeConverter.TryToIndex(index, out UInt32 @int)) return Get(@int);
      return Get(TypeConverter.ToString(index));
    }

    public BoxedValue Get(Boolean index) => Get(TypeConverter.ToString(index));

    public BoxedValue Get(Double index) {
      if (TypeConverter.TryToIndex(index, out UInt32 parsed)) return Get(parsed);
      return Get(TypeConverter.ToString(index));
    }

    public BoxedValue Get(Object index) {
      String @string = TypeConverter.ToString(index);
      if (TypeConverter.TryToIndex(@string, out UInt32 parsed)) return Get(parsed);
      return Get(@string);
    }

    public BoxedValue Get(Undefined _) => Get("undefined");

    public BoxedValue Get(CommonObject index) {
      String @string = TypeConverter.ToString(index);
      if (TypeConverter.TryToIndex(@string, out UInt32 parsed)) return Get(parsed);
      return Get(@string);
    }

    public T GetT<T>(String name) => Get(name).Unbox<T>();

    public T GetT<T>(UInt32 index) => Get(index).Unbox<T>();

    //----------------------------------------------------------------------------
    // Overloaded .Has methods that convert their argument into either a String
    // (property) or uint32 (index) and fowards the call to the correct .Has
    //----------------------------------------------------------------------------

    public Boolean Has(BoxedValue index) {
      if (TypeConverter.TryToIndex(index, out UInt32 @int)) return Has(@int);
      return Has(TypeConverter.ToString(index));
    }

    public Boolean Has(Boolean index) => Has(TypeConverter.ToString(index));

    public Boolean Has(Double index) {
      if (TypeConverter.TryToIndex(index, out UInt32 parsed)) return Has(parsed);
      return Has(TypeConverter.ToString(index));
    }

    public Boolean Has(Object index) {
      String @string = TypeConverter.ToString(index);
      if (TypeConverter.TryToIndex(@string, out UInt32 parsed)) return Has(parsed);
      return Has(@string);
    }

    public Boolean Has(Undefined _) => Has("undefined");

    public Boolean Has(CommonObject index) {
      String @string = TypeConverter.ToString(index);
      if (TypeConverter.TryToIndex(@string, out UInt32 parsed)) return Has(parsed);
      return Has(@string);
    }


    //----------------------------------------------------------------------------
    // Overloaded .Has methods that convert their argument into either a String
    // (property) or uint32 (index) and fowards the call to the correct .Has
    //----------------------------------------------------------------------------

    public Boolean Delete(BoxedValue index) {
      if (TypeConverter.TryToIndex(index, out UInt32 @int)) return Delete(@int);
      return Delete(TypeConverter.ToString(index));
    }

    public Boolean Delete(Boolean index) => Delete(TypeConverter.ToString(index));

    public Boolean Delete(Double index) {
      if (TypeConverter.TryToIndex(index, out UInt32 parsed)) return Delete(parsed);
      return Delete(TypeConverter.ToString(index));
    }

    public Boolean Delete(Object index) {
      String name = TypeConverter.ToString(index);
      if (TypeConverter.TryToIndex(name, out UInt32 parsed)) return Delete(parsed);
      return Delete(name);
    }

    public Boolean Delete(Undefined _) => Has("undefined");

    public Boolean Delete(CommonObject index) {
      String name = TypeConverter.ToString(index);
      if (TypeConverter.TryToIndex(name, out UInt32 parsed)) return Delete(parsed);
      return Delete(name);
    }

    public virtual Tuple<UInt32, HashSet<String>> CollectProperties()
      => collectProperties(0, new HashSet<String>(), this);

    private Tuple<UInt32, HashSet<String>> collectProperties(UInt32 length,
                                                             HashSet<String> set,
                                                             CommonObject current) {

      if (current != null) {
        if (current is ArrayObject array)
          length = length < array.Length ? array.Length : length;

        foreach (KeyValuePair<String, Int32> kvp in current.PropertySchema.IndexMap) {
          Descriptor descriptor = current.Properties[kvp.Value];
          if (descriptor.HasValue && descriptor.IsEnumerable) set.Add(kvp.Key);
        }

        return collectProperties(length, set, current.Prototype);
      } else {
        return Tuple.Create(length, set);
      }
    }

    public virtual IEnumerable<BoxedValue> CollectIndexValues() {
      UInt32 length = Length;
      UInt32 index = 0u;
      while (index < length) {
        yield return Get(index);
        index++;
      }
    }
  }
}
