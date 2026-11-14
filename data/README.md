# WvW location data

## Seared Borderlands beta (2026-11-14)

Map IDs 1651 (green), 1652 (red), and 1654 (blue) use the embedded
`wvw-beta-maps.json` snapshot from
https://api.guildwars2.com/v2/maps?ids=1651,1652,1654&lang=en.
All three use the shared wiki image:
https://wiki.guildwars2.com/images/c/c1/Seared_Borderlands_map.jpg
(2560 × 3584, © ArenaNet; contains existing developer annotations).
The north-up image matches the API map aspect ratio and is projected across the
API map rectangle. Alignment has been checked with synthetic coordinates, not
yet calibrated against an actual beta fight.
No objective data, target markers, or nearby-objective labels are enabled for
the beta maps. Older beta entries are reimported when their logs remain available.

Public ArenaNet API snapshot retrieved 2026-09-26, embedded in the executable:

- https://api.guildwars2.com/v2/maps?ids=38,94,95,96,1099,968&lang=en
- https://api.guildwars2.com/v2/wvw/objectives?ids=all&lang=en

Objectives retain only camps, towers, keeps and castles with coordinates on the
included maps. Map 94 is no longer returned by the API; it has a code-only fallback.
Objective coordinates use the continent coordinate system; EVTC positions use
map coordinates. Convert using `map_rect` and `continent_rect`, reversing Y.
Location parsing and the History map work offline. The embedded game map images
(© ArenaNet) are sourced from [GW2 Elite Insights' image references](https://github.com/baaron4/GW2-Elite-Insights-Parser/blob/master/GW2EI.Library/GW2EI.Services/GW2EIEvtcParser/ParserHelpers/Images/LogImages.cs):

- Eternal Battlegrounds: https://i.imgur.com/t0khtQd.png
- Alpine Borderlands (both colors): https://i.imgur.com/nVu2ivF.png
- Desert Borderlands: https://i.imgur.com/R5p9fqw.png
- Edge of the Mists: https://i.imgur.com/iEpKYL0.jpg

The image bounds come from [Elite Insights' WvW replay geometry](https://github.com/baaron4/GW2-Elite-Insights-Parser/blob/master/GW2EI.Library/GW2EI.Services/GW2EIEvtcParser/LogLogic/WvW/WvWLogic.cs).
The Eternal Battlegrounds image is offset 950 game units east and 2250 north
relative to the API rectangle. Tracks and markers receive the same correction
at draw time, so existing History entries do not need to be reimported.

EVTC event definitions: https://www.deltaconnected.com/arcdps/evtc/README.txt
(`CBTS_MAPID` = 25, `CBTS_POSITION` = 19, `CBTS_MAPCHANGE` = 65).

The place is an estimate, not proof that combat occurred inside an objective:
prefer POV positions within combat start/end, otherwise squad positions. Each
observed second votes for the nearest objective within 12,000 game units.
Require three observed seconds and at least 60% agreement; otherwise show only
the map. Invalid positions, unknown maps and map transitions have no place label.
API names remain English, with short names Bay/Hills/Garrison on alpine maps and
SM for Stonemist Castle.
