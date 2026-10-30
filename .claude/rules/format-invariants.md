---
paths:
  - "src/Easy.TimeSeries/Internal/**"
  - "src/Easy.TimeSeries/Writer.cs"
  - "src/Easy.TimeSeries/Reader.cs"
  - "src/Easy.TimeSeries/Header.cs"
---

# Format Invariants

Worth knowing before changing bit-level code. The byte format itself is in `docs/layout.md`.

- A file is one `Header` followed by N column blocks. Each column block is one `ColumnHeader` (9 bytes) followed by
  `DataLength` bytes of packed words. Words are 8 bytes, little-endian.
- `BitWriter.CommitRecord()` must be called **once per logical entry** by each domain writer (not once per
  `BitWriter.Write` call). Domain writers may call `Write` several times per entry (e.g. `CategoryWriter` writes a
  prefix bit plus the id; `Int32Writer` writes a block descriptor plus the value). Forgetting `CommitRecord` makes
  `ColumnHeader.Records` wrong and breaks the reader.
- `ColumnHeader.TotalBits` is derived from `DataLength` (bytes) and `BitsInLastWord`; do not multiply `DataLength`
  by `WordBitSize` - it is already byte-counted.
- `Constants.WordByteSize = 8`, `Constants.WordBitSize = 64`. Timestamp encoding has a fixed `Epoch = 2000-01-01 UTC`
  and a 41-bit ceiling - see `Constants.TimeStamp`.
- `CreateWriters` in `Writer` must request `sizeHint + ColumnHeader.Size` because the first `ColumnHeader.Size`
  bytes of each column buffer are reserved for the header, written at `Flush`.
- For `ScaledNumber32`/`ScaledNumber64`, `ColumnInfo.Meta` stores `decimalPlaces` (e.g. `2`), not the scale
  (e.g. `100`). Both writer and reader sides must do `scale = (int)Math.Pow(10, decimalPlaces)`.
- A changed golden fixture in `tests/data/golden/v1` is a format change, and a format change is a two-repo change
  (see "Build, test, run" in `AGENTS.md`).
