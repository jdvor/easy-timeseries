namespace Easy.TimeSeries.Abstractions;

public interface IMaterializer<out T>
    where T : class, new()
{
    bool BeginColumn(int column, int rowCount);

    void Hydrate<TPropValue>(TPropValue value)
        where TPropValue : struct;

    void Hydrate(string value);

    T[] GetResult();
}
