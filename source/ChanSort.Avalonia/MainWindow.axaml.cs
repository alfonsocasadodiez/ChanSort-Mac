using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Animation;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ChanSort.Api;

namespace ChanSort.Avalonia
{
  public partial class MainWindow : Window
  {
    private const string WikiUrl = "https://github.com/PredatH0r/ChanSort/wiki";
    private const string GithubUrl = "https://github.com/alfonsocasadodiez/ChanSort-Mac";

    private readonly Settings settings = Settings.Load();
    private readonly IList<ISerializerPlugin> plugins;
    private readonly KeyModifiers cmdKey = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;

    private Encoding defaultEncoding;
    private ISerializerPlugin currentPlugin;
    private SerializerBase serializer;
    private DataRoot dataRoot;
    private Editor editor;
    private ChannelList currentList;
    private string currentFile;
    private readonly List<ChannelRow> rows = new();
    private readonly Dictionary<ChannelInfo, ChannelRow> rowByChannel = new();
    private DataGridCollectionView leftView;
    private DataGridCollectionView rightView;
    private DataGrid lastFocusedGrid;
    private bool closeConfirmed;
    private int suppressEvents;
    private List<(int index, string caption)> subListOptions = new();

    private NativeMenu mnuRecent, mnuLists, mnuFavSet, mnuFavUnset, mnuEncoding;
    private NativeMenuItem miCloseGap, miShowWarnings;

    public string FileToOpenOnStartup { get; set; }

    public int SubListIndex { get; private set; }

    #region ctor

    public MainWindow()
    {
      InitializeComponent();

      var version = Assembly.GetExecutingAssembly().GetName().Version;
      this.Title = "ChanSort";
      if (settings.WindowWidth > 400 && settings.WindowHeight > 300)
      {
        this.Width = settings.WindowWidth;
        this.Height = settings.WindowHeight;
      }

      try { this.defaultEncoding = Encoding.GetEncoding(string.IsNullOrEmpty(settings.Encoding) ? "windows-1252" : settings.Encoding); }
      catch { this.defaultEncoding = Encoding.GetEncoding("windows-1252"); }

      // The Api.View.Default object gives loaders access to UI functions
      View.Default = new View
      {
        CreateActionBox = msg => new ActionBoxDialog(msg),
        MessageBoxImpl = (msg, caption, buttons, icon) => Dialogs.RunSync(() => Dialogs.MessageBox(this, msg, caption, buttons, icon)),
        ShowHtmlBoxImpl = (html, title, width, height, onUrlClick) => Dialogs.RunSync(async () => { await Dialogs.ShowText(this, title, Dialogs.HtmlToText(html)); return true; })
      };

      // same defaults as the columns that are visible by default in the Windows UI
      ChannelList.DefaultVisibleColumns = new List<string>
      {
        "OldPosition", "Position", "Name", "ShortName", "_Favorites", "Lock", "Skip", "Hidden", "Encrypted", "ServiceTypeName", "FreqInMhz", "Polarity",
        "ChannelOrTransponder", "Satellite", "OriginalNetworkId", "TransportStreamId", "ServiceId", "PcrPid", "VideoPid", "AudioPid", "SymbolRate",
        "NetworkName", "NetworkOperator", "Provider"
      };

      this.plugins = LoadSerializerPlugins();

      if (OperatingSystem.IsMacOS())
      {
        // unified title bar: the sidebar extends below the traffic lights and the toolbar shares the title bar
        this.ExtendClientAreaToDecorationsHint = true;
        this.ExtendClientAreaChromeHints = global::Avalonia.Platform.ExtendClientAreaChromeHints.PreferSystemChrome;
        this.ExtendClientAreaTitleBarHeightHint = 52;
        this.TransparencyLevelHint = new[] { WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.None };
        this.Background = Brushes.Transparent;
      }
      else
        this.sidebarTitleArea.Height = 6;

      this.BuildMenu();
      this.WireEvents();
      this.UpdateUiState();
      this.lastFocusedGrid = this.gridRight;

      this.Opened += async (_, _) =>
      {
        if (!string.IsNullOrEmpty(this.FileToOpenOnStartup) && File.Exists(this.FileToOpenOnStartup))
          await this.LoadFile(this.FileToOpenOnStartup);
      };
    }

    #endregion

    #region LoadSerializerPlugins()

    private static IList<ISerializerPlugin> LoadSerializerPlugins()
    {
      var list = new List<ISerializerPlugin>();
      list.Add(new RefSerializerPlugin());
      var exeDir = AppContext.BaseDirectory;
      foreach (var file in Directory.GetFiles(exeDir, "ChanSort.Loader.*.dll"))
      {
        try
        {
          var assembly = Assembly.LoadFrom(file);
          foreach (var type in assembly.GetTypes())
          {
            if (typeof(ISerializerPlugin).IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface)
            {
              var plugin = (ISerializerPlugin)Activator.CreateInstance(type);
              plugin.DllName = Path.GetFileName(file);
              list.Add(plugin);
            }
          }
        }
        catch (Exception ex)
        {
          Trace.WriteLine("Plugin " + file + ": " + ex);
        }
      }
      list.Sort((a, b) => string.Compare(a.PluginName, b.PluginName, StringComparison.Ordinal));
      return list;
    }

    #endregion

    #region BuildMenu()

    private void BuildMenu()
    {
      var menu = new NativeMenu();

      // File
      var file = new NativeMenu();
      file.Add(Item("Open…", () => this.ShowOpenFileDialog(), new KeyGesture(Key.O, cmdKey)));
      this.mnuRecent = new NativeMenu();
      file.Add(new NativeMenuItem("Open Recent") { Menu = this.mnuRecent });
      file.Add(Item("Reload", () => this.Reload()));
      file.Add(new NativeMenuItemSeparator());
      file.Add(Item("Save", () => this.Save(), new KeyGesture(Key.S, cmdKey)));
      file.Add(Item("Save As…", () => this.SaveAs(), new KeyGesture(Key.S, cmdKey | KeyModifiers.Shift)));
      file.Add(new NativeMenuItemSeparator());
      file.Add(Item("Apply Reference List…", () => this.ApplyReferenceList(), new KeyGesture(Key.R, cmdKey)));
      file.Add(Item("Apply Included Reference List…", () => this.ApplyReferenceList(Path.Combine(AppContext.BaseDirectory, "ReferenceLists"))));
      file.Add(Item("Save Reference List…", () => this.SaveReferenceList()));
      file.Add(new NativeMenuItemSeparator());
      file.Add(Item("File Information…", () => this.ShowFileInformation(), new KeyGesture(Key.I, cmdKey)));
      if (!OperatingSystem.IsMacOS())
      {
        file.Add(new NativeMenuItemSeparator());
        file.Add(Item("Quit", () => { this.Close(); return Task.CompletedTask; }, new KeyGesture(Key.Q, cmdKey)));
      }
      menu.Add(new NativeMenuItem("File") { Menu = file });

      // Edit
      var edit = new NativeMenu();
      edit.Add(Item("Add to Sorted List", () => this.Run(this.AddChannels)));
      edit.Add(Item("Remove from Sorted List", () => this.Run(this.RemoveChannels), new KeyGesture(Key.Back, cmdKey)));
      edit.Add(Item("Move Up", () => this.Run(() => this.MoveChannels(true)), new KeyGesture(Key.Up, cmdKey)));
      edit.Add(Item("Move Down", () => this.Run(() => this.MoveChannels(false)), new KeyGesture(Key.Down, cmdKey)));
      edit.Add(Item("Set Number…", () => this.SetNumberInteractive(), new KeyGesture(Key.N, cmdKey | KeyModifiers.Shift)));
      edit.Add(new NativeMenuItemSeparator());
      edit.Add(Item("Sort Selected Alphabetically", () => this.Run(this.SortSelected)));
      edit.Add(Item("Renumber Selected", () => this.Run(this.RenumberSelected)));
      edit.Add(Item("Add All Unsorted Channels", () => this.Run(this.AddAllUnsorted)));
      edit.Add(new NativeMenuItemSeparator());
      this.mnuFavSet = new NativeMenu();
      this.mnuFavUnset = new NativeMenu();
      edit.Add(new NativeMenuItem("Add to Favorites") { Menu = this.mnuFavSet });
      edit.Add(new NativeMenuItem("Remove from Favorites") { Menu = this.mnuFavUnset });
      edit.Add(Item("Toggle Parental Lock", () => this.Run(() => this.ToggleFlag(c => c.Lock, (c, v) => c.Lock = v)), new KeyGesture(Key.L, cmdKey)));
      edit.Add(Item("Toggle Skip", () => this.Run(() => this.ToggleFlag(c => c.Skip, (c, v) => c.Skip = v)), new KeyGesture(Key.K, cmdKey)));
      edit.Add(Item("Toggle Hide", () => this.Run(() => this.ToggleFlag(c => c.Hidden, (c, v) => c.Hidden = v)), new KeyGesture(Key.H, cmdKey | KeyModifiers.Shift)));
      edit.Add(Item("Rename Channel…", () => this.RenameInteractive(), new KeyGesture(Key.E, cmdKey)));
      menu.Add(new NativeMenuItem("Channels") { Menu = edit });

      // View
      var view = new NativeMenu();
      this.mnuLists = new NativeMenu();
      view.Add(new NativeMenuItem("Channel List") { Menu = this.mnuLists });
      this.mnuEncoding = new NativeMenu();
      view.Add(new NativeMenuItem("Character Set") { Menu = this.mnuEncoding });
      view.Add(new NativeMenuItemSeparator());
      this.miCloseGap = new NativeMenuItem("Close Gap When Removing Channels") { ToggleType = NativeMenuItemToggleType.CheckBox, IsChecked = settings.CloseGapWhenRemoving };
      this.miCloseGap.Click += (_, _) => { settings.CloseGapWhenRemoving = !settings.CloseGapWhenRemoving; this.miCloseGap.IsChecked = settings.CloseGapWhenRemoving; settings.Save(); };
      view.Add(this.miCloseGap);
      this.miShowWarnings = new NativeMenuItem("Show Warnings After Loading") { ToggleType = NativeMenuItemToggleType.CheckBox, IsChecked = settings.ShowWarningsAfterLoad };
      this.miShowWarnings.Click += (_, _) => { settings.ShowWarningsAfterLoad = !settings.ShowWarningsAfterLoad; this.miShowWarnings.IsChecked = settings.ShowWarningsAfterLoad; settings.Save(); };
      view.Add(this.miShowWarnings);
      menu.Add(new NativeMenuItem("View") { Menu = view });

      // Help
      var help = new NativeMenu();
      help.Add(Item("ChanSort Documentation (Wiki)", () => { OpenUrl(WikiUrl); return Task.CompletedTask; }));
      help.Add(Item("ChanSort for Mac on GitHub", () => { OpenUrl(GithubUrl); return Task.CompletedTask; }));
      help.Add(Item("Supported TV Models", () => { OpenUrl("https://github.com/PredatH0r/ChanSort/blob/master/source/fileformats.md"); return Task.CompletedTask; }));
      if (!OperatingSystem.IsMacOS())
        help.Add(Item("About ChanSort", () => this.ShowAbout()));
      menu.Add(new NativeMenuItem("Help") { Menu = help });

      NativeMenu.SetMenu(this, menu);

      this.FillEncodingMenu();
      this.UpdateRecentMenu();
    }

    private NativeMenuItem Item(string text, Func<Task> action, KeyGesture gesture = null)
    {
      var item = new NativeMenuItem(text);
      if (gesture != null)
        item.Gesture = gesture;
      item.Click += async (_, _) => await this.TryExecute(action);
      return item;
    }

    private void FillEncodingMenu()
    {
      var encodings = new[]
      {
        ("utf-8", "Unicode (UTF-8)"),
        ("windows-1250", "Central European (Windows-1250)"),
        ("windows-1251", "Cyrillic (Windows-1251)"),
        ("windows-1252", "Western European (Windows-1252)"),
        ("windows-1253", "Greek (Windows-1253)"),
        ("windows-1254", "Turkish (Windows-1254)"),
        ("windows-1257", "Baltic (Windows-1257)"),
        ("iso-8859-1", "Western European (ISO-8859-1)"),
        ("iso-8859-2", "Central European (ISO-8859-2)"),
        ("iso-8859-5", "Cyrillic (ISO-8859-5)"),
        ("iso-8859-7", "Greek (ISO-8859-7)"),
        ("iso-8859-9", "Turkish (ISO-8859-9)"),
        ("iso-8859-15", "Latin 9 (ISO-8859-15)"),
      };
      this.mnuEncoding.Items.Clear();
      foreach (var (name, caption) in encodings)
      {
        var item = new NativeMenuItem(caption) { ToggleType = NativeMenuItemToggleType.Radio, IsChecked = string.Equals(this.defaultEncoding.WebName, name, StringComparison.OrdinalIgnoreCase) };
        var encName = name;
        item.Click += async (_, _) => await this.TryExecute(() =>
        {
          this.defaultEncoding = Encoding.GetEncoding(encName);
          settings.Encoding = encName;
          settings.Save();
          if (this.serializer != null)
            this.serializer.DefaultEncoding = this.defaultEncoding;
          this.FillEncodingMenu();
          this.RefreshGrids();
          return Task.CompletedTask;
        });
        this.mnuEncoding.Items.Add(item);
      }
    }

    private void UpdateRecentMenu()
    {
      this.mnuRecent.Items.Clear();
      foreach (var file in settings.RecentFiles.Where(File.Exists))
      {
        var path = file;
        this.mnuRecent.Items.Add(Item(path, () => this.LoadFile(path)));
      }
      if (this.mnuRecent.Items.Count == 0)
        this.mnuRecent.Items.Add(new NativeMenuItem("(empty)") { IsEnabled = false });
    }

    #endregion

    #region WireEvents()

    private void WireEvents()
    {
      this.btnOpen.Click += async (_, _) => await this.TryExecute(this.ShowOpenFileDialog);
      this.btnSave.Click += async (_, _) => await this.TryExecute(this.Save);
      this.btnRefList.Click += async (_, _) => await this.TryExecute(() => this.ApplyReferenceList());
      this.btnInfo.Click += async (_, _) => await this.TryExecute(this.ShowFileInformation);
      this.btnUp.Click += async (_, _) => await this.Run(() => this.MoveChannels(true));
      this.btnDown.Click += async (_, _) => await this.Run(() => this.MoveChannels(false));
      this.btnRemove.Click += async (_, _) => await this.Run(this.RemoveChannels);
      this.btnSetNr.Click += async (_, _) => await this.TryExecute(this.SetNumberInteractive);
      this.btnSortAz.Click += async (_, _) => await this.Run(this.SortSelected);
      this.btnRenum.Click += async (_, _) => await this.Run(this.RenumberSelected);
      this.btnAdd.Click += async (_, _) => await this.Run(this.AddChannels);
      this.btnAddAll.Click += async (_, _) => await this.Run(this.AddAllUnsorted);

      this.lstLists.SelectionChanged += (_, _) =>
      {
        if (this.suppressEvents > 0) return;
        if (this.lstLists.SelectedItem is SidebarItem { Tag: ChannelList list })
          this.ShowChannelList(list);
      };
      this.lstSubLists.SelectionChanged += (_, _) =>
      {
        if (this.suppressEvents > 0) return;
        var idx = this.lstSubLists.SelectedIndex;
        if (idx >= 0 && idx < this.subListOptions.Count)
        {
          this.SubListIndex = this.subListOptions[idx].index;
          if (this.editor != null)
            this.editor.SubListIndex = this.SubListIndex;
          this.ShowChannelList(this.currentList); // rebuilds the columns ("Pr#" vs. "Fav #")
        }
      };

      this.txtFilterLeft.TextChanged += (_, _) => this.leftView?.Refresh();
      this.txtFilterRight.TextChanged += (_, _) => this.rightView?.Refresh();
      this.cbOnlyUnsorted.IsCheckedChanged += (_, _) => this.rightView?.Refresh();

      this.gridLeft.GotFocus += (_, _) => this.lastFocusedGrid = this.gridLeft;
      this.gridRight.GotFocus += (_, _) => this.lastFocusedGrid = this.gridRight;
      this.gridLeft.AddHandler(KeyDownEvent, this.Grid_KeyDown, RoutingStrategies.Tunnel);
      this.gridRight.AddHandler(KeyDownEvent, this.Grid_KeyDown, RoutingStrategies.Tunnel);
      this.gridRight.DoubleTapped += async (_, e) =>
      {
        if (e.Source is Control c && c.FindAncestorOfType<DataGridColumnHeader>() != null) return;
        await this.Run(this.AddChannels);
      };
      this.gridLeft.SelectionChanged += (_, _) => this.UpdateStatus();
      this.gridRight.SelectionChanged += (_, _) => this.UpdateStatus();

      this.gridLeft.ContextMenu = this.CreateGridContextMenu(true);
      this.gridRight.ContextMenu = this.CreateGridContextMenu(false);

      this.gridLeft.CellEditEnded += (_, _) => { if (this.suppressEvents == 0) this.RefreshGridsDeferred(); };

      this.AddHandler(DragDrop.DragOverEvent, (_, e) =>
      {
        e.DragEffects = e.Data.Contains(DataFormats.Files) ? DragDropEffects.Copy : DragDropEffects.None;
      });
      this.AddHandler(DragDrop.DropEvent, async (_, e) =>
      {
        var file = e.Data.GetFiles()?.Select(f => f.TryGetLocalPath()).FirstOrDefault(p => p != null);
        if (file != null)
          await this.TryExecute(() => this.LoadFile(file));
      });

      this.Closing += this.MainWindow_Closing;

      // the toolbar and the top of the sidebar act as the window's title bar
      foreach (var titleArea in new Control[] { this.toolbar, this.sidebarTitleArea })
        titleArea.PointerPressed += this.TitleArea_PointerPressed;
    }

    private ContextMenu CreateGridContextMenu(bool left)
    {
      var items = new List<object>();
      MenuItem Mi(string header, Func<Task> action)
      {
        var mi = new MenuItem { Header = header };
        mi.Click += async (_, _) => await this.TryExecute(action);
        return mi;
      }

      if (left)
      {
        items.Add(Mi("Move Up", () => this.Run(() => this.MoveChannels(true))));
        items.Add(Mi("Move Down", () => this.Run(() => this.MoveChannels(false))));
        items.Add(Mi("Remove from Sorted List", () => this.Run(this.RemoveChannels)));
        items.Add(Mi("Set Number…", this.SetNumberInteractive));
        items.Add(new Separator());
        items.Add(Mi("Sort Alphabetically", () => this.Run(this.SortSelected)));
        items.Add(Mi("Renumber", () => this.Run(this.RenumberSelected)));
      }
      else
      {
        items.Add(Mi("Add to Sorted List", () => this.Run(this.AddChannels)));
        items.Add(Mi("Remove from Sorted List", () => this.Run(this.RemoveChannels)));
        items.Add(Mi("Set Number…", this.SetNumberInteractive));
      }
      items.Add(new Separator());
      items.Add(Mi("Rename…", this.RenameInteractive));
      items.Add(Mi("Toggle Parental Lock", () => this.Run(() => this.ToggleFlag(c => c.Lock, (c, v) => c.Lock = v))));
      items.Add(Mi("Toggle Skip", () => this.Run(() => this.ToggleFlag(c => c.Skip, (c, v) => c.Skip = v))));
      items.Add(Mi("Toggle Hide", () => this.Run(() => this.ToggleFlag(c => c.Hidden, (c, v) => c.Hidden = v))));

      var menu = new ContextMenu { ItemsSource = items };
      menu.Opening += (_, _) =>
      {
        this.lastFocusedGrid = left ? this.gridLeft : this.gridRight;
      };
      return menu;
    }

    private async void Grid_KeyDown(object sender, KeyEventArgs e)
    {
      if (e.Source is TextBox) // cell editor is active
        return;
      var grid = (DataGrid)sender;
      this.lastFocusedGrid = grid;
      if (e.KeyModifiers == KeyModifiers.None)
      {
        if (grid == this.gridRight && e.Key == Key.Enter)
        {
          e.Handled = true;
          await this.Run(this.AddChannels);
        }
        else if (grid == this.gridLeft && (e.Key == Key.Delete || e.Key == Key.Back))
        {
          e.Handled = true;
          await this.Run(this.RemoveChannels);
        }
      }
    }

    #endregion

    #region Grid columns

    private void BuildColumns()
    {
      var list = this.currentList;
      var source = list?.SignalSource ?? 0;
      var nameEditable = false;
      if (this.serializer != null && list != null)
      {
        SignalSource src = 0;
        if ((this.serializer.Features.ChannelNameEdit & ChannelNameEditMode.Analog) != 0)
          src |= SignalSource.Analog;
        if ((this.serializer.Features.ChannelNameEdit & ChannelNameEditMode.Digital) != 0)
          src |= SignalSource.Dvb;
        nameEditable = (list.SignalSource & src) != 0;
      }
      var readOnlyList = list?.ReadOnly ?? true;
      var favEditable = this.dataRoot != null && !this.dataRoot.SortedFavorites && this.dataRoot.SupportedFavorites != 0;
      var shortNameEditable = this.serializer?.Features.AllowShortNameEdit ?? false;
      var mixedFavList = list?.IsMixedSourceFavoritesList ?? false;

      bool Visible(string field)
      {
        if (list == null)
          return false;
        if (mixedFavList)
        {
          if (field == "Source") return true;
          if (field is "Lock" or "Skip" or "Hidden") return false;
        }

        var filter = list.VisibleColumnFieldNames;
        if (filter != null)
        {
          if (filter.Contains("+" + field))
            return true;
          if (filter.Contains("-" + field) || !filter.Contains(field))
            return field is "Position" or "Name"; // never hide the essentials
        }

        switch (field)
        {
          case "_Favorites": return this.dataRoot.SupportedFavorites != 0;
          case "ChannelOrTransponder": return (source & SignalSource.Sat) == 0;
          case "ShortName":
          case "Encrypted":
          case "ServiceId":
          case "ServiceTypeName":
          case "TransportStreamId":
          case "NetworkName":
          case "Provider":
          case "OriginalNetworkId":
          case "SymbolRate":
            return (source & SignalSource.Dvb) != 0;
          case "Satellite":
          case "Polarity":
            return (source & SignalSource.Sat) != 0;
          case "Skip": return (source & SignalSource.Dvb) != 0 && this.dataRoot.CanSkip;
          case "Lock": return (source & SignalSource.Dvb) != 0 && this.dataRoot.CanLock;
          case "Hidden": return (source & SignalSource.Dvb) != 0 && this.dataRoot.CanHide;
        }
        return true;
      }

      DataGridColumn Text(string header, string path, string field, bool editable = false, double width = double.NaN, string sortPath = null)
      {
        var col = new DataGridTextColumn
        {
          Header = header,
          Binding = new Binding(path) { Mode = editable ? BindingMode.TwoWay : BindingMode.OneWay },
          IsReadOnly = !editable || readOnlyList,
          IsVisible = Visible(field),
          SortMemberPath = sortPath ?? path,
        };
        if (!double.IsNaN(width))
          col.Width = new DataGridLength(width);
        return col;
      }

      var posHeader = this.SubListIndex == 0 ? "New Pr#" : "Fav #";
      var oldPosHeader = this.SubListIndex == 0 ? "Old Pr#" : "Old Fav #";

      // left grid: the sorted list
      this.gridLeft.Columns.Clear();
      this.gridLeft.Columns.Add(Text(posHeader, nameof(ChannelRow.Position), "Position", true, 90));
      this.gridLeft.Columns.Add(Text("Channel name", nameof(ChannelRow.Name), "Name", nameEditable, 200));
      this.gridLeft.Columns.Add(Text("Fav", nameof(ChannelRow.Favorites), "_Favorites", favEditable, 64));
      this.gridLeft.Columns.Add(Text("Lock", nameof(ChannelRow.LockText), "Lock", false, 68));
      this.gridLeft.Columns.Add(Text("Skip", nameof(ChannelRow.SkipText), "Skip", false, 68));
      this.gridLeft.Columns.Add(Text("Hide", nameof(ChannelRow.HiddenText), "Hidden", false, 68));
      this.gridLeft.Columns.Add(Text("Type", nameof(ChannelRow.ServiceTypeName), "ServiceTypeName", false, 80));
      var leftSource = Text("Source", nameof(ChannelRow.Source), "Source", false, 80);
      leftSource.IsVisible = mixedFavList;
      this.gridLeft.Columns.Add(leftSource);
      this.gridLeft.Columns.Add(Text(oldPosHeader, nameof(ChannelRow.OldPositionText), "OldPosition", false, 90, nameof(ChannelRow.OldPosition)));

      // right grid: all channels with all details
      this.gridRight.Columns.Clear();
      this.gridRight.Columns.Add(Text(oldPosHeader, nameof(ChannelRow.OldPositionText), "OldPosition", false, 90, nameof(ChannelRow.OldPosition)));
      this.gridRight.Columns.Add(Text(posHeader, nameof(ChannelRow.PositionText), "Position", false, 90, nameof(ChannelRow.SortKey)));
      var rightSource = Text("Source", nameof(ChannelRow.Source), "Source", false, 80);
      rightSource.IsVisible = mixedFavList;
      this.gridRight.Columns.Add(rightSource);
      this.gridRight.Columns.Add(Text("Channel name", nameof(ChannelRow.Name), "Name", nameEditable, 200));
      this.gridRight.Columns.Add(Text("Short name", nameof(ChannelRow.ShortName), "ShortName", shortNameEditable, 90));
      this.gridRight.Columns.Add(Text("Fav", nameof(ChannelRow.Favorites), "_Favorites", favEditable, 64));
      this.gridRight.Columns.Add(Text("Lock", nameof(ChannelRow.LockText), "Lock", false, 68));
      this.gridRight.Columns.Add(Text("Skip", nameof(ChannelRow.SkipText), "Skip", false, 68));
      this.gridRight.Columns.Add(Text("Hide", nameof(ChannelRow.HiddenText), "Hidden", false, 68));
      this.gridRight.Columns.Add(Text("Deleted", nameof(ChannelRow.DeletedText), "IsDeleted", false, 86));
      this.gridRight.Columns.Add(Text("Crypt", nameof(ChannelRow.Encrypted), "Encrypted", false, 74));
      this.gridRight.Columns.Add(Text("Type", nameof(ChannelRow.ServiceTypeName), "ServiceTypeName", false, 80));
      this.gridRight.Columns.Add(Text("Freq (MHz)", nameof(ChannelRow.FreqInMhz), "FreqInMhz", false, 80));
      this.gridRight.Columns.Add(Text("Channel/Transp.", nameof(ChannelRow.ChannelOrTransponder), "ChannelOrTransponder", false, 90));
      this.gridRight.Columns.Add(Text("Satellite", nameof(ChannelRow.Satellite), "Satellite", false, 110));
      this.gridRight.Columns.Add(Text("Pol.", nameof(ChannelRow.Polarity), "Polarity", false, 64));
      this.gridRight.Columns.Add(Text("ONID", nameof(ChannelRow.OriginalNetworkId), "OriginalNetworkId", false, 60));
      this.gridRight.Columns.Add(Text("TSID", nameof(ChannelRow.TransportStreamId), "TransportStreamId", false, 60));
      this.gridRight.Columns.Add(Text("SID", nameof(ChannelRow.ServiceId), "ServiceId", false, 60));
      this.gridRight.Columns.Add(Text("Symbol rate", nameof(ChannelRow.SymbolRate), "SymbolRate", false, 80));
      this.gridRight.Columns.Add(Text("Network", nameof(ChannelRow.NetworkName), "NetworkName", false, 110));
      this.gridRight.Columns.Add(Text("Provider", nameof(ChannelRow.Provider), "Provider", false, 110));
      var uid = Text("UID", nameof(ChannelRow.Uid), "Uid", false, 140);
      uid.IsVisible = false;
      this.gridRight.Columns.Add(uid);
    }

    #endregion

    #region ShowOpenFileDialog(), LoadFile(), Reload()

    private async Task ShowOpenFileDialog()
    {
      var startDir = settings.LastDirectory;
      IStorageFolder folder = null;
      if (!string.IsNullOrEmpty(startDir) && Directory.Exists(startDir))
        folder = await this.StorageProvider.TryGetFolderFromPathAsync(startDir);

      var files = await this.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
      {
        Title = "Open TV channel list",
        AllowMultiple = false,
        SuggestedStartLocation = folder
      });
      var path = files?.FirstOrDefault()?.TryGetLocalPath();
      Log.Write("open file picker result: " + (path ?? "(cancelled)"));
      if (path == null)
        return;
      await this.LoadFile(path);
    }

    /// <summary>
    /// Called when a file is opened through Finder (double-click / "Open with" / dropped onto the dock icon)
    /// </summary>
    public async void OpenFileFromOutside(string path)
    {
      if (!this.IsVisible)
      {
        this.FileToOpenOnStartup = path;
        return;
      }
      await this.TryExecute(() => this.LoadFile(path));
    }

    private Task Reload()
    {
      if (string.IsNullOrEmpty(this.currentFile))
        return Task.CompletedTask;
      return this.LoadFile(this.currentFile, this.currentPlugin, true);
    }

    private async Task LoadFile(string path, ISerializerPlugin hint = null, bool isReload = false)
    {
      if (!File.Exists(path))
      {
        await Dialogs.MessageBox(this, "The file does not exist:\n" + path, "ChanSort", View.MessageBoxButtons.OK, View.MessageBoxIcon.Error);
        return;
      }

      if (await this.DetectCommonFileCorruptions(path))
        return;

      if (!isReload && !await this.PromptSaveAndContinue())
        return;

      var (ser, plugin) = await this.CreateAndLoadSerializer(path, hint);
      if (ser == null)
        return;

      var listIndex = isReload ? this.lstLists.SelectedIndex : -1;

      this.serializer?.Dispose();
      ser.DataRoot.ValidateAfterLoad();
      this.serializer = ser;
      this.currentPlugin = plugin;
      this.dataRoot = ser.DataRoot;
      this.currentFile = ser.FileName ?? path;
      this.editor = new Editor { DataRoot = this.dataRoot };
      this.SubListIndex = 0;
      this.editor.SubListIndex = 0;

      settings.LastDirectory = Path.GetDirectoryName(path);
      settings.AddRecentFile(path);
      settings.Save();
      this.UpdateRecentMenu();

      this.FillChannelListTabs(listIndex);
      this.FillFavoritesMenu();
      this.UpdateUiState();

      if (this.dataRoot.Warnings.Length > 0 && settings.ShowWarningsAfterLoad)
        await Dialogs.ShowText(this, "Warnings", this.dataRoot.Warnings.ToString(), true);

      if (isReload)
      {
        this.dataRoot.ApplyCurrentProgramNumbers();
        this.RefreshGrids();
      }
      else
        await this.InitInitialChannelOrder();
    }

    private async Task<bool> DetectCommonFileCorruptions(string path)
    {
      var content = await File.ReadAllBytesAsync(path);
      if (content.Length > 0 && content.All(b => b == content[0]))
      {
        await Dialogs.MessageBox(this, "The file content is invalid (all bytes have the same value).\n\nThis usually means that the USB stick was removed before the TV finished writing the file.\nPlease export the channel list from your TV again.", "ChanSort", View.MessageBoxButtons.OK, View.MessageBoxIcon.Error);
        return true;
      }
      return false;
    }

    /// <summary>
    /// Finds the loader(s) matching the file name and tries them until one can successfully read the file.
    /// Loading is done on a worker thread to keep the UI responsive.
    /// </summary>
    private async Task<(SerializerBase, ISerializerPlugin)> CreateAndLoadSerializer(string path, ISerializerPlugin hint)
    {
      Directory.SetCurrentDirectory(AppContext.BaseDirectory); // loaders read their .ini files from the current directory

      var candidates = new List<ISerializerPlugin>();
      if (hint != null)
        candidates.Add(hint);
      else
      {
        var fileName = Path.GetFileName(path).ToLowerInvariant();
        foreach (var plugin in this.plugins)
        {
          foreach (var filter in plugin.FileFilter.ToLowerInvariant().Split(';'))
          {
            var regex = "^" + Regex.Escape(filter).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            if (Regex.IsMatch(fileName, regex))
            {
              candidates.Add(plugin);
              break;
            }
          }
        }
      }

      var errorMsgs = new StringBuilder();
      string authoritativeError = null;
      SerializerBase result = null;
      ISerializerPlugin resultPlugin = null;
      var encoding = this.defaultEncoding;

      this.SetBusy("Loading " + Path.GetFileName(path) + " …");
      try
      {
        await Task.Run(() =>
        {
          foreach (var plugin in candidates)
          {
            SerializerBase ser = null;
            try
            {
              ser = plugin.CreateSerializer(path);
              if (ser == null)
                continue;
              ser.DefaultEncoding = encoding;
              ser.Load();
              result = ser;
              resultPlugin = plugin;
              return;
            }
            catch (Exception ex)
            {
              ser?.Dispose();
              if (ex is LoaderException lex)
              {
                if (lex.Recovery == LoaderException.RecoveryMode.Fail)
                {
                  authoritativeError = ex.Message;
                  return;
                }
                errorMsgs.AppendLine($"{plugin.PluginName}: {ex.Message}\n");
              }
              else if (ex is FileLoadException)
                errorMsgs.AppendLine($"{plugin.PluginName}: {ex.Message}\n");
              else
                errorMsgs.AppendLine($"{plugin.PluginName}: {ex}\n");
            }
          }
        });
      }
      finally
      {
        this.SetBusy(null);
      }

      if (result != null)
        return (result, resultPlugin);

      if (authoritativeError != null)
        await Dialogs.MessageBox(this, authoritativeError, "ChanSort", View.MessageBoxButtons.OK, View.MessageBoxIcon.Error);
      else
      {
        var msg = candidates.Count == 0
          ? $"No loader is registered for this file name:\n{path}\n\nPlease check that you selected the channel list file exported by your TV (see Help / Supported TV Models)."
          : $"None of the loaders could read the file:\n{path}\n\n{errorMsgs}";
        await Dialogs.ShowText(this, "Unable to load file", msg, true);
      }
      return (null, null);
    }

    #endregion

    #region PromptSaveAndContinue()

    private async Task<bool> PromptSaveAndContinue()
    {
      if (this.dataRoot == null || !this.dataRoot.NeedsSaving)
        return true;

      var res = await Dialogs.ActionBox(this, "The channel list has been modified. Do you want to save your changes?", new List<(string, int)>
      {
        ("Save changes", 1),
        ("Discard changes", 2),
        ("Cancel", 0)
      }, 0);

      if (res == 0)
        return false;
      if (res == 1)
        return await this.Save();
      return true;
    }

    #endregion

    #region InitInitialChannelOrder()

    private async Task InitInitialChannelOrder()
    {
      if (this.dataRoot.ChannelLists.All(l => l.Count == 0 || l.ReadOnly))
      {
        this.dataRoot.ApplyCurrentProgramNumbers();
        this.RefreshGrids();
        return;
      }

      Log.Write("showing initial channel order prompt");
      var res = await Dialogs.ActionBox(this, "How would you like to start editing the channel list?", new List<(string, int)>
      {
        ("Use the current channel order of the TV", 1),
        ("Copy the order from a reference list / another channel list file…", 2),
        ("Start with an empty list and add channels one by one", 3),
      }, 1);

      Log.Write("initial channel order prompt result: " + res);
      if (res == 1)
        this.dataRoot.ApplyCurrentProgramNumbers();
      else if (res == 2)
      {
        this.dataRoot.ApplyCurrentProgramNumbers();
        this.RefreshGrids();
        await this.ApplyReferenceList();
        return;
      }
      this.RefreshGrids();
    }

    #endregion

    #region FillChannelListTabs(), ShowChannelList()

    private void FillChannelListTabs(int selectIndex)
    {
      ++this.suppressEvents;
      try
      {
        var tabs = new List<SidebarItem>();
        this.mnuLists.Items.Clear();
        ChannelList mostChannels = null;
        int i = 0;
        foreach (var list in this.dataRoot.ChannelLists)
        {
          if (list.Channels.Count == 0)
            continue;
          tabs.Add(new SidebarItem(list.ShortCaption ?? list.Caption, this.GetListIcon(list), list.Channels.Count(ch => ch.OldProgramNr != -1).ToString(), list));
          if (!list.IsMixedSourceFavoritesList && (mostChannels == null || list.Count > mostChannels.Count))
            mostChannels = list;

          var lst = list;
          var idx = i;
          var mi = new NativeMenuItem(list.Caption);
          if (i < 9)
            mi.Gesture = new KeyGesture(Key.D1 + i, KeyModifiers.Alt | cmdKey);
          mi.Click += (_, _) => this.lstLists.SelectedIndex = idx;
          this.mnuLists.Items.Add(mi);
          ++i;
        }

        this.lstLists.ItemsSource = tabs;
        if (tabs.Count == 0)
        {
          this.ShowChannelList(null);
          return;
        }

        var sel = selectIndex >= 0 && selectIndex < tabs.Count ? selectIndex : Math.Max(0, tabs.FindIndex(t => t.Tag == mostChannels));
        this.lstLists.SelectedIndex = sel;
        this.ShowChannelList((ChannelList)tabs[sel].Tag);
      }
      finally
      {
        --this.suppressEvents;
      }
    }

    private void ShowChannelList(ChannelList list)
    {
      this.currentList = list;
      if (this.editor != null)
        this.editor.ChannelList = list;

      this.UpdateSubListOptions();

      // detach the data before rebuilding the columns, otherwise the DataGrid fails when it has a current cell
      this.gridLeft.ItemsSource = null;
      this.gridRight.ItemsSource = null;
      this.rows.Clear();
      this.rowByChannel.Clear();
      if (list != null)
      {
        foreach (var ch in list.Channels.OrderBy(c => c.OldProgramNr < 0 ? int.MaxValue : c.OldProgramNr).ThenBy(c => c.Name))
        {
          var row = new ChannelRow(ch, this);
          this.rows.Add(row);
          this.rowByChannel[ch] = row;
        }
      }

      this.BuildColumns();

      this.leftView = new DataGridCollectionView(this.rows) { Filter = this.LeftFilter };
      this.leftView.SortDescriptions.Add(DataGridSortDescription.FromPath(nameof(ChannelRow.SortKey)));
      this.rightView = new DataGridCollectionView(this.rows) { Filter = this.RightFilter };
      this.gridLeft.ItemsSource = this.leftView;
      this.gridRight.ItemsSource = this.rightView;

      this.UpdateUiState();
    }

    private void UpdateSubListOptions()
    {
      ++this.suppressEvents;
      try
      {
        this.subListOptions = new List<(int, string)>();
        var list = this.currentList;
        if (list != null && this.dataRoot != null)
        {
          var mixed = this.dataRoot.MixedSourceFavorites;
          if (!(mixed && list.IsMixedSourceFavoritesList))
            this.subListOptions.Add((0, "Program Numbers"));
          if (this.dataRoot.SortedFavorites && !(mixed && !list.IsMixedSourceFavoritesList))
          {
            var favCount = Math.Min(this.serializer.Features.MaxFavoriteLists, 26);
            for (int i = 0; i < favCount; i++)
              this.subListOptions.Add((i + 1, list.GetFavListCaption(i, true) ?? ("Fav " + (char)('A' + i))));
          }
        }

        if (this.subListOptions.Count == 0)
          this.subListOptions.Add((0, "Program Numbers"));

        this.lstSubLists.ItemsSource = this.subListOptions.Select(o => new SidebarItem(o.caption, this.Icon(o.index == 0 ? "IconNumbered" : "IconStar"), null, o.index)).ToList();
        var sel = this.subListOptions.FindIndex(o => o.index == this.SubListIndex);
        if (sel < 0) sel = 0;
        this.lstSubLists.SelectedIndex = sel;
        this.SubListIndex = this.subListOptions[sel].index;
        if (this.editor != null)
          this.editor.SubListIndex = this.SubListIndex;
        this.lstSubLists.IsVisible = this.lblSubList.IsVisible = this.subListOptions.Count > 1;
      }
      finally
      {
        --this.suppressEvents;
      }
    }

    private bool LeftFilter(object o)
    {
      var row = (ChannelRow)o;
      if (row.Position < 0)
        return false;
      var filter = this.txtFilterLeft.Text;
      return string.IsNullOrEmpty(filter) || (row.Name ?? "").Contains(filter, StringComparison.CurrentCultureIgnoreCase);
    }

    private bool RightFilter(object o)
    {
      var row = (ChannelRow)o;
      if (this.cbOnlyUnsorted.IsChecked == true && row.Position >= 0)
        return false;
      var filter = this.txtFilterRight.Text;
      return string.IsNullOrEmpty(filter) || (row.Name ?? "").Contains(filter, StringComparison.CurrentCultureIgnoreCase);
    }

    #endregion

    #region RefreshGrids()

    /// <summary>
    /// Re-evaluates filters/sorting of both grids and restores the selection
    /// </summary>
    private void RefreshGrids(IEnumerable<ChannelInfo> selectLeft = null, IEnumerable<ChannelInfo> selectRight = null)
    {
      if (this.leftView == null)
      {
        this.UpdateUiState();
        return;
      }

      this.EndPendingEdits();
      var leftSel = (selectLeft ?? this.SelectedChannels(this.gridLeft)).ToList();
      var rightSel = (selectRight ?? this.SelectedChannels(this.gridRight)).ToList();

      // the editor may have added proxy channels (e.g. from a reference list)
      if (this.currentList != null && this.currentList.Channels.Count != this.rows.Count)
      {
        foreach (var ch in this.currentList.Channels)
        {
          if (!this.rowByChannel.ContainsKey(ch))
          {
            var row = new ChannelRow(ch, this);
            this.rows.Add(row);
            this.rowByChannel[ch] = row;
          }
        }
        var removed = this.rows.Where(r => !this.currentList.Channels.Contains(r.Channel)).ToList();
        foreach (var r in removed)
        {
          this.rows.Remove(r);
          this.rowByChannel.Remove(r.Channel);
        }
      }

      foreach (var row in this.rows)
        row.NotifyAll();
      this.leftView.Refresh();
      this.rightView.Refresh();

      Select(this.gridLeft, leftSel);
      Select(this.gridRight, rightSel);
      this.UpdateUiState();
    }

    /// <summary>
    /// DataGridCollectionView.Refresh() throws while a cell is being edited, so any pending edit must be committed first
    /// </summary>
    private void EndPendingEdits()
    {
      foreach (var (grid, view) in new[] { (this.gridLeft, this.leftView), (this.gridRight, this.rightView) })
      {
        if (view == null)
          continue;
        if (view.IsEditingItem || view.IsAddingNew)
        {
          ++this.suppressEvents;
          try
          {
            if (!grid.CommitEdit())
              grid.CancelEdit();
          }
          finally
          {
            --this.suppressEvents;
          }
        }
        if (view.IsEditingItem)
          view.CommitEdit();
        if (view.IsAddingNew)
          view.CancelNew();
      }
    }

    private void RefreshGridsDeferred()
    {
      Dispatcher.UIThread.Post(() => this.RefreshGrids(), DispatcherPriority.Background);
    }

    private void Select(DataGrid grid, List<ChannelInfo> channels)
    {
      grid.SelectedItems.Clear();
      ChannelRow first = null;
      foreach (var ch in channels)
      {
        if (this.rowByChannel.TryGetValue(ch, out var row))
        {
          var view = grid == this.gridLeft ? this.leftView : this.rightView;
          if (!view.Contains(row))
            continue;
          grid.SelectedItems.Add(row);
          first ??= row;
        }
      }
      if (first != null)
        grid.ScrollIntoView(first, null);
    }

    private List<ChannelInfo> SelectedChannels(DataGrid grid)
    {
      return grid.SelectedItems.OfType<ChannelRow>().Select(r => r.Channel).ToList();
    }

    /// <summary>
    /// selected channels in the order they appear in the sorted list
    /// </summary>
    private List<ChannelInfo> SelectedChannelsByPosition(DataGrid grid)
    {
      return this.SelectedChannels(grid).OrderBy(c => c.GetPosition(this.SubListIndex)).ToList();
    }

    #endregion

    #region Editing operations

    private async Task Run(Action action)
    {
      if (this.editor == null || this.currentList == null)
        return;
      if (this.currentList.ReadOnly)
      {
        await Dialogs.MessageBox(this, "This channel list is predefined by the TV and can't be modified.", "ChanSort", View.MessageBoxButtons.OK, View.MessageBoxIcon.Information);
        return;
      }
      await this.TryExecute(() =>
      {
        action();
        return Task.CompletedTask;
      });
    }

    private void AddChannels()
    {
      var selected = this.SelectedChannels(this.gridRight).OrderBy(c => this.rows.IndexOf(this.rowByChannel[c])).ToList();
      if (selected.Count == 0)
        return;

      // insert after the selected channel in the left list, or append at the end
      var contextChannel = this.SelectedChannelsByPosition(this.gridLeft).LastOrDefault();
      this.editor.RemoveChannels(selected, settings.CloseGapWhenRemoving);
      if (contextChannel != null && contextChannel.GetPosition(this.SubListIndex) >= 0)
        this.currentList.InsertProgramNumber = contextChannel.GetPosition(this.SubListIndex) + 1;
      else
      {
        var max = this.currentList.Channels.Select(c => c.GetPosition(this.SubListIndex)).DefaultIfEmpty(0).Max();
        this.currentList.InsertProgramNumber = Math.Max(max + 1, this.currentList.FirstProgramNumber);
      }

      this.editor.AddChannels(selected);
      this.dataRoot.NeedsSaving = true;

      // select the next unsorted channel in the right list to allow quick consecutive adding
      var nextRight = this.NextRowAfter(this.gridRight, selected.Last());
      this.RefreshGrids(selected.Count > 0 ? new[] { selected.Last() } : null, nextRight != null ? new[] { nextRight } : selected);
    }

    private ChannelInfo NextRowAfter(DataGrid grid, ChannelInfo channel)
    {
      var view = grid == this.gridLeft ? this.leftView : this.rightView;
      if (!this.rowByChannel.TryGetValue(channel, out var row))
        return null;
      var idx = view.IndexOf(row);
      if (idx < 0 || idx + 1 >= view.Count)
        return null;
      return ((ChannelRow)view[idx + 1]).Channel;
    }

    private void RemoveChannels()
    {
      var selected = this.SelectedChannelsByPosition(this.lastFocusedGrid).Where(c => c.GetPosition(this.SubListIndex) >= 0).ToList();
      if (selected.Count == 0)
        return;

      // after removing, select the row following the last removed one
      var next = this.NextRowAfter(this.gridLeft, selected.Last());
      while (next != null && selected.Contains(next))
        next = this.NextRowAfter(this.gridLeft, next);

      this.editor.RemoveChannels(selected, settings.CloseGapWhenRemoving);
      this.RefreshGrids(next != null ? new[] { next } : Array.Empty<ChannelInfo>(), selected);
    }

    private void MoveChannels(bool up)
    {
      var selected = this.SelectedChannelsByPosition(this.gridLeft);
      if (selected.Count == 0)
        return;
      this.editor.MoveChannels(selected, up);
      this.RefreshGrids(selected);
    }

    private async Task SetNumberInteractive()
    {
      if (this.editor == null || this.currentList == null || this.currentList.ReadOnly)
        return;
      var grid = this.lastFocusedGrid ?? this.gridLeft;
      var selected = this.SelectedChannelsByPosition(grid);
      if (selected.Count == 0)
        return;
      var first = selected[0].GetPosition(this.SubListIndex);
      var text = await Dialogs.InputBox(this, "Set number", $"New number for the {selected.Count} selected channel(s):", first > 0 ? first.ToString() : "");
      if (text == null || !int.TryParse(text.Trim(), out var nr) || nr < 0)
        return;
      this.editor.SetSlotNumber(selected, nr, false, settings.CloseGapWhenRemoving);
      this.RefreshGrids(selected, selected);
    }

    internal void SetChannelNumber(ChannelRow row, int nr)
    {
      if (this.editor == null || nr == row.Position || nr < 0)
        return;
      var list = new List<ChannelInfo> { row.Channel };
      this.editor.SetSlotNumber(list, nr, false, settings.CloseGapWhenRemoving);
      this.dataRoot.NeedsSaving = true;
      this.RefreshGridsDeferred();
    }

    private void SortSelected()
    {
      var selected = this.SelectedChannelsByPosition(this.gridLeft);
      if (selected.Count <= 1)
        selected = this.leftView.OfType<ChannelRow>().Select(r => r.Channel).ToList();
      this.editor.SortSelectedChannels(selected);
      this.RefreshGrids();
    }

    private void RenumberSelected()
    {
      var selected = this.SelectedChannelsByPosition(this.gridLeft);
      if (selected.Count <= 1)
        selected = this.leftView.OfType<ChannelRow>().Select(r => r.Channel).ToList();
      this.editor.RenumberChannels(selected);
      this.RefreshGrids();
    }

    private void AddAllUnsorted()
    {
      var maxNr = this.currentList.InsertProgramNumber;
      foreach (var channel in this.currentList.Channels)
        maxNr = Math.Max(maxNr, channel.GetPosition(this.SubListIndex) + 1);

      foreach (var row in this.rightView.OfType<ChannelRow>().ToList())
      {
        var channel = row.Channel;
        if (channel.GetPosition(this.SubListIndex) == -1 && !channel.IsDeleted)
          channel.SetPosition(this.SubListIndex, maxNr++);
      }
      this.dataRoot.NeedsSaving = true;
      this.RefreshGrids();
    }

    private void ToggleFlag(Func<ChannelInfo, bool> get, Action<ChannelInfo, bool> set)
    {
      var selected = this.SelectedChannels(this.lastFocusedGrid);
      if (selected.Count == 0)
        return;
      var newValue = !get(selected[0]);
      foreach (var ch in selected)
        set(ch, newValue);
      this.dataRoot.NeedsSaving = true;
      this.RefreshGrids();
    }

    private void SetFavorite(int favIndex, bool set)
    {
      if (this.SubListIndex == favIndex + 1)
        return;
      var selected = this.SelectedChannels(this.lastFocusedGrid);
      if (selected.Count == 0)
        return;
      this.editor.SetFavorites(selected, favIndex, set);
      this.dataRoot.NeedsSaving = true;
      this.RefreshGrids();
    }

    private void FillFavoritesMenu()
    {
      this.mnuFavSet.Items.Clear();
      this.mnuFavUnset.Items.Clear();
      if (this.serializer == null || this.dataRoot.SupportedFavorites == 0)
        return;
      var count = Math.Min(this.serializer.Features.MaxFavoriteLists, 26);
      for (int i = 0; i < count; i++)
      {
        var idx = i;
        var c = (char)('A' + i);
        var key = i < 9 ? Key.D1 + i : (i == 9 ? Key.D0 : Key.None);
        this.mnuFavSet.Items.Add(Item(c.ToString(), () => this.Run(() => this.SetFavorite(idx, true)), key != Key.None ? new KeyGesture(key, cmdKey) : null));
        this.mnuFavUnset.Items.Add(Item(c.ToString(), () => this.Run(() => this.SetFavorite(idx, false)), key != Key.None ? new KeyGesture(key, cmdKey | KeyModifiers.Shift) : null));
      }
    }

    internal void SetFavoritesText(ChannelRow row, string value)
    {
      var fav = ChannelInfo.ParseFavString(value ?? "") & this.dataRoot.SupportedFavorites;
      if (fav == row.Channel.Favorites)
        return;
      row.Channel.Favorites = fav;
      this.MarkDirty();
    }

    private async Task RenameInteractive()
    {
      if (this.currentList == null || this.currentList.ReadOnly)
        return;
      var ch = this.SelectedChannels(this.lastFocusedGrid).FirstOrDefault();
      if (ch == null)
        return;
      var name = await Dialogs.InputBox(this, "Rename channel", "New channel name:", ch.Name);
      if (name == null)
        return;
      this.RenameChannel(this.rowByChannel[ch], name);
      this.RefreshGrids();
    }

    internal void RenameChannel(ChannelRow row, string newName)
    {
      var ch = row.Channel;
      if (ch.Name == newName)
        return;
      var max = this.currentList?.MaxChannelNameLength ?? 0;
      if (max > 0 && newName != null && newName.Length > max)
        newName = newName.Substring(0, max);
      ch.Name = newName;
      ch.IsNameModified = true;
      this.MarkDirty();
    }

    internal void MarkDirty()
    {
      if (this.dataRoot == null)
        return;
      this.dataRoot.NeedsSaving = true;
      this.UpdateUiState();
    }

    #endregion

    #region ApplyReferenceList()

    private async Task ApplyReferenceList(string startDir = null)
    {
      if (this.dataRoot == null)
        return;

      startDir ??= settings.LastDirectory;
      IStorageFolder folder = null;
      if (!string.IsNullOrEmpty(startDir) && Directory.Exists(startDir))
        folder = await this.StorageProvider.TryGetFolderFromPathAsync(startDir);
      var files = await this.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
      {
        Title = "Select a reference list or another TV channel list",
        AllowMultiple = false,
        SuggestedStartLocation = folder
      });
      var path = files?.FirstOrDefault()?.TryGetLocalPath();
      Log.Write("reference list picker result: " + (path ?? "(cancelled)"));
      if (path == null)
        return;
      await this.ApplyReferenceFile(path);
    }

    private async Task ApplyReferenceFile(string path)
    {
      var (refSer, _) = await this.CreateAndLoadSerializer(path, null);
      if (refSer == null)
        return;

      try
      {
        var refRoot = refSer.DataRoot;
        bool anyMatch = refRoot.ChannelLists.Any(r =>
        {
          var tv = this.dataRoot.GetChannelList(r.SignalSource);
          return tv != null && tv.SignalSource == r.SignalSource && r.Channels.Count > 0;
        });

        if (anyMatch)
          this.editor.ApplyReferenceList(refRoot);
        else
        {
          // no list in the reference file has the same signal source: apply the largest reference list to the current TV list
          var refList = refRoot.ChannelLists.OrderByDescending(l => l.Channels.Count).FirstOrDefault();
          if (refList == null || refList.Channels.Count == 0 || this.currentList == null)
          {
            await Dialogs.MessageBox(this, "The reference list doesn't contain any channels.", "ChanSort", View.MessageBoxButtons.OK, View.MessageBoxIcon.Warning);
            return;
          }
          foreach (var ch in this.currentList.Channels)
            ch.SetPosition(0, -1);
          this.editor.ApplyReferenceList(refRoot, refList, 0, this.currentList, -1, false);
        }

        this.dataRoot.NeedsSaving = true;
        var listIndex = this.lstLists.SelectedIndex;
        this.FillChannelListTabs(listIndex);
        this.RefreshGrids();

        var unsorted = this.currentList?.Channels.Count(c => c.GetPosition(this.SubListIndex) < 0 && !c.IsDeleted) ?? 0;
        this.lblStatus.Text = $"Reference list applied: {Path.GetFileName(path)}" + (unsorted > 0 ? $" – {unsorted} channel(s) of the current list were not found in the reference list" : "");
      }
      finally
      {
        refSer.Dispose();
      }
    }

    #endregion

    #region Save(), SaveAs()

    private async Task<bool> Save()
    {
      if (this.serializer == null)
        return false;

      if (!await this.HandleChannelNumberGaps())
        return false;
      if (!await this.PromptHandlingOfUnsortedChannels())
        return false;
      if (this.serializer.Features.EnforceTvBeforeRadioBeforeData)
        this.editor.EnforceTvBeforeRadioBeforeData();

      var ser = this.serializer;
      this.SetBusy("Saving …");
      try
      {
        await Task.Run(() =>
        {
          foreach (var filePath in ser.GetDataFilePaths())
          {
            if (File.Exists(filePath))
            {
              var bakFile = filePath + ".bak";
              if (!File.Exists(bakFile))
                File.Copy(filePath, bakFile);
            }
          }
          ser.Save();
        });
        this.dataRoot.ValidateAfterSave();
        this.dataRoot.NeedsSaving = false;
      }
      catch (IOException ex)
      {
        this.SetBusy(null);
        await Dialogs.MessageBox(this, "The file could not be saved.\n" + ex.Message, "Error", View.MessageBoxButtons.OK, View.MessageBoxIcon.Error);
        return false;
      }
      finally
      {
        this.SetBusy(null);
      }

      this.RefreshGrids();
      this.lblStatus.Text = "Saved " + (ser.SaveAsFileName ?? ser.FileName) + " at " + DateTime.Now.ToShortTimeString();
      return true;
    }

    private async Task SaveAs()
    {
      if (this.serializer == null)
        return;
      if (!this.serializer.Features.CanSaveAs)
      {
        await Dialogs.MessageBox(this, "This file format can only be saved under its original name.", "ChanSort", View.MessageBoxButtons.OK, View.MessageBoxIcon.Information);
        return;
      }

      var folder = await this.StorageProvider.TryGetFolderFromPathAsync(Path.GetDirectoryName(this.currentFile) ?? "");
      var file = await this.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
      {
        Title = "Save channel list as",
        SuggestedFileName = Path.GetFileName(this.currentFile),
        SuggestedStartLocation = folder,
        DefaultExtension = Path.GetExtension(this.currentFile),
        ShowOverwritePrompt = true
      });
      var path = file?.TryGetLocalPath();
      if (path == null)
        return;
      this.serializer.SaveAsFileName = path;
      this.currentFile = path;
      await this.Save();
    }

    private async Task<bool> HandleChannelNumberGaps()
    {
      if (this.serializer.Features.CanHaveGaps)
        return true;

      if (this.ProcessChannelNumberGaps(true))
      {
        var res = await Dialogs.MessageBox(this, "This TV model doesn't allow gaps in the channel numbers.\n\nDo you want to renumber the channels consecutively?\n(Selecting \"No\" will save the list with gaps, which might cause problems on your TV)",
          "ChanSort", View.MessageBoxButtons.YesNoCancel, View.MessageBoxIcon.Question);
        if (res == View.DialogResult.Cancel)
          return false;
        if (res == View.DialogResult.Yes)
          this.ProcessChannelNumberGaps(false);
      }
      return true;
    }

    private bool ProcessChannelNumberGaps(bool testOnly)
    {
      var wasRenumbered = false;
      foreach (var list in this.dataRoot.ChannelLists)
      {
        if (list.IsMixedSourceFavoritesList)
          continue;
        var chNr = 1;
        foreach (var channel in list.Channels.OrderBy(c => c.NewProgramNr).ToList())
        {
          if (channel.IsDeleted || channel.NewProgramNr < 0)
            continue;

          if (channel.IsProxy && !testOnly)
          {
            list.Channels.Remove(channel); // remove proxy channels so they don't cause duplicate numbers
            continue;
          }

          if (channel.NewProgramNr == 0 && chNr == 1)
            chNr = 0;
          if (channel.NewProgramNr != chNr)
          {
            if (testOnly)
              return true;
            wasRenumbered = true;
            channel.NewProgramNr = chNr;
          }
          ++chNr;
        }
      }
      return wasRenumbered;
    }

    private async Task<bool> PromptHandlingOfUnsortedChannels()
    {
      var hasUnsorted = this.dataRoot.ChannelLists.Any(l => l.Channels.Any(c => c.NewProgramNr < 0 && !c.IsDeleted));
      var mode = UnsortedChannelMode.Delete;
      if (hasUnsorted)
      {
        var actions = new List<(string, int)> { ("Append them at the end of the list", (int)UnsortedChannelMode.Append) };
        if (this.serializer.Features.CanHideChannels)
          actions.Add(("Append them at the end of the list and hide them", (int)UnsortedChannelMode.AppendAndHide));
        if (this.serializer.Features.DeleteMode != SerializerBase.DeleteMode.NotSupported)
          actions.Add(("Delete them", (int)UnsortedChannelMode.Delete));
        actions.Add(("Cancel", -1));
        var res = await Dialogs.ActionBox(this, "Some channels were not added to the sorted list. What should happen with them?", actions, -1);
        if (res == -1)
          return false;
        mode = (UnsortedChannelMode)res;
      }

      this.dataRoot.AssignNumbersToUnsortedAndDeletedChannels(mode);
      return true;
    }

    #endregion

    #region SaveReferenceList()

    private async Task SaveReferenceList()
    {
      if (this.dataRoot == null)
        return;
      var dir = Path.GetDirectoryName(this.currentFile) ?? "";
      var folder = await this.StorageProvider.TryGetFolderFromPathAsync(dir);
      var file = await this.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
      {
        Title = "Save reference list",
        SuggestedFileName = Path.GetFileNameWithoutExtension(this.currentFile) + ".txt",
        SuggestedStartLocation = folder,
        DefaultExtension = ".txt",
        ShowOverwritePrompt = true,
        FileTypeChoices = new[]
        {
          new FilePickerFileType("ChanSort single-list (current list)") { Patterns = new[] { "*.txt" } },
          new FilePickerFileType("ChanSort multi-list (all lists)") { Patterns = new[] { "*.csv" } },
          new FilePickerFileType("SamToolBox") { Patterns = new[] { "*.chl" } },
        }
      });
      var path = file?.TryGetLocalPath();
      if (path == null)
        return;
      this.SaveReferenceFile(path);
    }

    private void SaveReferenceFile(string path)
    {
      var ext = Path.GetExtension(path).ToLowerInvariant();
      if (ext == ".csv")
        CsvRefListSerializer.Save(path, this.dataRoot);
      else
        TxtRefListSerializer.Save(path, this.currentList);
      this.lblStatus.Text = "Reference list saved: " + path;
    }

    #endregion

    #region ShowFileInformation(), ShowAbout()

    private async Task ShowFileInformation()
    {
      if (this.serializer == null)
        return;
      await Dialogs.ShowText(this, "File information", this.serializer.GetFileInformation(), true);
    }

    public async Task ShowAbout()
    {
      var version = Assembly.GetExecutingAssembly().GetName().Version;
      var loaders = string.Join("\n", this.plugins.Select(p => "  • " + p.PluginName));
      var text = $"ChanSort for macOS {version?.ToString(3)}\n\n" +
                 "Reorder the channel list of your TV.\n\n" +
                 "Original ChanSort by Horst Beham (PredatH0r): https://github.com/PredatH0r/ChanSort\n" +
                 $"macOS port: {GithubUrl}\n\n" +
                 "License: GNU General Public License v3\n" +
                 "THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND. USE AT YOUR OWN RISK.\n\n" +
                 "Loaded file format plugins:\n" + loaders;
      await Dialogs.ShowText(this, "About ChanSort", text);
    }

    private static void OpenUrl(string url)
    {
      try
      {
        if (OperatingSystem.IsMacOS())
          Process.Start("open", url);
        else if (OperatingSystem.IsLinux())
          Process.Start("xdg-open", url);
        else
          Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
      }
      catch
      {
        // ignore
      }
    }

    #endregion

    #region UpdateUiState(), UpdateStatus(), SetBusy()

    private void UpdateUiState()
    {
      var loaded = this.serializer != null;
      var editable = loaded && this.currentList != null && !this.currentList.ReadOnly;
      this.btnSave.IsEnabled = loaded;
      this.btnRefList.IsEnabled = loaded;
      this.btnInfo.IsEnabled = loaded;
      foreach (var b in new[] { this.btnUp, this.btnDown, this.btnRemove, this.btnSetNr, this.btnSortAz, this.btnRenum, this.btnAdd, this.btnAddAll })
        b.IsEnabled = editable;

      var edited = this.dataRoot?.NeedsSaving == true;
      if (string.IsNullOrEmpty(this.currentFile))
      {
        this.Title = "ChanSort";
        this.lblSubtitle.Text = "No channel list opened";
      }
      else
      {
        this.Title = "ChanSort – " + Path.GetFileName(this.currentFile) + (edited ? " — Edited" : "");
        this.lblSubtitle.Text = Path.GetFileName(this.currentFile) + (edited ? " — Edited" : "");
      }
      this.UpdateStatus();
    }

    private void UpdateStatus()
    {
      if (this.currentList == null)
      {
        this.lblStatusRight.Text = "";
        this.lblLeftCount.Text = this.lblRightCount.Text = "";
        return;
      }
      var sorted = this.leftView?.Count ?? 0;
      var total = this.currentList.Channels.Count;
      string Selected(DataGrid g) => g.SelectedItems.Count > 1 ? $" · {g.SelectedItems.Count} selected" : "";
      this.lblLeftCount.Text = sorted + Selected(this.gridLeft);
      this.lblRightCount.Text = total + Selected(this.gridRight);
      this.lblStatusRight.Text = (this.currentPlugin?.PluginName ?? "") + (this.currentList.ReadOnly ? "\nRead-only list (predefined by the TV)" : "");
      if (this.serializer != null && this.lblStatus.Text?.StartsWith("Open the channel") == true)
        this.lblStatus.Text = "";
    }

    private void SetBusy(string message)
    {
      this.busyOverlay.IsVisible = message != null;
      this.busyProgress.IsIndeterminate = message != null; // an indeterminate progress bar keeps animating (and using CPU) even when hidden
      if (message != null)
        this.lblBusy.Text = message;
    }

    #endregion

    #region TryExecute()

    private async Task TryExecute(Func<Task> action)
    {
      if (this.IsSheetOpen)
        return;
      try
      {
        await action();
      }
      catch (Exception ex)
      {
        Log.Write("Error: " + ex);
        this.SetBusy(null);
        await Dialogs.ShowText(this, "Error", ex.ToString(), true);
      }
    }

    #endregion

    #region SidebarItem, icons

    public sealed class SidebarItem
    {
      public SidebarItem(string caption, Geometry icon, string count, object tag)
      {
        this.Caption = caption;
        this.Icon = icon;
        this.Count = count;
        this.Tag = tag;
      }

      public string Caption { get; }
      public Geometry Icon { get; }
      public string Count { get; }
      public object Tag { get; }
    }

    private Geometry Icon(string key) => this.TryFindResource(key, out var res) ? res as Geometry : null;

    private Geometry GetListIcon(ChannelList list)
    {
      var src = list.SignalSource;
      if (list.IsMixedSourceFavoritesList)
        return this.Icon("IconStar");
      if ((src & SignalSource.MaskTvRadioData) == SignalSource.Radio)
        return this.Icon("IconRadio");
      if ((src & SignalSource.MaskBcastSystem) == SignalSource.Ip)
        return this.Icon("IconWeb");
      if ((src & SignalSource.MaskBcastMedium) == SignalSource.Sat)
        return this.Icon("IconSatellite");
      return this.Icon("IconTv");
    }

    #endregion

    #region Title bar: dragging, zooming, transparency

    private void TitleArea_PointerPressed(object sender, PointerPressedEventArgs e)
    {
      if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        return;
      if (e.Source is Visual v && (v is Button || v.FindAncestorOfType<Button>() != null))
        return;
      if (e.ClickCount == 2)
        this.WindowState = this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
      else
        this.BeginMoveDrag(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
      base.OnPropertyChanged(change);
      // without window transparency (vibrancy) the window needs an opaque background
      if (change.Property == ActualTransparencyLevelProperty && OperatingSystem.IsMacOS())
      {
        if (this.ActualTransparencyLevel == WindowTransparencyLevel.None)
          this[!BackgroundProperty] = new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("CsWindowBackground");
        else
          this.Background = Brushes.Transparent;
      }
    }

    #endregion

    #region Sheets

    private readonly List<(Control overlay, IInputElement previousFocus)> openSheets = new();

    public bool IsSheetOpen => this.openSheets.Count > 0;

    internal Panel SheetHost => this.sheetHost;

    /// <summary>
    /// Shows a dialog as a macOS style sheet that slides out below the toolbar. Returns a handle for CloseSheet()
    /// </summary>
    internal object ShowSheet(Control content, double width, Control focus)
    {
      var top = this.toolbar.TranslatePoint(new Point(0, this.toolbar.Bounds.Height), this)?.Y ?? 52;
      var card = new Border
      {
        Classes = { "sheet" },
        Width = Math.Min(width, Math.Max(320, this.Bounds.Width - 60)),
        MaxHeight = Math.Max(200, this.Bounds.Height - top - 40),
        HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
        VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Top,
        Margin = new Thickness(0, top + 6, 0, 0),
        Child = content,
        Opacity = 0,
        RenderTransform = TransformOperations.Parse("translateY(-14px)"),
        Transitions = new Transitions
        {
          new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(160) },
          new TransformOperationsTransition { Property = RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(180), Easing = new global::Avalonia.Animation.Easings.CubicEaseOut() }
        }
      };
      var overlay = new Panel { Children = { card } };
      overlay[!Panel.BackgroundProperty] = new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("CsDim");

      var previousFocus = this.FocusManager?.GetFocusedElement();
      this.openSheets.Add((overlay, previousFocus));
      this.sheetHost.Children.Add(overlay);
      this.sheetHost.IsVisible = true;

      Dispatcher.UIThread.Post(() =>
      {
        card.Opacity = 1;
        card.RenderTransform = TransformOperations.Parse("translateY(0px)");
        (focus ?? content).Focus();
      }, DispatcherPriority.Background);
      return overlay;
    }

    internal void CloseSheet(object handle)
    {
      var idx = this.openSheets.FindIndex(s => s.overlay == handle);
      if (idx < 0)
        return;
      var (overlay, previousFocus) = this.openSheets[idx];
      this.openSheets.RemoveAt(idx);
      this.sheetHost.Children.Remove(overlay);
      this.sheetHost.IsVisible = this.openSheets.Count > 0;
      if (this.openSheets.Count == 0)
        previousFocus?.Focus();
    }

    #endregion

    #region MainWindow_Closing

    private async void MainWindow_Closing(object sender, WindowClosingEventArgs e)
    {
      if (this.closeConfirmed || this.dataRoot == null || !this.dataRoot.NeedsSaving)
      {
        this.SaveWindowSettings();
        return;
      }

      e.Cancel = true;
      if (await this.PromptSaveAndContinue())
      {
        this.closeConfirmed = true;
        this.SaveWindowSettings();
        this.Close();
      }
    }

    private void SaveWindowSettings()
    {
      if (this.WindowState == WindowState.Normal)
      {
        settings.WindowWidth = this.Width;
        settings.WindowHeight = this.Height;
      }
      settings.Save();
    }

    #endregion
  }
}
