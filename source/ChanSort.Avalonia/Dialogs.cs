using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ChanSort.Api;

namespace ChanSort.Avalonia
{
  /// <summary>
  /// Simple, code-only dialogs (message box, action box, text viewer, input box)
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

    #region CreateDialog()

    private static Window CreateDialog(string title, double width)
    {
      return new Window
      {
        Title = string.IsNullOrEmpty(title) ? "ChanSort" : title,
        Width = width,
        SizeToContent = SizeToContent.Height,
        CanResize = false,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        ShowInTaskbar = false,
      };
    }

    private static Window Owner(Window owner)
    {
      if (owner != null)
        return owner;
      if (Application.Current?.ApplicationLifetime is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime d)
        return d.MainWindow;
      return null;
    }

    private static async Task ShowDialog(Window dlg, Window owner)
    {
      owner = Owner(owner);
      if (owner != null && owner.IsVisible)
        await dlg.ShowDialog(owner);
      else
      {
        var tcs = new TaskCompletionSource<bool>();
        dlg.Closed += (_, _) => tcs.TrySetResult(true);
        dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        dlg.Show();
        await tcs.Task;
      }
    }

    #endregion

    #region MessageBox()

    public static async Task<View.DialogResult> MessageBox(Window owner, string msg, string caption = "", View.MessageBoxButtons buttons = View.MessageBoxButtons.OK, View.MessageBoxIcon icon = View.MessageBoxIcon.None)
    {
      var dlg = CreateDialog(caption, 480);
      var result = View.DialogResult.Cancel;
      if (buttons == View.MessageBoxButtons.OK)
        result = View.DialogResult.OK;
      else if (buttons == View.MessageBoxButtons.YesNo)
        result = View.DialogResult.No;

      var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
      void AddButton(string text, View.DialogResult res, bool isDefault = false, bool isCancel = false)
      {
        var btn = new Button { Content = text, MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center, IsDefault = isDefault, IsCancel = isCancel };
        btn.Click += (_, _) => { result = res; dlg.Close(); };
        buttonPanel.Children.Add(btn);
      }

      switch (buttons)
      {
        case View.MessageBoxButtons.OK: AddButton("OK", View.DialogResult.OK, true, true); break;
        case View.MessageBoxButtons.OKCancel: AddButton("Cancel", View.DialogResult.Cancel, false, true); AddButton("OK", View.DialogResult.OK, true); break;
        case View.MessageBoxButtons.YesNo: AddButton("No", View.DialogResult.No, false, true); AddButton("Yes", View.DialogResult.Yes, true); break;
        case View.MessageBoxButtons.YesNoCancel: AddButton("Cancel", View.DialogResult.Cancel, false, true); AddButton("No", View.DialogResult.No); AddButton("Yes", View.DialogResult.Yes, true); break;
        case View.MessageBoxButtons.RetryCancel: AddButton("Cancel", View.DialogResult.Cancel, false, true); AddButton("Retry", View.DialogResult.Retry, true); break;
        case View.MessageBoxButtons.AbortRetryIgnore: AddButton("Abort", View.DialogResult.Abort, false, true); AddButton("Retry", View.DialogResult.Retry); AddButton("Ignore", View.DialogResult.Ignore, true); break;
      }

      var body = new DockPanel { Margin = new Thickness(16), LastChildFill = true };
      DockPanel.SetDock(buttonPanel, Dock.Bottom);
      buttonPanel.Margin = new Thickness(0, 16, 0, 0);
      body.Children.Add(buttonPanel);
      body.Children.Add(new ScrollViewer
      {
        MaxHeight = 500,
        Content = new SelectableTextBlock { Text = msg ?? "", TextWrapping = TextWrapping.Wrap }
      });
      dlg.Content = body;

      await ShowDialog(dlg, owner);
      return result;
    }

    #endregion

    #region ShowText()

    public static async Task ShowText(Window owner, string title, string text, bool monospace = false)
    {
      var dlg = new Window
      {
        Title = title ?? "ChanSort",
        Width = 760,
        Height = 520,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        ShowInTaskbar = false
      };
      var ok = new Button { Content = "OK", MinWidth = 80, IsDefault = true, IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
      ok.Click += (_, _) => dlg.Close();
      var box = new TextBox
      {
        Text = text ?? "",
        IsReadOnly = true,
        AcceptsReturn = true,
        TextWrapping = monospace ? TextWrapping.NoWrap : TextWrapping.Wrap,
      };
      if (monospace)
        box.FontFamily = new FontFamily("Menlo, Consolas, monospace");
      ScrollViewer.SetHorizontalScrollBarVisibility(box, global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);
      var panel = new DockPanel { Margin = new Thickness(12) };
      DockPanel.SetDock(ok, Dock.Bottom);
      panel.Children.Add(ok);
      panel.Children.Add(box);
      dlg.Content = panel;
      await ShowDialog(dlg, owner);
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
      var dlg = CreateDialog(title, 420);
      string result = null;
      var box = new TextBox { Text = value ?? "" };
      var ok = new Button { Content = "OK", MinWidth = 80, IsDefault = true };
      var cancel = new Button { Content = "Cancel", MinWidth = 80, IsCancel = true };
      ok.Click += (_, _) => { result = box.Text; dlg.Close(); };
      cancel.Click += (_, _) => dlg.Close();
      var panel = new StackPanel { Margin = new Thickness(16), Spacing = 8 };
      panel.Children.Add(new TextBlock { Text = prompt, TextWrapping = TextWrapping.Wrap });
      panel.Children.Add(box);
      panel.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0), Children = { cancel, ok } });
      dlg.Content = panel;
      dlg.Opened += (_, _) => { box.Focus(); box.SelectAll(); };
      await ShowDialog(dlg, owner);
      return result;
    }

    #endregion

    #region ActionBox()

    /// <summary>
    /// Shows a message and a vertical list of buttons. Returns the result value of the selected action or cancelResult.
    /// </summary>
    public static async Task<int> ActionBox(Window owner, string message, IList<(string text, int result)> actions, int cancelResult)
    {
      var dlg = CreateDialog("ChanSort", 520);
      var result = cancelResult;
      var panel = new StackPanel { Margin = new Thickness(16), Spacing = 6 };
      if (!string.IsNullOrEmpty(message))
        panel.Children.Add(new SelectableTextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) });
      bool first = true;
      foreach (var action in actions)
      {
        var btn = new Button
        {
          Content = new TextBlock { Text = action.text, TextWrapping = TextWrapping.Wrap },
          HorizontalAlignment = HorizontalAlignment.Stretch,
          HorizontalContentAlignment = HorizontalAlignment.Left,
          Padding = new Thickness(12, 8),
          IsDefault = first,
        };
        if (first)
          btn.Classes.Add("accent");
        first = false;
        var res = action.result;
        btn.Click += (_, _) => { result = res; dlg.Close(); };
        panel.Children.Add(btn);
      }
      dlg.KeyDown += (_, e) => { if (e.Key == Key.Escape) dlg.Close(); };
      dlg.Content = panel;
      await ShowDialog(dlg, owner);
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
