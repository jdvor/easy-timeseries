namespace Easy.TimeSeries.Aggregation;

/// <summary>
/// Binds a spec to raw file headers, one file at a time. The first file fixes the decoded CLR type of every input and
/// creates the concrete functions; every later file is validated against them. Inherited output encodings are
/// resolved after the last file ("finest wins"), because only then are all raw encodings known.
/// </summary>
internal sealed class SpecBinding
{
    private readonly AggregationSpec spec;
    private readonly List<Slot> slots = [];
    private readonly List<BoundFunction> functions = [];
    private string? timeLabel;
    private OutputEncoding[]? outputs;

    public SpecBinding(AggregationSpec spec)
    {
        this.spec = spec;
    }

    /// <summary>Raw time column label; the aggregated time column keeps it.</summary>
    public string TimeLabel => timeLabel ?? throw NotBound();

    /// <summary>Distinct raw input columns, each decoded once per chunk.</summary>
    public IReadOnlyList<Slot> Slots => slots;

    /// <summary>Functions in output order, with the slots of their inputs.</summary>
    public IReadOnlyList<BoundFunction> Functions => functions;

    /// <summary>Precision of the aggregated time column: the coarsest one that stores slice starts exactly.</summary>
    public TimePrecision TimePrecision
        => spec.AggregationTimeSpan == AggregationTimeSpan.Day ? TimePrecision.Days : TimePrecision.Seconds;

    /// <summary>Resolved output encoding per function; available after <see cref="ResolveOutputs"/>.</summary>
    public IReadOnlyList<OutputEncoding> Outputs => outputs ?? throw NotBound();

    /// <summary>Matches the spec against one raw header and records its encodings.</summary>
    /// <exception cref="AggregationException">The header does not match the spec.</exception>
    public FileBinding Bind(Header header, string? rawName)
    {
        var columns = new HeaderColumns(header, rawName);
        if (timeLabel is null)
        {
            Establish(columns);
        }

        var time = FindTime(columns);
        if (!string.Equals(columns.Label(time), timeLabel, StringComparison.Ordinal))
        {
            throw new AggregationException(
                $"Time column is '{columns.Label(time)}' here but '{timeLabel}' in other raw files; name it with Time(...).",
                rawName,
                column: columns.Label(time));
        }

        var group = spec.GroupColumn is null ? -1 : FindGroup(columns);
        var slotColumns = new int[slots.Count];
        for (var i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            var index = slot.Find(columns);
            var info = header.Columns[index];
            var type = ClrTypes.Of(info.ValueType);
            if (type != slot.Type)
            {
                throw new AggregationException(
                    $"Column '{slot}' decodes to {type.Name} here but {slot.Type.Name} is expected.",
                    rawName,
                    column: slot.ToString());
            }

            slot.Seen.Add((info.ValueType, info.Meta));
            slotColumns[i] = index;
        }

        outputs = null;
        return new FileBinding(time, group, slotColumns);
    }

    /// <summary>Resolves inherited encodings over every raw file bound so far and checks result compatibility.</summary>
    public IReadOnlyList<OutputEncoding> ResolveOutputs()
    {
        if (timeLabel is null)
        {
            throw NotBound();
        }

        var resolved = new OutputEncoding[functions.Count];
        for (var i = 0; i < functions.Count; i++)
        {
            var f = functions[i];
            var encoding = f.Function.Output.IsInherited ? Inherit(f) : f.Function.Output;
            if (!ClrTypes.IsCompatible(f.Function.ResultType, encoding.ValueType))
            {
                throw new AggregationException(
                    $"Function '{f.Function.Name}' returns {f.Function.ResultType.Name}, which cannot be stored as {encoding}.",
                    function: f.Function.Name);
            }

            resolved[i] = encoding;
        }

        outputs = resolved;
        return resolved;
    }

    /// <summary>The aggregated layout: time, group, count, functions.</summary>
    public IReadOnlyList<AggregatedColumn> Describe()
    {
        var encodings = outputs ?? ResolveOutputs();
        var result = new List<AggregatedColumn>(functions.Count + 3)
        {
            new(0, TimeLabel, ColumnValueType.DateTimeOrdered, (int)TimePrecision, typeof(DateTime)),
        };

        if (spec.GroupColumn is { } group)
        {
            result.Add(new(result.Count, group, ColumnValueType.Category, 0, typeof(string)));
        }

        result.Add(new(result.Count, spec.CountColumn, ColumnValueType.Int32, 0, typeof(int)));
        for (var i = 0; i < functions.Count; i++)
        {
            var f = functions[i].Function;
            result.Add(new(result.Count, f.Name, encodings[i].ValueType, encodings[i].Meta, f.ResultType));
        }

        return result;
    }

    private static AggregationException NotBound() => new("The spec has not been bound to any raw header yet.");

    private static bool IsScaled(ColumnValueType t) => t is ColumnValueType.ScaledNumber32 or ColumnValueType.ScaledNumber64;

    private static bool IsTime(ColumnValueType t)
        => t is ColumnValueType.DateTimeOrdered or ColumnValueType.DateTimeUnordered or ColumnValueType.TimeSpan;

    // Min, Max and Last of an ordered time column can decrease, so the ordered encoding is never an output.
    private static ColumnValueType Normalize(ColumnValueType t)
        => t == ColumnValueType.DateTimeOrdered ? ColumnValueType.DateTimeUnordered : t;

    /// <summary>"Finest wins" over every raw encoding of the function's single input.</summary>
    private OutputEncoding Inherit(BoundFunction f)
    {
        var seen = slots[f.InputSlots[0]].Seen;
        var kinds = seen.Select(x => Normalize(x.Type)).Distinct().ToArray();
        var timePrecision = (TimePrecision)seen.Where(x => IsTime(x.Type)).Select(x => x.Meta).DefaultIfEmpty(0).Min();
        var resultType = f.Function.ResultType;
        if (kinds.Length == 1)
        {
            var kind = kinds[0];
            var meta = IsScaled(kind) ? seen.Max(x => x.Meta) : IsTime(kind) ? (int)timePrecision : 0;
            if (f.Function.Output.IsSameAsInput || IsScaled(kind) || (kind == ColumnValueType.Decimal && resultType == typeof(decimal)))
            {
                return OutputEncoding.Of(kind, meta);
            }
        }

        return ClrTypes.Lossless(resultType, timePrecision);
    }

    /// <summary>First raw file: fixes the time label, creates slots and the concrete functions.</summary>
    private void Establish(HeaderColumns columns)
    {
        var time = FindTime(columns);
        var label = columns.Label(time);
        foreach (var entry in spec.Entries)
        {
            if (entry is ColumnEntry c)
            {
                var index = columns.Find(c.Label);
                var type = ClrTypes.Of(columns.Header.Columns[index].ValueType);
                var slot = GetOrAddSlot(new Slot(c.Label, null, type), columns, function: null);
                foreach (var descriptor in c.Descriptors)
                {
                    functions.Add(new BoundFunction(descriptor.Bind(c.Label, type), [slot]));
                }

                continue;
            }

            var function = ((FunctionEntry)entry).Function;
            var inputSlots = new int[function.Inputs.Count];
            for (var i = 0; i < inputSlots.Length; i++)
            {
                var input = function.Inputs[i];
                var candidate = input.Label is { } inputLabel
                    ? new Slot(inputLabel, null, input.ValueType)
                    : new Slot(columns.LabelAt(input.ColumnIndex!.Value, function.Name), input.ColumnIndex, input.ValueType);
                inputSlots[i] = GetOrAddSlot(candidate, columns, function.Name);
            }

            functions.Add(new BoundFunction(function, inputSlots));
        }

        ValidateOutputLabels(label, columns.RawName);
        timeLabel = label;
    }

    private int GetOrAddSlot(Slot candidate, HeaderColumns columns, string? function)
    {
        for (var i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot.Index == candidate.Index && string.Equals(slot.Label, candidate.Label, StringComparison.Ordinal))
            {
                return slot.Type == candidate.Type
                    ? i
                    : throw new AggregationException(
                        $"Column '{slot}' is read as {candidate.Type.Name} by '{function}' but as {slot.Type.Name} elsewhere in the spec.",
                        columns.RawName,
                        column: slot.ToString(),
                        function: function);
            }
        }

        slots.Add(candidate);
        return slots.Count - 1;
    }

    private void ValidateOutputLabels(string time, string? rawName)
    {
        var labels = new HashSet<string>(StringComparer.Ordinal) { time };
        Add(spec.GroupColumn);
        Add(spec.CountColumn);
        foreach (var f in functions)
        {
            if (f.Function.Name.Length > ColumnInfo.MaxLabelLength)
            {
                throw new AggregationException(
                    $"Label '{f.Function.Name}' exceeds {ColumnInfo.MaxLabelLength} characters; set a shorter one with As(...).",
                    rawName,
                    function: f.Function.Name);
            }

            Add(f.Function.Name);
        }

        void Add(string? label)
        {
            if (label is not null && !labels.Add(label))
            {
                throw new AggregationException(
                    $"Aggregated label '{label}' is used twice; set an explicit label with As(...) or CountLabel(...).",
                    rawName,
                    column: label);
            }
        }
    }

    private int FindTime(HeaderColumns columns)
    {
        if (spec.TimeColumn is { } label)
        {
            var index = columns.Find(label);
            return IsDateTime(columns.Header.Columns[index].ValueType)
                ? index
                : throw new AggregationException($"Time column '{label}' is not a DateTime column.", columns.RawName, column: label);
        }

        var candidates = columns.Header.Columns.Where(x => IsDateTime(x.ValueType)).ToArray();
        return candidates.Length == 1
            ? candidates[0].Index
            : throw new AggregationException(
                $"The raw file has {candidates.Length} DateTime columns; name the time column with Time(...).",
                columns.RawName);
    }

    private int FindGroup(HeaderColumns columns)
    {
        var label = spec.GroupColumn!;
        var index = columns.Find(label);
        return columns.Header.Columns[index].ValueType == ColumnValueType.Category
            ? index
            : throw new AggregationException($"Group column '{label}' is not a Category column.", columns.RawName, column: label);
    }

    private static bool IsDateTime(ColumnValueType t)
        => t is ColumnValueType.DateTimeOrdered or ColumnValueType.DateTimeUnordered;

    /// <summary>One raw column decoded per chunk, matched by label or by index.</summary>
    internal sealed class Slot(string label, int? index, Type type)
    {
        public string Label { get; } = label;

        public int? Index { get; } = index;

        public Type Type { get; } = type;

        /// <summary>Distinct raw encodings seen across the bound files.</summary>
        public HashSet<(ColumnValueType Type, int Meta)> Seen { get; } = [];

        public int Find(HeaderColumns columns)
        {
            if (Index is not { } index)
            {
                return columns.Find(Label);
            }

            var label = columns.LabelAt(index, function: null);
            return string.Equals(label, Label, StringComparison.Ordinal)
                ? index
                : throw new AggregationException(
                    $"Column #{index} is '{label}' here but '{Label}' in other raw files.",
                    columns.RawName,
                    column: ToString());
        }

        public override string ToString() => Index is { } i ? $"#{i} ({Label})" : Label;
    }

    /// <summary>Label lookup over one header; duplicate labels are ambiguous.</summary>
    internal sealed class HeaderColumns
    {
        private readonly Dictionary<string, int> byLabel = new(StringComparer.Ordinal);

        public HeaderColumns(Header header, string? rawName)
        {
            Header = header;
            RawName = rawName;
            foreach (var column in header.Columns)
            {
                byLabel[column.Label] = byLabel.ContainsKey(column.Label) ? -1 : column.Index;
            }
        }

        public Header Header { get; }

        public string? RawName { get; }

        public string Label(int index) => Header.Columns[index].Label;

        public int Find(string label)
            => byLabel.TryGetValue(label, out var index)
                ? index >= 0
                    ? index
                    : throw new AggregationException($"Column label '{label}' is not unique in the raw file.", RawName, column: label)
                : throw new AggregationException($"Column '{label}' is missing in the raw file.", RawName, column: label);

        public string LabelAt(int index, string? function)
            => index < Header.Columns.Length
                ? Header.Columns[index].Label
                : throw new AggregationException(
                    $"Column #{index} does not exist; the raw file has {Header.Columns.Length} columns.",
                    RawName,
                    column: $"#{index}",
                    function: function);
    }
}

/// <summary>A function with the slot index of each of its inputs.</summary>
internal sealed record BoundFunction(IAggregationFunction Function, int[] InputSlots);

/// <summary>Where the spec's columns are in one raw file.</summary>
/// <param name="TimeColumn">Raw index of the time column.</param>
/// <param name="GroupColumn">Raw index of the group column, or -1.</param>
/// <param name="SlotColumns">Raw index per slot.</param>
internal sealed record FileBinding(int TimeColumn, int GroupColumn, int[] SlotColumns)
{
    /// <summary>Distinct raw columns to decode, for <c>ReadOptions.ColumnIndexes</c>.</summary>
    public int[] ReadColumns { get; } =
        [.. new[] { TimeColumn, GroupColumn }.Where(x => x >= 0).Concat(SlotColumns).Distinct().Order()];
}
