using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using ChanSort.Api;

namespace ChanSort.Avalonia
{
  /// <summary>
  /// Dialogs in the style of macOS alerts. They are shown as sheets inside the main window
  /// (or as a separate window when the main window isn't available).
  /// </summary>
  public static class Dialogs
  {
    #region RunSync()

    /// <summary>
    /// The loaders call into Api.View synchronously. When called from a worker thread, the dialog is shown on the UI thread
    /// and the worker waits for the result. When called on the UI thread, a nested dispatcher loop is used.
    /// </summary>
    public static T RunSync<T>(Func<Task<T>> asyncFunc)
    {
      if (!Dispatcher.UIThread.CheckAccess())
        return Dispatcher.UIThread.InvokeAsync(asyncFunc).GetAwaiter().GetResult();

      using var cts = new CancellationTokenSource();
      var task = asyncFunc();
      task.ContinueWith(_ => cts.Cancel(), TaskScheduler.FromCurrentSynchronizationContext());
      Dispatcher.UIThread.MainLoop(cts.Token);
      return task.GetAwaiter().GetResult();
    }

    #endregion

    #region ShowModal()

    private static Window Owner(Window owner)
    {
      if (owner != null)
        return owner;
      if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d)
        return d.MainWindow;
      return null;
    }

    /// <summary>
    /// Shows the content created by build() modally. build() receives the function that closes the dialog
    /// and returns the content and the control that should get the keyboard focus.
    /// </summary>
    private static async Task ShowModal(Window owner, string title, double width, Func<Action, (Control content, Control focus)> build)
    {
      owner = Owner(owner);
      var tcs = new TaskCompletionSource<bool>();
      Action close = null;
      var (content, focus) = build(() => close?.Invoke());

      Log.Write($"dialog '{title}' opening");
      if (owner is MainWindow mw && mw.IsVisible)
      {
        var handle = mw.ShowSheet(content, width, focus);
        close = () =>
        {
          mw.CloseSheet(handle);
          Log.Write($"dialog '{title}' closed");
          tcs.TrySetResult(true);
        };
      }
      else
      {
        var dlg = new Window
        {
          Title = string.IsNullOrEmpty(title) ? "ChanSort" : title,
          Width = width,
          SizeToContent = SizeToContent.Height,
          CanResize = false,
          ShowInTaskbar = false,
          WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
          Content = new Border { Padding = new Thickness(20), Child = content }
        };
        dlg[!Window.BackgroundProperty] = new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("CsSheetBackground");
        close = () => dlg.Close();
        dlg.Closed += (_, _) =>
        {
          Log.Write($"dialog '{title}' closed");
          tcs.TrySetResult(true);
        };
        dlg.Opened += (_, _) => (focus ?? content).Focus();
        if (owner != null && owner.IsVisible)
          _ = dlg.ShowDialog(owner);
        else
          dlg.Show();
      }
      await tcs.Task;
    }

    #endregion

    #region Building blocks

    private static Control AppIcon(double size = 56)
    {
      try
      {
        using var stream = AssetLoader.Open(new Uri("avares://ChanSort/Assets/icon.png"));
        return new Image { Source = new Bitmap(stream), Width = size, Height = size, VerticalAlignment = VerticalAlignment.Top };
      }
      catch
      {
        return new Panel { Width = size, Height = size };
      }
    }

    private static Button PushButton(string text, bool isDefault, bool isCancel, Action onClick)
    {
      var btn = new Button
      {
        Content = text,
        MinWidth = 76,
        HorizontalContentAlignment = HorizontalAlignment.Center,
        IsDefault = isDefault,
        IsCancel = isCancel,
      };
      if (isDefault)
        btn.Classes.Add("accent");
      btn.Click += (_, _) => onClick();
      return btn;
    }

    /// <summary>
    /// macOS alert layout: app icon on the left, bold message and informative text on the right, buttons below
    /// </summary>
    private static Control AlertLayout(string title, string text, Control extra, Control buttons)
    {
      var right = new StackPanel { Spacing = 8 };
      if (!string.IsNullOrEmpty(title))
        right.Children.Add(new SelectableTextBlock { Text = title, Classes = { "alertTitle" } });
      if (!string.IsNullOrEmpty(text))
      {
        right.Children.Add(new ScrollViewer
        {
          MaxHeight = 320,
          HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
          Content = new SelectableTextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 12 }
        });
      }
      if (extra != null)
        right.Children.Add(extra);
      buttons.Margin = new Thickness(0, 10, 0, 0);
      right.Children.Add(buttons);

      var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
      var icon = AppIcon();
      icon.Margin = new Thickness(0, 0, 16, 0);
      grid.Children.Add(icon);
      Grid.SetColumn(right, 1);
      grid.Children.Add(right);
      return grid;
    }

    #endregion

    #region MessageBox()

    public static async Task<View.DialogResult> MessageBox(Window owner, string msg, string caption = "", View.MessageBoxButtons buttons = View.MessageBoxButtons.OK, View.MessageBoxIcon icon = View.MessageBoxIcon.None)
    {
      var result = buttons switch
      {
        View.MessageBoxButtons.OK => View.DialogResult.OK,
        View.MessageBoxButtons.YesNo => View.DialogResult.No,
        _ => View.DialogResult.Cancel
      };

      // like NSAlert: a short message becomes the bold title, a long one is shown as informative text below the caption
      msg = (msg ?? "").Trim();
      string title, text;
      if (msg.Length <= 160 && !msg.Contains('\n'))
      {
        title = msg;
        text = null;
      }
      else
      {
        title = string.IsNullOrEmpty(caption) ? "ChanSort" : caption;
        text = msg;
      }

      await ShowModal(owner, caption, 460, close =>
      {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        Button defaultButton = null;
        void Add(string label, View.DialogResult res, bool isDefault = false, bool isCancel = false)
        {
          var b = PushButton(label, isDefault, isCancel, () => { Log.Write($"message box: clicked '{label}'"); result = res; close(); });
          panel.Children.Add(b);
          if (isDefault)
            defaultButton = b;
        }

        switch (buttons)
        {
          case View.MessageBoxButtons.OK: Add("OK", View.DialogResult.OK, true, true); break;
          case View.MessageBoxButtons.OKCancel: Add("Cancel", View.DialogResult.Cancel, false, true); Add("OK", View.DialogResult.OK, true); break;
          case View.MessageBoxButtons.YesNo: Add("No", View.DialogResult.No, false, true); Add("Yes", View.DialogResult.Yes, true); break;
          case View.MessageBoxButtons.YesNoCancel: Add("Cancel", View.DialogResult.Cancel, false, true); Add("No", View.DialogResult.No); Add("Yes", View.DialogResult.Yes, true); break;
          case View.MessageBoxButtons.RetryCancel: Add("Cancel", View.DialogResult.Cancel, false, true); Add("Retry", View.DialogResult.Retry, true); break;
          case View.MessageBoxButtons.AbortRetryIgnore: Add("Abort", View.DialogResult.Abort, false, true); Add("Retry", View.DialogResult.Retry); Add("Ignore", View.DialogResult.Ignore, true); break;
        }
        return (AlertLayout(title, text, null, panel), defaultButton);
      });
      return result;
    }

    #endregion

    #region ShowText()

    public static async Task ShowText(Window owner, string title, string text, bool monospace = false)
    {
      await ShowModal(owner, title, 760, close =>
      {
        var ok = PushButton("OK", true, true, close);
        ok.HorizontalAlignment = HorizontalAlignment.Right;
        ok.Margin = new Thickness(0, 12, 0, 0);
        var box = new TextBox
        {
          Text = text ?? "",
          IsReadOnly = true,
          AcceptsReturn = true,
          TextWrapping = monospace ? TextWrapping.NoWrap : TextWrapping.Wrap,
          Height = 380,
          FontSize = 12,
        };
        if (monospace)
          box.FontFamily = new FontFamily("Menlo, Consolas, monospace");
        ScrollViewer.SetHorizontalScrollBarVisibility(box, global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);
        var panel = new DockPanel();
        var header = new TextBlock { Text = title ?? "ChanSort", Classes = { "alertTitle" }, Margin = new Thickness(0, 0, 0, 10) };
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(ok, Dock.Bottom);
        panel.Children.Add(header);
        panel.Children.Add(ok);
        panel.Children.Add(box);
        return (panel, ok);
      });
    }

    public static string HtmlToText(string html)
    {
      if (string.IsNullOrEmpty(html))
        return "";
      var s = Regex.Replace(html, @"<\s*(br|/p|/div|/h\d|/li|/tr)\s*/?>", "\n", RegexOptions.IgnoreCase);
      s = Regex.Replace(s, @"<\s*li[^>]*>", "• ", RegexOptions.IgnoreCase);
      s = Regex.Replace(s, @"<a\s[^>]*href\s*=\s*""([^""]*)""[^>]*>(.*?)</a>", "$2 ($1)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
      s = Regex.Replace(s, @"<(style|script)[^>]*>.*?</\1>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
      s = Regex.Replace(s, @"<[^>]+>", "");
      s = WebUtility.HtmlDecode(s);
      s = Regex.Replace(s, @"\n{3,}", "\n\n");
      return s.Trim();
    }

    #endregion

    #region InputBox()

    public static async Task<string> InputBox(Window owner, string title, string prompt, string value)
    {
      string result = null;
      await ShowModal(owner, title, 420, close =>
      {
        var box = new TextBox { Text = value ?? "" };
        box.AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(box.SelectAll, DispatcherPriority.Background);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(PushButton("Cancel", false, true, close));
        buttons.Children.Add(PushButton("OK", true, false, () => { result = box.Text; close(); }));
        return (AlertLayout(title, prompt, box, buttons), box);
      });
      return result;
    }

    #endregion

    #region ActionBox()

    /// <summary>
    /// Shows a message and a vertical list of buttons (like a macOS alert with many options).
    /// Returns the result value of the selected action or cancelResult.
    /// </summary>
    public static async Task<int> ActionBox(Window owner, string message, IList<(string text, int result)> actions, int cancelResult)
    {
      var result = cancelResult;
      await ShowModal(owner, "ChanSort", 480, close =>
      {
        var buttons = new StackPanel { Spacing = 6 };
        Button first = null;
        foreach (var action in actions)
        {
          var res = action.result;
          var isCancel = res == cancelResult || action.text == "Cancel";
          var btn = PushButton(action.text, first == null, false, () =>
          {
            Log.Write($"action box: clicked '{action.text}'");
            result = res;
            close();
          });
          btn.Content = new TextBlock { Text = action.text, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center };
          btn.HorizontalAlignment = HorizontalAlignment.Stretch;
          btn.Padding = new Thickness(12, 5);
          first ??= btn;
          buttons.Children.Add(btn);
        }

        var content = AlertLayout(message, null, null, buttons);
        content.KeyDown += (_, e) =>
        {
          if (e.Key == Key.Escape)
          {
            e.Handled = true;
            close();
          }
        };
        return (content, first);
      });
      return result;
    }

    #endregion
  }

  #region class ActionBoxDialog

  /// <summary>
  /// Implementation of Api.IActionBoxDialog, which is used by some loaders to let the user pick an option
  /// </summary>
  public class ActionBoxDialog : IActionBoxDialog
  {
    private readonly List<(string text, int result)> actions = new();

    public ActionBoxDialog(string message)
    {
      this.Message = message;
    }

    public string Message { get; set; }
    public int SelectedAction { get; private set; } = (int)View.DialogResult.Cancel;

    public void AddAction(string text, int result) => this.actions.Add((text, result));

    public void ShowDialog()
    {
      this.SelectedAction = Dialogs.RunSync(() => Dialogs.ActionBox(null, this.Message, this.actions, (int)View.DialogResult.Cancel));
    }

    public void Dispose()
    {
    }
  }

  #endregion
}
