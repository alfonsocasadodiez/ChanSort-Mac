using System.ComponentModel;
using System.Text;
using ChanSort.Api;

namespace ChanSort.Avalonia
{
  /// <summary>
  /// Bindable wrapper around a ChannelInfo. The same row object is shown in both grids.
  /// "Position" refers to the currently selected sub list (0=program number, 1..n=favorite list n)
  /// </summary>
  public class ChannelRow : INotifyPropertyChanged
  {
    private readonly MainWindow owner;

    public ChannelRow(ChannelInfo channel, MainWindow owner)
    {
      this.Channel = channel;
      this.owner = owner;
    }

    public ChannelInfo Channel { get; }

    public event PropertyChangedEventHandler PropertyChanged;

    public void NotifyAll() => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));

    private int SubList => this.owner.SubListIndex;

    public int Position
    {
      get => this.Channel.GetPosition(this.SubList);
      set => this.owner.SetChannelNumber(this, value);
    }

    public int OldPosition => this.Channel.GetOldPosition(this.SubList);

    public string PositionText => this.Position < 0 ? "" : this.Position.ToString();

    public string OldPositionText => this.OldPosition < 0 ? "" : this.OldPosition.ToString();

    public string Name
    {
      get => this.Channel.Name;
      set => this.owner.RenameChannel(this, value);
    }

    public string ShortName
    {
      get => this.Channel.ShortName;
      set
      {
        if (this.Channel.ShortName == value)
          return;
        this.Channel.ShortName = value;
        this.owner.MarkDirty();
      }
    }

    public string Favorites
    {
      get => GetFavString(this.Channel.Favorites);
      set => this.owner.SetFavoritesText(this, value);
    }

    public bool Lock => this.Channel.Lock;
    public bool Skip => this.Channel.Skip;
    public bool Hidden => this.Channel.Hidden;
    public bool IsDeleted => this.Channel.IsDeleted;
    public bool IsProxy => this.Channel.IsProxy;
    public bool IsUnsorted => this.Position < 0;
    public string Encrypted => this.Channel.Encrypted == true ? "✓" : "";
    public string Source => this.Channel.Source;
    public string ServiceTypeName => this.Channel.ServiceTypeName;
    public string ServiceType => this.Channel.ServiceType == 0 ? "" : this.Channel.ServiceType.ToString();
    public string FreqInMhz => this.Channel.FreqInMhz == 0 ? "" : this.Channel.FreqInMhz.ToString("0.###");
    public string ChannelOrTransponder => this.Channel.ChannelOrTransponder;
    public string Satellite => this.Channel.Satellite;
    public string Polarity => this.Channel.Polarity == 0 ? "" : this.Channel.Polarity.ToString();
    public string OriginalNetworkId => this.Channel.OriginalNetworkId == 0 ? "" : this.Channel.OriginalNetworkId.ToString();
    public string TransportStreamId => this.Channel.TransportStreamId == 0 ? "" : this.Channel.TransportStreamId.ToString();
    public string ServiceId => this.Channel.ServiceId == 0 ? "" : this.Channel.ServiceId.ToString();
    public string SymbolRate => this.Channel.SymbolRate == 0 ? "" : this.Channel.SymbolRate.ToString();
    public string Provider => this.Channel.Provider;
    public string NetworkName => this.Channel.NetworkName;
    public string Uid => this.Channel.Uid;

    public string LockText => this.Channel.Lock ? "🔒" : "";
    public string SkipText => this.Channel.Skip ? "✓" : "";
    public string HiddenText => this.Channel.Hidden ? "✓" : "";
    public string DeletedText => this.Channel.IsDeleted ? "✓" : "";

    /// <summary>
    /// sort key for the left list, which puts channels without a number at the end
    /// </summary>
    public int SortKey => this.Position < 0 ? int.MaxValue : this.Position;

    internal static string GetFavString(Favorites fav)
    {
      if (fav == 0)
        return string.Empty;

      var sb = new StringBuilder();
      int i = 0;
      for (var mask = (ulong)fav; mask != 0; mask >>= 1)
      {
        if (i >= 26)
        {
          sb.Append('+');
          break;
        }

        if ((mask & 1) != 0)
          sb.Append((char)('A' + i));
        ++i;
      }
      return sb.ToString();
    }
  }
}
