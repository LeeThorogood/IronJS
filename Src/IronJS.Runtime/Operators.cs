using System;

namespace IronJS.Runtime {
  public static class Operators {
    /// <summary>
    /// Implements the unary `typeof` operator.
    /// </summary>
    public static String typeOf(BoxedValue value) {
      if (value.IsNumber) return "number";
      if (value.IsNull) return "object";
      return TypeTags.GetName(value.Tag);
    }

    /// <summary>
    /// Implements the unary `!` operator.
    /// </summary>
    public static Boolean not(BoxedValue value) => !TypeConverter.ToBoolean(value);

    /// <summary>
    /// Implements the unary `~` operator.
    /// </summary>
    public static Double bitCmpl(BoxedValue value)
      => ~TypeConverter.ToInt32(TypeConverter.ToNumber(value));

    /// <summary>
    /// Implements the unary `+` operator.
    /// </summary>
    public static BoxedValue plus(BoxedValue value)
      => BoxedValue.Box(TypeConverter.ToNumber(value));

    /// <summary>
    /// Implements the unary `-` operator.
    /// </summary>
    public static BoxedValue minus(BoxedValue o)
      => BoxedValue.Box((Double)(TypeConverter.ToNumber(o) * -1.0));

    /// <summary>
    /// Implements the binary `in` operator.
    /// </summary>
    public static Boolean @in(Environment env, BoxedValue l, BoxedValue r) {
      if (!r.IsObject) return env.RaiseTypeError<Boolean>("Right operand is not a object");
      if (TypeConverter.TryToIndex(l, out UInt32 index)) return r.Object.Has(index);
      String name = TypeConverter.ToString(l);
      return r.Object.Has(name);
    }

    /// <summary>
    /// Implements the binary `instanceof` operator.
    /// </summary>
    public static Boolean instanceOf(Environment env, BoxedValue l, BoxedValue r) {
      if (!r.IsFunction) return env.RaiseTypeError<Boolean>("Right operand is not a function");
      if (!l.IsObject) return false;
      return r.Func.HasInstance(l.Object);
    }

    /// <summary>
    /// Supports the binary comparison operators.
    /// </summary>
    private static Boolean Compare(BoxedValue l,
                                   BoxedValue r,
                                   Boolean rightToLeft,
                                   Func<String, String, Boolean> stringCompare,
                                   Func<Double, Double, Boolean> numberCompare) {

      if ((l.Tag == TypeTags.String || l.Tag == TypeTags.SuffixString) &&
          (r.Tag == TypeTags.String || r.Tag == TypeTags.SuffixString))
        return stringCompare(l.Clr.ToString(), r.Clr.ToString());

      if (l.IsNumber && r.IsNumber) return numberCompare(l.Number, r.Number);

      BoxedValue lPrim, rPrim;
      if (rightToLeft) {
        rPrim = TypeConverter.ToPrimitive(r, DefaultValueHint.Number);
        lPrim = TypeConverter.ToPrimitive(l, DefaultValueHint.Number);
      } else {
        lPrim = TypeConverter.ToPrimitive(l, DefaultValueHint.Number);
        rPrim = TypeConverter.ToPrimitive(r, DefaultValueHint.Number);
      }


      if ((lPrim.Tag == TypeTags.String || lPrim.Tag == TypeTags.SuffixString) &&
          (rPrim.Tag == TypeTags.String || rPrim.Tag == TypeTags.SuffixString))
        return stringCompare(lPrim.Clr.ToString(), rPrim.Clr.ToString());


      Double lNum = TypeConverter.ToNumber(lPrim);
      Double rNum = TypeConverter.ToNumber(rPrim);
      return numberCompare(lNum, rNum);
    }

    /// <summary>
    /// Implements the binary `&lt;` operator.
    /// </summary>
    public static Boolean lt(BoxedValue l, BoxedValue r)
      => Compare(l, r, false, (a, b) => String.CompareOrdinal(a, b) < 0, (a, b) => a < b);

    /// <summary>
    /// Implements the binary `&lt;=` operator.
    /// </summary>
    public static Boolean ltEq(BoxedValue l, BoxedValue r)
      => Compare(l, r, true, (a, b) => String.CompareOrdinal(a, b) <= 0, (a, b) => a <= b);


    /// <summary>
    /// Implements the binary `&gt;` operator.
    /// </summary>
    public static Boolean gt(BoxedValue l, BoxedValue r)
      => Compare(l, r, true, (a, b) => String.CompareOrdinal(a, b) > 0, (a, b) => a > b);

    /// <summary>
    /// Implements the binary `&gt;=` operator.
    /// </summary>
    public static Boolean gtEq(BoxedValue l, BoxedValue r)
      => Compare(l, r, false, (a, b) => String.CompareOrdinal(a, b) >= 0, (a, b) => a >= b);

    /// <summary>
    /// Implements the binary `==` operator.
    /// </summary>
    public static Boolean eq(BoxedValue l, BoxedValue r) {
      if (same(l, r)) return true;

      if (l.IsNull && r.IsUndefined || l.IsUndefined && r.IsNull) return true;

      if (l.IsNumber && r.IsString) return l.Number == TypeConverter.ToNumber(r);

      if (l.IsString && r.IsNumber) return TypeConverter.ToNumber(l) == r.Number;

      if (l.Tag == TypeTags.Bool) return eq(BoxedValue.Box(TypeConverter.ToNumber(l)), r);

      if (r.Tag == TypeTags.Bool) return eq(l, BoxedValue.Box(TypeConverter.ToNumber(r)));

      if (l.Tag >= TypeTags.Object) {
        if (r.Tag == TypeTags.SuffixString || r.Tag == TypeTags.String || r.IsNumber)
          return eq(TypeConverter.ToPrimitive(l.Object, DefaultValueHint.None), r);

        return false;
      }

      if (r.Tag >= TypeTags.Object) {
        if (l.Tag == TypeTags.SuffixString || l.Tag == TypeTags.String || l.IsNumber)
          return eq(l, TypeConverter.ToPrimitive(r.Object, DefaultValueHint.None));

        return false;
      }

      return false;
    }

    /// <summary>
    /// Implements the binary `!=` operator.
    /// </summary>
    public static Boolean notEq(BoxedValue l, BoxedValue r) => !eq(l, r);

    /// <summary>
    /// Implements the binary `===` operator.
    /// </summary>
    public static Boolean same(BoxedValue l, BoxedValue r) {
      if (l.IsNumber && r.IsNumber) return l.Number == r.Number;

      if ((l.Tag == TypeTags.String || l.Tag == TypeTags.SuffixString) &&
          (r.Tag == TypeTags.String || r.Tag == TypeTags.SuffixString))
        return l.Clr.ToString() == r.Clr.ToString();

      if (l.Tag == r.Tag) {
        switch (l.Tag) {
          case TypeTags.Undefined: return true;
          case TypeTags.Bool: return l.Bool == r.Bool;
          case TypeTags.Clr:
          case TypeTags.Function:
          case TypeTags.Object: return ReferenceEquals(l.Clr, r.Clr);
          default: return false;
        }
      }

      return false;
    }

    /// <summary>
    /// Implements the binary `!==` operator.
    /// </summary>
    public static Boolean notSame(BoxedValue l, BoxedValue r) => !same(l, r);

    /// <summary>
    /// Implements the binary `+` operator.
    /// </summary>
    public static BoxedValue add(BoxedValue l, BoxedValue r) {
      if (l.Tag == TypeTags.SuffixString) {
        SuffixString newString =
          SuffixString.Concat(l.SuffixString, TypeConverter.ToString(TypeConverter.ToPrimitive(r)));

        return BoxedValue.Box(newString);
      }

      if (l.Tag == TypeTags.String || r.Tag == TypeTags.String || r.Tag == TypeTags.SuffixString) {
        SuffixString newString =
          SuffixString.Concat(TypeConverter.ToString(TypeConverter.ToPrimitive(l)),
                              TypeConverter.ToString(TypeConverter.ToPrimitive(r)));

        return BoxedValue.Box(newString);
      }

      BoxedValue lPrim = TypeConverter.ToPrimitive(l);
      BoxedValue rPrim = TypeConverter.ToPrimitive(r);

      if (lPrim.Tag == TypeTags.SuffixString) {
        SuffixString newString =
          SuffixString.Concat(lPrim.SuffixString, TypeConverter.ToString(rPrim));

        return BoxedValue.Box(newString);
      }

      if (lPrim.Tag == TypeTags.String ||
          rPrim.Tag == TypeTags.String ||
          rPrim.Tag == TypeTags.SuffixString) {
        SuffixString newString =
          SuffixString.Concat(TypeConverter.ToString(lPrim), TypeConverter.ToString(rPrim));

        return BoxedValue.Box(newString);
      }

      return BoxedValue.Box(TypeConverter.ToNumber(lPrim) + TypeConverter.ToNumber(rPrim));
    }
  }
}
