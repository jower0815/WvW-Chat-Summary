# WvW Summary Tool

Portable Windows companion app for Guild Wars 2. It reads the newest ArcDPS WvW EVTC/ZEVTC log and copies a compact, 199-character-safe fight summary to the clipboard. It never sends input to the game.

## Usage

1. Start `WvWSummaryTool.exe`.
2. Check the automatically detected ArcDPS log folder.
3. After a fight, press `F8`.
4. In Guild Wars 2, paste the copied line into chat yourself.

Example: `1:13 | 38r v 34b | Rot: 0D/1Down/2.4M | Blau: 31D/42Down/572.1k`

## Build

```powershell
dotnet build
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The EVTC parsing logic is independently adapted from the MIT-licensed [WvW Fight Analysis Addon](https://github.com/jake-greygoose/WvW-Fight-Analysis-Addon).
