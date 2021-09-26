﻿using System;

using Microsoft.FSharp.Collections;

namespace IronJS.Runtime {
  using DynamicScope = FSharpList<Tuple<Int32, CommonObject>>;

  public delegate Delegate FunctionCompiler(FunctionObject self, Type type);
  public delegate Object GlobalCode(FunctionObject self, CommonObject @this);
  public delegate Object EvalCode(FunctionObject self,
                                  CommonObject @this,
                                  BoxedValue[] privateScope,
                                  BoxedValue[] sharedScope,
                                  DynamicScope scope);
}
