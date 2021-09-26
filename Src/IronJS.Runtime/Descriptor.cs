using System;

namespace IronJS.Runtime {
  public struct Descriptor {
    public BoxedValue Value;
    public UInt16 Attributes;
    public Boolean HasValue;

    public Boolean IsWritable {
      get => (Attributes & DescriptorAttrs.ReadOnly) == DescriptorAttrs.None;
    }
    public Boolean IsDeletable {
      get => (Attributes & DescriptorAttrs.DontDelete) == DescriptorAttrs.None;
    }
    public Boolean IsEnumerable {
      get => (Attributes & DescriptorAttrs.DontEnum) == DescriptorAttrs.None;
    }
  }
}
