# WvW Summary Tool

Portable Windows companion app for Guild Wars 2. It reads the newest ArcDPS WvW EVTC/ZEVTC log and copies a compact, 199-character-safe fight summary to the clipboard. It never sends input to the game.

## Usage

1. Start `WvWSummaryTool.exe`.
2. Open **Einstellungen …** to change the automatically detected ArcDPS log folder or the global hotkey.
3. After a fight, press your configured hotkey (default: `F8`).
4. In Guild Wars 2, paste the copied line into chat yourself.

Settings are saved per Windows user in `%LOCALAPPDATA%\WvWSummaryTool\settings.json`
and restored on the next start. Choose F1–F24 (except the Windows-reserved F12),
optionally combined with Ctrl, Alt and Shift. Unavailable shortcuts are rejected
without changing your existing settings. The copy button also works if Windows
cannot register the saved shortcut at startup.

Use the separate red, green and blue buttons either to copy the five highest
specialization totals or the five highest individual player-damage values for that
team. These buttons intentionally have no global shortcut.

Open **History …** to select an older fight and copy its summary, top classes,
or top individual player damage again, even if the source log is no longer available.
The most recent imported fights are saved in
`%LOCALAPPDATA%\WvWSummaryTool\history.json` and restored on startup.
Entries are ordered by the log's modification time; copying the same source path
again updates its entry without creating a duplicate. On startup and every ten
seconds while the app is running, the configured log folder (including subfolders)
is scanned in the background for the latest readable WvW logs. Under
**Einstellungen … → History-Limit**, choose 1–1,000 fights (default: 100).
Lowering the limit removes the oldest history entries, leaving the source logs
available for reimport if the limit is raised later. New or changed
EVTC/ZEVTC files are imported automatically without changing the clipboard.
Unchanged imported files are skipped; recently modified, locked or incomplete
files are retried on a later scan. An open History window updates automatically.

The History window includes a map with objective labels and a movement timeline.
Green marks the first recorded position, red the current/end position and cyan
the movement up to the selected time. Drag the slider to inspect the route.
The recording player's positions are preferred; otherwise the route uses the
squad's average position, labelled **Squad-Mittelpunkt**. This is not a replay
of every player. Missing samples and large jumps are not joined by a line.
Logs without usable position data show no route.

The game map backgrounds used by [GW2 Elite Insights](https://github.com/baaron4/GW2-Elite-Insights-Parser)
are bundled with the app and work offline. Objective markers and movement are
drawn over the terrain using the matching map coordinates.
Positions and combat data are never uploaded. Older history entries are upgraded
by the automatic scan if their source files are still available; otherwise their
existing summaries remain usable without a movement route.

Example: `1:13 | 38r v 34b | Rot: 0 deaths/1Down/2.4M | Blau: 31 deaths/42Down/572.1k`

When the log contains map metadata, the summary starts with **EBG**, **BBL**,
**GBL**, **RBL** or **EotM**. Valid combat positions can add an approximate nearby
objective, for example `BBL ~ Hills | 1:13 | …`. The `~` means "near", not necessarily
inside the objective. Moving fights without a dominant place and logs without
usable positions show only the map. Logs without map metadata keep the original
format. Location data is bundled for offline use; no API key is needed.
If space is tight, the place (then the map) is omitted to preserve room for combat
statistics within the 199-character chat limit.

Top damage example: `red top players: 1 Reaper 1.8M | 2 Scourge 1.5M | 3 Berserker 1.2M | ...`

Class total example: `red top classes: 2 Catalyst 1.5M | 5 Core Necromancer 1.3M | 7 Dragonhunter 1.3M | ...`

The EVTC parsing logic is independently adapted from the MIT-licensed [WvW Fight Analysis Addon](https://github.com/jake-greygoose/WvW-Fight-Analysis-Addon).
