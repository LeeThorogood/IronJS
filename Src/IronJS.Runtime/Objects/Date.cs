using System;

namespace IronJS.Runtime {
  public class DateObject : CommonObject {
    public DateObject(Environment env, DateTime date)
      : base(env, env.Maps.Base, env.Prototypes.Date) {

      Date = date;
    }

    public DateObject(Environment env, Int64 ticks) : this(env, TicksToDateTime(ticks)) { }

    public DateObject(Environment env, Double ticks) : this(env, TicksToDateTime(ticks)) { }

    private static readonly Int64 offset = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
    private static readonly Int64 tickScale = 10000L;

    public override String ClassName { get => "Date"; }

    public DateTime Date { get; set; }

    public Boolean HasValidDate { get => Date != DateTime.MinValue; }

    public override BoxedValue DefaultValue(DefaultValueHint hint) {
      if (hint == DefaultValueHint.None) hint = DefaultValueHint.String;

      return base.DefaultValue(hint);
    }

    public static DateTime TicksToDateTime(Int64 ticks)
      => new DateTime(ticks * tickScale + offset, DateTimeKind.Utc);

    public static DateTime TicksToDateTime(Double ticks) {
      if (Double.IsNaN(ticks) || Double.IsInfinity(ticks)) return DateTime.MinValue;

      return TicksToDateTime((Int64)ticks);
    }

    public static Int64 DateTimeToTicks(DateTime date)
      => (date.ToUniversalTime().Ticks - offset) / tickScale;
  }
}
