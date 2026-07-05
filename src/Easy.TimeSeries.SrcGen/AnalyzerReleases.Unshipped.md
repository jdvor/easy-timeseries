; Unshipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
ETS001 | EasyTimeSeries | Error | Duplicate column index
ETS002 | EasyTimeSeries | Error | Column indexes must start at 0 and be contiguous
ETS003 | EasyTimeSeries | Error | Unsupported column property type
ETS004 | EasyTimeSeries | Error | Nullable column property (format has no null representation)
ETS005 | EasyTimeSeries | Error | Explicit ValueType incompatible with the property CLR type
ETS006 | EasyTimeSeries | Error | Scaled number column requires NumberPrecision DecimalPlaces1..5
ETS007 | EasyTimeSeries | Error | Column property accessor missing or not accessible
ETS008 | EasyTimeSeries | Error | Annotated type not usable for generation
ETS009 | EasyTimeSeries | Warning | Column attribute option has no effect and is ignored
ETS010 | EasyTimeSeries | Error | Annotated type has no [Column] properties
ETS011 | EasyTimeSeries | Error | Compilation does not reference Easy.TimeSeries
