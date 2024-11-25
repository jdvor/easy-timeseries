namespace Easy.TimeSeries;

using Easy.TimeSeries.Abstractions;

public sealed class ReflectionBasedDeserializer<T> : IHydrator<T>
    where T : class, new()
{
    public ReflectionBasedDeserializer()
    {
    }

    public void Hydrate<TPropValue>(int column, int row, TPropValue value)
        where TPropValue : struct
    {
        throw new NotImplementedException();
    }

    public void Hydrate(int column, int row, string value)
    {
        throw new NotImplementedException();
    }

    public T[] GetResult()
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        // TODO release managed resources here
    }
}
