using System;
using Avalonia;

namespace ChanSort.Avalonia
{
  internal static class Program
  {
    [STAThread]
    public static void Main(string[] args)
    {
      BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
      return AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .With(new MacOSPlatformOptions { DisableDefaultApplicationMenuItems = false })
        .LogToTrace();
    }
  }
}
