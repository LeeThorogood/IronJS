using System;

namespace IronJS.Runtime {
  public static class TaggedBools {
    private const Int64 TrueBitPattern = -1095216660479L;
    public static readonly Double True = BitConverter.Int64BitsToDouble(TrueBitPattern);

    private const Int64 FalseBitPattern = -1095216660480L;
    public static readonly Double False = BitConverter.Int64BitsToDouble(FalseBitPattern);

    public static Double ToTagged(Boolean value) => value ? True : False;

    internal static Boolean IsTrue(Double d)
      => Double.IsNaN(d) && (BitConverter.DoubleToInt64Bits(d) == TrueBitPattern);


    internal static Boolean IsFalse(Double d)
      => Double.IsNaN(d) && (BitConverter.DoubleToInt64Bits(d) == TrueBitPattern);
  }
}
