namespace Easy.TimeSeries.TestData;

using System.Globalization;

public static class Data
{
    private const string DataDirEnvVarName = "TS_TESTS_DATA";

    public static readonly Lazy<DirectoryInfo> DataDir = new(FindDataDir);

    private static string FindSolutionDirPath()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (IsSolutionDir(dir))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        return string.Empty;

        static bool IsSolutionDir(string dir)
        {
            return Directory.EnumerateFiles(dir, "*.sln", SearchOption.TopDirectoryOnly).Any()
                   || Directory.EnumerateFiles(dir, "*.slnx", SearchOption.TopDirectoryOnly).Any();
        }
    }

    private static DirectoryInfo FindDataDir()
    {
        var envSet = Environment.GetEnvironmentVariable(DataDirEnvVarName);
        if (!string.IsNullOrEmpty(envSet) && Directory.Exists(envSet))
        {
            return new DirectoryInfo(envSet);
        }


        var solutionDir = FindSolutionDirPath();
        var path = Path.GetFullPath(Path.Combine(solutionDir, "tests", "data"));
        var dir = new DirectoryInfo(path);
        if (!dir.Exists)
        {
            throw new InvalidOperationException("Where is solution/tests/data directory?");
        }

        return dir;
    }

    private static List<T> ReadDataFile<T>(
        string fileName,
        Func<string, T?> lineParser,
        bool hasHeaders = true,
        int capacity = 1000)
        where T : class
    {
        var result = new List<T>(capacity);
        var path = Path.Combine(DataDir.Value.FullName, fileName);
        using var reader = new StreamReader(path);
        var first = true;
        while (reader.ReadLine() is { } line)
        {
            if (hasHeaders && first)
            {
                // headers
                first = false;
                continue;
            }

            var item = lineParser(line);
            if (item is null)
            {
                continue;
            }

            result.Add(item);
        }

        reader.Close();

        return result;
    }

    private static long GetDataFileSize(string fileName)
    {
        var path = Path.Combine(DataDir.Value.FullName, fileName);
        return new FileInfo(path).Length;
    }

    public static List<PowerPlant> GetPowerPlants()
    {
        return ReadDataFile(
            fileName: "power_plants.csv",
            lineParser: line =>
            {
                var parts = line.Split([','], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 5)
                {
                    return null;
                }

                var capacity = float.Parse(parts[1], CultureInfo.InvariantCulture);
                var latitude = float.Parse(parts[2], CultureInfo.InvariantCulture);
                var longitude = float.Parse(parts[3], CultureInfo.InvariantCulture);
                return new PowerPlant(parts[0].Trim(), capacity, latitude, longitude, parts[4].Trim());
            },
            capacity: 35_000);
    }

    public static long GetPowerPlantsCsvFileSize() => GetDataFileSize("power_plants.csv");

    public static List<Boeing> GetBoeing()
    {
        return ReadDataFile(
            fileName: "Boeing.csv",
            lineParser: line =>
            {
                var parts = line.Split([','], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 9)
                {
                    return null;
                }

                var time = DateTime.ParseExact(parts[8].Trim(), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                time = DateTime.SpecifyKind(time, DateTimeKind.Utc);

                return new Boeing
                {
                    Year = int.Parse(parts[0], CultureInfo.InvariantCulture),
                    Month = int.Parse(parts[1], CultureInfo.InvariantCulture),
                    Day = int.Parse(parts[2], CultureInfo.InvariantCulture),
                    Hour = int.Parse(parts[3], CultureInfo.InvariantCulture),
                    Minute = int.Parse(parts[4], CultureInfo.InvariantCulture),
                    Second = int.Parse(parts[5], CultureInfo.InvariantCulture),
                    Price = float.Parse(parts[6], CultureInfo.InvariantCulture),
                    Volume = int.Parse(parts[7], CultureInfo.InvariantCulture),
                    Time = time,
                };
            },
            capacity: 225_000);
    }

    public static long GetBoeingCsvFileSize() => GetDataFileSize("Boeing.csv");

    public static List<Macro4> GetMacro4()
    {
        return ReadDataFile(
            fileName: "Macro4Series.csv",
            lineParser: line =>
            {
                var parts = line.Split([','], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 4)
                {
                    return null;
                }

                return new Macro4
                {
                    rgnp = double.Parse(parts[0], CultureInfo.InvariantCulture),
                    tb3m = double.Parse(parts[1], CultureInfo.InvariantCulture),
                    lnm1 = double.Parse(parts[2], CultureInfo.InvariantCulture),
                    gs10 = double.Parse(parts[3], CultureInfo.InvariantCulture),
                };
            },
            capacity: 215);
    }

    public static long GetMacro4CsvFileSize() => GetDataFileSize("Macro4Series.csv");

    public static List<Gold> GetGold()
    {
        return ReadDataFile(
            fileName: "Gold.csv",
            lineParser: line =>
            {
                var parts = line.Split([','], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2)
                {
                    return null;
                }

                var date = DateOnly.Parse(parts[0].Trim(), CultureInfo.InvariantCulture);

                return new Gold
                {
                    Date = new DateTime(date, TimeOnly.MinValue, DateTimeKind.Utc),
                    Value = float.Parse(parts[1], CultureInfo.InvariantCulture),
                };
            },
            capacity: 5_528);
    }

    public static long GetGoldCsvFileSize() => GetDataFileSize("Gold.csv");

    public static List<Vix> GetVix()
    {
        return ReadDataFile(
            fileName: "d-vix0411.csv",
            lineParser: line =>
            {
                var parts = line.Split([','], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 5)
                {
                    return null;
                }

                var date = DateOnly.ParseExact(parts[0].Trim(), "M/d/yyyy", CultureInfo.InvariantCulture);

                return new Vix
                {
                    Date = new DateTime(date, TimeOnly.MinValue, DateTimeKind.Utc),
                    Open = float.Parse(parts[1], CultureInfo.InvariantCulture),
                    High = float.Parse(parts[2], CultureInfo.InvariantCulture),
                    Low = float.Parse(parts[3], CultureInfo.InvariantCulture),
                    Close = float.Parse(parts[4], CultureInfo.InvariantCulture),
                };
            },
            capacity: 1_989);
    }

    public static long GetVixCsvFileSize() => GetDataFileSize("d-vix0411.csv");
}
