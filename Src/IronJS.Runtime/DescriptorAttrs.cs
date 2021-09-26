using System;

namespace IronJS.Runtime {
  public static class DescriptorAttrs {
    public const UInt16 None             = 0;
    public const UInt16 ReadOnly         = 1;
    public const UInt16 DontEnum         = 2;
    public const UInt16 DontDelete       = 4;
    public const UInt16 DontEnumOrDelete = DontEnum | DontDelete;
    public const UInt16 Immutable        = ReadOnly | DontEnum | DontDelete;
  }
}
