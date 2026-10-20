using System;
using Avalonia;

namespace ChanSort.Avalonia
{
  internal static class Program
  {
    [STAThread]
    public static void Main(string[] args)
    {
      Log.Write("--- ChanSort started, args: " + string.Join(" ", args));
      AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Write("Unhandled exception: " + e.ExceptionObject);
      System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) => Log.Write("Unobserved task exception: " + e.Exception);
      BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
      var builder = AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .With(new MacOSPlatformOptions { DisableDefaultApplicationMenuItems = false })
        .LogToTrace();
      // macOS: use the system font (San Francisco); elsewhere the bundled Inter font gives a consistent look
      return OperatingSystem.IsMacOS() ? builder : builder.WithInterFont();
    }
  }
}
