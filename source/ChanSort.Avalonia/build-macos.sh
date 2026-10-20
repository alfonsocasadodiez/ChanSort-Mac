#!/bin/bash
# Builds ChanSort.app for macOS (Apple Silicon and Intel) and packages it as .zip and .dmg files.
# Usage: ./build-macos.sh [version]      e.g. ./build-macos.sh 2026.10.20
# Requires the .NET 10 SDK. The resulting app bundles are self-contained (no .NET installation needed).
set -euo pipefail

cd "$(dirname "$0")"
VERSION="${1:-$(date +%Y.%m.%d)}"
PROJECT="ChanSort.Avalonia.csproj"
OUT="$(cd ..; pwd)/dist-macos"

rm -rf "$OUT"
mkdir -p "$OUT"

for RID in osx-arm64 osx-x64; do
  echo "=== Building $RID ($VERSION)"
  PUBLISH="$OUT/publish-$RID"
  dotnet publish "$PROJECT" -c Release -r "$RID" --self-contained true \
    -p:Version="$VERSION" -p:UseAppHost=true -p:DebugType=None -p:DebugSymbols=false \
    -o "$PUBLISH"

  APP="$OUT/$RID/ChanSort.app"
  mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
  cp -R "$PUBLISH/" "$APP/Contents/MacOS/"
  cp Assets/ChanSort.icns "$APP/Contents/Resources/"
  sed "s/__VERSION__/$VERSION/g" macOS/Info.plist > "$APP/Contents/Info.plist"
  chmod +x "$APP/Contents/MacOS/ChanSort"

  # ad-hoc signature (required to run on Apple Silicon); the app is not notarized
  codesign --force --deep --sign - "$APP"

  ARCH=$([ "$RID" = "osx-arm64" ] && echo "AppleSilicon" || echo "Intel")
  ditto -c -k --sequesterRsrc --keepParent "$APP" "$OUT/ChanSort_${VERSION}_macOS_${ARCH}.zip"

  DMGDIR="$OUT/dmg-$RID"
  mkdir -p "$DMGDIR"
  cp -R "$APP" "$DMGDIR/"
  ln -s /Applications "$DMGDIR/Applications"
  hdiutil create -volname "ChanSort" -srcfolder "$DMGDIR" -ov -format UDZO "$OUT/ChanSort_${VERSION}_macOS_${ARCH}.dmg" >/dev/null
  rm -rf "$DMGDIR" "$PUBLISH"
done

echo "=== Done:"
ls -lh "$OUT"/*.zip "$OUT"/*.dmg
