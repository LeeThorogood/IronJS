using System;
using System.Text;

namespace DebugConsole {
  class CallbackWriter : System.IO.TextWriter {
    readonly Action<String> callback;

    public CallbackWriter(Action<String> callback) => this.callback = callback;

    public override Encoding Encoding { get => Encoding.UTF8; }

    public override void Write(String value) => callback(value);

    public override void WriteLine(String value) => Write(value + "\n");
  }
}
