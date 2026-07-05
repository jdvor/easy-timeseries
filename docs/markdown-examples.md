# Markdown Examples

Reference page for formatting and visualization features available in this mdbook.
Plugins installed: `mdbook-toc`, `mdbook-mermaid`, `mdbook-admonish`, `mdbook-variables`.

<!-- toc -->

---

## Text Formatting

**Bold**, *italic*, ***bold italic***, ~~strikethrough~~, `inline code`.

> Blockquote text. Can span multiple lines.
> Second line of the blockquote.
>
> > Nested blockquote.

Superscript via HTML: 10<sup>6</sup> records. Subscript: H<sub>2</sub>O.

---

## Lists

Unordered:

- Alpha
- Bravo
  - Charlie
  - Delta
    - Echo
- Foxtrot

Ordered:

1. First step
2. Second step
   1. Sub-step A
   2. Sub-step B
3. Third step

Task list:

- [x] Write format examples
- [x] Add mermaid diagrams
- [ ] Publish to GitHub Pages

---

## Code

Inline: call `ReadBuilder.Build<T>()` to materialize records.

Fenced block with syntax highlighting:

```csharp
public sealed class PowerPlant
{
    [Column(0)] public DateTime Timestamp { get; set; }
    [Column(1)] public float OutputMw { get; set; }
    [Column(2, decimalPlaces: 2)] public decimal Price { get; set; }
    [Column(3)] public string? Zone { get; set; }
}
```

Shell:

```bash
dotnet test tests/Easy.TimeSeries.Tests/ -c Release --nologo
```

---

## Tables

| Column type       | Writer method     | Reader method       | Notes                        |
|-------------------|-------------------|---------------------|------------------------------|
| `DateTime`        | `AddTime`         | `ReadTime`          | Epoch 2000-01-01, 41-bit cap |
| `int`             | `AddInt32`        | `ReadInt32`         |                              |
| `long`            | `AddInt64`        | `ReadInt64`         |                              |
| `float`           | `AddFloat`        | `ReadFloat`         |                              |
| `double`          | `AddDouble`       | `ReadDouble`        |                              |
| `decimal`         | `AddScaled32`     | `ReadScaled32`      | `decimalPlaces` in meta      |
| `bool`            | `AddBool`         | `ReadBool`          |                              |
| `string`          | `AddCategory`     | `ReadCategory`      | Dictionary-encoded           |

---

## Admonitions (`mdbook-admonish`)

```admonish note
Default note block. Use for supplementary context that is worth capturing but not critical.
```

```admonish tip
Use `ArrayPool<T>.Shared` on hot write paths to avoid per-record allocations.
```

```admonish info title="Epoch"
The timestamp encoder uses a fixed epoch of **2000-01-01 UTC** and stores values in
the smallest precision that fits the column's declared `TimePrecision`. The 41-bit
ceiling limits the maximum storable timestamp to roughly year 2069 at millisecond
precision.
```

```admonish warning
`BitWriter.CommitRecord()` must be called **exactly once per logical entry** by every
domain writer. Skipping it corrupts `ColumnHeader.Records` and breaks the reader.
```

```admonish danger title="Do not call on the hot path"
`ReflectionBasedMaterializer` uses compiled `Expression`-based setters. The first call
per type compiles and caches the delegate. Calling the constructor on every read will
re-compile every time and cause severe throughput degradation.
```

```admonish bug
Scaled numbers read as zero when `ColumnInfo.Meta` is missing the `decimalPlaces` field
because both writer and reader derive the scale factor from it. Missing meta defaults to
`0`, giving a scale of `1` — which truncates all fractional values.
```

```admonish example
Round-trip a `PowerPlant` DTO through in-memory storage:

```csharp
var storage = new InMemoryStorage();
using (var w = new Writer(storage, granularity: TimeGranularity.Hour))
{
    w.AddTime(0, TimePrecision.Second);
    w.AddFloat(1);
    await w.FlushAsync(timestamp);
}
var records = await new ReadBuilder(storage)
    .Build<PowerPlant>(timestamp)
    .ToListAsync();
```
```

```admonish quote title="Design goal"
"Low-level columnar APIs stay easy to use directly. Higher-level conveniences sit on top
in a use-it-or-ignore-it fashion — never make them mandatory."
```

---

## Diagrams (`mdbook-mermaid`)

### File format — block structure

```mermaid
block-beta
  columns 4
  H["Header\n(variable)"]:4
  C0H["ColumnHeader\n(9 bytes)"] C0D["Column 0 data\n(DataLength bytes)"]:3
  C1H["ColumnHeader\n(9 bytes)"] C1D["Column 1 data\n(DataLength bytes)"]:3
  CN["..."]
  style H fill:#4a6fa5,color:#fff
  style C0H fill:#6b8f71,color:#fff
  style C1H fill:#6b8f71,color:#fff
  style C0D fill:#a8c5a0,color:#000
  style C1D fill:#a8c5a0,color:#000
```

### Write pipeline — sequence

```mermaid
sequenceDiagram
    participant App
    participant Writer
    participant ColumnWriter as Column writer<br/>(per type)
    participant BitWriter
    participant Storage

    App->>Writer: AddInt32(columnIndex, value)
    Writer->>ColumnWriter: Write(value)
    ColumnWriter->>BitWriter: Write(descriptor)
    ColumnWriter->>BitWriter: Write(value)
    ColumnWriter->>BitWriter: CommitRecord()
    App->>Writer: FlushAsync(timestamp)
    Writer->>ColumnWriter: Flush() — writes ColumnHeader
    Writer->>Storage: WriteAsync(buffer)
```

### Read pipeline — flowchart

```mermaid
flowchart LR
    S([Storage]) --> RB[ReadBuilder]
    RB -->|"per column\ndispatch"| R0[DateTime\nReader]
    RB --> R1[Int32\nReader]
    RB --> R2[Category\nReader]
    R0 & R1 & R2 -->|SetColumn| M[IMaterializer&lt;T&gt;]
    M -->|"yield"| DTO([T records])
```

### ER — key types

```mermaid
erDiagram
    Writer ||--o{ ColumnWriter : "creates one per column"
    ColumnWriter ||--|| BitWriter : owns
    Writer ||--|| IWriteStorage : flushes-to

    ReadBuilder ||--o{ ColumnReader : "dispatches one per column"
    ColumnReader ||--|| BitReader : owns
    ReadBuilder ||--|| IMaterializer : "pushes values into"
    ReadBuilder ||--|| IReadStorage : reads-from

    Header ||--o{ ColumnInfo : contains
    ColumnInfo ||--|| ColumnValueType : "describes type via"
```

### Project dependencies

```mermaid
graph TD
    Abs["Easy.TimeSeries<br/>.Abstractions"]
    Core["Easy.TimeSeries"]
    SrcGen["Easy.TimeSeries<br/>.SrcGen"]
    Azure["Easy.TimeSeries<br/>.AzureBlobs"]
    Parquet["Easy.TimeSeries<br/>.Parquet"]
    Cmd["Easy.TimeSeries<br/>.CmdLine"]
    Tests["Easy.TimeSeries<br/>.Tests"]
    Bench["Easy.TimeSeries<br/>.Benchmarks"]
    Sample["Easy.Sample"]

    Core --> Abs
    SrcGen --> Abs
    Azure --> Core
    Parquet --> Core
    Cmd --> Core
    Tests --> Core
    Bench --> Core
    Sample --> Core
```

---

## Variables (`mdbook-variables`)

The following values are injected from `book.toml` at build time:

| Variable          | Value rendered here                  |
|-------------------|--------------------------------------|
| `author_name`     | {{author_name}}                      |
| `author_email`    | {{author_email}}                     |
| `copyright`       | {{copyright}}                        |

---

## Links and References

- [mdbook documentation](https://rust-lang.github.io/mdBook/)
- [mdbook-admonish](https://github.com/tommilligan/mdbook-admonish)
- [mdbook-mermaid](https://github.com/badboy/mdbook-mermaid)
- [mdbook-toc](https://github.com/badboy/mdbook-toc)
- [mdbook-variables](https://gitlab.com/tglman/mdbook-variables)

Internal cross-references: [Layout details](layout.md), [Code style](code-style.md).
