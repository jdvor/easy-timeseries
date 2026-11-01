# Code Style

This document describes the C# style and conventions for `easy-timeseries`. It applies to all projects under `src/`
and `tests/`.

The library is small, public, and performance-sensitive. We lean conservative on public API surface, aggressive on
allocations, and pragmatic everywhere else. When in doubt, look at existing code in `src/Easy.TimeSeries/` - it is the
reference.

What tooling can enforce, it does: `.editorconfig`, the analyzers configured in `src/Directory.Build.props`, and the
banned APIs in `src/BannedSymbols.txt` all fail or warn in the build. This document covers only what they cannot.
Treat analyzer warnings as work to fix, not to suppress.

## Keep the design straightforward

The most important rule: write the simplest code that solves today's problem.

- Don't design for futures that may never come. No extension points, plugin mechanisms, strategy hierarchies or
  configuration options "in case we need them later". Add them when a second real use appears - changing simple code
  then is cheaper than carrying an abstraction nobody uses.
- Don't introduce an interface or abstract base class for a single implementation. A concrete `sealed` class is the
  default; tests use the real thing or a real in-memory variant, not a mock that needs an interface.
- An interface earns its place when external code implements it or must depend on it without the implementation:
  the storage contracts, `IWriter<T>`, `IMaterializer<T>`. Such contracts belong in `Easy.TimeSeries.Abstractions`.
  An extension point specific to one package, such as `IAggregationFunction`, may stay in that package.
- Don't wrap a single dependency in a "provider", "service", "manager" or "helper" class that only forwards calls.
- No mediator pattern (e.g. MediatR) or similar indirection. A direct method call is easier to follow and to debug.
- No third-party DI container in the libraries; consumer applications wire things up themselves.

## File layout

- One public type per file. Small private helpers can live in the same file as their only consumer.
- File name matches the primary type name.
- File-scoped namespace, with `using` directives **inside** the namespace:

  ```csharp
  namespace Easy.TimeSeries;

  using System.Buffers;
  using System.Buffers.Binary;
  using static Constants;

  internal sealed class BitWriter
  {
      // ...
  }
  ```

- `using static <ConstantsClass>` is encouraged where it removes verbose qualification on hot paths (see how
  `Constants` is imported in `BitWriter`, `Int32Writer`, etc.).

## Project and namespace organization

- Organize by feature, not by class type. Existing examples: `Storage/`, `Paths/`, `Internal/`, `WriteTime/`. Do not
  introduce folders like `Providers/`, `Services/`, `Validators/`, `Helpers/`.
- Keep the namespace tree shallow. Excluding the `Easy.TimeSeries.` prefix, a sensible maximum depth is 2.
- A namespace with very few types is suspicious unless those types form a public API contract on purpose, as in
  `Easy.TimeSeries.Abstractions`.
- Implementation details that consumers must never see live under `Internal/` and are `internal`. Tests, benchmarks,
  and the CLI reach them through `InternalsVisibleTo` (configured in `src/Directory.Build.props`).

## Type design

- `sealed` by default for implementation classes. Open for extension only if the public API actually requires it.
- `internal ref struct` is the default shape for **readers** (see `Int32Reader`, `BitReader`,
  `DateTimeOrderedReader`, ...). They are stack-only, hold a `ReadOnlySpan<byte>` directly, and pay no allocation cost
  per column.
- `internal sealed class` is the default shape for **writers** (see `Int32Writer`, `BitWriter`, ...). They are
  heap-allocated once per column and reused across many values, so the cost is amortized.
- Use `readonly record struct` for small value-like types that participate in equality/`ToString` for free
  (`ColumnInfo`, `Column`, `Header`).
- Use `public enum X : byte` for enums that are written to disk - the byte width is part of the on-disk format.
- Prefer primary constructors for plain ctors that only assign fields (see `CategoryWriter(BitWriter bitWriter)`,
  `FloatWriter(BitWriter bitWriter)`).
- Field naming: `private readonly` fields use camelCase, no `_` prefix and no `this.` qualification on access.
  Constants and `static readonly` fields use PascalCase.

## Performance

This is where the library is opinionated.

- Treat any allocation on the per-value hot path as a bug. `Span<T>`, `ReadOnlySpan<T>`, `ArrayPool<T>.Shared`, and
  `ref struct` are the right tools; LINQ and `IEnumerable<T>` are not (LINQ is fine in startup/setup code).
- Use `PooledArrayBufferWriter` instead of `MemoryStream` or fresh `byte[]` allocations when accumulating a column.
  Always `Dispose` it (the pool will leak otherwise) - see the `using` pattern in `Writer`.
- Prefer `BinaryPrimitives.Read*LittleEndian` / `Write*LittleEndian` over `BitConverter` for fixed-endian reads and
  writes. `BitConverter.SingleToInt32Bits` / `DoubleToInt64Bits` is fine for bit reinterpretation.
- Prefer hardware intrinsics through `BitOperations` (`LeadingZeroCount`, `TrailingZeroCount`) over hand-written bit
  loops; `BlockBenchmark` guards that choice.
- `[MethodImpl(MethodImplOptions.AggressiveInlining)]` on tight helpers that are called inside per-value loops
  (`WriteWord`, `ReadWord`, the `Expect.*` family). Do not sprinkle it on everything.
- `[DebuggerStepThrough]` on guard-clause helpers so step-through debugging skips them (`Expect.*`).
- Use `TimeProvider` for the current time, so time-dependent code can be tested.

## API design

- The public surface lives in `Easy.TimeSeries` and `Easy.TimeSeries.Abstractions`. Adding to it is cheap; removing
  is a breaking change. Be deliberate.
- Use `Try*` patterns for parsing/decoding code paths that might legitimately fail (`Header.TryReadFrom`,
  `ColumnHeader.TryReadFrom`, `CategoryMap.TryReadFrom`). The `Try*` method returns `bool` and emits the parsed value
  via `out`. Don't throw for "this isn't our format" - exceptions are for programmer error and corrupt data, not for
  control flow.
- Accept `ReadOnlySpan<byte>` for read paths and `IBufferWriter<byte>` for write paths instead of `byte[]` or
  `Stream`. They compose with both pooled buffers and direct I/O.
- Async I/O is `Task`-returning with `ConfigureAwait(false)` (see `Writer.WriteToAsync`). A library must not capture
  the synchronization context.
- Use `CancellationToken cancellationToken = default` as the **last** parameter on async public methods.

## Guard clauses

- In `Easy.TimeSeries`, use the `Expect.*` helpers in `Internal/Expect.cs` (`Expect.Range`, `Expect.NotNull`,
  `Expect.Utc`, ...). They are `AggressiveInlining` + `DebuggerStepThrough` and capture the argument name
  automatically via `[CallerArgumentExpression]`.
- `Expect` is internal to the core library. Other projects use the BCL throw helpers
  (`ArgumentNullException.ThrowIfNull`, `ArgumentOutOfRangeException.ThrowIfNegative`, ...).
- Do not write hand-rolled `if (x == null) throw new ArgumentNullException(...)` boilerplate.
- Validate at the public boundary, not at every internal frame. Internal callers are trusted; `Debug.Assert` is
  acceptable for internal pre-/post-conditions on hot paths.
- `CA1062` (ValidateArgumentsOfPublicMethods) is off because the guard helpers cover the cases that matter. Do not
  lean on this to skip validation that protects format correctness.

## Documentation and comments

- XML docs are not mandatory. Write them on **public** API where the name alone doesn't carry the meaning (a `Writer`
  ctor with a `rows` parameter benefits from one; a `WriteTo(Span<byte>)` does not).
- Default to no inline comments. Only add one when the *why* is non-obvious: a hidden constraint, an invariant, a
  workaround, or a performance trade-off.
- Don't restate what the code does. Don't reference the current ticket, PR, or caller.

XML docs are read as-is in the editor, not rendered by another tool. So:

- Use XML tags sparingly - `<summary>`, `<param>`, `<remarks>`, `<see cref>` cover almost everything.
- Format width and whitespace for a human reader.
- Keep `<summary>` short - a few sentences at most. Put a longer explanation in `<remarks>`.
- Document truly complex ideas, algorithms or procedures in a page of this book, and link to it from the code. The
  byte format, for example, lives in [Data Layout Details](03-layout.md), not in XML comments.

## Tests

- xUnit v3 (see `Directory.Packages.props`). One test class per production type or feature area.
- Test method names use **snake_case** sentences describing the behavior (`Paths_that_are_not_raw_pass_through`,
  `Aborted_raw_write_aggregates_nothing`). Avoid the `Method_Condition_Result` pattern - it produces long names that
  don't display well. The body of the test documents the conditions.
- `[Theory]` over `[Fact]` when the same logic is exercised with different inputs. Inline data with `[InlineData]`;
  use a `MemberData` source only when the data is large or shared.
- Round-trip tests should write then read through the public `Writer`/`Reader` pipeline, not just exercise a single
  internal type, to catch wiring bugs (`ReferentialDataTests`, `BitsTests` are good examples).
- Integration tests live in the same test project; if they grow large enough to warrant a separate project, name it
  `Easy.TimeSeries.IntegrationTests` (already provisioned in `InternalsVisibleTo`).
- Use `BufferUtil` and similar small test helpers rather than recreating buffer setup inline in each test.

## Extension methods

- Use sparingly. Reach for an extension method only when extending a type you don't own, or when you genuinely need
  to keep a public API small.
- Centralize them. Each project gets at most one `Extensions.cs` at the root namespace of that project. Don't organize
  them by the type being extended; don't scatter them across folders.
- Extensions live in the root namespace of the project (no separate `*.Extensions` namespace). They should be in scope
  through the existing `using` directives that already pull in the project.
- Extension methods with a single call site (tests excluded) are a smell - prefer a local function or a
  private/internal static method.

## What we don't use

- No reflection on hot paths in shipping code. `ReflectionBasedMaterializer` exists as a correctness baseline; hot
  paths use the materializer emitted by `Easy.TimeSeries.SrcGen`.
- No `dynamic`, no runtime code emit (`Reflection.Emit`). Expression-tree-compiled delegates are acceptable when the
  result is cached.
