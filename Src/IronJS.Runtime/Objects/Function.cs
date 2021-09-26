using System;
using System.Dynamic;
using System.Linq;
using Microsoft.FSharp.Collections;

namespace IronJS.Runtime {
  using DynamicScope = FSharpList<Tuple<Int32, CommonObject>>;

  public class FunctionObject : CommonObject {
    public FunctionMetaData MetaData;
    public DynamicScope DynamicScope;
    public BoxedValue[] SharedScope;
    public readonly BoxedValue[] ReusablePrivateScope;

    public FunctionObject(Environment env,
                          UInt64 id,
                          BoxedValue[] sharedScope,
                          DynamicScope dynamicScope)
      : base(env, env.Maps.Function, env.Prototypes.Function) {

      MetaData = env.GetFunctionMetaData(id);
      SharedScope = sharedScope;
      DynamicScope = dynamicScope;
    }

    public FunctionObject(Environment env, FunctionMetaData metaData, Schema schema)
      : base(env, schema, env.Prototypes.Function) {

      MetaData = metaData;
      SharedScope = new BoxedValue[0];
      DynamicScope = DynamicScope.Empty;
    }

    public FunctionObject(Environment env) : base(env) {
      MetaData = env.GetFunctionMetaData(0UL);
      SharedScope = null;
      DynamicScope = DynamicScope.Empty;
    }

    public override String ClassName { get => "Function"; }

    public String Name { get => MetaData.Name; }

    public override Boolean TryInvoke(InvokeBinder binder, Object[] args, out Object result) {
      BoxedValue[] boxedArgs = args.Select(x => BoxedValue.Box(x)).ToArray();
      result = Call(Env.Globals, boxedArgs).UnboxObject();
      return true;
    }

    public CommonObject InstancePrototype {
      get {
        BoxedValue prototype = Get("prototype");
        switch (prototype.Tag) {
          case TypeTags.Function:
          case TypeTags.Object:
            return prototype.Object;

          default:
            return Env.Prototypes.Object;
        }
      }
    }

    public CommonObject NewInstance() {
      CommonObject @object = Env.NewObject();
      @object.Prototype = InstancePrototype;
      return @object;
    }

    public Boolean HasInstance(CommonObject value) {
      BoxedValue @object = Get("prototype");

      if (!@object.IsObject)
        return Env.RaiseTypeError<Boolean>("prototype property is not an Object");

      value = value?.Prototype;

      while (value != null) {
        if (ReferenceEquals(@object.Object, value)) return true;
        value = value.Prototype;
      }

      return false;
    }

    public BoxedValue Call(CommonObject @this) {
      return
          MetaData
              .GetDelegate<Func<FunctionObject, CommonObject, BoxedValue>>(this)
              .Invoke(this, @this);
    }

    public BoxedValue Call<T0>(CommonObject @this, T0 a0) {
      return
          MetaData
              .GetDelegate<Func<FunctionObject, CommonObject, T0, BoxedValue>>(this)
              .Invoke(this, @this, a0);
    }

    public BoxedValue Call<T0, T1>(CommonObject @this, T0 a0, T1 a1) {
      return
          MetaData
              .GetDelegate<Func<FunctionObject, CommonObject, T0, T1, BoxedValue>>(this)
              .Invoke(this, @this, a0, a1);
    }

    public BoxedValue Call<T0, T1, T2>(CommonObject @this, T0 a0, T1 a1, T2 a2) {
      return
          MetaData
              .GetDelegate<Func<FunctionObject, CommonObject, T0, T1, T2, BoxedValue>>(this)
              .Invoke(this, @this, a0, a1, a2);
    }

    public BoxedValue Call<T0, T1, T2, T3>(CommonObject @this, T0 a0, T1 a1, T2 a2, T3 a3) {
      return
          MetaData
              .GetDelegate<Func<FunctionObject, CommonObject, T0, T1, T2, T3, BoxedValue>>(this)
              .Invoke(this, @this, a0, a1, a2, a3);
    }

    public BoxedValue Call(CommonObject @this, BoxedValue[] args) {
      return
          MetaData
              .GetDelegate<Func<FunctionObject, CommonObject, BoxedValue[], BoxedValue>>(this)
              .Invoke(this, @this, args);
    }

    public BoxedValue Construct() {
      switch (MetaData.FunctionType) {
        case FunctionType.NativeConstructor:
          return Call(null);

        case FunctionType.UserDefined:
          CommonObject @object = NewInstance();
          return PickReturnObject(Call(@object), @object);

        default:
          return Env.RaiseTypeError<BoxedValue>();
      }
    }

    public BoxedValue Construct<T0>(T0 a0) {
      switch (MetaData.FunctionType) {
        case FunctionType.NativeConstructor:
          return Call(null, a0);

        case FunctionType.UserDefined:
          CommonObject @object = NewInstance();
          return PickReturnObject(Call(@object, a0), @object);

        default:
          return Env.RaiseTypeError<BoxedValue>();
      }
    }

    public BoxedValue Construct<T0, T1>(T0 a0, T1 a1) {
      switch (MetaData.FunctionType) {
        case FunctionType.NativeConstructor:
          return Call(null, a0, a1);

        case FunctionType.UserDefined:
          CommonObject @object = NewInstance();
          return PickReturnObject(Call(@object, a0, a1), @object);

        default:
          return Env.RaiseTypeError<BoxedValue>();
      }
    }

    public BoxedValue Construct<T0, T1, T2>(T0 a0, T1 a1, T2 a2) {
      switch (MetaData.FunctionType) {
        case FunctionType.NativeConstructor:
          return Call(null, a0, a1, a2);

        case FunctionType.UserDefined:
          CommonObject @object = NewInstance();
          return PickReturnObject(Call(@object, a0, a1, a2), @object);

        default:
          return Env.RaiseTypeError<BoxedValue>();
      }
    }

    public BoxedValue Construct<T0, T1, T2, T3>(T0 a0, T1 a1, T2 a2, T3 a3) {
      switch (MetaData.FunctionType) {
        case FunctionType.NativeConstructor:
          return Call(null, a0, a1, a2, a3);

        case FunctionType.UserDefined:
          CommonObject @object = NewInstance();
          return PickReturnObject(Call(@object, a0, a1, a2, a3), @object);

        default:
          return Env.RaiseTypeError<BoxedValue>();
      }
    }

    public BoxedValue Construct(BoxedValue[] args) {
      switch (MetaData.FunctionType) {
        case FunctionType.NativeConstructor:
          return Call(null, args);

        case FunctionType.UserDefined:
          CommonObject @object = NewInstance();
          return PickReturnObject(Call(@object, args), @object);

        default:
          return Env.RaiseTypeError<BoxedValue>();
      }
    }

    public BoxedValue PickReturnObject(BoxedValue value, CommonObject @object) {
      switch (value.Tag) {
        case TypeTags.Function: return BoxedValue.Box(value.Func);
        case TypeTags.Object: return BoxedValue.Box(value.Object);
        default: return BoxedValue.Box(@object);
      }
    }
  }
}
