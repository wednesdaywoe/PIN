# The Client's UI Is Readable Source

The whole 1962 interface ships as loose, uncompiled Lua and XML under
`Firefall/system/gui/components/`. Nothing is packed and nothing is compiled: the components are
plain text, with original author names and commented-out code still in them.

**This is ground truth for what the client expects, and it outranks guessing.** A capture says what
the wire carried; the UI source says what the client *does* with it, including which paths were
switched off before the servers went dark. Read it before inventing a message shape.

The client lives at
`~/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/Firefall/system/gui`, and
`components/MainUI/` holds nearly everything a player sees.

The engine exposes itself to Lua as `Game.*`, `Player.*` and `Component.*` calls, and pushes
messages in as named events bound in each component's `.xml`. So a component tells you three
things a packet capture cannot:

- which engine call sends a request (`Game.RequestKnownResourceLocations()`),
- what field names the engine hands back (`plot.x`, `plot.radius`, `plot.composition[i].percent`),
- whether the event that would draw it is still bound at all.

## Reading the resource layer, as an example

The M4 milestone was built on scan messages the client no longer listens to. Three components
settled it in an afternoon (2026-08-13):

| Component | Fed by | State in 1962 |
|-----------|--------|---------------|
| `HUD/Heatmap/Heatmap.lua` | `ResourceLocationInfosResponse` (2:140) | requests the data, but the event that draws it is commented out |
| `Panels/WorldMap/WorldMap_ResourceScans.lua` | each outpost's `NearbyResourceItems` | alive; this is the map layer a player sees |
| `HUD/ResourceScans/ResourceScans.lua` | `GeographicalReportResponse` (2:139) | alive and fully bound |

The heatmap's `DequeuePlots` reads `plot.x/y/z`, `plot.radius` and
`plot.composition[i].{itemTypeId, percent}` — **which confirms the field order of
`ResourceLocationInfo` that the capture could not**, because the one recorded retail response was
empty. `Unk1/2/3` are the position, `Unk4` is the radius, and the inner pair is item id and
percent.

## The heatmap patch

Two lines, applied 2026-08-13, both in
`components/MainUI/HUD/Heatmap/`. Backups sit beside them as `*.preheatmap.bak`.

`Heatmap.xml` — Red 5 commented out the binding, so `OnHeatmapUpdated` could never fire and the
deposit blobs could never be drawn:

```diff
-    <!--
-		<Event name="ON_HEATMAP_UPDATED"  bind="OnHeatmapUpdated"/>
-		-->
+		<Event name="ON_HEATMAP_UPDATED"  bind="OnHeatmapUpdated"/>
```

`Heatmap.lua` — the default filter is the *string* `"none"`, which `DequeuePlots` compares against
item ids. It never matches, so every plot scores zero heat and is skipped. Only `nil` means
show-everything, and the map panel that used to send `nil` is gone from this build:

```diff
-	UpdateFilter(FILTER_NONE);
+	UpdateFilter(nil);
```

To undo, restore the backups:

```
cd ~/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/Firefall/system/gui/components/MainUI/HUD/Heatmap
cp Heatmap.xml.preheatmap.bak Heatmap.xml
cp Heatmap.lua.preheatmap.bak Heatmap.lua
```

**Edit these files as bytes, not as text.** They are CRLF throughout; a tool that rewrites line
endings turns a two-line change into a whole-file rewrite and makes future diffs useless.

This is a client-side change and travels with the install, not with the server — a second machine
needs it applied again. It is checked by
[Thump Placement S1](In-Game-Tests/Thump-Placement.md), which passes on the outpost readout alone,
so a revert costs the overlay and nothing else.
