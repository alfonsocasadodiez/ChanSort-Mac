using System;
using System.IO;

namespace ChanSort.Avalonia
{
  /// <summary>
  /// Simple diagnostic log (macOS: ~/Library/Logs/ChanSort/ChanSort.log)
  /// </summary>
  public static class Log
  {
    private static readonly object sync = new();

    public static string FilePath { get; } = Path.Combine(
      OperatingSystem.IsMacOS() ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Logs") : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
      "ChanSort", "ChanSort.log");

    public static void Write(string message)
    {
      try
      {
        lock (sync)
        {
          Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
          File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
      }
      catch
      {
        // logging must never break the app
      }
    }
  }
}
