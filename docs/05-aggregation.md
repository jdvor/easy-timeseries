# Aggregation

`Easy.TimeSeries.Aggregation` turns raw readings into coarser views: one row per minute, per hour, per day, and per
group. An aggregated file is an ordinary `.ets` file, readable by the same `Reader`, source-generated readers and the
Parquet converter.

Three ideas carry the whole package:

1. Every aggregation is defined by **two time spans**: how much raw time one row collapses, and how much time one file
   covers.
2. **The downsampler is the engine.** Whatever triggers an aggregation, `Downsampler` computes it: one aggregated file
   from all raw files of one storage time span.
3. **Write-time aggregation needs your maintenance job.** The library keeps aggregations up to date as raw files are
   written, but it starts no threads. Completing longer spans, retrying and recovering is work your application
   schedules.

## Two time spans

Mixing these two up is the most likely source of mistakes.

| Term | Answers the question | Example | Constraint |
| --- | --- | --- | --- |
| **Aggregation time span** | How much raw time is collapsed into one aggregated row? | 1 minute | Must fit into the storage time span |
| **Storage time span** | How much time does one file cover? | 1 day | Same as the raw storage time span or longer |

An aggregation with a 1-minute aggregation time span and a 1-day storage time span writes one file per day, each
holding up to 1440 rows per group. One raw root can feed several aggregations:

```mermaid
flowchart LR
    R["raw<br/>hourly files"] --> B["agg-10s-hourly<br/>10 s rows, hourly files"]
    R --> C["agg-1m-daily<br/>1 min rows, daily files"]
    R --> D["agg-15m-monthly<br/>15 min rows, monthly files"]
```

## The downsampler

`Downsampler` computes one aggregated file per call, from all raw files of its storage time span. Every other way to
aggregate is a trigger for it:

```mermaid
flowchart LR
    A["your code<br/>batch call"] --> D["Downsampler"]
    B["ets downsample<br/>command line"] --> D
    C["AggregatingCatalog<br/>write-time hook"] --> D
    M["AggregationMaintenance<br/>your scheduled job"] --> D
    D --> F["aggregated .ets file"]
```

So whatever the downsampler guarantees holds for every path:

- Raw data is processed in chunks, in time order. Peak memory is about one chunk of the needed columns, however long
  the storage time span. A chunk is one raw file, or the raw files of one slice when the aggregation time span is
  longer than the raw storage time span (`Day` over hourly files).
- Only raw columns named in the spec are decoded. Raw files may differ in schema - columns added, reordered, or stored
  with another type that decodes alike (e.g. Double and DoubleRaw); inputs are matched by label in each file.
- Recomputing a file is deterministic: the layout depends on the spec alone, and rows never reorder.
- When nothing is aggregated, the target is never written to and no file appears.

| Overload | Input | Output |
| --- | --- | --- |
| `RunAsync(catalog, rawPaths, subjectId, spanStart)` | The subject's raw files found in a catalog | `{spec root}/{date folders}/{subjectId}.ets` in the same catalog |
| `RunAsync(spanStart, rawFiles, target)` | Any `IReadStorage` per raw bucket | Any `IWriteStorage` |
| `RunAsync(spanStart, rawBuffer, target)` | One raw file in memory | Any `IWriteStorage` |

`AggregationResult` reports the rows and groups written and the raw files and rows read.

## Quick start

An `AggregationSpec` names the time spans, the grouping and the functions per column. A `Downsampler` runs it:

```csharp
using Easy.TimeSeries.Aggregation;
using Easy.TimeSeries.Paths;
using Easy.TimeSeries.Storage;

var spec = new AggregationSpec(
        aggregationTimeSpan: AggregationTimeSpan.Minute,
        storageTimeSpan: TimeGranularity.Day)
    .GroupBy("Sensor")
    .Column("Voltage", Agg.Avg, Agg.Min, Agg.Max, Agg.Q95, Agg.Quantile(0.999))
    .Column("Current", Agg.Avg, Agg.StdDev.WithOutput(OutputEncoding.ScaledNumber32(3)))
    .Column("Status", Agg.Last);

var catalog = new FileCatalog(new DirectoryInfo("/data"));
var raw = new PathBuilder("raw", TimeGranularity.Hour);
var downsampler = new Downsampler(spec, rawStorageTimeSpan: TimeGranularity.Hour);

// reads raw/2026/10/25/{00..23}/sensor-a.ets, writes agg-1m-daily/2026/10/25/sensor-a.ets
var result = await downsampler.RunAsync(catalog, raw, "sensor-a", new DateTime(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc));
```

The aggregated file of this spec, for two sensors:

| Timestamp | Sensor | Count | Voltage.Avg | Voltage.Min | ... | Status.Last |
| --- | --- | --- | --- | --- | --- | --- |
| 14:05 | sensor-a | 600 | 230.4 | 228.9 | ... | ok |
| 14:05 | sensor-b | 598 | 229.8 | 227.1 | ... | ok |
| 14:06 | sensor-a | 601 | 231.0 | 229.3 | ... | warn |

Each row covers one slice of the aggregation time span, aligned to midnight UTC and stamped with the slice start. A
slice without raw rows produces no row, so the time column has gaps; `Count` shows sparse slices.

The aggregated files land under a root that names both time spans, with date folders following the storage time span:

```text
agg-1m-daily / 2026/10/25 / sensor-a.ets
   │    │         │
   │    │         └─ date folders: storage time span (1 day -> yyyy/MM/dd)
   │    └─ storage time span: daily
   └─ aggregation time span: 1 minute
```

Storage time spans are spelled out (`hourly`, `daily`, `monthly`, `yearly`) so that "month" never collides with the
"m" of minutes. `spec.CreatePathBuilder()` returns the `PathBuilder` of the aggregated files, for range queries.

## Write-time aggregation

Aggregations can be kept up to date as raw files are written. Two pieces work together:

- `AggregatingCatalog` decorates the storage catalog the raw writers use. It needs no change to the writers: it works
  with the plain `Writer` and every generated writer.
- `AggregationMaintenance` does the work the hook defers. **Your application runs it**, from a scheduled job or a
  hosted service.

The hook does what it can at once and leaves a note for the rest:

| Aggregated vs raw storage time span | What happens when a raw file is written |
| --- | --- |
| Equal, e.g. hourly over hourly raw | The aggregated file is computed from the bytes just written, with no second read |
| Longer, e.g. daily over hourly raw | A dirty marker is set; the aggregated file is computed once the span closes |

A daily aggregated file over hourly raw files cannot be final until the day is over, and computing it after every
hourly write would read the same raw files 24 times. A marker collapses all those writes into one recompute, done by
maintenance after the span closed.

```mermaid
sequenceDiagram
    participant App as raw writer
    participant AC as AggregatingCatalog
    participant S as storage
    participant M as AggregationMaintenance
    App->>AC: OpenRawWrite, write, CloseAsync
    AC->>S: commit raw file
    AC->>S: equal storage time span: write aggregated file now
    AC->>S: longer storage time span: set dirty marker
    Note over M: later, after the span closed
    M->>S: recompute aggregated file from all its raw files
    M->>S: delete marker only if its version is unchanged
```

```csharp
var configuration = new AggregationConfiguration
{
    Raw = new PathBuilder("raw", TimeGranularity.Hour),
    Specs = [tenSecondsHourly, oneMinuteDaily, fifteenMinutesMonthly],
    RawRetention = TimeSpan.FromDays(40),
    OnFailure = failure => logger.LogWarning(failure.Exception, "Aggregation failed: {Failure}", failure),
};

var blobs = new AzureBlobCatalog(containerClient);
var catalog = new AggregatingCatalog(blobs, configuration);

// application write path - unchanged except where the storage comes from
await using var storage = catalog.OpenRawWrite(bucketStart, "sensor-a");
await sensorWriter.WriteAsync(rows, storage);
```

`CloseAsync` of a raw file never fails because of aggregation. When it returns, the raw file is committed; a failed
immediate aggregation falls back to a marker and is reported to `OnFailure`.

### Your responsibility: periodic maintenance

Without a maintenance job, aggregated files of longer storage time spans are never written, and nothing retries a
failure. The job owes four things:

1. **Process markers** with `ProcessDirtyAsync`, every few minutes. A marker is handled once its span end plus
   `GracePeriod` (default 5 minutes) has passed, so a daily file appears at the earliest 5 minutes after midnight UTC
   and at the latest one job interval later.
2. **Recover** with `RecoverAsync(from, to)` over a sliding window, e.g. the last 7 days, less often - hourly or daily.
   It sets markers for raw files written without the hook, and for markers lost when marking failed after a raw
   commit. It recomputes nothing itself.
3. **Watch failures.** `OnFailure` reports problems as they happen; `ProcessDirtyAsync` returns counters per run. A
   marker that keeps failing ends in a failed folder: inspect it with `ListFailedAsync`, fix the cause, re-arm it with
   `RearmAsync`.
4. **Configure retention to match.** `RawRetention` must be at least the longest storage time span plus the grace
   period - a monthly aggregation needs 31 days plus 5 minutes - and your raw retention job must not delete raw files
   sooner.

```csharp
var maintenance = new AggregationMaintenance(blobs, configuration);

// every few minutes
var run = await maintenance.ProcessDirtyAsync(cancellationToken);
if (run.MovedToFailed > 0)
{
    await foreach (var failed in maintenance.ListFailedAsync(cancellationToken))
    {
        logger.LogError("Aggregation {Spec} {Subject} {Start} failed: {Error}",
            failed.Spec, failed.SubjectId, failed.StorageSpanStart, failed.LastError);
    }
}

// hourly or daily
var now = timeProvider.GetUtcNow().UtcDateTime;
await maintenance.RecoverAsync(now.AddDays(-7), now, cancellationToken);
```

How many jobs may run at once depends on the backend:

| Backend | Maintenance jobs per root |
| --- | --- |
| `AzureBlobCatalog` | Any number: conditional deletes are ETag preconditions checked by the service |
| `FileCatalog` | One: compare-and-delete is not atomic across processes |

## Command line

`ets downsample` runs the batch downsampler over a directory, with predefined functions only:

```shell
ets downsample --input ./data/raw --output ./data --raw-granularity hour \
    --aggregation 1m --storage day --from 2026-10-01 --to 2026-11-01 \
    --group Sensor --columns Voltage=avg+min+max+q95,Current=avg+stddev,Status=last,Ok
```

| Option | Meaning |
| --- | --- |
| `--input` | Directory with raw files in the `PathBuilder` layout, without a root |
| `--output` | Directory under which the aggregated root is created |
| `--raw-granularity` | Raw storage time span: `hour`, `day`, `month`, `year` |
| `--aggregation` | Aggregation time span slug: `1s`, `10s`, `1m`, `5m`, `15m`, `1h`, `1d` |
| `--storage` | Aggregated storage time span: `hour`, `day`, `month`, `year` |
| `--from`, `--to` | UTC range; storage time spans overlapping it are computed |
| `--columns` | Columns separated by `,`, functions by `+`, e.g. `Voltage=avg+q99.9,Status` |
| `--group` | Optional group column |
| `--subject` | Optional; every subject found in the range when omitted |
| `--time` | Optional raw time column, for raw files with several DateTime columns |
| `--force` | Overwrite existing aggregated files |

Function names are `avg`, `median`, `min`, `max`, `stddev`, `trueratio`, `last`, `q95`, `q99` and `q<percent>`.
A failing file is reported and the run continues; the exit code is 1 if any file failed.

## The spec in detail

`AggregationSpec` is immutable: every method returns a new spec, so a base spec can be shared and derived.

| Method | Purpose |
| --- | --- |
| `new AggregationSpec(aggregationTimeSpan, storageTimeSpan)` | Both time spans, required |
| `.Column(label, functions...)` | Predefined functions over one raw column |
| `.Function(function)` | A custom function, usually over several raw columns |
| `.GroupBy(label)` | One series per value of a raw Category column |
| `.Time(label)` | Names the raw time column; needed only when a raw file has several DateTime columns |
| `.CountLabel(label)` | Renames the `Count` column |
| `.WithRoot(root)` | Replaces the default root `agg-{slug}-{word}` |
| `.WithTimeSpans(aggregation, storage)` | Same columns, other time spans |
| `.Describe(rawHeaders)` | The output columns, without decoding data; useful for testing a reader DTO |

| Aggregation time span | Slug |
| --- | --- |
| `Second` | `1s` |
| `TenSeconds` | `10s` |
| `Minute` | `1m` |
| `FiveMinutes` | `5m` |
| `FifteenMinutes` | `15m` |
| `Hour` | `1h` |
| `Day` | `1d` |

- Slices are half-open. A row at 14:05:59.999 belongs to 14:05, one at 14:06:00 to 14:06.
- Output columns are, in order:
  1. time
  2. group, if any
  3. count
  4. one column per function, in the order of `Column` and `Function` calls
- Rows are sorted by time, then group by ordinal string comparison.
- Every row of a raw file must lie within the raw storage time span its bucket start names. A row outside fails the
  aggregated file with an error naming the raw file.

## Predefined functions

| Function | Applies to | Label | Result type |
| --- | --- | --- | --- |
| `Agg.Avg` | numeric | `{column}.Avg` | `double`; `decimal` for Decimal |
| `Agg.Median` | numeric | `{column}.Median` | `double`; `decimal` for Decimal |
| `Agg.Q95`, `Agg.Q99` | numeric | `{column}.Q95`, `{column}.Q99` | `double`; `decimal` for Decimal |
| `Agg.Quantile(p)` | numeric | `{column}.Q{p x 100}`, e.g. `Q99.9` | `double`; `decimal` for Decimal |
| `Agg.Min`, `Agg.Max` | numeric, DateTime, TimeSpan | `{column}.Min`, `{column}.Max` | same as the input |
| `Agg.StdDev` | numeric | `{column}.StdDev` | `double` |
| `Agg.TrueRatio` | Bool | `{column}.TrueRatio` | `double` |
| `Agg.Last` | any | `{column}.Last` | same as the input |

- Numeric means Int32, Int64, Float, Double, ScaledNumber32/64, Decimal and the raw variants.
- A column named without functions defaults to Avg (numeric), TrueRatio (Bool) or Last (Category).
- `.As("label")` sets an explicit label; `.WithOutput(encoding)` an explicit output encoding.
- Every aggregated label must be unique. `Agg.Q95` and `Agg.Quantile(0.95)` on one column collide; `Agg.Median` and
  `Agg.Quantile(0.5)` do not (`Median` and `Q50`).

Definitions:

- **Quantiles** interpolate linearly between the two closest ranks (Hyndman-Fan type 7, as NumPy and Excel
  `PERCENTILE.INC`). Q50 equals the median.
- **StdDev** is the population standard deviation (divides by n); a single row yields 0.
- **Avg** sums integers exactly in `Int128` and doubles with compensated summation. Decimal stays exact.
- **Last** is the row with the latest timestamp; among equal timestamps the row written last in the raw file.

### Non-finite values

- Numeric functions skip NaN. Median and quantiles rank only real values.
- Last copies the value as is, NaN included.
- When every value of a slice is NaN, the result is NaN. `Count` still counts all raw rows.
- Infinity is a value: it sorts to the ends and propagates into Avg and StdDev.

### Result encodings

| Raw column | Min, Max, Last | Avg, Median, quantiles, StdDev |
| --- | --- | --- |
| ScaledNumber32/64 with N decimals | same | same scaled type with N decimals |
| Decimal | Decimal | Decimal; StdDev is Double |
| DateTimeOrdered | DateTimeUnordered, same precision | - |
| Any other | same | Double |

- Min, Max and Last of a DateTimeOrdered column are stored unordered, because with grouping they can decrease.
- When raw files of one aggregated file differ, the finest encoding wins: the most decimal places, or the lossless type
  (Float, Double, Int32, Int64) when the column types differ.
- The aggregated time column is DateTimeOrdered with `Seconds` precision, or `Days` for the `Day` aggregation time span.

The core scaled writers saturate silently, so the downsampler checks every result written to a scaled encoding: NaN,
infinity and values whose scaled integer does not fit fail the aggregated file. The error names the function; set a
wider encoding with `.WithOutput(...)`, e.g. StdDev of a 1-decimal voltage stored with 3 decimals so small spreads do
not round to zero.

## Custom functions

A custom function implements `IAggregationFunction<TOut>` and is added with `.Function(...)`. It can read several raw
columns of the same slice, including the time column.

```csharp
public sealed class AveragePower(string voltage, string current) : IAggregationFunction<double>
{
    public string Name => "Power.Avg";

    public IReadOnlyList<AggregationInput> Inputs { get; } =
    [
        AggregationInput.Of<float>(voltage),   // ScaledNumber32 decodes as float
        AggregationInput.Of<double>(current),
    ];

    public OutputEncoding Output => OutputEncoding.ScaledNumber64(decimalPlaces: 1);

    public double Compute(in AggregationSlice slice)
    {
        var u = slice.Values<float>(0);
        var i = slice.Values<double>(1);
        var sum = 0d;
        for (var k = 0; k < u.Length; k++)
        {
            sum += u[k] * i[k];
        }

        return sum / u.Length;
    }
}
```

- `Inputs` order defines the index used with `slice.Values<T>(index)`. Values are aligned row by row, in time order,
  and never empty.
- `T` is the decoded CLR type: `float`, `double`, `decimal`, `int`, `long`, `DateTime`, `TimeSpan`, `bool` or
  `string`.
- A function with several inputs must declare a concrete `Output`. One with a single input may use
  `OutputEncoding.SameAsInput` or `OutputEncoding.FromInputPrecision`.
- `Compute` is called once per slice and must be thread-safe.

| `AggregationSlice` member | Meaning |
| --- | --- |
| `Values<T>(input)` | One input's values within the slice |
| `Sorted<T>(input)` | Sorted copy without NaN, shared by all functions on the same raw column within the slice |
| `Times` | Timestamps of the rows, non-decreasing |
| `Start`, `End` | Bounds of the slice's aggregation time span |
| `Group` | Group value, or `null` |
| `Project(input)` | A view with only that input, at index 0, for calling a predefined function |

Predefined functions are public classes in `Easy.TimeSeries.Aggregation.Functions` and can be reused inside a custom
one:

```csharp
private readonly Quantile<double> q90 = new("Current", 0.9);

public double Compute(in AggregationSlice slice)
    => slice.Values<float>(0)[^1] * q90.Compute(slice.Project(1));
```

## How write-time aggregation stays correct

The guarantees behind the maintenance job, for when a counter or a failure needs explaining.

### The hook

- If two writers commit the same raw path at once, the hook detects it and sets a marker rather than keeping a result
  computed from the losing writer's bytes.
- `OnFailure` is for monitoring only; exceptions it throws are ignored.
- Every other catalog operation passes through unchanged. Deleting a raw file, e.g. for retention, marks nothing.

### Dirty markers

A marker records that one aggregated file must be (re)computed:

```text
dirty / agg-1m-daily / 2026/10/25 / sensor-a.json
  │         │             │
  │         │             └─ date folders and subject of the aggregated file
  │         └─ aggregated root
  └─ marker root (MarkerRoot, default "dirty")
```

Failed markers live under `{marker root}/failed/`, so a spec root must not start with a `failed` folder.

`ProcessDirtyAsync` handles each marker:

1. It is skipped while its span is open: until the span end plus `GracePeriod`.
2. The aggregated file is computed from all raw files of the span in storage.
3. The marker is deleted only if its version is unchanged.

A raw write during step 2 rewrites the marker, the delete in step 3 fails, and the next run includes the late data.
A late raw file therefore can never be lost from its aggregated file. Many raw writes into one span collapse into one
marker, so a backfill costs one recompute per aggregated file.

- `MaxDegreeOfParallelism` bounds how many aggregated files one call computes at once.
- Subject ids are file names, so listing the raw files of a span returns every subject's files. One call lists each
  span once and shares the listing among all its markers, so closing a day for many subjects costs one listing.

The counters `ProcessDirtyAsync` returns, per marker:

| Counter | Meaning |
| --- | --- |
| `Processed` | Aggregated file recomputed |
| `SkippedOpen` | Span still open or within the grace period |
| `Retried` | Failed attempt that will be retried |
| `Deferred` | Failed marker waiting for its retry delay |
| `MovedToFailed` | Last allowed attempt failed; marker moved to the failed folder |
| `RejectedByRetention` | Span whose raw files may be gone; marker removed without recomputing |

### Failing markers

Some failures repeat on every attempt: a raw row outside its time span, a schema that no longer matches the spec, a
result out of range.

- A failed attempt rewrites the marker with an attempt count and the error.
- The next attempt waits `RetryDelay` (default 10 minutes), doubled after each further failure. A short storage outage
  therefore does not use up all attempts: with the defaults a marker reaches the failed folder 30 minutes after its
  first failure at the earliest.
- After `MaxAttempts` (default 3), the marker moves to `dirty/failed/...`.
- A later raw write into the span sets a fresh marker with no delay, so replacing the bad raw file retries
  automatically.
- `MarkDirtyAsync` requests a recompute of one aggregated file explicitly, e.g. after a spec changed.

### Raw retention

Raw files are usually kept shorter than their aggregations. Recomputing a span whose oldest raw files are already gone
would overwrite a complete aggregated file with a partial one.

- A span that starts earlier than now minus `RawRetention` is never recomputed. Its markers are removed and reported
  as `RejectedByRetention`.
- Missing raw files are never a reason to recompute; an aggregated file without raw files is the expected state after
  retention.

### Recovery and source records

Each time the hook or the marker processor writes an aggregated file, a source record is written after it, in the same
folder. It lists the path and version of every raw file read, and the version the aggregated write committed.

```text
agg-1m-hourly/2026/10/25/14/sensor-a.ets              aggregated file
agg-1m-hourly/2026/10/25/14/sensor-a.sources.json     raw files it was computed from
dirty/agg-1m-hourly/2026/10/25/14/sensor-a.json       pending recompute, while one is due
```

`RecoverAsync` lists the raw files in its range, clipped to the raw retention, and sets a marker for every aggregated
file whose source record:

- is missing,
- does not list one of those raw files in its current version, or
- was written for another version of the aggregated file.

The raw versions tell whether the inputs changed; the aggregated version tells whether the file in storage is still
the one the record describes, and not one an overlapping run wrote over it. Comparing versions needs no clock, so a
file computed seconds after its raw files is not mistaken for stale, and a raw file replaced within the same second is
still noticed.

- Deleted raw files never make an aggregated file stale.
- An aggregated file written without a record, e.g. by `ets downsample`, is recomputed once if recovery covers it.
- A failed marker is left alone until one of the span's raw files is newer than the failure, so a span that keeps
  failing is not retried on every recovery run.
- A record lives and is deleted together with its aggregated file. `PathBuilder.TryParse` never matches it, because
  subject ids contain no dot, but a plain listing of an aggregated folder shows it.
- On a `FileCatalog`, recovery also removes temp files older than `OrphanedTempFileAge` (default 1 hour), left by
  crashed writes.

`FileCatalog` versions are the last-write time and size of a file. A marker rewritten within the file system's
timestamp resolution with the same size would keep its version; with one maintenance job and real-world write rates
this does not happen.

## Limits and errors

| Situation | Thrown as | When |
| --- | --- | --- |
| Invalid time spans, labels, quantile level, duplicate labels | `ArgumentException` | Building the spec |
| More than 255 output columns | `ArgumentException` | Building the spec |
| Missing input, wrong CLR type, unsupported function for a type | `AggregationException` | Reading each raw file |
| Row outside its raw file's storage time span | `AggregationException` | Reading each raw file |
| More than 32,767 groups, or group labels over 64 KiB | `AggregationException` | Aggregating |
| NaN, infinity or out-of-range value for a scaled encoding | `AggregationException` | Writing results |
| Storage I/O | Original exception | Any |

`AggregationException` carries the raw path, column and function involved, when known. In the write-time path none of
these escape `CloseAsync`; they reach `OnFailure` wrapped in an `AggregationFailure`.
