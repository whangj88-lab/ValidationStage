# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

`ValidationStage` (renamed from `MotionProbeStation` on 2026-10-01; the outer folder was renamed from `MotionProbe_Station` too) is a from-scratch sibling project to `18.CSH030EX_4Line_Calibration` (referred to below as
"A"; this project is "B"). A is a full 4-line camera-module calibration station (vision + motion + driver-IC +
probes). B strips that down to **motion + probe only** — no vision, no recipe/spec/Pass-Fail logic. It exists to
jog a 6-axis stack and read contact-probe values while a new fixture/process is being worked out.

Requirements as given by the user (2026-09-28/29), verbatim intent preserved:
1. Motion only (Motorized Stage + Hexapod), built by referencing A's motion code.
2. Same probe-measurement concept as A, but the probe hardware changed to **Solartron DP10/DP20**, so that layer
   needed to be rewritten (A's probe code doesn't apply).
3. No vision — motion-control only.
4. UI should be as easy to understand as possible for an operator.
5. Control concept: **X, Y, Z are driven by the Motorized Stage; TX, TY, TZ are driven by the Hexapod** — the same
   split A's `F_Motion_SK_PI` uses (`_skAxes = {X,Y,Z}`, `_piAxes = {TX,TY,TZ}`). The hexapod's own X/Y/Z are never
   subscribed and stay at 0 (in A only the temporary `ScanAxisHexapodonly` moves them, and it returns them to 0).
   User asked (2026-09-29) not to display hexapod X/Y/Z in B.

Scope decisions made in conversation (not to be silently re-litigated in a future session):
- UI = manual jog + live probe readout only. No automated measurement sequence, no recipe, no Pass/Fail. (User
  explicitly chose this over "A와 유사한 자동 측정 시퀀스" and "수동+자동 병행".)
- Solution lives in its own folder/repo, not as a project added to A's `.sln`.
- Probe channel count/layout is **not** hardcoded (unlike A's fixed 7-port `GageCounter`) — see below.

## Build

```bash
MSBUILD="C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/amd64/MSBuild.exe"
"$MSBUILD" ValidationStage.sln -t:Build -p:Configuration=Debug -p:Platform=x64 -v:minimal -m
```

Output: `ValidationStage/bin/x64/Debug/ValidationStage.exe`. net48, x64-only (matches A; `PI_GCS2_DLL_x64.dll`
is a 64-bit native DLL). A `CopyDlls` post-build target copies `PIDll/PI_GCS2_DLL_x64.dll` into the output dir, same
pattern as A's `CopyDlls` target.

No tests, no CI, no lint — same as A.

### Build prerequisite outside the repo

`OrbitLibrary.dll` (the Solartron Orbit3 .NET SDK) is referenced by absolute path from the installed
**Orbit3 Support Pack for Windows**:
```
C:\Program Files (x86)\Solartron Metrology\Orbit3 Support Pack for Windows\Drivers\Library\OrbitLibrary.dll
```
A fresh machine needs that support pack installed before this builds — same category of prerequisite as A's
Matrox/Diolan/Basler absolute-path references. The DLL is AnyCPU/MSIL (confirmed via
`[System.Reflection.AssemblyName]::GetAssemblyName(...).ProcessorArchitecture` = `MSIL`, not `X86`), so it loads
fine in this project's x64 process — no bitness conflict with `PI_GCS2_DLL_x64.dll`.

## Architecture

### Linear stage: `Motion/MMT_Motion.cs` (MMT MMDC-ST466, Ethernet) — replaced `SK_Motion` on 2026-09-30

The stage vendor changed. The X/Y/Z stage is now **MMT** (Micro Motion Technology): controller MMDC-ST466
(4-axis 2-phase stepper driver), stages X,Y = **AM1-0602-3DY**, Z = **AZ-0803-3DY**, controller axes 1,2,3 = X,Y,Z.
`MMT_Motion` mirrors `SK_Motion`'s public surface (events, `Connect(axes, host)`, `MoveAsync` in µm, `JogRun`/
`JogStop`, `StopEmergencyAsync`, `GetPositions`) so MotionController/F_Main barely changed. `SK_Motion.cs` is kept on
disk but **removed from the .csproj** (in case the old stage comes back).

- **Transport = Ethernet TCP** (`192.168.0.123:5001` default; host box accepts `ip` or `ip:port`, saved to
  `StageHost.txt`). The user wanted the controller's USB-B port (shows up as FTDI COM5), but it never answered
  anything — any baud/RTS/DTR/terminator/`&n` station — even with MMT's own sample GUI, while Ethernet worked. The
  rear LED blinks 2x = "HOST0, 통신선택 됨" per the manual; HOST0/HOST1 mapping and how to switch is undocumented —
  ask MMT (mmt@micromt.com) if serial/USB is ever needed. PC needs a 192.168.0.x address on the adapter wired to it.
- **Protocol** (manual `17.Calibration System/Doc/[단축 Stage조합] MMT 4axis driver controller manual_Kor
  (2023.07.04).pdf`; Korean text doesn't extract with pdftotext — render pages, e.g. `Windows.Data.Pdf`): request
  `"[axis]cmd[data]\r"`, reply `"*..."`. **Replies have no consistent terminator** (measured: only `#` ends in CR), so
  `ReadReply` ends a reply after a 10 ms quiet gap. ≥2 ms between reply and next request. Key commands: `ma0/ma1`
  relative/absolute, `d`/`g` move, `j+`/`j-`/`s` jog/stop, `@s` all-stop, `ips` (IN+POS+STATUS in one reply,
  e.g. `*IN000000*#1POS0*#1STATUS0001_0100_0000_0011_1101`, rightmost char = bit0: bit1 driver err, bit3 0=moving,
  bit9 overheat, bit12 CW limit, bit13 CCW limit), `st0` motor power on (**controller powers up with ST1 = off**, so
  `Connect` sends `st0`; `Disconnect` leaves motors energized). `V/A/AD` are **stored in controller flash**, so
  `SetSpeed` reads first and only writes when different. The sample's `PZ` isn't in the manual; `p0` sets position 0.
- **Units:** 51200 µstep/rev. X,Y lead 1 mm (catalog `MMT_카다로그(국문).pdf` p.45; model code "-3DY" = cross-roller,
  other motor, motor-only, standard lead — decoded with the vendor's model-code table the user supplied) →
  0.01953 µm/µstep. Z is a wedge stage: rise/rev = lead 1 mm × tan(slope). **AZ-0803 isn't in the catalog; slope
  taken as 20° like AZ-0808** (`ZSlopeDegree`) → 0.00711 µm/µstep — **verified with a DP probe by the user
  (2026-09-30)**. Because of the wedge, Z moves ~2.7× less per motor rev than X/Y. Z speed levels were
  first half X/Y's rev/s (~5.5× slower in mm/s); the user chose (2026-09-30) to give Z the **same rev/s as X/Y**, so
  Z is now ~2.7× slower in mm/s (0.18/0.73/1.8 mm/s) — intentional, don't "fix" it to equal mm/s.
- **Controller quirk (measured 2026-09-30): after a jog is stopped with `s`, the axis stays Busy** (`r` → `B`,
  STATUS bit3 = 0) with the position frozen; another `s` doesn't clear it, but a completed positioning move does.
  This made the UI show MOVING forever and reject the next jog. Fix: `JogStop` (and `StopEmergencyAsync`) run
  `ClearBusyAfterStop` in the background — wait until the position stops changing, then a zero-distance relative move
  (`ma0`, `d0`, `g`), verified on hardware to return Ready with no position change. `WaitForStopAsync` also treats
  "busy but position unchanged for 3 s" as stuck and clears it the same way.
- **Stage home = the controller's own coordinates** (user request 2026-09-30, replacing the file-saved `StageHome.txt`
  approach; the hexapod still uses `HexapodHome.txt`). Buttons: [기계 원점 찾기] → `HomeAsync` = `hm0` on X,Y,Z
  (toward the − limit; confirm dialog first), [현재 위치를 원점으로] → `SetZero` = `p0`, [원점으로 이동] → absolute
  move to 0. Controller-stored homing speeds were 10 mm/s (HMV 512000), so `HomeAsync` first sets HMV to the 중속
  value (HMA/HMAD ×10, HMVF ÷10; write-if-different). Position counters restart at 0 on controller power-up, so run
  [기계 원점 찾기] after powering the controller on. Not yet run on the stage: homing direction (HME=1 stored) and
  whether the position reads 0 after homing (HMO=0) are unverified.
- Limit mapping verified on the stage (2026-09-30): **CCW limit (bit13) = +, CW limit (bit12) = −** (REV=1).

### Reused from A, unmodified

`ValidationStage/Motion/AxisStatus.cs`, `PI_Motion.cs`, `PI_GCS2.cs` are byte-for-byte copies of
`18.CSH030EX_4Line_Calibration/Code/Source/Motion/*.cs` (as was `SK_Motion.cs`, now unused — it had a local fix for
an infinite `ReadLine` retry that froze the UI). They have **zero dependency on `Global`/`CSH030Ex`/A-specific
code**. If A's motion layer improves, re-copy rather than hand-patching a divergent copy. That's also why the motion
classes keep A's namespace `MotorizedStage_SK_PI` (deliberately not renamed in the ValidationStage rename); B's own
code is `ValidationStage` / `ValidationStage.Probe`.

Both the stage class and `PI_Motion` (the PI GCS2 hexapod) expose an identical
`Connect(Axis[] axes, string target)` — you connect them with whatever subset of the 6-axis `Axis` enum
(`X, Y, Z, TX, TY, TZ`) they should own. That's what makes requirement 5 (split axes across two controllers) a
non-issue: no protocol code had to change, just which axes get passed to `Connect()`.

**Unit convention (confirmed by reading `PI_Motion.MoveAsync`, not assumed):** the public API on both devices uses
**µm for X/Y/Z** and **arcmin (arc-minutes) for TX/TY/TZ**. Internally PI_Motion converts µm→mm and arcmin→deg
before calling into GCS2. Don't relabel these without re-checking `MoveAsync`'s conversion lines.

**Jog = press-and-hold continuous motion on both devices:**
- Stage: `MMT_Motion.JogRun` (`j+`/`j-`, native continuous jog) until `JogStop` (`s`).
- Hexapod: **`MotionController.HexapodJogRun`/`HexapodJogStop`, not `PI_Motion.JogRun`** (changed 2026-10-01).
  `PI_Motion.JogRun` (A's original, left untouched) sends a single 0.1° `MVR` per press, so holding the button
  stopped after 6 arcmin — the user reported this as "jog cuts off midway". PI hexapods have no native continuous
  jog, so `HexapodJogRun` reads `qTMN`/`qTMX`, `MOV`s toward the travel limit (checked with `qVMO`, halving the
  distance until reachable since the workspace is coupled to the other axes' tilt), and `HexapodJogStop` = `HLT`.
  It calls GCS2 directly using `PI_Motion._deviceId` via **reflection**, to keep `PI_Motion.cs` identical to A.
  Direction: + button = displayed arcmin increases = deg decreases (PI_Motion's arcmin = deg × −60). Not yet
  verified on hardware. First hardware try (2026-10-01) failed with "Failed to read position/limits" (one of
  qPOS/qTMN/qTMX, which one unknown), so now: each failing call logs its GCS error code/text; if qTMN/qTMX fail it
  falls back to a ±30° span (`FallbackHexapodJogSpanDeg`); if qVMO fails it tries MOV directly and halves on rejection.
  Result on hardware: qPOS fine, **qTMN/qTMX → GCS error 551 "This query is not supported for this coordinate
  system type"** — so the fallback is the normal path on this hexapod; the first 551 sets
  `_hexapodLimitQueryUnsupported` (no log at all — user asked to remove even the one-time log, since it's the normal path).
  Reachability search is a **binary search with qVMO** for the farthest reachable target (0.001° resolution). The
  earlier "halve until reachable" stopped far short of the workspace edge, so near the limit the jog moved a bit,
  stopped, moved a bit… (user-reported 2026-10-01). Halving survives only as the fallback when qVMO itself fails.

**Hexapod IP scan, added 2026-10-01:** [스캔] button between the hexapod IP box and [연결]
(`HexapodScanButton_Click` → `MotionController.ScanHexapodControllersAsync` = `PI_EnumerateTCPIPDevices`, the same
broadcast search PIMikroMove's TCP/IP "Search" does — the "localhost" the user saw there is just that dialog's default
host text). One result fills the IP box; several show a `ContextMenuStrip` to pick from. It never connects by itself.
IP/port are regex-parsed from the description (`ParseHostFromDescription`); a non-50000 port only logs a warning
because `PI_Motion` always connects on 50000. Only finds controllers on the PC's own subnet. Not yet run on hardware —
the exact description format is assumed.

**Hexapod coordinate systems, added 2026-10-02:** [좌표계] button in the hexapod connection row (after [해제]; the
home row has no room — the home label would wrap out of sight) opens `F_CoordSystem` (own designer file), a
**look-alike of PIMikroMove's "Manage Coordinate Systems"** (user asked for it to match `Image1.png` as closely as
possible after the first plain version, `Image2.png`): tree on the left (`Name (Type)   ---active---`, bold), "CS
Properties" group (Name / Type / Position X..W / [Set Coord. Sys.]), a `>` button that expands a read-only extra
panel (NLM/PLM soft limits, SSL, SST, SPI pivot as an axis table), bottom toolbar [+] [trash] [Activate CS] [refresh]
[Save and Reset Coordinate System][☰] (Segoe MDL2 Assets glyphs), status line with a clear button. That dialog is
PIMikroMove's, **not** in the GCS2 DLL (the DLL's only dialogs are `InterfaceSetupDlg` and PiStages/UserStages edit).
Scope: **list + create + activate + delete** — Save/Reset is present but disabled (tooltip: use PIMikroMove).
- **Parent (added 2026-10-02, user-reported):** a `KSD` alone always lands under ZERO (seen on hardware, `Image3.png`).
  [+] now remembers the tree selection as `_newParent` (selection stays visible); after the define, if the parent
  isn't ZERO, `LinkHexapodCoordSystem` = `KLN child parent`. Link failure is reported separately (CS exists, unlinked).
- **Delete (added 2026-10-02):** trash = `DeleteHexapodCoordSystem` (`KRM`) after a confirm; only non-ZERO, non-`(PI)`,
  inactive CSs; warns that the controller may refuse when the CS has children. KLN/KRM not yet run on hardware.
- **`KLS?` returns XML** (measured 2026-10-02): `<SingleCoordinateSystem><ZERO Name= Parent= Used= Type=><POS X=.. U=../>
  <NLM/><PLM/><SSL/><SPI R S T/><SST/></ZERO><PI_BASE Type="KSB(PI)" Parent="PI_LEVELLING">…<PI_LEVELLING
  Type="KLD(PI)" Parent="HEXAPOD">…`. Parsed with `XElement` in `MotionController.GetHexapodCoordSystems`. On this
  hexapod: ZERO limits X ±17.02, Y ±16.02, Z ±6.52 mm, U/V ±10.005, W ±21.005 deg.
- `KEN?` returns only `PI_LEVELLING=KLD(PI)`, `PI_BASE=KSB(PI)` — not ZERO. Like PIMikroMove, `(PI)` types are hidden and
  "no visible CS active ⇒ ZERO is active". `KLN?`: `ZERO=PI_BASE PI_LEVELLING HEXAPOD`.
- [+] → editable Name/Type(KSD/KST/KSW)/Position → [Set Coord. Sys.] (`DefineHexapodCoordSystem`). Selecting an existing
  inactive KSD/KST/KSW lets you edit and re-Set it; ZERO, `(PI)` and active ones are read-only. Name `[A-Za-z0-9_]+`.
  [Activate CS] = `KEN` with a confirm (platform doesn't move, but positions and the saved `HexapodHome.txt` meaning
  change). Values are **controller-native mm/deg with no sign flip** — unlike the main screen's arcmin (= deg × −60).
  Refused while moving. Rendered against the real hexapod (read-only) and screenshot-checked; create/activate not
  yet run on hardware. Defined CSs may not survive a controller power cycle unless saved (Save/Reset not implemented).

**Hexapod 3D view, added 2026-10-02:** [3D 보기] in the hexapod connection row (after [좌표계]) opens the non-modal
`F_Hexapod3D` (single instance, `_hexapod3DForm`), a look-alike of PIMikroMove's 3D hexapod view (user's
screenshot). **Not provided by the GCS2 DLL** — built here with WPF 3D (`Viewport3D` in an `ElementHost`; references
PresentationCore/PresentationFramework/WindowsBase/WindowsFormsIntegration/System.Xaml; no NuGet). Code in `View3D/`:
- Model data comes from PI's own install, `C:\ProgramData\PI\PIHexapodDataFiles\` (the same files PIMikroMove uses):
  `HexdataCollisionData\hexdata_<model>.dat` (h0, base joints `b[1..3,1..6]` at z=−h0, platform joints
  `a0[1..3,1..6]` at z=0 — the "Base set"/"Platform set"; the file also has a second set `b[*,7..12]` that is not
  used) and `3D\*\*_CAD.ini` section `[<model>]` (STL file names + offsets). Model name = `CST?` (`H-811.I2_AXIS_X` →
  `H-811.I2`). This station: **C-887 (SN 126041474, FW 2.10.1.3) + H-811.I2** (`3D\M811\`). `H-811.I2.zip` (OBJ +
  json) didn't extract with .NET ZipFile ("invalid data") and isn't needed.
- Placement rules (derived from STL bounds + ini, screenshot-checked): plates are XY-centred on their bbox; baseplate
  z = −h0 + raw z + offset Z (−8.5), platform z-centred + offset Z (+4.5). Strut STLs have **+Y along the strut**
  (base→platform); lower part origin at `B + dir·LowerOffset.Z` (27) with local X offset (−3.8), upper part origin
  at `A − dir·UpperOffset.Z` (29) — they overlap like a telescope. Strut roll (twist about its axis) is chosen so local
  +X points radially outward — **not verified** against PIMikroMove.
- Pose: `qPOS X..W` is in the active CS, so `F_Hexapod3D` converts with `KLT? <active> ZERO`:
  `P_zero = K·P_active·K⁻¹` (row-vector `Matrix3D`: `K⁻¹·P·K`). Measured: active CS was **TILTEDCS (KSD) = ZERO
  rotated U=180°** (that's why PIMikroMove's KSD axis labels look mirrored). Rotation order assumed fixed-axis U→V→W
  (`Rotate X, Y, Z` appended). Correct at the home pose; not yet compared against PIMikroMove with a real tilt. The
  triad (red X / green Y / blue Z) shows the active CS on the platform (`csFrame·platformPose`).
- `MotionController`: `GetHexapodModelName`, `GetHexapodRawPose`, `GetHexapodActiveUserCoordSystem` (KEN? minus
  `(PI)`), `GetHexapodTransformToZero` (KLT?) — polled (pose 100 ms, CS 1 s), so they return null without logging.
- **Axis labels (added 2026-10-02, user request):** each arrow tip is projected to screen (`HexapodScene.Project`,
  FieldOfView = horizontal) and a 2D `TextBlock` on a `Canvas` overlay is placed there — `X TILTEDCS` etc. on the
  platform triad (`SetCoordSystemName` when the active CS changes) and `X (ZERO)` etc. on the floor axes. Deliberately
  not 3D text, which reads mirrored from some angles (as PIMikroMove's `X KSD` does). Re-placed on pose update, camera
  move and resize.
- Mouse: drag = orbit, wheel = zoom, double-click = reset view. Rendered against the real hexapod (read-only) and with a
  synthetic tilt; screenshots looked right.

**Hexapod reference (FRF), added 2026-10-01:** after the hexapod controller is power-cycled, every move fails with
GCS error 5 "Unallowable move attempted on unreferenced axis, or move attempted with servo off" (seen on hardware).
`MotionController.IsHexapodReferenced` (`qFRF` on all 6 axes `X Y Z U V W` — FRF is whole-platform) and
`ReferenceHexapodAsync` (servo ON via `SVO` if `qSVO` shows any off → `FRF` all 6 → wait until not moving and
`qFRF` all true, 120 s timeout). UI: [레퍼런스] button first in the hexapod home row (confirm dialog — the platform
moves to the reference position, all axes 0); right after a successful hexapod connect, if unreferenced, it logs and
offers the same dialog. While referencing, the hexapod per-axis [정지] buttons and [전체 정지] are disabled (user
request); [전체 정지 (비상)] deliberately stays enabled for safety. `HexapodJogRun` refuses with a "[레퍼런스] 먼저" message when unreferenced. Not used:
`PI_Motion.ReferenceAsync` (its `WaitForReadyAsync` can return before motion starts). Not yet verified on hardware.

F_Main's `BindAxis` calls jogRun on `MouseDown` and jogStop on `MouseUp`, plus on `MouseCaptureChanged` (once per
press) so losing the mouse while holding (Alt+Tab etc.) still stops the hexapod instead of letting it run to the limit.

**Stop buttons (added 2026-09-30):** a [정지] button per axis row (`BindStop` → `MotionController.StopAxisAsync`) and
[전체 정지] next to [전체 정지 (비상)] (`StopAllAsync` vs `EmergencyStopAllAsync`). Normal = decelerating stop: MMT
`S` (+ busy clearing) and PI `HLT`. Emergency = MMT `@s` and PI `STP`. MMT only has the one `S` stop command, so
normal vs emergency differs only on the hexapod.

**Speed levels:** `ApplySpeedLevel(int level)` in `Motion/MotionController.cs`. Stage: `MMT_Motion.SpeedLevelValues`
(µsteps/s; user asked for conservative values: all axes 0.5/2/5 rev/s = X,Y 0.5/2/5 mm/s, Z ≈0.18/0.73/1.8 mm/s), accel/decel = 10× velocity.
Hexapod: A's formula `PI.SetSpeed(speedValue * 1.5)` (from `Source/Motion/F_Motion_SK_PI.cs`). One shared combo drives both devices (hexapod speed
is PI `VLS`, a single system velocity, not per-axis); `ApplySpeedLevel` skips a device that isn't connected. **After
every successful connect (either device) the speed is forced back to 저속** (`F_Main.ResetSpeedToLow`, user request
2026-09-29) so a device never moves at whatever speed its controller retained; because the combo is shared, this
also resets the other already-connected device to 저속 so the combo always matches reality.

### New: `Probe/SolartronProbe.cs`

This replaces A's `GageCounter` (`Source/MySingleton.cs`), which drove 7 fixed COM ports 1:1 with 7 gauges via a
plain-text `"GA01\r\n"` protocol. Solartron's Orbit3 system is architecturally different: **one Orbit network
(RS232IM/USBIM/ETHIM controller) carries multiple DP modules daisy-chained on a shared bus**, discovered/identified
in software rather than wired 1:1 to COM ports. `SolartronProbe` therefore:

- Connects once via `OrbitServer.Connect()` (auto-detects whatever controller type is attached — no need to know
  in advance whether it's RS232IM/USBIM/ETHIM).
- Discovers modules via `Modules.Ping()` (~12s, run off the UI thread — see `ScanAsync`) or `Modules.NotifyAddModule()`
  for a never-before-seen module (blocks until the operator moves that probe).
- Treats "connected module" as "channel" — **channel count is not hardcoded anywhere**, unlike A's 7-port
  `GageCounter`. This was a deliberate design choice, not a placeholder for "figure out the real count later":
  however many DP modules are wired to the bus is however many rows show up.
- Persists a `ModuleID -> operator label` mapping to `ProbeLabels.txt` next to the exe (e.g. map a module ID to
  "TZ1"), so the grid shows meaningful names instead of raw module IDs across restarts. Edited in the grid's "라벨"
  column, saved on `CellEndEdit`.
- **All settings files live in the exe folder** (`AppDomain.CurrentDomain.BaseDirectory`, fixed 2026-09-30 — they
  used to be relative to the working directory, so a shortcut with a different "Start in" lost them):
  `ProbeLabels.txt`, `StageHost.txt`, `HexapodHost.txt`, `HexapodHome.txt`.
- DP10/DP20/DP2 are **probe stroke-range variants** (2/10/20 mm), not different protocols — confirmed by reading
  `503094 - DP_LE_AGM Manual.pdf` in the Support Pack. The SDK's `OrbitModuleDP`/`OrbitModule` classes handle all of
  them identically; there is no DP10-specific vs DP20-specific code anywhere in this project, and there shouldn't
  need to be.
- **Probe reconnect after a physical disconnect = stop reading on error, resume manually** (2026-09-30).
  Symptom: a probe unplugged mid-read only came back after a power cycle. The user tested Solartron's
  `Software/OrbitLibraryTest.exe`: its "Start Continuous" (source: `ProgramData\...\Examples\Orbit Library Test
  Project\OrbitLibraryTest\ModuleReadingUC.cs`) is just a loop on `ReadingInUnits` that **stops on the first
  exception** ("Receive Timeout"); re-plug + Start again reads fine, no power cycle. B used to keep polling every
  300 ms (including the dead module). So F_Main now mirrors it: `_probeReading` flag + [읽기 시작/중지] toggle; any
  `IsError` channel in a refresh → `StopProbeReading(...)` + log; operator re-plugs and presses [읽기 시작]. Connect
  auto-starts reading. Not yet re-verified on hardware with B itself.
  The periodic read runs **off the UI thread**: `ProbeTimer_Tick` → `RefreshProbeGridAsync` → `Task.Run(ReadAll)`,
  with `_probeReadInFlight` preventing overlapping reads (a dead module blocks until Receive Timeout); results are
  discarded if reading was stopped/disconnected meanwhile. `Zero`/`ZeroAll` wait up to 1 s for `_busLock`; `_labels`
  has its own `lock` since `SetLabel` (UI) and `ReadAll` (background) touch it concurrently.
  While reading is stopped, [스캔]/[새 모듈 추가] still update the grid via `UpdateProbeListWhileStopped` →
  `SolartronProbe.GetModuleList()` (module IDs/labels only, no `ReadingInUnits`, so the bus stays quiet); new rows
  show "-" / "읽기 중지", existing rows keep their last values.
  Tried and **removed** (user: didn't work): a `FindHotswapped()`-based recovery (auto on connect + [모듈 복구]
  button doing `ClearModules`→`FindHotswapped`→`Ping`). Don't re-add it. (A NetSpeed/187.5k-mismatch theory from
  the manuals was also floated; the test above made it unnecessary.)
- All SDK access is serialized on `_busLock`: Ping/Notify `lock`; `ReadAll` uses `Monitor.TryEnter` and returns
  `null` = "busy, skip this tick"; `Zero`/`ZeroAll`/`Disconnect` wait briefly then give up with a log message. `Disconnect` calls `StopNotify()` first if a [새 모듈 추가] wait is pending.
  F_Main clears the grid on 해제 and removes rows whose module is no longer in the list.
- **Probe values are in µm** (user request 2026-09-30): `ReadMicrometers` converts `ReadingInUnits` using the
  module's `UnitsOfMeasure` (DP reports "mm" → ×1000); unknown units throw so the row shows an error instead of a
  silently wrong number. Zero offsets are stored in µm too. Grid shows `0.000` µm, header "측정값 (µm)".
- **Per-row [읽기] button** (`_colRead`, next to [영점], added 2026-10-01): one-shot read of that module only —
  `SolartronProbe.Read(moduleId)` (waits ≤1 s for `_busLock`, same error shape as `ReadAll`) run via `Task.Run` from
  `F_Main.ReadSingleProbeAsync`; `_singleReadsInFlight` blocks double-clicks on the same module. Works while
  continuous reading is stopped (state shows "개별 읽기"); an error is shown/logged but does **not** stop continuous reading.
- Zero/tare is a **software offset** (`Zero`/`ZeroAll`, stored per module ID), not a hardware preset. `OrbitModule`
  does expose `PresetInUnits` for a hardware zero if that's ever needed instead.

Reference material, if the SDK needs re-checking:
- `OrbitLibrary.xml` (next to the DLL) — full XML-doc API reference, grep-able.
- `C:\ProgramData\Solartron Metrology\Solartron Support Files\Examples\Orbit3 CSharp Example\` — Solartron's own
  working C# sample (`Form1.cs`); `SolartronProbe.cs`'s Connect/Scan/Read/Zero pattern follows this example.
- Manuals under `...\Orbit3 Support Pack for Windows\Manuals\` (PDF; use `pdftotext -layout` to search them quickly).

### `Motion/MotionController.cs`

Thin wrapper, not a big abstraction: owns one `MMT_Motion` (`StageAxes = {X,Y,Z}`) and one `PI_Motion`
(`HexapodAxes = {TX,TY,TZ}`), plus the shared speed-level and emergency-stop-both-devices logic described above.
Also owns per-axis absolute move (`MoveAbsAsync`, machine coordinates) and **home (원점), split per device**.
Stage: controller-native — `HomeStageAsync` (`hm0`), `SetStageZero` (`p0`), `MoveStageToZeroAsync` (see the MMT
section above). Hexapod: `SetHexapodHomeFromCurrent`/`MoveHexapodHomeAsync` (TX,TY,TZ arcmin → `HexapodHome.txt`,
in the exe folder like `ProbeLabels.txt`). The hexapod version mirrors A's `SetHome6DFormCurPos`/
`MoveHome6D` (the tail end of A's `FindCSHorg`): nothing is written to the controller, home is just a saved target. One
deliberate difference from A (user's choice): the hexapod home stores the **actual current TX/TY/TZ**, not forced 0 —
A zeroes them because it corrects tilt via vision, which B doesn't have.
`F_Main` still talks to `_motion.Stage` / `_motion.Hexapod` directly for anything not shared (connect/disconnect
per device, jog).

### `F_Main.cs`

Single form. Layout lives in `F_Main.Designer.cs` (`InitializeComponent`, editable in the VS designer — the user
asked for this on 2026-09-29; it was originally built in code). Simple button/grid/timer events are wired in the
designer to named handlers in `F_Main.cs`; jog buttons and the axis→label dictionaries are wired in code via
`BindAxis()` because they need per-axis parameters. Keep `InitializeComponent` designer-parsable (no lambdas/loops).
Layout: stage panel (left) + hexapod panel (right) on top, speed/e-stop row,
probe `DataGridView` (auto-discovered rows, editable label column, per-row zero button) filling the middle, log
textbox docked at the bottom. A `Timer` (300 ms) drives probe-grid refresh; motion readouts update from
`OnStatusChanged` events pushed by `MMT_Motion`/`PI_Motion` on a background thread (marshalled via `RunOnUi`).

## Status / what's not yet verified

Build and UI smoke-tested (compiles clean, launches, screenshot-verified layout) **without real hardware attached**
in this session. Not yet verified against physical equipment:
- MMT stage over Ethernet (connect/`st0`/`ips` polling only checked read-only so far), PI hexapod over a real host/IP — jog direction, step size, speed levels, absolute
  move and home save/move. (A's hexapod IP is `169.254.3.106`.) Connection settings persist the same way: after a
  **successful** `ConnectStage`/`ConnectHexapod`, the port/host is saved to `StageHost.txt`/`HexapodHost.txt` and
  pre-filled on the next launch (defaults `192.168.0.123` / `127.0.0.1`).
- Solartron Orbit3 controller + actual DP10/DP20 modules — Connect/Scan/NotifyAddModule/Zero end-to-end.

When testing against real hardware turns up a wrong assumption (e.g. jog direction sign, a unit label), fix it in
the class it actually belongs to (`MMT_Motion`/`PI_Motion` for motion, `SolartronProbe` for probe), not by papering
over it in `F_Main`.
