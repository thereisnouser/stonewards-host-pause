# Stonewards Host Pause (experimental 0.1.0)

A **host-only** multiplayer pause prototype for Stonewards. The host presses **F9** to pause or resume gameplay. Guests do **not** install BepInEx or this mod.

## How it works

The Stonewards `Assembly-CSharp.dll` build from 2026-10-13 includes the game's **native** Mirror RPC `NetworkHelper.RpcChangeTimeScale(float)`. Its generated user-code calls `UnityEngine.Time.set_timeScale` on recipients. This plugin invokes the game's own RPC, not a custom network message, so unmodified clients can receive it.

- Default pause/resume shortcut: **F9** (configurable in BepInEx settings).
- The game time scale is changed to zero on connected clients and the host.
- A small text indicator shows **on the host only**. There is **no guest-side custom UI**.
- Automatic resume if an upgrade/end-of-wave blocking phase begins, or the host's run/plugin ends.
- Native pause RPC is periodically reapplied to account for late joiners.

## Build / install

Requires .NET SDK, Stonewards, BepInEx 5 in the host's r2modman profile.

```powershell
.\build-and-install.ps1 -GameDir 'C:\Program Files (x86)\Steam\steamapps\common\Stonewards' -ProfileDir 'C:\path\to\r2modman\profiles\stonewards-host-pause'
```

The script places `StonewardsHostPause.dll` into `BepInEx/plugins/StonewardsHostPause/` in the selected **host** profile. The guest does not need to change anything.

## Experimental limitations

**Source prototype only. Not yet built or tested in the live game.** No claims of a guaranteed, glitch-free full pause. Some timers may use unscaled time, and client-side input/UI code may keep running even with `Time.timeScale = 0`. Check on two PCs before publishing. All players must run a compatible game build that already includes `NetworkHelper.RpcChangeTimeScale` (the reference build from October 13, 2026 contains it).

Test with the host and a vanilla guest: stop enemies and wave countdown; ensure neither player can move/attack; resume after several seconds; repeat across death, end-of-wave vote, upgrades, guest late-join and host disconnect. Test ordinary lobbies with the mod enabled too.

The source archive is intentionally not a Thunderstore release. No prebuilt DLL is included.
