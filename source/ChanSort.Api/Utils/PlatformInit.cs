#if NET
using System.Runtime.CompilerServices;
using System.Text;

namespace ChanSort.Api
{
  internal static class PlatformInit
  {
    /// <summary>
    /// .NET (Core) only ships UTF and ASCII encodings by default. Loaders rely on Windows/ISO code pages (e.g. 1252, iso-8859-x),
    /// so the code pages provider must be registered before any loader code runs.
    /// </summary>
    [ModuleInitializer]
    internal static void Init()
    {
      Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }
  }
}
#endif
