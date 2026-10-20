Links
-----
[![EN](https://chansort.com/img/flag_en_24.png)](https://github.com/PredatH0r/ChanSort/blob/master/readme.md)
[![DE](https://chansort.com/img/flag_de_16.png)](https://github.com/PredatH0r/ChanSort/blob/master/readme_de.md)
[![PL](https://chansort.com/img/flag_pl_16.png)](https://github.com/PredatH0r/ChanSort/blob/master/readme_pl.md)
[![TR](https://chansort.com/img/flag_tr_16.png)](https://github.com/PredatH0r/ChanSort/blob/master/readme_tr-TR.md) |
[Download (Windows)](https://github.com/PredatH0r/ChanSort/releases) | 
[Download (macOS)](https://github.com/alfonsocasadodiez/ChanSort/releases/latest) | 
[Documentation](https://github.com/PredatH0r/ChanSort/wiki) |
[Forum](https://github.com/PredatH0r/ChanSort/issues) | 
[E-Mail](mailto:horst@beham.biz)

About ChanSort
--------------
ChanSort is a Windows and macOS application that allows you to reorder your TV's channel list.  
Most modern TVs can transfer channel lists via USB stick, which you can plug into your PC or Mac.  
ChanSort supports [file formats from numerous brands](#supported-tv-models) and can copy program numbers and
favorites from one file to another, even between different models and brands.

![screenshot](http://beham.biz/chansort/ChanSort-en.png)

Features
--------
- Reorder channels (change numbers directly, move up/down, drag&drop, double-click)
- Use another channel list as a reference to apply the same ordering
- Multi-selection for editing multiple channels at once
- Single-list view (showing assigned channels first and then all unassigned channels)
- Side-by-side view of new/sorted list and original/complete list (similar to playlist and library)
- Rename or delete channels
- Manage favorites, parental lock, channel skipping (when zapping), channel hiding
- User interface in English, German, Spanish, Turkish, Portuguese, Russian and Romanian
- Unicode character support for channel names (Latin, Cyrillic, Greek, ...)

NOT supported:
- adding new transponders or channels
- changing tuner related properties of channels (ONID, TSID, SID, frequency, APID, VPID, ...)

Some features may not be available on all TV models and channel types (analog, digital, sat, cable, ...)

! USE AT YOUR OWN RISK !
------------------------
Most of this software was written without support from TV manufacturers or access to any official 
documentation about the file formats. It is solely based on analysing existing data files, trial and error.
There is a chance of unwanted side-effects or even damage to your TV, as reported in 2 cases.

Hisense is the only manufacturer who provided technical information and a test device.

Supported TV models 
-------------------
ChanSort supports a large number of file formats, but it's impossible to tell for every brand and TV model 
what file format it uses (with may even change with firmware updates).  
This list gives some examples of what should be supported, but even if your model or brand is not in this list, 
it may work anyway:
- [Samsung](source/fileformats.md#samsung)
- [LG](source/fileformats.md#lg)
- [Sony](source/fileformats.md#sony)
- [Hisense](source/fileformats.md#hisense)
- [Panasonic](source/fileformats.md#panasonic)
- [TCL](source/fileformats.md#tcl)
- [Philips](source/fileformats.md#philips)
- [Sharp, Dyon, Blaupunkt, Hisense, Changhong, Grundig, alphatronics, JTC Genesis, ...](source/fileformats.md#sharp)
- [Toshiba](source/fileformats.md#toshiba)
- [Grundig](source/fileformats.md#grundig)
- [SatcoDX: ITT, Medion, Nabo, ok., PEAQ, Schaub-Lorenz, Silva-Schneider, Telefunken, ...](source/fileformats.md#satcodx)
- [DBM: Xoro, Strong, TechniSat, ...](source/fileformats.md#dbm)
- [Loewe](source/fileformats.md#hisense)
- [Vision Edge 4K](source/fileformats.md#visionedge)
- [VDR](source/fileformats.md#vdr)
- [SAT>IP m3u](source/fileformats.md#m3u)
- [Enigma2](source/fileformats.md#enigma2)


System requirements
-------------------
**Windows**:  
- Windows 7 SP1, Windows 8.1, Windows 10 v1606 or later, Windows 11 (with x86, x64 or ARM CPU)
- [Microsoft .NET Framework 4.8](https://dotnet.microsoft.com/download/dotnet-framework)
- The .NET FW 4.8 does NOT work with Windows 7 without SP1, Windows 8 or Windows 10 prior to v1606

**Linux**:  
- wine (sudo apt-get install wine)
- winetricks (sudo apt-get install winetricks)
- start winetricks, select or create a wineprefix (32 bit or 64 bit), select
  "Install Windows DLL or component" and install the "dotnet48" package and ignore dozens of message boxes
- right-click on ChanSort.exe and select "open with", "all applications", "A wine application"  
Last tested with Ubuntu 24.10, wine-9.0, winetricks 20240105

Or using Docker: https://github.com/PredatH0r/ChanSort/issues/285

**Mac** (native app):  
- macOS 12 (Monterey) or later, on Apple Silicon (M1/M2/M3/M4…) or Intel Macs. No .NET installation is needed.
- Download the `.dmg` (or `.zip`) for your Mac from the [macOS releases page](https://github.com/alfonsocasadodiez/ChanSort/releases/latest):
  - `ChanSort_<version>_macOS_AppleSilicon.dmg` for Macs with an Apple chip
  - `ChanSort_<version>_macOS_Intel.dmg` for Intel Macs  
  (Apple menu / About This Mac shows which chip your Mac has)
- Open the .dmg and drag ChanSort into your Applications folder.
- The app is not notarized by Apple, so the first launch is blocked by Gatekeeper. To allow it, either
  - try to open it once, then go to System Settings / Privacy & Security and click "Open Anyway" (macOS 15 and later), or
  - right-click ChanSort in Applications, choose "Open" and confirm (macOS 14 and earlier), or
  - run `xattr -dr com.apple.quarantine /Applications/ChanSort.app` in Terminal.
- Open your channel list with File / Open, or drag & drop the file onto the ChanSort window.

The macOS version uses the same file format loaders as the Windows version, with a simplified user interface:
- sorted list (left) and full channel list (right), with buttons/shortcuts to add, remove, move (⌘↑ / ⌘↓), renumber and sort channels
- edit numbers and names directly in the grid, favorites, parental lock, skip and hide flags
- apply reference lists (including the included sample lists), save reference lists, choose the character set
- not available on macOS (yet): drag & drop inside the grids, printing / Excel export, the LG "TV settings" dialog,
  user interface languages other than English, auto-update check

Alternatively, you can run the Windows version in a Windows VM (Parallels, UTM, VMware Fusion).

**Hardware**:
- USB stick/SD-card to transfer the channel list between your TV and PC. A stick <= 32 GB with FAT32 file system 
is STRONGLY recommended. (Some TVs write garbage to NTFS and don't support exFAT at all)

Build from source
-----------------
See [build.md](source/build.md)  
macOS: install the [.NET 10 SDK](https://dotnet.microsoft.com/download) and run `source/ChanSort.Avalonia/build-macos.sh`

License (GPLv3)
---------------
GNU General Public Licence, Version 3: http://www.gnu.org/licenses/gpl.html  
Source code is available on https://github.com/PredatH0r/ChanSort

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.

IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY CLAIM, DAMAGES OR
OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
OTHER DEALINGS IN THE SOFTWARE.
