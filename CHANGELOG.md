# Changelog

The project was built by reverse-engineering a single monitor over one
afternoon, so the version numbers track the actual order of discoveries.

---

## v8 — final

- Reassert **AMA High** (`0xF0 = 1`) and **Display Mode Full** (`0x86 = 2`) on
  every profile. Both are part of the state the presets clobber.
- AMA meaning confirmed by three independent sources (BL2420PT register dump,
  XL2735 thread, OorBuster source).

## v7

- Fix: pressing the **same** button twice did nothing. `lastMode` was latched
  before the restore and never reset, so a second press compared equal and was
  skipped — leaving the monitor stuck on the preset. Removed entirely; the loop
  now derives everything from `0xDC`.
- Stopwatch-based scheduling (the blocking DDC calls made `Thread.Sleep`
  drift).
- Pause file `%TEMP%\xlkeeper.pause`.
- Auto Mode polled every 5th cycle instead of every cycle.
- Stuck detection with back-off.

## v6

- Map each button to a user profile instead of a fixed Standard.
- **Discovery:** the presets do not only change colour — they also reset the
  strobe to `Area 10 / Intensity 10`. Restoring `0xDC` alone was not enough.

## v5

- **Discovery:** `0xD8` is the Auto Mode button (`4` off / `5` on), separate
  from the picture mode. Force it off.
- Pause file and loop hardening.

## v4

- **Discovery:** `0xF0` is AMA and `0x86` is Display Mode. The presets drop AMA
  and can leave the mode on Aspect, which shrinks the image once the scaler
  believes it is running 1440 lines.

## v3

- Add per-button Area/Intensity pairs.
- **Discovery:** writing `0xDC = 0` restores the strobe values stored with
  Standard automatically. The extra writes are only needed to override them
  with something else.

## v2

- **Discovery:** the double image that survives Blur Reduction OFF is Instant
  Mode stuck at the scaler. `0xD5 = 0 → 1` re-locks the scanout.
- This was the whole reason the project exists; v1 could not fix it.

## v1

- Poll `0xDC` every 700 ms and write `0` when it is not Standard.
- Worked for colour, not for the double image.
