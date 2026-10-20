using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;

namespace ChanSort.Avalonia
{
  public partial class App : Application
  {
    public override void Initialize()
    {
      AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
      global::Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (_, e) =>
      {
        Log.Write("UI thread exception: " + e.Exception);
        e.Handled = true; // keep the app running, the error is shown/logged
      };

      if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
      {
        var main = new MainWindow();
        desktop.MainWindow = main;

        // file passed on the command line
        if (desktop.Args is { Length: > 0 })
          main.FileToOpenOnStartup = desktop.Args[0];
      }

      // file opened via Finder ("Open With" / drag onto dock icon)
      if (this.TryGetFeature(typeof(IActivatableLifetime)) is IActivatableLifetime activatable)
      {
        activatable.Activated += (_, e) =>
        {
          if (e is FileActivatedEventArgs fe)
          {
            foreach (var file in fe.Files)
            {
              var path = file.TryGetLocalPath();
              if (path != null && ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d && d.MainWindow is MainWindow mw)
                mw.OpenFileFromOutside(path);
              break;
            }
          }
        };
      }

      base.OnFrameworkInitializationCompleted();
    }

    private void About_Click(object sender, EventArgs e)
    {
      if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow is MainWindow mw)
        _ = mw.ShowAbout();
    }
  }
}
