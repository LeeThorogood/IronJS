using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Microsoft.FSharp.Collections;

namespace IronJS.Runtime {
  public class Environment {
    UInt64 currentSchemaId = 0;
    UInt64 currentFunctionId = 0;

    readonly Dictionary<UInt64, FunctionMetaData> functionMetaData
      = new Dictionary<UInt64, FunctionMetaData>();

    internal readonly Caches.WeakCache<Tuple<RegexOptions, String>, Regex> RegExpCache
      = new Caches.WeakCache<Tuple<RegexOptions, String>, Regex>();

    public readonly Caches.LimitCache<String, EvalCode> EvalCache
      = new Caches.LimitCache<String, EvalCode>(100);

    public BoxedValue Return;
    public CommonObject Globals;
    public Int32 Line;
    public Action<Int32, Int32, Dictionary<String, Object>> BreakPoint;

    public Maps Maps;
    public readonly Random Random = new Random();
    public readonly Prototypes Prototypes = Prototypes.Empty;
    public readonly Constructors Constructors = Constructors.Empty;

    public static BoxedValue BoxedZero { get => BoxedValue.Box(0.0); }
    public static BoxedValue BoxedNull { get => BoxedValue.Box(null, TypeTags.Clr); }

    public Environment() => functionMetaData.Add(0UL, null);

    public UInt64 NextFunctionId() => ++currentFunctionId;

    public UInt64 NextPropertyMapId() => ++currentSchemaId;

    public FunctionMetaData GetFunctionMetaData(UInt64 id) => functionMetaData[id];

    public Boolean HasFunctionMetaData(UInt64 id) => functionMetaData.ContainsKey(id);

    public void AddFunctionMetaData(FunctionMetaData metaData)
      => functionMetaData[metaData.Id] = metaData;

    public FunctionMetaData CreateHostMetaData(FunctionType functionType,
                                               FunctionCompiler compiler) {
      UInt64 id = NextFunctionId();
      FunctionMetaData metaData = new FunctionMetaData(id, functionType, compiler);
      AddFunctionMetaData(metaData);
      return metaData;
    }

    public FunctionMetaData CreateHostConstructorMetaData(FunctionCompiler compiler)
      => CreateHostMetaData(FunctionType.NativeConstructor, compiler);

    public FunctionMetaData CreateHostFunctionMetaData(FunctionCompiler compiler)
      => CreateHostMetaData(FunctionType.NativeFunction, compiler);

    public CommonObject NewObject() => new CommonObject(this, Maps.Base, Prototypes.Object);

    public CommonObject NewMath() => new MathObject(this);

    public CommonObject NewArray() => NewArray(0);

    public CommonObject NewArray(UInt32 size) => new ArrayObject(this, size) { Length = size };

    public CommonObject NewDate(DateTime date) => new DateObject(this, date);

    public CommonObject NewBoolean() => NewBoolean(false);

    public CommonObject NewBoolean(Boolean value) {
      BooleanObject boolean = new BooleanObject(this);
      boolean.Value.Value.Bool = value;
      boolean.Value.Value.Tag = 0xffffff01;
      boolean.Value.HasValue = true;
      return boolean;
    }

    public ErrorObject NewError() => new ErrorObject(this);

    public FunctionObject NewFunction(UInt64 id,
                                      Int32 args,
                                      BoxedValue[] closureScope,
                                      FSharpList<Tuple<Int32, CommonObject>> dynamicScope) {

      FunctionObject func = new FunctionObject(this, id, closureScope, dynamicScope);
      CommonObject proto = NewPrototype();
      proto.Put("constructor", func, 2);
      func.Put("prototype", proto, 4);
      func.Put("length", args, 7);
      return func;
    }

    public CommonObject NewNumber() => NewNumber(0.0);

    public CommonObject NewNumber(Double value) {
      NumberObject number = new NumberObject(this);
      number.Value.Value.Number = value;
      number.Value.HasValue = true;
      return number;
    }

    public CommonObject NewPrototype() => new CommonObject(this, Maps.Prototype, Prototypes.Object);

    public CommonObject NewRegExp() => NewRegExp("");

    public CommonObject NewRegExp(String pattern) => NewRegExp(pattern, "");

    public CommonObject NewRegExp(String pattern, String options) {
      pattern = pattern ?? "";
      options = options ?? "";

      Boolean multiline = false;
      Boolean ignoreCase = false;
      Boolean global = false;

      foreach (Char o in options) {
        if (o == 'm' && !multiline) multiline = true;
        else if (o == 'i' && !ignoreCase) ignoreCase = true;
        else if (o == 'g' && !global) global = true;
        else return RaiseSyntaxError<CommonObject>("Invalid RegExp options '" + options + "'");
      }

      RegexOptions opts = RegexOptions.None;

      if (multiline) opts |= RegexOptions.Multiline;

      if (ignoreCase) opts |= RegexOptions.IgnoreCase;

      return NewRegExp(pattern, opts, global);
    }

    public CommonObject NewRegExp(String pattern, RegexOptions options, Boolean isGlobal) {
      RegExpObject regexp = new RegExpObject(this, pattern, options, isGlobal);
      regexp.Put("source", pattern, 7);
      regexp.Put("global", isGlobal, 7);
      regexp.Put("ignoreCase", regexp.IgnoreCase, 7);
      regexp.Put("multiline", regexp.MultiLine, 7);
      regexp.Put("lastIndex", (Double)0.0, 6);
      return regexp;
    }

    public CommonObject NewString() => NewString(String.Empty);

    public CommonObject NewString(String value) {
      StringObject @String = new StringObject(this);
      @String.Properties[0].Value.Number = value.Length;
      @String.Properties[0].Attributes = 3;
      @String.Properties[0].HasValue = true;
      @String.Value.Value.Clr = value;
      @String.Value.Value.Tag = 0xffffff04;
      @String.Value.HasValue = true;
      return @String;
    }

    public T RaiseError<T>(CommonObject prototype, String message) {
      ErrorObject error = new ErrorObject(this) { Prototype = prototype };

      error.Put("message", message);
      throw new UserError(BoxedValue.Box(error), 0, 0);
    }

    public T RaiseEvalError<T>() => RaiseEvalError<T>("");

    public T RaiseEvalError<T>(String message) => RaiseError<T>(Prototypes.EvalError, message);

    public T RaiseRangeError<T>() => RaiseRangeError<T>("");

    public T RaiseRangeError<T>(String message) => RaiseError<T>(Prototypes.RangeError, message);

    public T RaiseReferenceError<T>() => RaiseReferenceError<T>("");

    public T RaiseReferenceError<T>(String message)
      => RaiseError<T>(Prototypes.ReferenceError, message);

    public T RaiseSyntaxError<T>() => RaiseSyntaxError<T>("");

    public T RaiseSyntaxError<T>(String message) => RaiseError<T>(Prototypes.SyntaxError, message);

    public T RaiseTypeError<T>() => RaiseTypeError<T>("");

    public T RaiseTypeError<T>(String message) => RaiseError<T>(Prototypes.TypeError, message);

    public T RaiseURIError<T>() => RaiseURIError<T>("");

    public T RaiseURIError<T>(String message) => RaiseError<T>(Prototypes.URIError, message);
  }

  public class Prototypes {
    public CommonObject Object;
    public CommonObject Array;
    public FunctionObject Function;
    public CommonObject String;
    public CommonObject Number;
    public CommonObject Boolean;
    public CommonObject Date;
    public CommonObject RegExp;
    public CommonObject Error;
    public CommonObject EvalError;
    public CommonObject RangeError;
    public CommonObject ReferenceError;
    public CommonObject SyntaxError;
    public CommonObject TypeError;
    public CommonObject URIError;

    public static Prototypes Empty { get => new Prototypes(); }
  }

  public class Constructors {
    public FunctionObject Object;
    public FunctionObject Array;
    public FunctionObject Function;
    public FunctionObject String;
    public FunctionObject Number;
    public FunctionObject Boolean;
    public FunctionObject Date;
    public FunctionObject RegExp;
    public FunctionObject Error;
    public FunctionObject EvalError;
    public FunctionObject RangeError;
    public FunctionObject ReferenceError;
    public FunctionObject SyntaxError;
    public FunctionObject TypeError;
    public FunctionObject URIError;

    public static Constructors Empty { get => new Constructors(); }
  }

  public class Maps {
    public Schema Base;
    public Schema Array;
    public Schema Function;
    public Schema Prototype;
    public Schema String;
    public Schema Number;
    public Schema Boolean;
    public Schema RegExp;

    public static Maps Create(Schema baseSchema) {
      Maps maps = new Maps();

      maps.Base = baseSchema;
      maps.Array = baseSchema.SubClass("length");
      maps.Function = baseSchema.SubClass(new[] { "length", "prototype" });
      maps.Prototype = baseSchema.SubClass("constructor");
      maps.String = baseSchema.SubClass("length");
      maps.Number = baseSchema;
      maps.Boolean = baseSchema;
      maps.RegExp = baseSchema.SubClass(new[] { "source",
                                                "global",
                                                "ignoreCase",
                                                "multiline",
                                                "lastIndex" }
                                       );

      return maps;
    }
  }
}
