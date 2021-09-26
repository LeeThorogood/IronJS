using System;
using System.Runtime.InteropServices;

namespace IronJS.Runtime {
  internal static class BoxedValueOffsets {
    public const Int32 ValueType = 0;
    public const Int32 Tag = 4;
    public const Int32 Marker = 6;
    public const Int32 ReferenceType = 8;
  }

  public static class Markers {
    public const UInt16 Number = 0xFFF8;
    public const UInt16 Tagged = 0xFFF9;
  }

  /// <summary>
  /// This is a NaN-tagged struct that is used for representing
  /// values that don't have a known type at runtime
  /// </summary>
  [StructLayout(LayoutKind.Explicit)]
  public struct BoxedValue {
    // Reference Types
    [FieldOffset(BoxedValueOffsets.ReferenceType)]
    public Object Clr;
    [FieldOffset(BoxedValueOffsets.ReferenceType)]
    public CommonObject Object;
    [FieldOffset(BoxedValueOffsets.ReferenceType)]
    public ArrayObject Array;
    [FieldOffset(BoxedValueOffsets.ReferenceType)]
    public FunctionObject Func;
    [FieldOffset(BoxedValueOffsets.ReferenceType)]
    public String String;
    [FieldOffset(BoxedValueOffsets.ReferenceType)]
    public SuffixString SuffixString;
    [FieldOffset(BoxedValueOffsets.ReferenceType)]
    private readonly BoxedValue[] Scope;

    // Value Types
    [FieldOffset(BoxedValueOffsets.ValueType)]
    public Boolean Bool;
    [FieldOffset(BoxedValueOffsets.ValueType)]
    public Double Number;

    // Type & Tag
    [FieldOffset(BoxedValueOffsets.Tag)]
    public UInt32 Tag;
    [FieldOffset(BoxedValueOffsets.Marker)]
    public UInt16 Marker;

    public Boolean IsNumber { get => Marker < Markers.Tagged; }
    public Boolean IsTagged { get => Marker > Markers.Number; }
    public Boolean IsString {
      get => IsTagged && (Tag == TypeTags.String || Tag == TypeTags.SuffixString);
    }
    public Boolean IsObject { get => IsTagged && Tag >= TypeTags.Object; }
    public Boolean IsFunction { get => IsTagged && Tag >= TypeTags.Function; }
    public Boolean IsBoolean { get => IsTagged && Tag == TypeTags.Bool; }
    public Boolean IsUndefined { get => IsTagged && Tag == TypeTags.Undefined; }
    public Boolean IsClr { get => IsTagged && Tag == TypeTags.Clr; }
    public Boolean IsRegExp { get => IsObject && Object is RegExpObject; }
    public Boolean IsNull { get => IsClr && Clr == null; }

    // As per ECMA-262, Section 8.6.2, the following types are primitive:
    //  Undefined, Null, Boolean, String, or Number
    public Boolean IsPrimitive {
      get => IsUndefined || IsNull || IsBoolean || IsString || IsNumber;
    }

    public Object ClrBoxed {
      get {
        if (IsNumber) return Number;
        if (Tag == TypeTags.Bool) return Bool;
        if (Tag == TypeTags.SuffixString) return SuffixString.ToString();
        return Clr;
      }
    }

    public T Unbox<T>() => (T)ClrBoxed;

    public Object UnboxObject() {
      if (IsNumber) {
        return Number;
      } else {
        switch (Tag) {
          case TypeTags.Bool:
            return Bool;
          case TypeTags.Clr:
            return Clr;
          case TypeTags.Function:
            return Func;
          case TypeTags.Object:
            return Object;
          case TypeTags.String:
            return String;
          case TypeTags.SuffixString:
            return SuffixString;
          case TypeTags.Undefined:
            return Undefined.Instance;
          default:
            return this;
        }
      }
    }

    public static BoxedValue Box(CommonObject value) {
      BoxedValue box = new BoxedValue();
      box.Clr = value;
      box.Tag = TypeTags.Object;
      return box;
    }

    public static BoxedValue Box(FunctionObject value) {
      BoxedValue box = new BoxedValue();
      box.Clr = value;
      box.Tag = TypeTags.Function;
      return box;
    }

    public static BoxedValue Box(String value) {
      BoxedValue box = new BoxedValue();
      box.Clr = value;
      box.Tag = TypeTags.String;
      return box;
    }

    public static BoxedValue Box(SuffixString value) {
      BoxedValue box = new BoxedValue();
      box.Clr = value;
      box.Tag = TypeTags.SuffixString;
      return box;
    }

    public static BoxedValue Box(Double value) {
      BoxedValue box = new BoxedValue();
      box.Number = value;
      return box;
    }

    public static BoxedValue Box(Boolean value) {
      BoxedValue box = new BoxedValue();
      box.Number = value ? TaggedBools.True : TaggedBools.False;
      return box;
    }

    public static BoxedValue Box(Object value) {
      if (value is Double @double) return Box(@double);
      if (value is Int32 @Int32) return Box(@Int32);
      if (value is Boolean boolean) return Box(boolean);
      if (value is String @string) return Box(@string);
      if (value is SuffixString string1) return Box(string1);
      if (value is FunctionObject @object) return Box(@object);
      if (value is CommonObject object1) return Box(object1);
      if (value is Undefined undefined) return Box(undefined);

      BoxedValue box = new BoxedValue();
      box.Clr = value;
      box.Tag = TypeTags.Clr;
      return box;
    }

    public static BoxedValue Box(Object value, UInt32 tag) {
      BoxedValue box = new BoxedValue();
      box.Clr = value;
      box.Tag = tag;
      return box;
    }

    public static BoxedValue Box(Undefined _) => Undefined.Boxed;

    public static String FieldOfTag(UInt32 tag) {
      switch (tag) {
        case TypeTags.Bool:
          return BoxFields.Bool;
        case TypeTags.Clr:
          return BoxFields.Clr;
        case TypeTags.Function:
          return BoxFields.Function;
        case TypeTags.Object:
          return BoxFields.Object;
        case TypeTags.String:
          return BoxFields.String;
        case TypeTags.SuffixString:
          return BoxFields.SuffixString;
        case TypeTags.Undefined:
          return BoxFields.Undefined;
        case TypeTags.Number:
          return BoxFields.Number;
        default:
          throw new ArgumentException(String.Format("Invalid type tag '{0}'", tag));
      }
    }
  }

  public static class BoxingUtils {
    public static BoxedValue JsBox(Object o) {
      if (o is BoxedValue value) return value;

      if (o == null) return Environment.BoxedNull;

      UInt32 tag = TypeTag.OfType(o.GetType());
      switch (tag) {
        case TypeTags.Bool: return BoxedValue.Box((Boolean)o);
        case TypeTags.Number: return BoxedValue.Box((Double)o);
        default: return BoxedValue.Box(o, tag);
      }
    }

    public static Object ClrBox(Object o) {
      if (o is BoxedValue value) return value.ClrBoxed;
      return o;
    }
  }
}
