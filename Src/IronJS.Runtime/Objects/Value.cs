namespace IronJS.Runtime {
  public abstract class ValueObject : CommonObject {
    public Descriptor Value;

    public ValueObject(Environment env, Schema map, CommonObject prototype)
      : base(env, map, prototype) { }

    public static BoxedValue GetValue(CommonObject obj) {
      if (!(obj is ValueObject valObj))
        return obj.Env.RaiseTypeError<BoxedValue>("Cannot read the value of a non-value object.");

      return valObj.Value.Value;
    }
  }
}
