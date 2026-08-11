# Which Firefall PIN targets

**Build 1962, the v1.7 line, built 2016-05-05.** That is the last retail Firefall, and the only
client PIN is written against.

Wherever a source describes a different build, it describes a different game. Firefall was rebuilt
twice — once at v1.0 and again at v1.6/v1.7 — so beta-era documentation is not merely dated, it is
wrong about systems that still carry the same names.

## Evidence

All of it comes off the installed client and the repo, not off the internet.

| Signal | Value | Where |
|--------|-------|-------|
| Environment mnemonic | `prod-1962` | string in `FirefallClient.exe` |
| Window title string | `Firefall (v1.5.1962)` | string in `FirefallClient.exe` |
| PE `ProductVersion` | `1.7.0.0` | VERSIONINFO resource |
| PE `InternalName` | `Fury` | VERSIONINFO resource |
| PE link timestamp | 2016-05-05 04:12:21 UTC | COFF header |
| Steam depot | app 227700, depot 227701 | `appmanifest_227700.acf` |
| Protocol | GSS V66, Matrix V25 | [AeroMessages README](../../Lib/AeroMessages/README.md) |
| Protocol, stated | "1962 network protocol" | same |

The two version strings disagree, and the PE resource wins. `v1.5.1962` is a stale window-title
literal — the build number in it, 1962, is the part that was maintained. Read the number, not the
prefix.

Reproduce any of it:

```sh
FF=~/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/Firefall
strings -n 4 "$FF/system/bin/FirefallClient.exe" | grep -E 'prod-[0-9]+|Firefall \(v'
```

## Where 1962 sits

Firefall's public build numbers run monotonically across every version prefix, so they order the
history on their own.

| Build | Version | Date | Note |
|-------|---------|------|------|
| 1710 | v0.7 | 2013 | closed beta — the thumping/crafting game |
| 1783 | v1.0 | 2014-07 | retail launch, first rebuild |
| 1802 | v1.1 | 2014-09 | oldest capture in `Captures/` |
| 1869 | v1.3 | 2015-05 | middle capture |
| 1946 | v1.6 | 2016-03-02 | last patch the community wiki documents |
| **1962** | **v1.7** | **2016-05** | **what we target** |

v1.7 landed 2016-04-26 — Devil's Tusk, the elite rank overhaul, the retroactive XP curve. Build
1962 is a few weeks past that, and it is where the record stops: Red 5 collapsed that summer, The9
ran the servers untouched until shutdown on 2017-07-07. Nothing shipped after 1962.

This is why the 2016-11-15 capture is the useful one and the 2014/2015 ones are not — see
[CaptureReplay](../../Tools/CaptureReplay/README.md). It postdates 1962 but predates nothing, since
no build followed.

## In-game observation is not evidence about 1962

What the client does while connected to PIN is a statement about PIN, not about Firefall. Three
things blur the line:

- PIN ships **custom static data** of its own in
  [StaticDB/CustomData](../../UdpHosts/GameServer/StaticDB/CustomData) and hardcodes values in
  [HardcodedCharacterData.cs](../../UdpHosts/GameServer/Data/HardcodedCharacterData.cs).
- PIN implements only part of the game. Weapons are hitscan with no travel time, gravity or bounce;
  damage ignores resistances. A weapon that feels wrong may simply not be simulated yet.
- Build 1962 itself shipped with features switched off, so an absence in game may be authentic.

The test is the SDB, not the session. Worked example: PIN references a battleframe called
Archangel, which looks like invented content. `dbitems::Battleframe` holds id 82394, its
`RootItem.name_id` is 180445, and `dblocalization::LocalizedText` renders that as *Erzengel* /
*Archange* / *«Архангел»*. Localised into every shipped language — it is Red 5's, and it was in the
box. The suspicion was reasonable and the data settled it in one query.

## What this costs us

The community documented Firefall heavily through beta and thinned out fast after launch. The
last patch on the wiki is 1946, sixteen builds and two months short of ours, and the article bodies
are older than that — the Firecat page still carries a `v0.7.1710` stamp, from the closed beta.

So the surviving prose describes, in rough order of volume, a game that was deleted in 2014, a game
that was reworked in 2016, and — barely — ours. Every claim inherited from it needs a build stamp
before it can be trusted. See [Sources](Sources.md).
