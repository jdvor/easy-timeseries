namespace Easy.TimeSeries;

/// <summary>
/// File format version stamped into the header. Lets readers reject buffers they do not understand and allows the
/// format to evolve. Only <see cref="V1"/> is currently supported.
/// </summary>
public enum Version : byte
{
    /// <summary>Unset / invalid version.</summary>
    None = 0,

    /// <summary>Version 1 - the only format currently produced and accepted.</summary>
    V1,
}
