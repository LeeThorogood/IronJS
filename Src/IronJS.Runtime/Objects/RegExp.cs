using System;
using System.Text.RegularExpressions;

namespace IronJS.Runtime {
  public class RegExpObject : CommonObject {
    private readonly Regex regExp;
    private readonly Boolean global;

    public RegExpObject(Environment env, String pattern, RegexOptions options, Boolean global)
      : base(env, env.Maps.RegExp, env.Prototypes.RegExp) {

      this.global = global;

      try {
        options = (options | RegexOptions.ECMAScript) & ~RegexOptions.Compiled;
        Tuple<RegexOptions, String> key = Tuple.Create(options, pattern);
        regExp =
          env.RegExpCache.Lookup(key, () => new Regex(pattern, options | RegexOptions.Compiled));
      }
      catch (ArgumentException ex) {
        env.RaiseSyntaxError<Object>(ex.Message);
        return;
      }
    }

    public RegExpObject(Environment env, String pattern)
      : this(env, pattern, RegexOptions.None, false) { }

    public override String ClassName { get => "RegExp"; }

    public Boolean Global { get => global; }

    public Boolean IgnoreCase {
      get => (regExp.Options & RegexOptions.IgnoreCase) == RegexOptions.IgnoreCase;
    }

    public Boolean MultiLine {
      get => (regExp.Options & RegexOptions.Multiline) == RegexOptions.Multiline;
    }

    public Regex RegExp { get => regExp; }
  }
}
