using System;

namespace IronJS.Runtime {
  /*
  internal static class SourceCodePrinter
  {
      private static String[] SplitLines(String input)
      {
          var cleanedInput = (input ?? "").Replace("\r\n", "\n").Replace("\r", "\n");
          return System.Text.RegularExpressions.Regex.Split(cleanedInput, "\n");
      }

      private static String LineNumber(Int32 padding, Int32 input)
      {
          return (input.ToString()).PadLeft(padding, '0');
      }

      private static String MakeArrow(Int32 length)
      {
          var builder = new StringBuilder(length).Insert(0, "-", length);
          return builder + "^";
      }

      internal static String PrettyPrintSourceError(Tuple<Int32, Int32> aboveBelow, Tuple<Int32, Int32> lineCol, String source)
      {
          var above = aboveBelow.Item1;
          var below = aboveBelow.Item2;
          var line = lineCol.Item1;
          var column = lineCol.Item2;

          //TODO: Implement Source Code Pretty Printer

          throw new NotImplementedException();
      }
  }
  */

  public abstract class Error : Exception {
    public Error(String message) : base(message ?? "<unknown>") { }
  }

  public class CompilerError : Error {
    public Tuple<Int32, Int32> Position { get; private set; }
    new public String Source { get; private set; }
    public String Path { get; private set; }

    public CompilerError(String message, Tuple<Int32, Int32> position, String source, String path)
      : base(message) {

      Position = position ?? Tuple.Create(0, 0);
      Source = source ?? "<unknown>";
      Path = path ?? "<unknown>";
    }

    //TODO: Implement Raise methods
  }

  public class RuntimeError : Error {
    public RuntimeError(String message) : base(message) { }

    //TODO: Implement Raise methods
  }

  public class UserError : Error {
    public BoxedValue Value { get; private set; }
    public Int32 Line { get; private set; }
    public Int32 Column { get; private set; }

    public UserError(BoxedValue value, Int32 line, Int32 column)
      : base(TypeConverter.ToString(value)) {

      Value = value;
      Line = line;
      Column = column;
    }
  }
}
