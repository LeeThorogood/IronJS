using System;
using System.Collections.Generic;

namespace IronJS.Runtime {
  public static class TypeTags {
    public const UInt32 Box = 0x00000000u;
    public const UInt32 Bool = 0xFFFFFF01u;
    public const UInt32 Number = 0xFFFFFF02u;
    public const UInt32 Clr = 0xFFFFFF03u;
    public const UInt32 String = 0xFFFFFF04u;
    public const UInt32 SuffixString = 0xFFFFFF05u;
    public const UInt32 Undefined = 0xFFFFFF06u;
    public const UInt32 Object = 0xFFFFFF07u;
    public const UInt32 Function = 0xFFFFFF08u;

    private static readonly Dictionary<UInt32, String> names = new Dictionary<UInt32, String> {
      { Box, "internal" },
      { Bool, "boolean" },
      { Number, "number" },
      { Clr, "clr" },
      { String, "String" },
      { SuffixString, "String" },
      { Undefined, "undefined" },
      { Object, "object" },
      { Function, "function" }
    };

    public static String GetName(UInt32 tag) => names[tag];
  }

  public static class TypeTag {
    static readonly Dictionary<Type, UInt32> map = new Dictionary<Type, UInt32>();

    static TypeTag() {
      map.Add(typeof(Boolean), TypeTags.Bool);
      map.Add(typeof(Double), TypeTags.Number);
      map.Add(typeof(String), TypeTags.String);
      map.Add(typeof(SuffixString), TypeTags.SuffixString);
      map.Add(typeof(Undefined), TypeTags.Undefined);
      map.Add(typeof(FunctionObject), TypeTags.Function);
      map.Add(typeof(ArrayObject), TypeTags.Object);
      map.Add(typeof(CommonObject), TypeTags.Object);
      map.Add(typeof(ValueObject), TypeTags.Object);
      map.Add(typeof(StringObject), TypeTags.Object);
      map.Add(typeof(NumberObject), TypeTags.Object);
      map.Add(typeof(ErrorObject), TypeTags.Object);
      map.Add(typeof(MathObject), TypeTags.Object);
      map.Add(typeof(BooleanObject), TypeTags.Object);
      map.Add(typeof(RegExpObject), TypeTags.Object);
      map.Add(typeof(DateObject), TypeTags.Object);
      map.Add(typeof(BoxedValue), TypeTags.Box);
    }

    public static UInt32 OfType(Type type) {

      if (map.TryGetValue(type, out UInt32 tag)) return tag;

      return type.IsSubclassOf(typeof(CommonObject)) ? TypeTags.Object : TypeTags.Clr;
    }

    public static UInt32 OfObject(Object @object) {
      if (@object == null) return TypeTags.Clr;

      return OfType(@object.GetType());
    }
  }
}
