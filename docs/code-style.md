# Code Style

This document describes the C# style and conventions for `easy-timeseries`. It applies to all projects under `src/` and `tests/`.

The library is small, public, and performance-sensitive. The rules below reflect that: we lean conservative on public API surface, aggressive on allocations, and pragmatic everywhere else. When in doubt, look at existing code in `src/Easy.TimeSeries/` - it is the reference.

This doc is not a substitute for the `.editorconfig`, `src/BannedSymbols.txt`, or the analyzers configured in `src/Directory.Build.props`. Those are authoritative and enforced at build time; everything below is convention layered on top.

## Tooling baseline (enforced)

- Target framework: `net10.0` (`shared.props`). C# language version: `latest`.
- `Nullable enable`, `ImplicitUsings enable`, `InvariantGlobalization=true` for the core library.
- Analyzers on, `AnalysisMode=Recommended`, `EnforceCodeStyleInBuild=true`. Treat analyzer warnings as work to fix, not suppress, unless the rule is already centrally suppressed in `.editorconfig`.
- StyleCop conventions where they make sense (see `.editorconfig` for the precise on/off list).
- Banned APIs in `src/BannedSymbols.txt`: `DateTime.Now`, `DateTime.UtcNow`, `DateTimeOffset.Now`, `DateTimeOffset.UtcNow`. Use `TimeProvider.GetUtcNow` / `GetLocalNow` instead.
- File formatting: 4-space indent, LF line endings, UTF-8, trailing whitespace trimmed, final newline. Max line length 120.

## File layout

- One public type per file. Small private helpers can live in the same file as their only consumer.
- File name matches the primary type name.
- File-scoped namespace declaration. `using` directives go **inside** the namespace (`csharp_using_directive_placement = inside_namespace:warning`):

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

- `using static <ConstantsClass>` is encouraged where it removes verbose qualification on hot paths (see how `Constants` is imported in `BitWriter`, `Int32Writer`, etc.).

## Project and namespace organization

- Organize by feature, not by class type. Existing examples: `Storage/`, `Paths/`, `Internal/`. Do not introduce folders like `Providers/`, `Services/`, `Validators/`, `Helpers/`.
- Keep the namespace tree shallow. Excluding the `Easy.TimeSeries.` prefix, a sensible maximum depth is 2 (the core library currently uses depth 1).
- A namespace with very few types is suspicious unless those types form a public API contract on purpose. The library's `Easy.TimeSeries.Abstractions` is the obvious exception - it has small, stable public contract types and that is the point.
- Implementation details that consumers must never see live under `Internal/` and are `internal`. Tests, benchmarks, and the CLI reach them through `InternalsVisibleTo` (configured in `src/Directory.Build.props`).
- Do not use the mediator pattern (or MediatR-style indirection). Do not wrap a single dependency in a "provider" class just for the sake of it.

## Type design

- `sealed` by default for implementation classes. Open for extension only if the public API actually requires it. Existing code uses `sealed class` or `ref struct` for almost every implementation type.
- `internal ref struct` is the default shape for **readers** (see `Int32Reader`, `BitReader`, `DateTimeReader`, ...). They are stack-only, hold a `ReadOnlySpan<byte>` directly, and pay no allocation cost per column.
- `internal sealed class` is the default shape for **writers** (see `Int32Writer`, `BitWriter`, ...). They are heap-allocated once per column and reused across many values, so the cost is amortized.
- Use `readonly record struct` for small value-like types that participate in equality/`ToString` for free (`ColumnInfo`, `Column`, `Header`).
- Use `public enum X : byte` for enums that are written to disk - the byte width is part of the on-disk format.
- Prefer primary constructors for plain ctors that only assign fields (see `CategoryWriter(BitWriter bitWriter)`, `FloatWriter(BitWriter bitWriter)`).
- Field naming: `private readonly` fields use camelCase, no `_` prefix and no `this.` qualification on access (`SA1101` is disabled). Constants and `static readonly` fields use PascalCase.

## Performance

This is where the library is opinionated.

- Treat any allocation on the per-value hot path as a bug. `Span<T>`, `ReadOnlySpan<T>`, `ArrayPool<T>.Shared`, and `ref struct` are the right tools; LINQ and `IEnumerable<T>` are not (LINQ is fine in startup/setup code).
- Use `PooledArrayBufferWriter` instead of `MemoryStream` or fresh `byte[]` allocations when accumulating a column. Always `Dispose` it (the pool will leak otherwise) - see the `using` pattern in `Writer`.
- Prefer `BinaryPrimitives.Read*LittleEndian` / `Write*LittleEndian` over `BitConverter` for fixed-endian reads and writes. `BitConverter.SingleToInt32Bits` / `DoubleToInt64Bits` is fine for bit reinterpretation.
- `[MethodImpl(MethodImplOptions.AggressiveInlining)]` on tight helpers that are called inside per-value loops (`WriteWord`, `ReadWord`, the `Expect.*` family). Do not sprinkle it on everything.
- `[DebuggerStepThrough]` on guard-clause helpers so step-through debugging skips them (`Expect.*`).
- Lookup tables beat compute on the hot path even when they cost a few hundred bytes of static data - see `Constants.Size32.LeadingZerosLookup`.

## API design

- The public surface lives in `Easy.TimeSeries` and `Easy.TimeSeries.Abstractions`. Adding to it is cheap; removing is a breaking change. Be deliberate.
- Use `Try*` patterns for parsing/decoding code paths that might legitimately fail (`Header.TryReadFrom`, `ColumnHeader.TryReadFrom`, `CategoryMap.TryReadFrom`). The `Try*` method returns `bool` and emits the parsed value via `out`. Don't throw for "this isn't our format" - exceptions are for programmer error and corrupt data, not for control flow.
- Accept `ReadOnlySpan<byte>` for read paths and `IBufferWriter<byte>` for write paths instead of `byte[]` or `Stream`. They compose with both pooled buffers and direct I/O.
- Async I/O is `Task`-returning with `ConfigureAwait(false)` (see `Writer.WriteToAsync`). A library must not capture the synchronization context.
- Use `CancellationToken cancellationToken = default` as the **last** parameter on async public methods.

## Guard clauses

- Use the `Expect.*` helpers in `Internal/Expect.cs` (`Expect.Range`, `Expect.NotNull`, `Expect.Utc`, `Expect.MinLength`, ...). They are `AggressiveInlining` + `DebuggerStepThrough` and capture the argument name automatically via `[CallerArgumentExpression]`.
- Do not write hand-rolled `if (x == null) throw new ArgumentNullException(...)` boilerplate. Use `Expect.NotNull(x)`.
- Validate at the public boundary, not at every internal frame. Internal callers are trusted; `Debug.Assert` is acceptable for internal pre-/post-conditions on hot paths.
- `CA1062` (ValidateArgumentsOfPublicMethods) is off because `Expect.*` covers the cases that matter. Do not lean on this to skip validation that protects format correctness.

## Documentation and comments

- `GenerateDocumentationFile=true` is set, but `CS1591` and `CS1573` are off - XML docs are not mandatory. Write them on **public** API where the name alone doesn't carry the meaning (a `Writer` ctor with a `rows` parameter benefits from one; a `WriteTo(Span<byte>)` does not).
- Default to no inline comments. Only add one when the *why* is non-obvious: a hidden constraint, an invariant, a workaround, or a performance trade-off (good example: the lookup-table-vs-compute comment in `Block.CountLeadingZeros32`).
- Don't restate what the code does. Don't reference the current ticket, PR, or caller.

## Tests

- xUnit v3 (see `Directory.Packages.props`). One test class per production type or feature area.
- Test method names use **snake_case** describing the use case (`Write_and_read_dto_collection`, `Read_returns_expected_count_for_int32_column`). Avoid the `condition_what_result` pattern - it produces long names that don't display well. The body of the test documents the conditions.
- `[Theory]` over `[Fact]` when the same logic is exercised with different inputs. Inline data with `[InlineData]`; use a `MemberData` source only when the data is large or shared.
- Round-trip tests should write then read through the public `Writer`/`ReadBuilder` pipeline, not just exercise a single internal type, to catch wiring bugs (`ReferentialDataTests`, `BitsTests` are good examples).
- Integration tests live in the same test project; if they grow large enough to warrant a separate project, name it `Easy.TimeSeries.IntegrationTests` (already provisioned in `InternalsVisibleTo`).
- Use `BufferUtil` and similar small test helpers rather than recreating buffer setup inline in each test.

## Extension methods

- Use sparingly. Reach for an extension method only when extending a type you don't own, or when you genuinely need to keep a public API small.
- Centralize them. Each project gets at most one `Extensions.cs` at the root namespace of that project. Don't organize them by the type being extended; don't scatter them across folders.
- Extensions live in the root namespace of the project (no separate `*.Extensions` namespace). They should be in scope through the existing `using` directives that already pull in the project.
- Extension methods with a single call site (tests excluded) are a smell - prefer a local function or a private/internal static method.

## Commits and branches

- Conventional Commits, lowercase header (`feat:`, `fix:`, `refactor:`, `test:`, `doc:`, `perf:`, `chore:`, `ci:`, `build:`, `revert:`). The CI workflow uses commit prefixes to drive semantic version bumps.
- Header is imperative, max 89 chars, no trailing period. Body explains *why*, not *what*.
- One logical change per commit. Tests ship in the same commit as the feature or fix they cover.
- Branch names use prefixes: `feature/<topic>`, `fix/<topic>`. No personal or opaque names.

## What we don't use

- No mediator pattern (e.g. MediatR).
- No "provider", "service", "manager", or "helper" classes that exist only to forward calls.
- No reflection on hot paths in shipping code. `ReflectionBasedHydrator` exists as a correctness baseline; the source-generator project is meant to replace it.
- No `dynamic`, no runtime code emit (`Reflection.Emit`). Expression-tree-compiled delegates are acceptable when the result is cached.
- No `DateTime.Now` or `DateTime.UtcNow` (banned). Use `TimeProvider`.
- No third-party DI container in the core library; consumer apps wire things up themselves.
