# UAD package list asset

`uad_lists.json` is the package metadata from
[Universal Android Debloater](https://github.com/0x192/universal-android-debloater), read from the
pinned reference tree at commit `11f27c671cba278d71296cdef4c5a5dba06add5e` (2023-04-10).
UAD is GPL-3.0; the upstream `LICENSE` and FPM's third-party notices apply. The FPM application is
GPL-3.0-or-later.

- Size: **845,509 bytes**
- SHA-256: `e25a5087ea1730575e48aefa0d1f65990c0eaffc179c9950867aaf4ffbd85383`
- Source rows: **2,400**; unique ids: **2,394**; duplicate ids: **6** (last-wins, reported by the
  validator)
- This file is embedded in `PCManager.Core` and is the fallback when no valid cached update exists.
- Do not edit by hand. A validated user-initiated update writes
  `%LOCALAPPDATA%\FPM\android\lists\uad_lists.json` and its `.sha256` sidecar instead; regenerate
  the embedded copy from the reference and update this file when the pinned reference changes.
