namespace Easy.TimeSeries.Storage;

/// <summary>
/// Validation of logical storage paths, shared by the <see cref="Abstractions.IStorageCatalog"/> implementations.
/// </summary>
public static class StoragePath
{
    public const char Separator = '/';

    /// <summary>Whether <paramref name="path"/> is a valid logical path, see <see cref="Validate"/>.</summary>
    public static bool IsValid(string path)
    {
        try
        {
            Validate(path);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// Throws unless <paramref name="path"/> is a logical path: non-empty, separated by <c>/</c>, relative, without
    /// empty, <c>.</c> or <c>..</c> segments and without <c>\</c>.
    /// </summary>
    /// <param name="allowPrefix">Accept a listing prefix: empty, or ending with <c>/</c> or a partial segment.</param>
    public static void Validate(string path, bool allowPrefix = false, string paramName = "path")
    {
        ArgumentNullException.ThrowIfNull(path, paramName);
        if (path.Length == 0)
        {
            if (allowPrefix)
            {
                return;
            }

            throw new ArgumentException("Storage path must not be empty.", paramName);
        }

        if (path[0] == Separator || path.Contains('\\', StringComparison.Ordinal))
        {
            throw new ArgumentException($"'{path}' is not a logical storage path: use relative paths with '/'.", paramName);
        }

        var segments = path.Split(Separator);
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];

            // A prefix may end with '/' or with a partial segment.
            var isLast = i == segments.Length - 1;
            if ((segment.Length == 0 && !(allowPrefix && isLast)) || segment is "." or "..")
            {
                throw new ArgumentException($"'{path}' is not a logical storage path: invalid segment '{segment}'.", paramName);
            }
        }
    }
}
