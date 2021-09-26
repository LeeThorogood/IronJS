using System;

namespace IronJS.Runtime {
  public class StringObject : ValueObject {
    public StringObject(Environment env) : base(env, env.Maps.String, env.Prototypes.String) { }

    public override String ClassName { get => "String"; }

    public override BoxedValue Get(UInt32 @uint) {
      Int32 @int = (Int32)@uint;
      String @string = Value.Value.String;

      if (Value.HasValue && @int < @string.Length) return BoxedValue.Box(@string[@int].ToString());

      return Undefined.Boxed;
    }

    public override BoxedValue Get(String @string) {
      if (Int32.TryParse(@string, out Int32 @int)) {
        if (@int >= 0) return Get((UInt32)@int);

        return Undefined.Boxed;
      }

      return base.Get(@string);
    }
  }
}
