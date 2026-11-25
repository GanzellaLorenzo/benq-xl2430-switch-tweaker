# BenQ XL2430 S-Switch Tweaker

Turn the S-Switch preset buttons on a **BenQ/Zowie XL2430 (XL2430T)** into real, safe profiles — and stop them from wrecking your color calibration and your blur reduction.

A ~6 KB Windows watchdog that watches one DDC/CI register and puts things back in under a second.

```
button 1  →  Area 48 / Intensity 20 / AMA High / Full   (1.00 ms strobe)
button 2  →  Area 44 / Intensity 18 / AMA High / Full   (1.33 ms)
button 3  →  Area 40 / Intensity 16 / AMA High / Full   (1.67 ms)
Auto Mode →  forced off
```

---

## The problem

On the XL2430/XL2430T the S-Switch preset buttons are **not** normal profiles. Pressing one:

1. Loads the **low-level color LUT of FPS1, FPS2 or RTS** — not the one you saved. Saving "Standard" into Gamer 1 only saves the OSD numbers; the color pipeline stays hardwired to FPS1. BenQ called this *"by design"*. It was never fixed, not even in the final firmware V014-20190306.
2. **Also resets the strobe** to `Area 10 / Intensity 10`, silently destroying a tuned blur-reduction setup.
3. On some units, **leaves Instant Mode stuck**: the flag still reads `ON`, but the scaler falls back to frame buffering. With a large vertical total that shows up as a permanent double image — and it does **not** go away by turning Blur Reduction off.

Point 3 is the nasty one. It looks like a broken panel. It is not.

---

## The fix

The monitor exposes its picture mode over DDC/CI as **VCP `0xDC`**:

| Value | Mode |
|---|---|
| `0` | Standard |
| `0x18` (24) | Gamer 1 |
| `0x19` (25) | Gamer 2 |
| `0x1A` (26) | Gamer 3 |

`0xDC` is writable. Writing `0` restores Standard **and** the stored strobe values in one shot. So instead of fighting the buttons, the watchdog simply listens and rewrites:

```
poll 0xDC every 700 ms
  if != 0  →  restore Standard
              pulse Instant Mode (0xD5: 0 → 1)   ← this is what unsticks the scaler
              set Area / Intensity for that button
              set AMA High (0xF0 = 1)
              set Display Mode Full (0x86 = 2)
              set Gamma 5 (0x72 = 160)

DDC stopped answering
  arm "needs relock"

DDC answers again (monitor woke up)
  wait 900 ms → same restore, profile 1
```

Every write is preceded by a read (`SetIfDiffers`). Reads never flicker, writes
do — this is what keeps the flicker count at two instead of six.

### Wake from sleep

The monitor comes back from standby with the scanout latched in a half-buffered
state: **permanent double image that survives turning Blur Reduction off**, and
that only an Instant Mode edge clears. Nothing in Windows reports it, and the
`0xD5` flag keeps reading `1`.

The watchdog detects it because the DDC channel drops while the panel is asleep,
arms itself, and re-locks on the first successful read. Measured at **2.0 s**
from wake to correct image, unattended.

The button press still happens. It just gets undone — and turned into *your* profile.

---

## Verified VCP map

Everything below was measured on real hardware, not guessed.

| VCP | Meaning | Notes |
|---|---|---|
| `0xDC` | Picture Mode | `0` Standard, `24/25/26` Gamer 1/2/3 |
| `0xD8` | Auto Mode | `4` off, `5` on |
| `0xD5` | Instant Mode | `0` off, `1` on |
| `0x4D` | Blur Reduction | `0` off, `1` on |
| `0x4E` | Strobe Area | `0–100` |
| `0x4F` | Strobe Intensity | `0–25` |
| `0xF0` | AMA | `0` Off, `1` High, `2` Premium |
| `0x86` | Display Mode | `1` 1:1, `2` Full, `5` Aspect |
| `0x87` | Sharpness | `0–10` |
| `0x89` | Color Vibrance | `0–20` |
| `0x7A` | Black eQualizer (likely) | `0–20` |
| `0x16/18/1A` | RGB gain | e.g. `99/100/99` |
| `0x72` | Gamma | `0–160` |
| `0xAE` | Vertical frequency | `10960` = 109.60 Hz |
| `0xC0` | Panel usage hours | read-only |

The XL2430 has **87** readable VCPs in total. See [`docs/VCP-MAP.md`](docs/VCP-MAP.md)
and, for what the firmware itself declares, [`docs/CAPABILITIES.md`](docs/CAPABILITIES.md).

> These are proprietary. BenQ reuses standard VCP codes with its own meanings, and values differ between models and firmware revisions. **Do not write to codes you have not read first.**

---

## The strobe math

With a large vertical total active, the backlight shuts off when:

```
Area + (26 − Intensity) > 6000 / refresh
```

At **110 Hz** that ceiling is **54**, so `48 + 6` is exactly at the edge.
At 120 Hz it drops to 50. Keep your profiles under the limit or the backlight will cut out mid-game.

---

## Install

Requires Windows + .NET Framework 4.x (built into Windows 10/11). No SDK.

```powershell
.\build.ps1        # compiles src\XLKeeper.cs with the bundled csc.exe
.\install.ps1      # copies to %LOCALAPPDATA%\XLKeeper and registers a logon task
```

`install.ps1` registers the task as **interactive only** (`/it`) — DDC/CI needs the user session, it does not work from session 0.

### Uninstall

```powershell
.\uninstall.ps1
```

### Pause without uninstalling

```cmd
echo. > %TEMP%\xlkeeper.pause
del %TEMP%\xlkeeper.pause
```

---

## Configure your profiles

Edit the two arrays at the top of `src/XLKeeper.cs`, rebuild, reinstall:

```csharp
static readonly uint[] PROFILE_AREA      = { 48, 44, 40 };
static readonly uint[] PROFILE_INTENSITY = { 20, 18, 16 };
```

Index 0 = button 1, index 1 = button 2, index 2 = button 3.

Respect `Area + (26 − Intensity) ≤ 6000 / refresh`.

---

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| Double image that survives Blur Reduction OFF | Instant Mode stuck | toggle `Picture → Instant Mode` OFF→ON, or let the watchdog do it |
| Backlight cuts out | Area + duty above the ceiling | lower Area |
| Nothing happens | task running in session 0 | re-register with `/it` |
| Values revert on their own | another tool writing `0xDC` | close Display Pilot / OorBuster |
| Watchdog fights your OSD changes | by design | it only restores *after* a preset press |

More in [`docs/TROUBLESHOOTING.md`](docs/TROUBLESHOOTING.md).

---

## Verified on

```
Monitor   BenQ Zowie XL2430   FW V014-20190306   panel AUO M240HW01 V8
Scaler    MStar 8556T
Host      Windows 10 19045 · GTX 970 · DisplayPort
Mode      1920x1080 @ 110 Hz, HT 2001 / VT 1502 (330.61 MHz)
```

The 110 Hz / VT 1502 mode was built with [ToastyX CRU](https://www.monitortests.com/forum/Thread-Custom-Resolution-Utility-CRU). Recipe in [`docs/CRU-CONFIG.md`](docs/CRU-CONFIG.md).

---

## Credit

None of this would exist without the people who mapped this platform a decade before me:

- **Falkentyne** — the VT tweak, the `1280x1440` scaler discovery, the S-Switch bug analysis, and basically every fact about the Z-series that is not in a manual.
- **Chief Blur Buster / Blur Busters** — strobe crosstalk theory and the Large Vertical Total technique.
- **ToastyX** — CRU.
- **hleVqq / OorBuster** — proof that a VCP watchdog works on BenQ hardware (it restores `0xCC`, `0xDC` and `0xF0`).
- **rockowitz / ddcutil** and **markdwags / ddcswitch** — documentation and tooling.

---

## Disclaimer

Unsolicited, unofficial, no affiliation with BenQ or Zowie.

Every VCP used here was read before being written, and only documented codes are touched. Still: DDC/CI lets you write to a monitor, and a wrong write to an unknown code can leave a display in a state that only a power cycle fixes. Use at your own risk. No warranty.

---

## License

MIT — see [LICENSE](LICENSE).
