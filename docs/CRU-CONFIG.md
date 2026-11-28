# CRU configuration — 1920x1080 @ 110 Hz with large vertical total

This is the timing the keeper was developed against. It is **not required** by
the keeper, but it is what makes the blur reduction worth protecting.

The point is the **vertical total**. Raising it makes the MStar scaler compute
the strobe for a taller virtual screen, which pushes most of the strobe
crosstalk off the physical panel. The trade-off is that the link is nearly
saturated, so the refresh rate has to come down to keep 8 bpc RGB.

```
2001 x 1502 x 110 = 330.61 MHz   ->  91.8% of the 8.64 Gbps HBR link
```

---

## The fast way (no CRU)

```powershell
# como Administrador
.\tools\restore-edid.ps1
```

O script grava o mesmo bloco EDID que o CRU gera direto no registro, valida os
checksums antes, e força o driver a reler desabilitando/habilitando o dispositivo
de monitor. Mede o resultado no fim.

É o caminho para depois de formatar o Windows: o override vive no registro, então
a formatação apaga. Nada é gravado no monitor.

## The manual way (CRU)

Use [ToastyX CRU](https://www.monitortests.com/forum/Thread-Custom-Resolution-Utility-CRU),
`Detailed resolutions → Add`:

| Field | Value |
|---|---|
| Active | `1920 x 1080` |
| Front porch | `48 x 3` |
| Sync width | `32 x 5` |
| Horizontal total | `2001` |
| Vertical total | `1502` |
| Refresh rate | `110.000` |

Then `restart64.exe` and select the mode.

Verify in the NVIDIA control panel: **RGB / 8 bpc / Full**.

---

## Why 110 and not 120

The XL2430 negiates **HBR only** — no HBR2. Four lanes give 8.64 Gbps usable,
which at 8 bpc is 360 MHz of pixel clock, with no headroom for driver overhead.

| Mode | Pixel clock | Link | RGB 8 bpc |
|---|---|---|---|
| 1080p144 native | 328.87 MHz | 91% | ✅ |
| 120 Hz + VT 1500 | 374.40 MHz | 104% | ❌ |
| 120 Hz + VT 1499 (HT 2001) | 359.94 MHz | 99.98% | ❌ falls back to 4:2:2 |
| **110 Hz + VT 1502 (HT 2001)** | **330.61 MHz** | **91.8%** | ✅ |

`HT 2001` is the floor: `1920 + 48 + 32 = 2000`. There is no horizontal blanking
left to shave.

Lowering the refresh also *reduces* crosstalk, because the crosstalk band height
is `GtG / line_time` and `line_time = 1 / (VT × refresh)`.

---

## Strobe ceiling

With a large vertical total the backlight cuts out when

```
Area + (26 - Intensity) > 6000 / refresh
```

| Refresh | Ceiling |
|---|---|
| 100 Hz | 60 |
| 110 Hz | 54 |
| 120 Hz | 50 |
| 125 Hz | 48 |

At 110 Hz, `48 + 6` is exactly at the edge. If the backlight flickers, drop Area
by one or two.

---

## Reverting

`reset-all.exe` in the CRU folder restores the original EDID. If the screen goes
black, boot into Safe Mode first.

The override lives in the registry, not in the monitor:

```
HKLM\SYSTEM\CurrentControlSet\Enum\DISPLAY\<monitor id>\<instance>\Device Parameters\EDID_OVERRIDE
```

Nothing is written to the panel, so a Windows reinstall removes it.

---

## Do not bother

- **144 Hz + vertical total.** Impossible. 144 Hz requires reduced blanking
  (VT ≈ 1098); anything higher gives `Out of Range`.
- **Forcing 6 bpc to buy bandwidth.** The panel is 6-bit + FRC anyway, but the
  driver does not expose 6 bpc over DisplayPort on this monitor.
- **Overclocking to 200+ Hz.** It works, but it costs 6 bpc *and* the vertical
  total tweak — you trade the thing you configured for refresh rate.
