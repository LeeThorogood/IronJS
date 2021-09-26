using System;

namespace IronJS.Runtime {
  public class NumberObject : ValueObject {
    public NumberObject(Environment env) : base(env, env.Maps.Number, env.Prototypes.Number) { }

    public override String ClassName { get => "Number"; }
  }
}
