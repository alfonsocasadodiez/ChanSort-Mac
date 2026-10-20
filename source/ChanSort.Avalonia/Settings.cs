using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ChanSort.Avalonia
{
  /// <summary>
  /// User settings, stored as JSON in the platform's application data folder
  /// (macOS: ~/Library/Application Support/ChanSort/settings.json)
  /// </summary>
  public class Settings
  {
    public List<string> RecentFiles { get; set; } = new();
    public string LastDirectory { get; set; }
    public string Encoding { get; set; } = "windows-1252";
    public double WindowWidth { get; set; }
    public double WindowHeight { get; set; }
    public bool ShowWarningsAfterLoad { get; set; } = true;
    public bool CloseGapWhenRemoving { get; set; } = true;

    private static string FilePath
    {
      get
      {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChanSort");
        return Path.Combine(dir, "settings.json");
      }
    }

    public static Settings Load()
    {
      try
      {
        if (File.Exists(FilePath))
          return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
      }
      catch
      {
        // ignore corrupt settings
      }
      return new Settings();
    }

    public void Save()
    {
      try
      {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
      }
      catch
      {
        // not critical
      }
    }

    public void AddRecentFile(string file)
    {
      if (string.IsNullOrEmpty(file))
        return;
      this.RecentFiles.Remove(file);
      this.RecentFiles.Insert(0, file);
      while (this.RecentFiles.Count > 10)
        this.RecentFiles.RemoveAt(this.RecentFiles.Count - 1);
    }
  }
}
