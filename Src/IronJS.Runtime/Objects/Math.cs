﻿using System;

namespace IronJS.Runtime {
  public class MathObject : CommonObject {
    public MathObject(Environment env) : base(env, env.Maps.Base, env.Prototypes.Object) { }

    public override String ClassName { get => "Math"; }
  }
}
