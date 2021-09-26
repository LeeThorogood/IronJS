using System;
using System.Text;

namespace IronJS.Runtime {
  /// <summary>
  /// 
  /// </summary>
  public class SuffixString {
    // The use of the Parent and Suffixes fields allows the implementation
    // to not leak memory when a longer String is reclaimed by garbage collection 
    // but the longer strings chars are still in the buffer.
    //[<DefaultValue>] val mutable Parent : SuffixString
    //[<DefaultValue>] val mutable Suffixes : Int32

    public Int32 Length;
    public StringBuilder Builder;
    public String Cached;

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override String ToString() => Cached ?? Builder.ToString(0, Length);

    /*
    override x.Finalize() =
    if x.Parent !== null then
        x.Parent.Suffixes <- x.Parent.Suffixes - 1
        if x.Suffixes = 0 
        then x.Builder.Remove(x.Parent.Length, x.Length-x.Parent.Length) |> ignore
        else x.Parent.Suffixes <- x.Parent.Suffixes + x.Suffixes
    */

    /// <summary>
    /// 
    /// </summary>
    /// <param name="current"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static SuffixString Concat(SuffixString current, Object right) {
      SuffixString @new;
      StringBuilder builder;

      String value = right.ToString();

      if (current.Length == current.Builder.Length) {
        builder = current.Builder.Append(value);
      } else {
        Int32 newLength = current.Length + value.Length;
        builder = new StringBuilder(current.ToString(), newLength);
        builder.Append(value);
      }

      @new = new SuffixString();
      @new.Length = builder.Length;
      @new.Builder = builder;

      return @new;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static SuffixString Concat(Object left, Object right) {
      String leftValue = left.ToString();
      String rightValue = right.ToString();
      SuffixString @new = new SuffixString();

      @new.Length = leftValue.Length + rightValue.Length;
      @new.Builder = new StringBuilder(leftValue, @new.Length);
      @new.Builder.Append(rightValue);

      return @new;
    }
  }
}
