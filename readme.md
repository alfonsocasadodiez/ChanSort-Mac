Links
-----
[![EN](https://chansort.com/img/flag_en_24.png)](https://github.com/PredatH0r/ChanSort/blob/master/readme.md)
[![DE](https://chansort.com/img/flag_de_16.png)](https://github.com/PredatH0r/ChanSort/blob/master/readme_de.md)
[![PL](https://chansort.com/img/flag_pl_16.png)](https://github.com/PredatH0r/ChanSort/blob/master/readme_pl.md)
[![TR](https://chansort.com/img/flag_tr_16.png)](https://github.com/PredatH0r/ChanSort/blob/master/readme_tr-TR.md) |
[Download (Windows)](https://github.com/PredatH0r/ChanSort/releases) | 
[Download (macOS)](https://github.com/alfonsocasadodiez/ChanSort-Mac/releases/latest) | 
[Documentation](https://github.com/PredatH0r/ChanSort/wiki) |
[Forum](https://github.com/PredatH0r/ChanSort/issues) | 
[E-Mail](mailto:horst@beham.biz)

ChanSort for macOS
------------------
**A native Mac version of ChanSort**, for Apple Silicon and Intel Macs. It uses the same file format support as
ChanSort for Windows, with a user interface designed for macOS (light and dark appearance, system accent color).

![ChanSort for macOS (dark appearance)](docs/macos/chansort-mac-dark.png)

**[Download the latest macOS version](https://github.com/alfonsocasadodiez/ChanSort-Mac/releases/latest)**
(`…_macOS_AppleSilicon.dmg` for Macs with an Apple chip, `…_macOS_Intel.dmg` for Intel Macs; macOS 12 or later, nothing else to install)

What's in the macOS version:
- **Mac-style window**: sidebar with your TV's channel lists (TV, radio, satellite, cable, IP, favorites) and the
  favorite lists, a unified toolbar, search fields, and button bars below each table
- **Sorted list and all channels side by side**: add channels with double-click / ↵ / +, remove with ⌫ / −,
  move them with ⌘↑ / ⌘↓, type a new number or name directly in the sorted list
- **Set number, sort by name, renumber without gaps, add all unsorted channels** with one click
- **Favorites, parental lock, skip and hide** flags, including TVs with sorted and mixed-source favorite lists
- **Reference lists**: copy the order from another TV file or a ChanSort .txt/.csv list (17 sample lists are included),
  and save your own reference lists
- **Dialogs as macOS sheets**, menu bar with keyboard shortcuts, drag & drop of files onto the window or the Dock icon,
  recent files, character set selection, file information
- A `.bak` copy of the original file is kept when saving

| Light appearance | Start dialog |
|---|---|
| ![ChanSort for macOS (light appearance)](docs/macos/chansort-mac-light.png) | ![Start dialog shown as a sheet](docs/macos/chansort-mac-start-sheet.png) |

Keyboard shortcuts: ⌘O open, ⌘S save, ⇧⌘S save as, ⌘R apply reference list, ⌘I file information,
⌘↑/⌘↓ move, ⌘⌫ remove, ⇧⌘N set number, ⌘E rename, ⌘1…⌘9 add to favorites A…I (⇧ to remove),
⌘L / ⌘K / ⇧⌘H toggle lock / skip / hide, ⌥⌘1…9 switch channel list.

**Installation**
1. Open the .dmg and drag ChanSort into your Applications folder.
2. The app is not notarized by Apple, so macOS blocks the first launch. Open it once, then go to
   System Settings / Privacy & Security and click "Open Anyway" (on macOS 14 and earlier: right-click the app and choose "Open"),
   or run `xattr -dr com.apple.quarantine /Applications/ChanSort.app` in Terminal.
3. Open your channel list with File / Open, or drop the file onto the window.

Not available on macOS (yet): drag & drop inside the tables, printing / Excel export, the LG "TV settings" dialog,
user interface languages other than English.

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

**Mac**:  
- macOS 12 (Monterey) or later, Apple Silicon or Intel. See [ChanSort for macOS](#chansort-for-macos) for download and installation.
- Alternatively, the Windows version can be used in a Windows VM (Parallels, UTM, VMware Fusion).

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
