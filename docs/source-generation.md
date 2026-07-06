# Source Generation

`Easy.TimeSeries.SrcGen` is a Roslyn incremental source generator that bridges row-based DTO classes and the
columnar storage APIs. Annotate a DTO once and the generator emits allocation-conscious, strongly typed
implementations of the `Easy.TimeSeries.Abstractions` contracts:

- `[GenerateWriter]` emits `{Dto}Writer : IWriter<{Dto}>`.
- `[GenerateReader]` emits `{Dto}Materializer : IMaterializer<{Dto}>` and `{Dto}Reader : IReader<{Dto}>`.

All generated classes are `public sealed partial`, live in the DTO's namespace, and carry
`[GeneratedCode]`. Being partial, they can be extended with convenience overloads (e.g. writing to a
`FileInfo` or returning `byte[]`) in a separate file.

## Annotating a DTO

```csharp
[GenerateReader]
[GenerateWriter]
public sealed class PowerNode
{
    [Column(0, Label = "Country")]
    public string CountryCode { get; set; } = string.Empty;

    [Column(1, Label = "Capacity (MW)", NumberPrecision = NumberPrecision.DecimalPlaces3)]
    public float CapacityMw { get; set; }

    [Column(2, Label = "Measurement Time", DateTimePrecision = DateTimePrecision.Milliseconds,
        DateTimeSort = DateTimeSort.Ascending)]
    public DateTime MeasurementTimeUtc { get; set; }
}
```

Requirements checked at compile time:

- The DTO must be a non-generic, non-nested, public or internal class. `[GenerateReader]` additionally
  requires a public parameterless constructor and a non-abstract class (the `IMaterializer<T>`
  contract is `class, new()`).
- Column indexes must start at 0 and be contiguous; they define the physical column order.
- Writer generation needs an accessible getter on each column property; reader generation needs an
  accessible non-init setter.
- Nullable properties (`int?`, `string?`) are rejected - the storage format has no null representation.
- If `Label` is omitted, the property name is used as the column label.

## Type mapping

With `ValueType = DataValueType.Auto` (the default), the column type is inferred from the property's CLR
type. An explicit `ValueType` overrides the inference but must stay compatible with the CLR type.

| CLR type   | Attribute options                  | Column value type | Writer call                             |
| ---------- | ---------------------------------- | ----------------- | --------------------------------------- |
| `string`   | -                                  | Category          | `AddCategory`                           |
| `int`      | -                                  | Int32             | `AddInt32`                              |
| `int`      | `NumberDistribution = Random`      | Int32Raw          | `AddInt32Random`                        |
| `long`     | -                                  | Int64             | `AddInt64`                              |
| `float`    | -                                  | Float             | `AddFloat`                              |
| `float`    | `NumberPrecision = DecimalPlacesN` | ScaledNumber32    | `AddScaledNumber32(..., N)`             |
| `float`    | `NumberDistribution = Random`      | FloatRaw          | `AddFloatRandom`                        |
| `double`   | -                                  | Double            | `AddDouble`                             |
| `double`   | `NumberPrecision = DecimalPlacesN` | ScaledNumber64    | `AddScaledNumber64(..., N)`             |
| `double`   | `NumberDistribution = Random`      | DoubleRaw         | `AddDoubleRandom`                       |
| `long`     | `NumberDistribution = Random`      | Int64Raw          | `AddInt64Random`                        |
| `decimal`  | (`NumberPrecision` is ignored)     | Decimal           | `AddDecimal`                            |
| `bool`     | -                                  | Bool              | `AddBool`                               |
| `DateTime` | `DateTimeSort = Ascending`         | DateTimeOrdered   | `AddTimeOrdered(..., DateTimePrecision)`|
| `DateTime` | default (`Unsorted`)               | DateTimeUnordered | `AddTimeUnordered(..., DateTimePrecision)` |
| `TimeSpan` | -                                  | TimeSpan          | `AddInterval(..., TimeSpanPrecision)`   |

`DateTimePrecision` defaults to `Milliseconds`, `TimeSpanPrecision` to `Seconds`; both translate 1:1 to the
core `TimePrecision` enum.

`NumberDistribution` defaults to `Continuous` (XOR/delta "Gorilla" encoding, compact for slowly-varying signals).
Set it to `Random` on a `float`, `double`, `long` or `int` column whose values are uncorrelated between rows (e.g.
geographic coordinates, identifiers) to store the values raw and Brotli-compressed instead - see the raw column
types in `layout.md`. It is mutually exclusive with `NumberPrecision`: if both are set the scaled encoding wins and
`NumberDistribution` is reported ignored (ETS009). Setting it on any other column type is likewise ignored (ETS009).

## Shape of the generated code

- The generated writer makes a single pass over the input collection into per-column arrays rented from
  `ArrayPool<T>.Shared` and hands the columnar `Writer` exact-length `ArraySegment<T>` views, in ascending
  column-index order (the storage format derives column indexes from call order). String arrays are
  returned to the pool with `clearArray: true`.
- The generated materializer routes values with a generic `Hydrate<TPropValue>` whose `is`-pattern chain
  covers only the value types the DTO uses. The JIT specializes the method per struct type argument and
  folds the type tests away, leaving a plain `switch` on the column index - no boxing, no delegates, no
  per-value allocations (this is what makes it preferable to `ReflectionBasedMaterializer` on hot paths).
- The generated reader is a thin dispatcher pairing `Easy.TimeSeries.Reader` with the generated
  materializer.

## Diagnostics

| ID     | Severity | Condition                                                               |
| ------ | -------- | ----------------------------------------------------------------------- |
| ETS001 | Error    | Duplicate column index                                                  |
| ETS002 | Error    | Column indexes do not start at 0 or are not contiguous                  |
| ETS003 | Error    | Property type cannot be mapped to a column value type                   |
| ETS004 | Error    | Column property is nullable                                             |
| ETS005 | Error    | Explicit `ValueType` incompatible with the property CLR type            |
| ETS006 | Error    | Scaled number column without `NumberPrecision` DecimalPlaces1..5        |
| ETS007 | Error    | Missing accessible getter (writer) / non-init setter (reader)           |
| ETS008 | Error    | Annotated type not usable (struct, generic, nested, inaccessible, ...)  |
| ETS009 | Warning  | Attribute option has no effect on this property and is ignored          |
| ETS010 | Error    | Annotated type has no `[Column]` properties                             |
| ETS011 | Error    | Compilation does not reference `Easy.TimeSeries`                        |

## Consuming the generator

Reference the generator project (or NuGet package) as an analyzer plus the runtime libraries:

```xml
<ItemGroup>
  <ProjectReference Include="..\Easy.TimeSeries\Easy.TimeSeries.csproj" />
  <ProjectReference Include="..\Easy.TimeSeries.SrcGen\Easy.TimeSeries.SrcGen.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

Set `<EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>` to inspect the emitted sources under
`obj/**/generated/`. A complete end-to-end example lives in `src/Easy.Sample`.
