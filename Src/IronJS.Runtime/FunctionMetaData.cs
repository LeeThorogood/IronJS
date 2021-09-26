using System;
using System.Collections.Generic;

namespace IronJS.Runtime {
  using ParameterStorage = Tuple<ParameterStorageType, Int32>;

  public class FunctionMetaData {
    readonly Dictionary<Type, Delegate> delegateCache = new Dictionary<Type, Delegate>();

    public String Name;

    public UInt64 Id { get; private set; }
    public String Source { get; private set; }
    public FunctionCompiler Compiler { get; private set; }
    public FunctionType FunctionType { get; private set; }
    public ParameterStorage[] ParameterStorage { get; private set; }

    public FunctionMetaData(UInt64 id,
                            FunctionType functionType,
                            FunctionCompiler compiler,
                            ParameterStorage[] parameterStorage) {

      Id = id;
      FunctionType = functionType;
      Compiler = compiler;
      ParameterStorage = parameterStorage;
    }

    public FunctionMetaData(UInt64 id, FunctionCompiler compiler, ParameterStorage[] storage)
      : this(id, FunctionType.UserDefined, compiler, storage) { }

    public FunctionMetaData(UInt64 id, FunctionType type, FunctionCompiler compiler)
      : this(id, type, compiler, new ParameterStorage[0]) { }

    public Delegate GetDelegate(FunctionObject function, Type delegateType) {

      if (!delegateCache.TryGetValue(delegateType, out Delegate compiled)) {
        delegateCache[delegateType] = compiled = Compiler(function, delegateType);
      }

      return compiled;
    }

    public T GetDelegate<T>(FunctionObject function) where T : class {
      return (T)(Object)GetDelegate(function, typeof(T));
    }
  }
}
