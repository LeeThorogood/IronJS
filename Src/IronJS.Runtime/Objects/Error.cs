using System;

namespace IronJS.Runtime {
  public class ErrorObject : CommonObject {
    public ErrorObject(Environment env) : base(env, env.Maps.Base, env.Prototypes.Error) { }

    public override String ClassName { get => "Error"; }
  }
}
