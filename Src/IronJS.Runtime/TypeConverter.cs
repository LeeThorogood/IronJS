using System;
using System.Globalization;
using System.Numerics;

namespace IronJS.Runtime {
  public class TypeConverter {
    public static BoxedValue ToBoxedValue(BoxedValue value) => value;

    public static BoxedValue ToBoxedValue(Double @double) => BoxedValue.Box(@double);

    public static BoxedValue ToBoxedValue(Boolean @bool) => BoxedValue.Box(@bool);

    public static BoxedValue ToBoxedValue(String @string) => BoxedValue.Box(@string);

    public static BoxedValue ToBoxedValue(SuffixString @string) => BoxedValue.Box(@string);

    public static BoxedValue ToBoxedValue(CommonObject @object) => BoxedValue.Box(@object);

    public static BoxedValue ToBoxedValue(FunctionObject function) => BoxedValue.Box(function);

    public static BoxedValue ToBoxedValue(Undefined _) => Undefined.Boxed;

    public static BoxedValue ToBoxedValue(Object @object) => BoxedValue.Box(@object);

    public static Object ToClrObject(Double @double) => @double;

    public static Object ToClrObject(Boolean @bool) => @bool;

    public static Object ToClrObject(String @string) => @string;

    public static Object ToClrObject(CommonObject @object) => @object;

    public static Object ToClrObject(FunctionObject function) => function;

    public static Object ToClrObject(Object @object) => @object;

    public static Object ToClrObject(BoxedValue value) {
      switch (value.Tag) {
        case TypeTags.Undefined:
          return null;

        case TypeTags.Bool:
          return value.Bool;

        case TypeTags.Object:
        case TypeTags.Function:
        case TypeTags.String:
        case TypeTags.Clr:
          return value.Clr;

        case TypeTags.SuffixString:
          return value.Clr.ToString();

        default:
          return value.Number;
      }
    }

    public static CommonObject ToObject(Environment _, CommonObject @object) => @object;

    public static CommonObject ToObject(Environment _, FunctionObject function) => function;

    public static CommonObject ToObject(Environment env, Undefined _)
      => env.RaiseTypeError<CommonObject>("Can't convert Undefined to Object");

    public static CommonObject ToObject(Environment env, Object _)
      => env.RaiseTypeError<CommonObject>("Can't convert Null or CLR to Object");

    public static CommonObject ToObject(Environment env, String @string) => env.NewString(@string);

    public static CommonObject ToObject(Environment env, Double @double) => env.NewNumber(@double);

    public static CommonObject ToObject(Environment env, Boolean @bool) => env.NewBoolean(@bool);

    public static CommonObject ToObject(Environment env, BoxedValue value) {
      switch (value.Tag) {
        case TypeTags.Object:
        case TypeTags.Function:
          return value.Object;

        case TypeTags.SuffixString:
          return env.NewString(value.Clr.ToString());

        case TypeTags.String:
          return env.NewString(value.String);

        case TypeTags.Bool:
          return env.NewBoolean(value.Bool);

        case TypeTags.Clr:
        case TypeTags.Undefined:
          return env.RaiseTypeError<CommonObject>("Can't convert Undefined, Null or CLR to Object");

        default:
          return env.NewNumber(value.Number);
      }
    }

    public static Boolean ToBoolean(Boolean @bool) => @bool;

    public static Boolean ToBoolean(Double @double) => (@double > 0.0) || (@double < 0.0);

    public static Boolean ToBoolean(Object @object) => @object != null;

    public static Boolean ToBoolean(String @string) => !String.IsNullOrEmpty(@string);

    public static Boolean ToBoolean(Undefined _) => false;

    public static Boolean ToBoolean(CommonObject _) => true;

    public static Boolean ToBoolean(BoxedValue value) {
      switch (value.Tag) {
        case TypeTags.Bool:
          return value.Bool;

        case TypeTags.String:
          return !String.IsNullOrEmpty(value.String);

        case TypeTags.SuffixString:
          var ss = (SuffixString)value.Clr;
          return ss.Length > 0;

        case TypeTags.Undefined:
          return false;

        case TypeTags.Clr:
          return value.Clr != null;

        case TypeTags.Object:
        case TypeTags.Function:
          return true;

        default:
          return ToBoolean(value.Number);
      }
    }

    public static BoxedValue ToPrimitive(Boolean @bool, DefaultValueHint _)
      => BoxedValue.Box(@bool);

    public static BoxedValue ToPrimitive(Double @double, DefaultValueHint _)
      => BoxedValue.Box(@double);

    public static BoxedValue ToPrimitive(String @string, DefaultValueHint _)
      => BoxedValue.Box(@string);

    public static BoxedValue ToPrimitive(CommonObject @object, DefaultValueHint hint)
      => @object.DefaultValue(hint);

    public static BoxedValue ToPrimitive(Undefined u, DefaultValueHint hint) {
      _ = u;
      _ = hint;
      return Undefined.Boxed;
    }

    public static BoxedValue ToPrimitive(Object @object, DefaultValueHint _) {
      if (@object == null) return BoxedValue.Box(default(Object));

      return BoxedValue.Box(@object.ToString());
    }

    public static BoxedValue ToPrimitive(BoxedValue value)
      => ToPrimitive(value, DefaultValueHint.None);

    public static BoxedValue ToPrimitive(BoxedValue value, DefaultValueHint hint) {
      switch (value.Tag) {
        case TypeTags.Clr:
          return ToPrimitive(value.Clr, hint);

        case TypeTags.Object:
        case TypeTags.Function:
          return value.Object.DefaultValue(hint);

        case TypeTags.SuffixString:
          return BoxedValue.Box(value.Clr.ToString());

        default:
          return value;
      }
    }

    public static String ToString(Boolean @bool) => @bool ? "true" : "false";

    public static String ToString(String @string) => @string;

    public static String ToString(Undefined _) => "undefined";

    public static String ToString(Object @object) => @object == null ? "null" : @object.ToString();

    /// These steps are outlined in the ECMA-262, Section 9.8.1
    public static String ToString(Double @double) {
      if (Double.IsNaN(@double)) return "NaN";

      if (@double == 0.0) return "0";

      String sign = (@double >= 0.0) ? "" : "-";

      @double = (@double >= 0.0) ? @double : -@double;

      if (Double.IsInfinity(@double)) return sign + "Infinity";

      String format = "0.00000000000000000e0";
      String[] parts = @double.ToString(format, CultureInfo.InvariantCulture).Split('e');
      String s = parts[0].TrimEnd('0').Replace(".", "");
      Int32 k = s.Length;
      Int32 n = Int32.Parse(parts[1]) + 1;

      if (k <= n && n <= 21) return sign + s + new String('0', n - k);
      else if (0 < n && n <= 21) return sign + s.Substring(0, n) + "." + s.Substring(n);
      else if (-6 < n && n <= 0) return sign + "0." + new String('0', -n) + s;

      String exponent = "e" + String.Format("{0:+0;-0}", n - 1);

      if (k == 1) return sign + s + exponent;
      else return s.Substring(0, 1) + "." + s.Substring(1) + exponent;
    }

    public static String ToString(CommonObject @object) {
      if (@object is StringObject @string) return @string.Value.Value.String;
      else return ToString(@object.DefaultValue(DefaultValueHint.String));
    }


    public static String ToString(BoxedValue v) {
      switch (v.Tag) {
        case TypeTags.Bool:
          return ToString(v.Bool);

        case TypeTags.String:
          return v.String;

        case TypeTags.SuffixString:
          return v.Clr.ToString();

        case TypeTags.Clr:
          return ToString(v.Clr);

        case TypeTags.Undefined:
          return "undefined";

        case TypeTags.Object:
        case TypeTags.Function:
          return ToString(v.Object);

        default:
          return ToString(v.Number);
      }
    }

    public static Double ToNumber(Boolean @bool) => @bool ? 1.0 : 0.0;

    public static Double ToNumber(Object @object) => @object != null ? 1.0 : 0.0;

    public static Double ToNumber(Undefined _) => Double.NaN;

    public static Double ToNumber(BoxedValue value) {
      if (value.Marker >= Markers.Tagged) {
        switch (value.Tag) {
          case TypeTags.Bool:
            return ToNumber(value.Bool);

          case TypeTags.String:
            return ToNumber(value.String);

          case TypeTags.SuffixString:
            return ToNumber(value.Clr.ToString());

          case TypeTags.Clr:
            return ToNumber(value.Clr);

          case TypeTags.Undefined:
            return Double.NaN;

          case TypeTags.Object:
          case TypeTags.Function:
            return ToNumber(value.Object);
        }
      }

      return value.Number;
    }

    public static Double ToNumber(FunctionObject function)
      => ToNumber(function.DefaultValue(DefaultValueHint.Number));

    public static Double ToNumber(CommonObject @object) {
      if (@object is NumberObject number) return number.Value.Value.Number;
      else return ToNumber(@object.DefaultValue(DefaultValueHint.Number));
    }

    public static Double ToNumber(String @string) {

      @string = @string?.Trim();
      if (String.IsNullOrEmpty(@string)) return 0.0;

      if (String.Equals(@string, "+Infinity")) return Double.PositiveInfinity;

      if (Double.TryParse(@string,
                          NumberStyles.Any,
                          CultureInfo.InvariantCulture,
                          out Double @double) && !@string.Contains(",")) {

        if (@double != 0.0) return @double;
        else return @string[0] == '-' ? -0.0 : 0.0;

      } else if (@string.Length > 1 &&
                 @string[0] == '0' &&
                 (@string[1] == 'x' || @string[1] == 'X')) {

        if (Int32.TryParse(@string.Substring(2),
                           NumberStyles.HexNumber,
                           CultureInfo.InvariantCulture,
                           out Int32 @int)) {

          return @int;
        }

        return Double.NaN;
      }

      try { return Convert.ToInt32(@string, 8); }
      catch (FormatException) { }
      catch (ArgumentException) { }
      catch (OverflowException) { }

      Boolean success = BigInteger.TryParse(@string,
                                            NumberStyles.Any,
                                            CultureInfo.InvariantCulture,
                                            out BigInteger bigInt);

      _ = bigInt;

      if (success && !@string.Contains(",")) return Double.PositiveInfinity;

      return Double.NaN;
    }

    public static Double ToNumber(Double @double) {
      if (TaggedBools.IsTrue(@double)) return 1.0;

      if (TaggedBools.IsFalse(@double)) return 0.0;

      return @double;
    }

    public static Int32 ToInt32(Double @double) => (Int32)(UInt32)@double;

    public static Int32 ToInt32(BoxedValue boxedValue) => (Int32)(UInt32)ToNumber(boxedValue);

    public static UInt32 ToUInt32(Double @double) => (UInt32)@double;

    public static UInt32 ToUInt32(BoxedValue boxedValue) => (UInt32)ToNumber(boxedValue);

    public static UInt16 ToUInt16(Double @double) => (UInt16)(UInt32)@double;

    public static UInt16 ToUInt16(BoxedValue boxedValue) => (UInt16)(UInt32)ToNumber(boxedValue);

    public static Int32 ToInteger(Double @double) {
      if (@double > 0x7fffffff) return 0x7fffffff;
      else return (Int32)(UInt32)@double;
    }

    public static Int32 ToInteger(BoxedValue boxedValue) => ToInteger(ToNumber(boxedValue));

    public static Boolean TryToIndex(Double value, out UInt32 index) {
      index = (UInt32)value;
      return index == value;
    }

    public static Boolean TryToIndex(String value, out UInt32 index)
      => UInt32.TryParse(value, out index);

    public static Boolean TryToIndex(BoxedValue value, out UInt32 index) {
      if (value.IsNumber) return TryToIndex(value.Number, out index);
      if (value.IsString) return TryToIndex(value.String, out index);

      index = default;
      return false;
    }
  }
}
