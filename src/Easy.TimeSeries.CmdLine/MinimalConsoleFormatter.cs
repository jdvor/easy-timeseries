namespace Easy.TimeSeries.CmdLine;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

internal sealed class MinimalConsoleFormatter : ConsoleFormatter
{
    private const string Default = "\e[39m\e[22m";
    private const string Gray = "\e[37m";
    private const string Green = "\e[32m";
    private const string Yellow = "\e[33m";
    private const string Red = "\e[31m";

    private readonly bool useColor;

    public MinimalConsoleFormatter()
        : base("minimal")
    {
        try
        {
            var v = Environment.GetEnvironmentVariable("NO_COLOR");
            var noColor = string.Equals(v, "1", StringComparison.OrdinalIgnoreCase)
                          || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
            useColor = !noColor;
        }
        catch
        {
            useColor = true;
        }
    }

    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        if (IsSslClientDisconnect(logEntry))
        {
            return;
        }

        var timestamp = TimeProvider.System.GetLocalNow().ToString("HH:mm:ss.fff");
        textWriter.Write($"[{timestamp}] ");

        if (useColor)
        {
            var color = GetColor(logEntry.LogLevel);
            textWriter.Write(color);
        }

        var logLevelString = GetLogLevelString(logEntry.LogLevel);
        textWriter.Write(logLevelString);

        if (useColor)
        {
            textWriter.Write(Default);
        }

        textWriter.Write(" ");

        var message = logEntry.Formatter(logEntry.State, logEntry.Exception);
        textWriter.WriteLine(message);

        if (logEntry.Exception != null)
        {
            textWriter.WriteLine(logEntry.Exception.ToString());
        }
    }

    /// <summary>
    /// Filter out benign SSL IOException caused by abrupt client disconnects on macOS.
    /// macOS returns EFAULT (errno 14) from the TLS stack instead of a clean connection-reset signal,
    /// which Kestrel logs as an unhandled Error. The connection is already dead, so there is nothing to act on.
    /// </summary>
    private static bool IsSslClientDisconnect<TState>(LogEntry<TState> logEntry)
    {
        return logEntry.Exception is System.IO.IOException { InnerException: System.ComponentModel.Win32Exception { NativeErrorCode: 14 } };
    }

    private static string GetLogLevelString(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => "TRCE",
            LogLevel.Debug => "DBUG",
            LogLevel.Information => "INFO",
            LogLevel.Warning => "WARN",
            LogLevel.Error => "FAIL",
            LogLevel.Critical => "CRIT",
            _ => "UNKN",
        };
    }

    private static string GetColor(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => Gray,
            LogLevel.Debug => Gray,
            LogLevel.Information => Green,
            LogLevel.Warning => Yellow,
            LogLevel.Error => Red,
            LogLevel.Critical => Red,
            _ => Default,
        };
    }
}
