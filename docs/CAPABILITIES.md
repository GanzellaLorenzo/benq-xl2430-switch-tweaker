# Capabilities string

What the monitor declares about itself, read straight from the DDC/CI
capabilities reply (`dxva2!CapabilitiesRequestAndCapabilitiesReply`).

This is the authoritative list. Everything in [`VCP-MAP.md`](VCP-MAP.md) was
derived by experiment; this file is what the firmware admits to.

```
(prot(monitor)type(lcd)model(XL2430)cmds(01 02 03 07 0C E3 F3)
vcp(02 04 08 10 12 14(04 05 08 0B) 16 18 19 1A 52 59 5A 5B 5C 5D 5E
60(01 03 11 12 0F) 62 65 67 68 69 6A 6B 6C 6E 70 7A 8D(01 02)
AA(01 02 FF) AC AE B2 B4 B6 C6 C8 C9
CC(01 02 03 04 05 06 07 09 0A 0B 0D 0E 0F 12 14 1A 1E 1F 20)
D5(00 01) DF 86(01 02 05 0B 0C 0E 0F 10 11) F7(00 02) D8
D9(18 19 1A) DC(00 03 15 16 17 18 19 1A) EA(00 01 02 03 04 05)
F4(00 01) 87 89 8A 90 9B 9C 9D 9E 9F A0 72(50 64 78 8C A0)
F8(00 0A 14 1E) FA(00 01) FB EF(00 01) F0(00 01 02) CD(00 01 02)
4F 4E 4D(00 01))mswhql(1)mccs_ver(2.2))
```

606 characters, MCCS 2.2.

---

## What it settles

### Picture modes — eight, not eleven

```
DC(00 03 15 16 17 18 19 1A)
```

| Value | Mode |
|---|---|
| `0x00` | Standard |
| `0x03` | Movie |
| `0x15` | FPS1 |
| `0x16` | FPS2 |
| `0x17` | RTS |
| `0x18` | Gamer 1 |
| `0x19` | Gamer 2 |
| `0x1A` | Gamer 3 |

There is **no Photo and no sRGB** on the XL2430, despite what the generic
Z-series tables suggest. The gamer triple is the last three values, which is
exactly what the S-Switch sends.

### Gamma — confirmed

```
72(50 64 78 8C A0)   →   80 / 100 / 120 / 140 / 160
```

Five steps, matching the measured mapping exactly.

### Instant Mode and Blur Reduction are first-class

```
D5(00 01)   Instant Mode
4D(00 01)   Blur Reduction
F0(00 01 02) AMA
```

None of these are undocumented hacks — the firmware advertises them.

### Colour preset

```
14(04 05 08 0B)  →  5000 K / 6500 K / 9300 K / User 1
```

So `0x0B` (11) is User 1, which is where a calibrated unit lives.

### Inputs

```
60(01 03 11 12 0F)  →  VGA / DVI / HDMI-1 / HDMI-2 / DisplayPort
```

`0x0F` = DisplayPort, matching what a DP setup reads.

### Display scaling

```
86(01 02 05 0B 0C 0E 0F 10 11)
```

`01` 1:1, `02` Full, `05` Aspect, plus `0B–11` — the Smart Scaling sizes
(17" to 23"W) that the OSD exposes.

### Undeclared but present

`0xE0`, `0xE1`, `0xED`, `0x19`, `0x50`, `0xA8`, `0xB5` answer DDC but are
**absent from the capabilities string**. Unknown semantics, do not write.

### `0xD9` is declared but inert

```
D9(18 19 1A)
```

The same three values as the gamer presets. It is writable (24 ↔ 25 ↔ 26) but
writing it changes nothing: picture mode, Area and Intensity all stay put. It
appears to be a selection pointer with no visible effect on this firmware.

---

## Reproducing

```powershell
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /optimize+ /out:Caps.exe tools\Caps.cs
.\Caps.exe
```

Output goes to `%TEMP%\caps.txt`. Must run in the interactive session.

Note: the capabilities reply is 606 characters here. `GetCapabilitiesStringLength`
returns the length in **characters**, and the buffer must be that size — passing a
smaller buffer silently truncates.
