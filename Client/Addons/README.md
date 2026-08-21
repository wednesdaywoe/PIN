# Client addons

Lua the client loads that PIN wrote. Kept here rather than in the game folder because the game folder
is a Steam install: it can be revalidated out from under you, it is not in version control, and a file
edited there has no history when it turns out to matter.

## Why this is possible at all

`system/gui/UISets/MainUI.xml` names two component folders, and the second is
`%FF_DOCUMENTS%\Addons` — the client has always loaded user components from Documents alongside its
own. Nothing has to be modified in the install to add a panel.

Under Proton, `%FF_DOCUMENTS%` is inside the prefix:

```
~/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/compatdata/227700/pfx/drive_c/users/steamuser/Documents/Firefall
```

## Installing

One directory per component, named the same as the component, holding `<Name>.lua` and `<Name>.xml`.

```
FF_DOCS=~/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/compatdata/227700/pfx/drive_c/users/steamuser/Documents/Firefall
mkdir -p "$FF_DOCS/Addons"
cp -r Client/Addons/FabProbe "$FF_DOCS/Addons/"
```

Components are read at startup, so a new component folder needs a client restart.

An *edited* file usually does not: `/reloadui` in chat calls `System.ReloadUI()` and re-reads the
components, and the component's load line reappearing in `console.log` is how you know it took. If it
does not reappear, restart.

## What's here

- **FabProbe** — read-only. Asks the client's surviving recipe functions what they return and writes
  the answers to the client log, to settle whether recipe lists come from the client's own database or
  from the server. This is CRAFT1's first step; see
  [Crafting](../../Game%20Testing/Crafting.html). Output is prefixed `FabProbe:` and re-runnable in
  game with `/fabprobe`.

- **FabCart** — writes. Adds and removes a recipe from the cart, to settle whether a filled cart ever
  reaches the server. It binds all six `ON_FAB_*` events, which nothing shipped binds; the server end
  needs no change, because PIN already logs commands it has no handler for. Output is prefixed
  `FabCart:`. Run it with `/fabcart [id]`, or `/fabcart hold [id]` and `/fabcart drop [id]` to split
  the two halves across frames.

  **It answered on the first run.** The cart reaches the server as `UpdateShoppingList` (command 194)
  on the mission-and-marker controller — the tracked-recipe list, a wishlist rather than a build
  order. No `Fabrication_*` command was sent and no `ON_FAB_*` event fired, so `Fabrication_Start` has
  no caller left in the script layer and PIN cannot wait to be asked for a build.

  `hold` then went on to read the message off the wire, which had been marked unknown since the
  message set was written: `01 01 00 00 00 59 25 01 00 00` is one entry carrying recipe 75097, and
  `UpdateShoppingList` is two byte-counted lists of `ShoppingListData`. The definition is fixed, and
  the server now decodes and logs the message rather than warning about an unrecognised id.

  **It has a second job now.** Because it binds every `ON_FAB_*` event and nothing shipped does, it is
  also the watcher for fabrication messages arriving in the other direction. The server's `fabrecipes`
  command sends a recipe list unprompted; `FabCart: EVENT fetch_recipes` in the client log is one of
  the two ways to see the client accept it. The other is the client's own console line,
  `Fabrication Recipe List:`, which needs no addon at all.

## Reading the output

The client's log is archived per run, newest last:

```
ls -t ~/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/compatdata/227700/pfx/drive_c/users/steamuser/AppData/Local/Red\ 5\ Studios/Firefall/*_last_run.log | head -1
```

Grep it for the component's prefix. A component whose Lua fails to parse logs a `SCRIPT` error there
and then does nothing, which looks identical to a component that was never installed — so check for
the load line before concluding the addon folder is wrong.
