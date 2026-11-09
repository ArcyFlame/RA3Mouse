RA3Mouse 1.0
Red Alert 3 style mouse (and optional RA3 hotkeys) for
Red Alert 2, Yuri's Revenge, Mental Omega and other YR mods - Steam, Origin/EA app, CD, and CnCNet.

------------------------------------------------------------------
WHAT IT DOES
------------------------------------------------------------------
  Right click             give orders (move, attack, enter, capture...)
  Right drag              scroll the map (unchanged)
  Left click              select; on empty ground it deselects
  Left drag               box select
  Double left click       select all of that type on screen
  Hold Alt + right click  set waypoints; release Alt and the units go
                          (needs the RA3 hotkeys below, or your waypoint key held instead of Alt)
  Ctrl + right click      force fire
  F12                     turn RA3Mouse on/off (high beep = on, low beep = off)

Left click keeps the normal RA2 behaviour on the sidebar and in cursor modes
(sell, repair, power, beacon, and the first click after a sidebar click, e.g. placing a
building or firing a superweapon). In those modes right click cancels the mode, like RA3.
RA3Mouse reads your sell/repair/power/beacon/waypoint keys from the game's own
Keyboard.ini / KeyboardMD.ini, so it works with any key layout.

------------------------------------------------------------------
IS IT SAFE ONLINE? (CnCNet)
------------------------------------------------------------------
Yes. RA3Mouse only turns your clicks into other clicks you could make yourself.
It does not change any game file or read/write game memory, so it cannot cause a
desync, and CnCNet sees a normal unmodified game. Other players do not need it.

------------------------------------------------------------------
INSTALL
------------------------------------------------------------------
1. Unzip anywhere (e.g. Documents\RA3Mouse).
2. Run RA3Mouse.exe. A red "3" icon appears in the tray.
3. Start the game normally (Steam, CnCNet client, Mental Omega client, ...).

Start with Windows: press Win+R, type  shell:startup , and put a shortcut to RA3Mouse.exe there.

Optional - RA3 hotkeys:
  Right-click the tray icon > "Apply RA3 hotkeys to a game folder..." and pick the folder
  with gamemd.exe (Yuri's Revenge / CnCNet / mods) or game.exe (Red Alert 2).
  Your old key files are backed up to <game folder>\RA3Mouse-backup\ first.
  The CnCNet client's own Hotkeys screen overwrites these if you save there.

  Q select all            W select same type     E/R/T/Y build tabs
  A guard (closest RA2 order to attack-move)     S stop      F deploy/ability
  X scatter    C repair    Z sell    P power     B beacon    H home base
  Space last event         N next idle harvester (Phobos games)   M / Shift+N prev/next unit
  Hold Alt waypoint mode   J K L ; camera bookmarks (Ctrl+key saves)
  1-0 groups: Ctrl+# create, # select, Shift+# add, Alt+# center
  Alt+F follow    Numpad+ screenshot    F5-F11 taunts

------------------------------------------------------------------
TROUBLESHOOTING
------------------------------------------------------------------
* Clicks do nothing / balloon says the game runs as administrator:
  tray icon > "Restart as administrator" (Windows blocks normal programs from clicking
  into admin programs).
* Sell/repair mode feels "stuck": press Esc. RA3Mouse follows the mode keys you press;
  if you leave a mode with the sidebar button it can get out of step.
* Sidebar not recognised in a mod with a different sidebar: change SidebarWidth in
  RA3Mouse.ini (tray > Settings file), then restart RA3Mouse.
* Mod with a renamed game exe: add it to ExtraGameExes in RA3Mouse.ini.
* Want vanilla left click but RA3 right click: LeftClickDeselects=no in RA3Mouse.ini.
* In the game menus a right click acts as a left click.
* Antivirus or SmartScreen warning: the exe is unsigned and uses a global mouse/keyboard
  hook (that is how it sees your clicks). The full source is in the "source" folder;
  run source\build.bat to build your own copy with the C# compiler built into Windows.
* Debug log: RA3Mouse.exe --log "%TEMP%\ra3mouse.log"

Command line:
  RA3Mouse.exe --apply-hotkeys "<game folder>"     apply RA3 hotkeys without the tray menu

Requirements: Windows 10/11 (.NET Framework 4, built into Windows). MIT licence, see LICENSE.txt.
