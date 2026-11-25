# VCP map — BenQ Zowie XL2430

Measured on a **XL2430**, firmware **V014-20190306**, panel AUO M240HW01 V8,
scaler MStar 8556T, over DisplayPort, MCCS 2.2.

The monitor answers **87** of the 256 VCP codes. Only the ones that matter are
documented here; the rest are either read-only identifiers or unknown.

> BenQ reuses standard MCCS codes with its own meanings. `0xDC` is defined by
> MCCS as *Display Application* with values `0x00–0x0A`; BenQ uses the reserved
> range `0x0B–0xEF` for picture modes. `0x4D/0x4E/0x4F` are not the MCCS
> meanings either.

---

## Controls used by the keeper

| VCP | Name | Values | Read | Write |
|---|---|---|---|---|
| `0xDC` | Picture Mode | `0` Standard · `24` Gamer 1 · `25` Gamer 2 · `26` Gamer 3 | ✅ | ✅ |
| `0xD8` | Auto Mode | `4` off · `5` on | ✅ | ✅ |
| `0xD5` | Instant Mode | `0` off · `1` on | ✅ | ✅ |
| `0x4D` | Blur Reduction | `0` off · `1` on | ✅ | ✅ |
| `0x4E` | Strobe Area | `0–100` | ✅ | ✅ |
| `0x4F` | Strobe Intensity | `0–25` | ✅ | ✅ |
| `0xF0` | AMA (overdrive) | `0` Off · `1` High · `2` Premium | ✅ | ✅ |
| `0x86` | Display Mode | `1` 1:1 · `2` Full · `5` Aspect | ✅ | ✅ |

`0xD5` is the one that matters most and the one nobody had documented.
Toggling it `0 → 1` is what unsticks a scaler that has fallen back to frame
buffering while still reporting Instant Mode as ON.

**Writes commit asynchronously, and the scaler is slower than the register.**

Two distinct delays were measured:

| Stage | Delay |
|---|---|
| register read-back after a write | up to ~2 s |
| scaler actually acting on the `0 → 1` edge | ~2.5 s after the `1 → 0` |

With a 700 ms gap the register ended at `1` but the image stayed doubled — the
edge never reached the scaler. A **2500 ms** gap is what was verified to clear
it. Do not tune this down without checking the image, not just the register.

---

## Image controls

| VCP | Meaning | Observed value |
|---|---|---|
| `0x10` | Brightness | `100` (max 100) |
| `0x12` | Contrast | `50` (max 100) |
| `0x16` | Video gain: Red | `99` |
| `0x18` | Video gain: Green | `100` |
| `0x1A` | Video gain: Blue | `99` |
| `0x72` | Gamma | `80`=1 · `100`=2 · `120`=3 · `140`=4 · `160`=5 |
| `0x7A` | Black eQualizer *(likely)* | `0` (max 20) |
| `0x87` | Sharpness | `5` (max 10) |
| `0x89` | Color Vibrance | `10` (max 20) |
| `0x8A` | Color Saturation | `50` (max 100) |
| `0x14` | Select Color Preset | `11` = User 1 |

`0x14` does **not** change when the S-Switch changes the picture mode — it is
the colour preset *inside* the mode, and a dead end for this problem.

---

## Read-only identifiers

| VCP | Meaning | Value |
|---|---|---|
| `0xAC` | Horizontal frequency | — |
| `0xAE` | Vertical frequency | `10960` = 109.60 Hz |
| `0xC0` | Panel usage time | hours |
| `0xC8` | Display controller type | `0x5605` → MStar |
| `0xC9` | Firmware version | `0x0201` → 2.1 |
| `0xDF` | VCP / MCCS version | `514` = 0x0202 → 2.2 |
| `0xB2` | Sub-pixel layout | — |
| `0xB6` | Display technology | `3` = LCD active matrix |

Note `0xAE` reports **109.60 Hz** while the timing is programmed for 110.000 Hz.
That is normal — the monitor measures the incoming signal, it does not echo the
programmed value.

---

## Unknown / do not touch

`0xE0` (0–3), `0xE1` (0–1), `0xD9` (max 26), `0xED`, `0xFA`, `0xFB`, `0x19`,
`0x50`, `0xA8`, `0xB5`.

They are manufacturer-specific (`0xE0–0xFF`) or absent from MCCS. None of them
could be correlated with any OSD control. **Read them if you are curious; do not
write to them.** MCCS explicitly reserves that range without any guarantee, and
a wrong write to an unknown code can leave a display needing a power cycle.

`0xE1` is *not* Instant Mode — `0xD5` is.

---

## How to rediscover this yourself

`ControlMyMonitor` (NirSoft) or `ddcswitch` can read the whole range:

```cmd
:: NirSoft — dumps every VCP the monitor answers
ControlMyMonitor.exe /scomma "" "\\.\DISPLAY1\Monitor0"

:: ddcswitch
ddcswitch get 0
```

Then change exactly one thing in the OSD, dump again, and diff. That is how
`0xDC`, `0xD5` and `0xD8` were found here.

Two warnings from experience:

1. **Close the OSD before reading.** Several BenQ models stop answering VCP
   requests while the OSD or the factory menu is open.
2. **`0x52` is read-only.** It reports the id of the last feature that changed.
   Seeing it move is expected and means nothing.

---

## Gamma encoding

`0x72` uses a linear scale, one step per OSD preset:

| OSD | `0x72` |
|---|---|
| 1 | 80 |
| 2 | 100 |
| 3 | **120** (factory default) |
| 4 | 140 |
| 5 | **160** |

The presets push this to **160 (gamma 5)**. That happens to be the right value
on this panel at high refresh — the 24" AUO loses gamma as the refresh rises, so
gamma 5 at 110 Hz reads closer to 2.2 than the factory gamma 3 does. The keeper
reasserts 160 so it cannot drift back.

If you prefer a different gamma, change `GAMMA_5` in the source.
