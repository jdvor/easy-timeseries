namespace Easy.TimeSeries;

using Easy.TimeSeries.Abstractions;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Reflection;

internal static class TypeCache
{
    private static readonly ConcurrentDictionary<Type, ImmutableDictionary<int, ColumnBinding>> Cache = new();

    public  static ImmutableDictionary<int, ColumnBinding> Get<T>()
        where T : class, new()
        => Cache.GetOrAdd(typeof(T), _ => Create(typeof(T)));

    static ImmutableDictionary<int, ColumnBinding> Create(Type type)
    {
        const BindingFlags getAndSetPublicProps = BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty | BindingFlags.SetProperty;
        var props = type.GetProperties(getAndSetPublicProps);
        var result = new Dictionary<int, ColumnBinding>(props.Length);
        foreach (var prop in props)
        {
            var attr = prop.GetCustomAttribute<ColumnAttribute>();
            if (attr is null)
            {
                continue;
            }

            var (columnValueType, nullable) = ToColumnValueType(attr.ValueType, prop.PropertyType);
            if (columnValueType == ColumnValueType.None)
            {
                continue;
            }

            var binding = new ColumnBinding
            {
                Index = attr.Index,
                Label = string.IsNullOrEmpty(attr.Label) ? prop.Name : attr.Label,
                ColumnValueType = columnValueType,
                IsNullable = nullable,
                Setter = BuildSetter(type, prop),
            };
            result.Add(binding.Index, binding);
        }

        return result.ToImmutableDictionary();
    }

    private static Action<object, object?> BuildSetter(Type declaringType, PropertyInfo prop)
    {
        // (object instance, object? value) => ((TDeclaring)instance).set_Prop((TProp)value)
        var instanceParam = Expression.Parameter(typeof(object), "instance");
        var valueParam = Expression.Parameter(typeof(object), "value");
        var call = Expression.Call(
            Expression.Convert(instanceParam, declaringType),
            prop.SetMethod!,
            Expression.Convert(valueParam, prop.PropertyType));
        return Expression.Lambda<Action<object, object?>>(call, instanceParam, valueParam).Compile();
    }

    static (ColumnValueType cvt, bool nullable) ToColumnValueType(DataValueType attrType, Type propType)
    {
        return attrType switch
        {
            DataValueType.DateTimeOrdered => (ColumnValueType.DateTimeOrdered, IsNullable(propType)),
            DataValueType.TimeSpan => (ColumnValueType.TimeSpan, IsNullable(propType)),
            DataValueType.Float => (ColumnValueType.Float, IsNullable(propType)),
            DataValueType.Double => (ColumnValueType.Double, IsNullable(propType)),
            DataValueType.Decimal => (ColumnValueType.Decimal, IsNullable(propType)),
            DataValueType.Int32 => (ColumnValueType.Int32, IsNullable(propType)),
            DataValueType.Int64 => (ColumnValueType.Int64, IsNullable(propType)),
            DataValueType.Bool => (ColumnValueType.Bool, IsNullable(propType)),
            DataValueType.ScaledNumber32 => (ColumnValueType.ScaledNumber32, IsNullable(propType)),
            DataValueType.ScaledNumber64 => (ColumnValueType.ScaledNumber64, IsNullable(propType)),
            DataValueType.Category => (ColumnValueType.Category, IsNullable(propType)),
            DataValueType.DateTimeUnordered => (ColumnValueType.DateTimeUnordered, IsNullable(propType)),
            _ => From(propType),
        };

        static (ColumnValueType cvt, bool nullable) From(Type type)
        {
            if (type == typeof(string))
            {
                return (ColumnValueType.Category, false);
            }

            if (type == typeof(int) || type == typeof(int?))
            {
                return (ColumnValueType.Int32, IsNullable(type));
            }

            if (type == typeof(long) || type == typeof(long?))
            {
                return (ColumnValueType.Int64, IsNullable(type));
            }

            if (type == typeof(double) || type == typeof(double?))
            {
                return (ColumnValueType.Double, IsNullable(type));
            }

            if (type == typeof(float) || type == typeof(float?))
            {
                return (ColumnValueType.Float, IsNullable(type));
            }

            if (type == typeof(decimal) || type == typeof(decimal?))
            {
                return (ColumnValueType.Decimal, IsNullable(type));
            }

            if (type == typeof(bool) || type == typeof(bool?))
            {
                return (ColumnValueType.Bool, IsNullable(type));
            }

            if (type == typeof(DateTime) || type == typeof(DateTime?))
            {
                return (ColumnValueType.DateTimeOrdered, IsNullable(type));
            }

            if (type == typeof(TimeSpan) || type == typeof(TimeSpan?))
            {
                return (ColumnValueType.TimeSpan, IsNullable(type));
            }

            return (ColumnValueType.None, false);
        }

        static bool IsNullable(Type type) => Nullable.GetUnderlyingType(type) != null;
    }

    internal sealed class ColumnBinding
    {
        public required int Index { get; init; }

        public required string Label { get; init; }

        public required bool IsNullable { get; init; }

        public required ColumnValueType ColumnValueType { get; init; }

        public required Action<object, object?> Setter { get; init; }

        public override string ToString() => Label;
    }
}
