namespace Easy.TimeSeries.CmdLine;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System.Diagnostics.CodeAnalysis;

internal static class Setup
{
    public static void ConfigureConfiguration(IConfigurationBuilder builder, string[] args)
    {
        builder
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables("TS_")
            .AddCommandLine(args);
    }

    [SuppressMessage("Trimming", "IL2026", Justification = "application will not be trimmed")]
    public static void ConfigureLogging(ILoggingBuilder builder)
    {
        builder.ClearProviders();
        builder.SetMinimumLevel(LogLevel.Information);
        foreach (var categoryName in IgnoreLogCategoryNames)
        {
            builder.AddFilter(categoryName, LogLevel.Error);
        }

        builder.AddConsoleFormatter<MinimalConsoleFormatter, ConsoleFormatterOptions>();
        builder.AddConsole(options => options.FormatterName = "minimal");
    }

    public static readonly string[] IgnoreLogCategoryNames =
    [
        "Microsoft.AspNetCore.StaticFiles",
        "Microsoft.AspNetCore.Mvc",
        "Microsoft.AspNetCore.Routing.EndpointMiddleware",
        "Microsoft.AspNetCore.Routing.EndpointRoutingMiddleware",
        "Microsoft.AspNetCore.Routing.Matching.DfaMatcher",
        "Microsoft.AspNetCore.Hosting.Diagnostics",
        "Microsoft.AspNetCore.Cors.Infrastructure.CorsService",
        "Microsoft.AspNetCore.Server.Kestrel",
        "Microsoft.AspNetCore.Server.Kestrel.Connections",
        "Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets",
    ];
}
