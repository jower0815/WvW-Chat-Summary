# WvW Summary Tool

Portable Windows companion app for Guild Wars 2. It reads the newest ArcDPS WvW EVTC/ZEVTC log and copies a compact, 199-character-safe fight summary to the clipboard. It never sends input to the game.

## Usage

1. Start `WvWSummaryTool.exe`.
2. Check the automatically detected ArcDPS log folder.
3. After a fight, press `F8`.
4. In Guild Wars 2, paste the copied line into chat yourself.

Use the separate red, green and blue buttons either to copy the five highest
specialization totals or the five highest individual player-damage values for that
team. These buttons intentionally have no global shortcut.

Example: `1:13 | 38r v 34b | Rot: 0D/1Down/2.4M | Blau: 31D/42Down/572.1k`

Top damage example: `red top players: 1 Reaper 1.8M | 2 Scourge 1.5M | 3 Berserker 1.2M | ...`

Class total example: `red top classes: 2 Catalyst 1.5M | 5 Core Necromancer 1.3M | 7 Dragonhunter 1.3M | ...`

The EVTC parsing logic is independently adapted from the MIT-licensed [WvW Fight Analysis Addon](https://github.com/jake-greygoose/WvW-Fight-Analysis-Addon).
