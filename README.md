# RA3Mouse

**Red Alert 3 style mouse controls (and optional RA3 hotkeys) for Red Alert 2, Yuri's Revenge, Mental Omega and other YR mods.**
Works with the Steam, EA app, Origin and CD versions, and with the **CnCNet** client.

In RA2, left click both selects and gives orders, and right click deselects.
RA3Mouse switches this to the RA3 scheme: **left click selects, right click gives orders**.

**[⬇ Download the latest release](../../releases/latest)**

## Controls

| You do | Result |
|---|---|
| Right click | Order: move, attack, enter, capture… |
| Right drag | Scroll the map (unchanged) |
| Left click | Select; on empty ground it deselects |
| Left drag | Box select |
| Double left click | Select all of that type on screen |
| **Hold Alt + right click** | Set waypoints; release Alt and the units go |
| Ctrl + right click | Force fire |
| F12 | Turn RA3Mouse on/off (high beep = on, low beep = off) |

Left click keeps its normal RA2 behaviour on the sidebar and in cursor modes. These modes are sell, repair, power and beacon, plus the first click after a sidebar click (for example placing a building or firing a superweapon). In those modes, right click cancels the mode, like in RA3.

RA3Mouse reads your sell, repair, power, beacon and waypoint keys from the game's own `Keyboard.ini` / `KeyboardMD.ini`, so it works with any key layout. Hold-Alt waypoints need the RA3 hotkeys below. With other layouts, hold your own waypoint key instead.

## Is it safe online? (CnCNet)

Yes. RA3Mouse only turns your clicks into other clicks you could make yourself.
It does **not** change game files and does **not** read or write game memory, so it can't cause a desync, and CnCNet sees a normal, unmodified game. Other players don't need it.

## Install

1. Download `RA3Mouse-x.y.zip` from [Releases](../../releases) and unzip it anywhere (for example `Documents\RA3Mouse`).
2. Run `RA3Mouse.exe`. A red **3** icon appears in the system tray.
3. Start the game as usual (Steam, CnCNet client, Mental Omega client…).

To start it with Windows, press <kbd>Win</kbd>+<kbd>R</kbd>, type `shell:startup`, and put a shortcut to `RA3Mouse.exe` in that folder.

### Optional: RA3 hotkeys

Right-click the tray icon, choose **Apply RA3 hotkeys to a game folder…**, and pick the folder with `gamemd.exe` (Yuri's Revenge, CnCNet, mods) or `game.exe` (Red Alert 2).
Your old key files are backed up first, to `<game folder>\RA3Mouse-backup\`.

| Key | Action | Key | Action |
|---|---|---|---|
| Q | Select all | W | Select same type |
| E / R / T / Y | Build tabs | A | Guard (closest RA2 order to attack-move) |
| S | Stop | F | Deploy / ability |
| X | Scatter | C | Repair mode |
| Z | Sell mode | P | Power mode |
| B | Beacon | H | Home base |
| Space | Last event | N | Next idle harvester (Phobos games) |
| M / Shift+N | Previous / next unit | Hold Alt | Waypoint mode |
| J K L ; | Camera bookmarks (Ctrl+key saves) | Alt+F | Follow |
| 1–0 | Groups: Ctrl+# create, # select, Shift+# add, Alt+# center | Numpad + | Screenshot |

If you save in the CnCNet client's own Hotkeys screen, it overwrites these keys.

## Troubleshooting

- **Clicks do nothing, or a balloon says the game runs as administrator.** Windows blocks normal programs from sending clicks to programs running as administrator. Use tray icon → **Restart as administrator**.
- **Sell or repair mode feels "stuck".** Press <kbd>Esc</kbd>. RA3Mouse follows the mode keys you press, so it can get out of step if you leave a mode with the sidebar button.
- **A mod with a different sidebar size.** Change `SidebarWidth` in `RA3Mouse.ini` (tray → Settings file), then restart RA3Mouse.
- **A mod with a renamed game exe.** Add the exe name to `ExtraGameExes` in `RA3Mouse.ini`.
- **You want vanilla left click but RA3 right click.** Set `LeftClickDeselects=no`.
- **Game menus:** a right click acts like a left click.
- **Antivirus or SmartScreen warning.** The exe is unsigned and uses a global mouse/keyboard hook, which is how it sees your clicks. You can build it yourself from this source (below).
- **Debug log:** `RA3Mouse.exe --log "%TEMP%\ra3mouse.log"`

## Build from source

No SDK needed. Run `build.bat`, which uses the C# compiler that ships with Windows (.NET Framework 4).
Everything is in one file: [`RA3Mouse.cs`](RA3Mouse.cs).

Command line options:

```
RA3Mouse.exe --apply-hotkeys "<game folder>"   apply RA3 hotkeys without the tray menu
RA3Mouse.exe --log "<file>"                    write a debug log
```

## How it works

RA3Mouse installs a low-level mouse and keyboard hook. It acts only while a known game window is in front (`game.exe`, `gamemd.exe`, `gamemd-spawn.exe`, plus any names you add in the settings):

- **Right click** on the battlefield is swallowed and replaced with a left click.
- **Plain left click** becomes "right click (deselect) + left click (select)".
- **Battlefield detection** uses the game resolution from `RA2.INI` / `RA2MD.INI` / `RA2MO.ini`. It also reads cnc-ddraw's `maintas` / `boxing` settings to tell where the sidebar and black borders are when the window is scaled.

## License

MIT. See [LICENSE](LICENSE).
