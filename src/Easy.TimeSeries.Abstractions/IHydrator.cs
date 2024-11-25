namespace Easy.TimeSeries.Abstractions;

public interface IHydrator<out T>
    where T : class, new()
{
    void Hydrate<TPropValue>(int column, int row, TPropValue value)
        where TPropValue : struct;

    void Hydrate(int column, int row, string value);

    T[] GetResult();
}
