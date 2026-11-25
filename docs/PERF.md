# Performance

The obvious worry with a program that talks to a monitor every 700 ms is
stutter. It was measured, not assumed.

`tools/PerfProbe.cs` compiles with the same `csc.exe` as the keeper and
reports two things:

1. **DDC latency** — how long a single `GetVCPFeature` takes.
2. **Frame timing** — `DwmFlush()` deltas over 20 s, which is the compositor's
   real presentation cadence.

---

## Results

Setup: XL2430 at 1920x1080 @ 110 Hz, GTX 970, DisplayPort, Windows 10 19045.

### DDC latency (`0xDC`)

| Condition | min | p50 | p95 | max |
|---|---|---|---|---|
| keeper idle | 50.9 ms | 66.9 ms | 118.0 ms | 118.6 ms |
| keeper off | 51.0 ms | **51.9 ms** | 68.2 ms | 68.2 ms |

DDC on this monitor is **slow** — 50 to 70 ms per read. The difference between
the two rows is contention between the probe and the keeper, not overhead
created by the keeper.

### Frame timing

| Condition | frames | avg | p99 | max | stutter > 1.5x |
|---|---|---|---|---|---|
| keeper on | 2198 | 9.096 ms (109.94 Hz) | 9.391 | 9.776 | **0 (0.000%)** |
| keeper off | 2198 | 9.096 ms (109.94 Hz) | 9.417 | 9.534 | **0 (0.000%)** |

Identical. Not "close" — the same average to three decimals.

### Stress

To bound the risk, the DDC bus was hammered as hard as it would go for 20 s
while frame timing was recorded:

```
DDC reads: 275 successes, 0 failures   (13.8 reads/s)
FRAMES:    2198  avg 9.095 ms (109.94 Hz)  p99 9.417  max 9.653
STUTTER:   0  (0.000%)
DIST:      2197 of 2198 frames within 1.05x of nominal
```

At 13.8 reads per second with ~70 ms per transaction the bus is essentially
**saturated**, and the frame time did not move. The keeper polls at 1.43
reads/s, so the measured headroom is roughly **10x**.

---

## Why

DDC/CI runs on the I2C/DDC pins of the connector — a separate low-speed bus.
The video stream on DisplayPort is a different pair of lanes entirely. There is
no mechanism by which a DDC transaction can delay a frame.

What *can* happen on some monitors is a visible hiccup when the monitor itself
is busy servicing the bus — a re-sync or a brief blank. It did not happen here,
including at saturation.

---

## Reproducing

```powershell
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /optimize+ /out:PerfProbe.exe tools\PerfProbe.cs
.\PerfProbe.exe
```

It must run in the interactive session — DDC/CI is unreachable from session 0.
Results are appended to `%TEMP%\perf.txt`.

Note: running the probe while the keeper is running will show failed reads.
Two processes reading DDC at the same time collide; that is expected and is the
reason the numbers above list both conditions.
