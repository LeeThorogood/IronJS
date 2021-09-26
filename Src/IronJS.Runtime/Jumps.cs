using System;

namespace IronJS.Runtime {
  public class FinallyBreakJump : Exception {
    public Int32 LabelId { get; private set; }

    public FinallyBreakJump(Int32 labelId) : base() { LabelId = labelId; }
  }

  public class FinallyContinueJump : Exception {
    public Int32 LabelId { get; private set; }

    public FinallyContinueJump(Int32 labelId) : base() { LabelId = labelId; }
  }

  public class FinallyReturnJump : Exception {
    public BoxedValue Value { get; private set; }

    public FinallyReturnJump(BoxedValue value) : base() { Value = value; }
  }
}
