﻿using System.Runtime.InteropServices;
using FluentResults;
using KUPReportGenerator.Helpers;
using KUPReportGenerator.Installer;
using Serilog;
using Spectre.Console;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.File("logs.txt",
        rollingInterval: RollingInterval.Day,
        rollOnFileSizeLimit: true)
    .CreateLogger();

try
{
    using var cancellationTokenSource = new CancellationTokenSource();

    var cancellationToken = cancellationTokenSource.Token;
    Console.CancelKeyPress += (_, e) =>
    {
        cancellationTokenSource.Cancel();
        e.Cancel = true;
    };

    AnsiConsole.Write(new FigletText("Installer").Centered().Color(Color.Green1));
    AnsiConsole.MarkupLine("Started, Press [green]Ctrl-C[/] to stop.");

    var install = await Install(cancellationToken);
    if (ConsoleHelpers.HasErrors(install))
    {
        ConsoleHelpers.WriteErrors(install);
    }
}
catch (Exception exc)
{
    AnsiConsole.WriteException(exc);
    Log.Error(exc, exc.Message);
}
finally
{
    Log.CloseAndFlush();
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("Press [green]any[/] key to exit.");
    Console.ReadKey();
}

async Task<ResultBase> Install(CancellationToken cancellationToken)
{
    var installManager = new InstallManager();

    var releases = await installManager.GetReleases(cancellationToken);
    if (releases.IsFailed)
    {
        return releases;
    }

    var version =
        await new SelectionPrompt<string>()
            .Title("What's [green]release version[/] would you like to update to?")
            .PageSize(15)
            .MoreChoicesText("[grey](Move up and down to reveal more release versions)[/]")
            .AddChoices(releases.Value.Select(r => r.Version))
            .ShowAsync(AnsiConsole.Console, cancellationToken);

    var release = releases.Value.FirstOrDefault(r => r.Version == version);
    if (release is null)
    {
        return releases;
    }

    AnsiConsole.MarkupLine("[green]Installing...[/]");

    var install = await installManager.Install(release!, OSPlatform.Windows, cancellationToken);
    if (install.IsFailed)
    {
        return install;
    }

    AnsiConsole.MarkupLine($"[green]Done.[/] Version [greenyellow]{release.Version}[/] has been successfully installed.");
    return Result.Ok();
}