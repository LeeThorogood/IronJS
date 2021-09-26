using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using IronJS.Runtime;
using Microsoft.Win32;
using WinFormsRichhTextBox = System.Windows.Forms.RichTextBox;

namespace DebugConsole {
  public partial class MainWindow : Window {
    const String CACHE_FILE = "source.cache";
    const String CACHE_FILE_LIST = "files.cache";
    readonly HashSet<Object> alreadyRendered = new HashSet<Object>();
    readonly ManualResetEvent breakpointEvent = new ManualResetEvent(true);
    readonly Dictionary<Type, Color> typeColors = new Dictionary<Type, Color>();
    readonly List<TextRange> currentBreakPoints = new List<TextRange>();

    Thread jsThread;
    TextRange currentHighlight;
    IronJS.Hosting.CSharp.Context context;
    String lastFileBrowserPath = null;

    public MainWindow() {
      InitializeComponent();
      Width = 1280;
      Height = 720;

      loadCacheFile();

      IronJS.Support.Debug.registerExprPrinter(expressionTreePrinter);
      IronJS.Support.Debug.registerAstPrinter(syntaxTreePrinter);

      typeColors.Add(typeof(Double), Colors.DarkOrchid);
      typeColors.Add(typeof(String), Colors.Brown);
      typeColors.Add(typeof(Boolean), Colors.DarkBlue);
      typeColors.Add(typeof(Undefined), Colors.DarkGoldenrod);
      typeColors.Add(typeof(CommonObject), Colors.DarkGreen);
      typeColors.Add(typeof(Object), Colors.Black);

      Console.SetOut(new CallbackWriter(printConsoleText));
      DataObject.AddPastingHandler(inputText, inputText_OnPaste);

      createEnvironment();

      Closing += MainWindow_Closing;

      loadFilesCache();
    }

    void loadFilesCache() {
      try {
        String[] files = File.ReadAllLines(CACHE_FILE_LIST);
        foreach (String file in files) {
          if (file.Trim() != "") addNewFilePanel(file);
        }
      }
      catch { }
    }

    void inputText_OnPaste(Object sender, DataObjectPastingEventArgs e) {
      // This little hack removes the RTF formatting on the input text

      if (!e.SourceDataObject.GetDataPresent(DataFormats.Rtf, true)) return;

      String rtf = e.SourceDataObject.GetData(DataFormats.Rtf) as String;
      WinFormsRichhTextBox rtb = new WinFormsRichhTextBox();
      rtb.Rtf = rtf;

      e.DataObject = new DataObject(DataFormats.UnicodeText, rtb.Text);
    }

    void MainWindow_Closing(Object sender, System.ComponentModel.CancelEventArgs e) {
      if (jsThread != null) jsThread.Abort();
    }

    void loadCacheFile() {
      try {
        doWithoutTextChange(() => {
          inputText.Document = new FlowDocument();

          foreach (String line in File.ReadLines(CACHE_FILE)) {
            inputText.Document.Blocks.Add(new Paragraph(new Run(line)));
          }
        });
      }
      catch { }
    }

    void displayLocalVariables(Dictionary<String, Object> locals) {
      Locals.Items.Clear();

      foreach (KeyValuePair<String, Object> kvp in locals) {
        alreadyRendered.Clear();
        Locals.Items.Add(renderProperty(kvp.Key, kvp.Value));
      }

      tabs.SelectedIndex = 4;
    }

    void displayGlobalVariables(CommonObject globals) {
      if (!(disableGlobalsCheckbox.IsChecked ?? false)) {
        EnvironmentVariables.Items.Clear();

        foreach (TreeViewItem item in renderObjectProperties(globals)) {
          alreadyRendered.Clear();
          EnvironmentVariables.Items.Add(item);
        }
      }
    }

    void highlightBreakpoint(Run run, Brush brush) {
      if (run.Text.Trim().StartsWith("#bp")) {
        currentHighlight = new TextRange(run.ContentStart, run.ContentEnd);
        currentHighlight.ApplyPropertyValue(TextElement.BackgroundProperty, brush);
        Rect position = run.ContentStart.GetCharacterRect(LogicalDirection.Forward);
        inputScroller.ScrollToVerticalOffset(position.Top);
        currentBreakPoints.Add(currentHighlight);
      }
    }

    void doForEachLine(Action<Run> foreachLine) {
      if (inputText.Document == null) return;

      doWithoutTextChange(() => {
        TextPointer navigator = inputText.Document.ContentStart;

        while (navigator.CompareTo(inputText.Document.ContentEnd) < 0) {
          TextPointerContext context = navigator.GetPointerContext(LogicalDirection.Backward);
          if (context == TextPointerContext.ElementStart && navigator.Parent is Run)
            foreachLine(navigator.Parent as Run);

          navigator = navigator.GetNextContextPosition(LogicalDirection.Forward);
        }
      });
    }

    void highlightActiveBreakPoint(Int32 line) {
      Int32 currentLine = 0;

      doForEachLine((run) => {
        ++currentLine;
        if (currentLine == line) {
          highlightBreakpoint(run, Brushes.Salmon);
        }
      });
    }

    void breakPoint(Int32 line, Int32 _, Dictionary<String, Object> scope) {
      //Play a sound
      System.Media.SystemSounds.Beep.Play();

      //Reset breakpoint event
      breakpointEvent.Reset();

      //
      Dispatcher.Invoke(new Action(() => {
        breakPointLabel.Content = "Hit breakpoint on line " + line;
        continueButton.Visibility = Visibility.Visible;
        displayGlobalVariables(context.Globals);
        displayLocalVariables(scope);
        highlightActiveBreakPoint(line);
      }));

      //Wait for UI thread to set event
      breakpointEvent.WaitOne();

      //
      Dispatcher.Invoke(new Action(() => {
        breakPointLabel.Content = "Running...";
        continueButton.Visibility = Visibility.Collapsed;
      }));
    }

    void expressionTreePrinter(String expressionTree) {
      Dispatcher.Invoke(new Action(() => expressionTreeOutput.AppendText(expressionTree)));
    }

    void syntaxTreePrinter(String syntaxTree) {
      Dispatcher.Invoke(new Action(() => syntaxTreeOutput.AppendText(syntaxTree)));
    }

    void printConsoleText(String value) {
      Dispatcher.Invoke(new Action(() => consoleOutput.AppendText(value)));
    }

    IEnumerable<TreeViewItem> renderObjectProperties(CommonObject jsObject) {
      if (jsObject != null && !alreadyRendered.Contains(jsObject)) {
        if (jsObject.Prototype != null) {
          yield return renderProperty("[[Prototype]]", jsObject.Prototype);
        }

        if (jsObject is ValueObject) {
          Object value = (jsObject as ValueObject).Value.Value.ClrBoxed;
          yield return renderProperty("[[Value]]", value);
        }

        alreadyRendered.Add(jsObject);

        foreach (KeyValuePair<String, Object> member in jsObject.Members) {
          yield return renderProperty(member.Key, member.Value);
        }

        if (jsObject is ArrayObject) {
          ArrayObject arrayObject = jsObject as ArrayObject;
          for (UInt32 i = 0u; i < arrayObject.Length; ++i) {
            yield return renderProperty("[" + i + "]", arrayObject.Get(i).ClrBoxed);
          }
        }
      }
    }

    TreeViewItem renderProperty(String name, Object value) {

      TreeViewItem item = new TreeViewItem();
      HeaderedItemsControl header = item;

      if (value != null && !typeColors.TryGetValue(value.GetType(), out Color color)) {
        _ = color;
        if (value is CommonObject) color = typeColors[typeof(CommonObject)];
        else color = typeColors[typeof(Object)];
      } else {
        color = typeColors[typeof(Object)];
      }

      header.Foreground = new SolidColorBrush(color);

      if (value is CommonObject) {
        CommonObject commonObject = value as CommonObject;
        item.Header = name + ": " + commonObject.ClassName;

        if (alreadyRendered.Contains(value)) {
          item.Header += " <recursive>";
        } else {
          foreach (TreeViewItem property in renderObjectProperties(commonObject)) {
            item.Items.Add(property);
          }
        }
      } else if (value is String) {
        item.Header = name + ": \"" + value + "\"";
      } else {
        item.Header = name + ": " + TypeConverter.ToString(BoxingUtils.JsBox(value));
      }

      return item;
    }

    String getAllInputText() {
      TextRange tr = new TextRange(inputText.Document.ContentStart,
                                   inputText.Document.ContentEnd);
      return tr.Text;
    }

    void printException(Exception exn) {
      tabs.SelectedIndex = 3;
      lastStatementOutput.Text = exn.ToString();
    }

    void doWithoutTextChange(Action action) {
      inputText.TextChanged -= inputText_TextChanged;
      action();
      inputText.TextChanged += inputText_TextChanged;
    }

    void runButton_Click(Object sender, RoutedEventArgs e) {
      runSource(getAllInputText());
    }

    void runSource(String source) {
      consoleOutput.Text = String.Empty;
      expressionTreeOutput.Text = String.Empty;
      syntaxTreeOutput.Text = String.Empty;
      lastStatementOutput.Text = String.Empty;

      var sources = new Stack<String>();
      sources.Push(source);
      runSources(sources);
    }

    void runSources(Stack<String> sources) {
      if (jsThread != null) return;

      doWithoutTextChange(() => {
        inputText.Focusable = false;
        inputText.Background = Brushes.WhiteSmoke;
      });

      jsThread = new Thread(() => {
        try {
          if (sources.Count > 0) {
            Dispatcher.Invoke(new Action(() => breakPointLabel.Content = "Running..."));

            dynamic result = context.Execute(sources.Pop());
            String resultAsString = TypeConverter.ToString(BoxingUtils.JsBox(result));

            Dispatcher.Invoke(new Action(() =>
                consoleOutput.Text += "\r\nLast statement: " + resultAsString));
          }
        }
        catch (Exception exn) {
          Dispatcher.Invoke(new Action(() => printException(exn)));
        }
        finally {
          Dispatcher.Invoke(new Action(() => {
            doWithoutTextChange(() => {
              inputText.Focusable = true;
              inputText.Background = Brushes.Transparent;
            });

            displayGlobalVariables(context.Globals);
          }));

          jsThread = null;

          if (sources.Count > 0) {
            Dispatcher.Invoke(new Action(() => {
              runSources(sources);
            }));
          } else {
            Dispatcher.Invoke(new Action(() => breakPointLabel.Content = ""));
          }
        }
      });

      jsThread.Start();
    }

    void continueButton_Click(Object sender, RoutedEventArgs e) {
      // We need to reset the color of the
      // previously hit breakpoint textrange
      if (currentHighlight != null) {
        currentHighlight.ApplyPropertyValue(TextElement.BackgroundProperty, Brushes.Transparent);
      }

      // Allow the executing thread to continue
      breakpointEvent.Set();
    }

    void inputText_TextChanged(Object sender, TextChangedEventArgs e)
      => File.WriteAllText(CACHE_FILE, getAllInputText());

    void createEnvironment() {
      context = new IronJS.Hosting.CSharp.Context();
      context.CreatePrintFunction();
      //TODO: context.Environment.BreakPoint = breakPoint;
    }

    void resetEnvironment_Click(Object sender, RoutedEventArgs e) {
      tabs.SelectedIndex = 2;
      createEnvironment();
      displayGlobalVariables(context.Globals);
    }

    List<String> getAllLoadedFiles() {
      List<String> files = new List<String>();

      foreach (Object child in filesPanel.Children) {
        if (child is StackPanel)
          foreach (Object subChild in (child as StackPanel).Children) {
            if (subChild is TextBox) {
              TextBox tb = subChild as TextBox;
              if (tb.Text != null && tb.Text.Trim() != "")
                files.Add(tb.Text.Trim() + "\r\n");
            }
          }
      }

      return files;
    }

    void saveFileListCache()
      => File.WriteAllText(CACHE_FILE_LIST, String.Concat(getAllLoadedFiles()));

    void addNewFileButton_Click(Object sender, RoutedEventArgs e) => addNewFilePanel(null);

    void addNewFilePanel(String initialFile) {
      StackPanel panel = new StackPanel();
      panel.Orientation = Orientation.Horizontal;

      Button removeButton = new Button();
      removeButton.Content = "Remove";
      removeButton.Width = 60;
      removeButton.Margin = new Thickness(5);

      TextBox filePathBox = new TextBox();
      filePathBox.Width = 400;
      filePathBox.Margin = new Thickness(5);

      Button browseButton = new Button();
      browseButton.Content = "Browse";
      browseButton.Width = 50;
      browseButton.Margin = new Thickness(5);

      Button runButton = new Button();
      runButton.Content = "Run";
      runButton.Width = 50;
      runButton.Margin = new Thickness(5);

      panel.Children.Add(removeButton);
      panel.Children.Add(filePathBox);
      panel.Children.Add(browseButton);
      panel.Children.Add(runButton);

      filesPanel.Children.Add(panel);

      removeButton.Click += (s, args) => {
        filesPanel.Children.Remove(panel);
        saveFileListCache();
      };

      browseButton.Click += (s, args) => {
        OpenFileDialog dlg = new OpenFileDialog();
        dlg.DefaultExt = ".js";

        if (!String.IsNullOrEmpty(lastFileBrowserPath))
          dlg.InitialDirectory = lastFileBrowserPath;

        if (dlg.ShowDialog() ?? false) {
          lastFileBrowserPath = Path.GetDirectoryName(dlg.FileName);
          filePathBox.Text = dlg.FileName;
        }

        saveFileListCache();
      };

      runButton.Click += (s, args) => {
        String path = (filePathBox.Text ?? "").Trim();

        if (path != "")
          try {
            runSource(File.ReadAllText(path));
          }
          catch { }
      };

      if (initialFile != null) filePathBox.Text = initialFile;
    }

    void runAllFilesButton_Click(Object sender, RoutedEventArgs e) {
      Stack<String> sources =
        new Stack<String>(
          getAllLoadedFiles().Reverse<String>().Select(x => File.ReadAllText(x.Trim()))
        );
      runSources(sources);
    }
  }
}
