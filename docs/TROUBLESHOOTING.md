# Troubleshooting

## The image is doubled and it does not go away

**That is the bug this project exists for.**

Symptom: the whole screen looks like two frames blended — text with a ghost
offset above or below. Turning **Blur Reduction OFF does not fix it**, which is
what makes it look like a dead panel.

Cause: **Instant Mode is stuck.** The flag still reads `ON` (`0xD5 = 1`) but the
scaler has fallen back to frame buffering. With a large vertical total the
buffered frame shows through.

This is **by far the most common way to hit it**: the monitor goes to standby
(Windows display timeout, power button, cable replug) and comes back with the
scanout latched in an intermediate state. The flag still reads `ON`, so software
cannot see the problem — only an edge fixes it.

Fix, in order:

1. `Picture → Instant Mode` → **OFF**, wait, → **ON**. This alone fixes it.
2. If it returns after pressing an S-Switch button, the keeper is not running.
3. If it returns after wake and the keeper is running, check the log for
   `monitor voltou -> relock do scanout`. If it is missing, the DDC channel
   never dropped, so the keeper never armed — pause it and toggle Instant Mode
   by hand.

The keeper handles this automatically: when DDC stops answering it arms
`needRelock`, and on the first successful read after that it waits 900 ms,
pulses Instant Mode and reasserts the profile. Measured at 2.0 s from wake to
correct image.

---

## Backlight shuts off

`Area + (26 − Intensity)` exceeded `6000 / refresh`.

At 110 Hz that ceiling is **54**. `48 / 20` = 54 is exactly at the limit; one
point more and the backlight cuts.

Lower `Area`.

---

## Nothing happens when I press a button

| Check | How |
|---|---|
| Is the process running? | `tasklist /fi "imagename eq XLKeeper.exe"` |
| Is it running in the right session? | Task Manager → Details → Session column must say `Console` / `1`, not `0` |
| Is it paused? | `dir %TEMP%\xlkeeper.pause` |
| Is DDC/CI enabled? | `Menu → System → DDC/CI → ON` |

A task registered without `/it` runs in session 0 and cannot reach the display.
Re-run `install.ps1`.

---

## The keeper and the OSD fight each other

By design, partially. The keeper only writes when `0xDC` is not `0`, so changing
brightness, gamma, sharpness or even Area/Intensity in the OSD is left alone.

The exception is **Display Mode**. `0x86 = 2 (Full)` is reasserted with every
profile, so if you deliberately want Aspect or 1:1, edit that line out.

To change anything freely, pause:

```cmd
echo. > %TEMP%\xlkeeper.pause
```

---

## Values revert after a few seconds

Something else is writing the same registers. Known culprits:

- **Display Pilot** — writes directly to monitor firmware, and its settings
  survive uninstalling. The only clean reset is `RESET ALL` in the OSD or
  `Recall All` in the factory menu.
- **OorBuster** — restores `0xCC`, `0xDC` and `0xF0` for the same reason this
  project does.
- **Twinkle Tray / ClickMonitorDDC** with a custom VCP hotkey.

---

## Monitor went dark / unusable

Nothing in this project writes outside documented codes, but if something else
did:

1. Unplug the monitor for 5–10 minutes. The DDC circuit stays powered in
   standby, so the plug matters.
2. Plug a source into **every** input — it may be stuck on a port that is no
   longer present.
3. Service menu: power off, hold the **2nd button from the bottom**, press
   power, release, press it once.
4. Factory menu: power off, hold **buttons 3 + 4**, press power, release, press
   **button 5** → `Recall All`.
5. Last resort: reflash V014 with a MStar ISP dongle.

---

## Reference values

The full verified state of a healthy unit:

```
0xDC = 0      Picture Mode Standard
0xD8 = 4      Auto Mode off
0xD5 = 1      Instant Mode on
0x4D = 1      Blur Reduction on
0x4E = 48     Strobe Area
0x4F = 20     Strobe Intensity
0xF0 = 1      AMA High
0x86 = 2      Display Mode Full
0x10 = 100    Brightness
0x12 = 50     Contrast
0x87 = 5      Sharpness
0x89 = 10     Color Vibrance
0x7A = 0      Black eQualizer
0x72 = 160    Gamma
```

If any of the first eight drift, the keeper is not doing its job.
