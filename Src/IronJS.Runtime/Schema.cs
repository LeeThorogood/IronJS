using System;
using System.Collections.Generic;

namespace IronJS.Runtime {
  using IndexMap = Dictionary<String, Int32>;
  using IndexStack = Stack<Int32>;
  using SchemaMap = Dictionary<String, Schema>;

  public class Schema {
    public readonly UInt64 Id;
    public readonly Environment Env;
    public readonly IndexMap IndexMap;
    public readonly SchemaMap SubSchemas;

    public Schema(Environment env, IndexMap indexMap) {
      Id = env.NextPropertyMapId();
      Env = env;
      IndexMap = indexMap;
      SubSchemas = new SchemaMap();
    }

    public Schema(Environment env, IndexMap indexMap, SchemaMap subSchemas) {
      Id = 1UL;
      Env = env;
      IndexMap = indexMap;
      SubSchemas = subSchemas;
    }

    public virtual DynamicSchema MakeDynamic() => new DynamicSchema(Env, IndexMap);

    public virtual Schema Delete(String name) => MakeDynamic().Delete(name);

    public virtual Schema SubClass(String name) {
      if (!SubSchemas.TryGetValue(name, out Schema subSchema)) {
        IndexMap newIndexMap = new IndexMap(IndexMap);
        newIndexMap.Add(name, newIndexMap.Count);
        SubSchemas[name] = subSchema = new Schema(Env, newIndexMap);
      }
      return subSchema;
    }

    public virtual Schema SubClass(IEnumerable<String> names) {
      Schema schema = this;

      foreach (String name in names) {
        schema = schema.SubClass(name);
      }

      return schema;
    }

    public bool TryGetIndex(String name, out Int32 index) => IndexMap.TryGetValue(name, out index);

    public static Schema CreateBaseSchema(Environment env) => new Schema(env, new IndexMap());
  }

  public class DynamicSchema : Schema {
    public readonly IndexStack FreeIndexes = new IndexStack();

    public DynamicSchema(Environment env, IndexMap indexMap)
      : base(env, new IndexMap(indexMap), null) { }

    public override DynamicSchema MakeDynamic() => this;

    public override Schema Delete(String name) {
      if (IndexMap.TryGetValue(name, out Int32 index)) {
        FreeIndexes.Push(index);
        IndexMap.Remove(name);
      }
      return this;
    }

    public override Schema SubClass(String name) {
      Int32 index = (FreeIndexes.Count > 0) ? FreeIndexes.Pop() : IndexMap.Count;
      IndexMap.Add(name, index);
      return this;
    }
  }
}
