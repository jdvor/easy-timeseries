namespace Easy.Sample;

using Easy.TimeSeries.Abstractions;

/// <summary>
/// Handwritten draft kept as a reference implementation for comparison with the source-generated
/// <c>PowerNodeMaterializer</c>. Not used by the sample.
/// </summary>
internal sealed class HandwrittenPowerNodeMaterializer : IMaterializer<PowerNode>
{
    private PowerNode[]? rows;
    private int currentColumn;
    private int currentRow;

    public bool BeginColumn(int column, int rowCount)
    {
        if (column is >= 0 and <= 10)
        {
            EnsureRowsOfSufficientSize(rowCount);
            currentColumn = column;
            currentRow = 0;
            return true;
        }

        return false;
    }

    public void Hydrate<TPropValue>(TPropValue value)
        where TPropValue : struct
    {
        var instance = rows![currentRow++];
        if (value is float f)
        {
            switch (currentColumn)
            {
                case 1: // <- column index
                    instance.CapacityMw = f;
                    break;

                case 2: // <- column index
                    instance.Latitude = f;
                    break;

                case 3: // <- column index
                    instance.Longitude = f;
                    break;
            }
        }
        else if (value is bool b)
        {
            switch (currentColumn)
            {
                case 5: // <- column index
                    instance.IsEnabled = b;
                    break;
            }
        }
        else if (value is decimal d)
        {
            switch (currentColumn)
            {
                case 7: // <- column index
                    instance.StartPrice = d;
                    break;
            }
        }
        else if (value is long l)
        {
            switch (currentColumn)
            {
                case 8: // <- column index
                    instance.PowerNodeId = l;
                    break;
            }
        }
        else if (value is TimeSpan ts)
        {
            switch (currentColumn)
            {
                case 6: // <- column index
                    instance.StartDuration = ts;
                    break;
            }
        }
        else if (value is DateTime dt)
        {
            switch (currentColumn)
            {
                case 9: // <- column index
                    instance.MeasurementTimeUtc = dt;
                    break;

                case 10: // <- column index
                    instance.CertifiedTimeUtc = dt;
                    break;
            }
        }
        else
        {
            throw TimeSeriesException.UnsupportedHydration(currentColumn, typeof(PowerNode), typeof(TPropValue));
        }
    }

    public void Hydrate(string value)
    {
        var instance = rows![currentRow++];
        switch (currentColumn)
        {
            case 0: // <- column index
                instance.CountryCode = value;
                break;

            case 4: // <- column index
                instance.PrimaryFuel = value;
                break;
        }
    }

    public PowerNode[] GetResult()
    {
        return rows ?? [];
    }

    private void EnsureRowsOfSufficientSize(int rowCount)
    {
        if (rows is null)
        {
            rows = new PowerNode[rowCount];
            for (var i = 0; i < rowCount; i++)
            {
                rows[i] = new PowerNode();
            }
        }
        else if (rowCount > rows.Length)
        {
            var resizedRows = new PowerNode[rowCount];
            Array.Copy(rows, resizedRows, rows.Length);
            for (var i = rows.Length; i < rowCount; i++)
            {
                resizedRows[i] = new PowerNode();
            }

            rows = resizedRows;
        }
    }
}
