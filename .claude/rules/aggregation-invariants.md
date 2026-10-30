---
paths:
  - "src/Easy.TimeSeries.Aggregation/WriteTime/**"
---

# Write-Time Aggregation Invariants

Worth knowing before changing write-time aggregation. The design is in `docs/aggregation.md`.

- `HookedWriteStorage.CloseAsync` must never throw because of aggregation once the inner raw commit succeeded.
  Everything after the commit falls back to setting a marker and reporting through `OnFailure`.
- The processor deletes a marker only with `ifMatch` = the version it listed. Dropping that condition loses raw data
  written during a recompute. Every marker write must change the version.
- Markers are the only path to recomputing a longer storage time span. Recovery sets markers; it never recomputes.
- Raw retention is checked against the span **start**: the oldest raw files of a span are deleted first.
  `AggregationConfiguration.Validate` requires retention of at least the longest storage time span plus the grace
  period.
- Recovery decides staleness from source records (`{subject}.sources.json` next to each aggregated `.ets`), the raw
  paths and versions an aggregated file was computed from, never from timestamps. Every write of an aggregated file
  by the hook or the processor must be followed by its record, with versions taken no later than the raw files were
  read.
- A source record also names the aggregated version it describes, taken from `CommittedVersion` of that write. The
  hook binds its kept bytes to the raw `CommittedVersion` the same way; a `GetAsync` after a commit proves nothing.
- Post-commit work in the hook runs with `CancellationToken.None`: the raw file is committed, so stopping halfway
  would leave aggregations neither computed nor marked.
