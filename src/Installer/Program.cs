using FluentResults;
using Installer;
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

    AnsiConsole.Write(new FigletText("AppInstaller").Centered().Color(Color.Green1));
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

async Task<Result> Install(CancellationToken cancellationToken)
{
    var repository = new GithubRepository(Constants.RepositoryOwner, Constants.Repository);
    var releases = await repository.GetReleases(cancellationToken);

    var version =
        await new SelectionPrompt<string>()
            .Title("What's [green]release version[/] would you like to update to?")
            .PageSize(15)
            .MoreChoicesText("[grey](Move up and down to reveal more release versions)[/]")
            .AddChoices(releases.Select(r => r.Version))
            .ShowAsync(AnsiConsole.Console, cancellationToken);

    var release = releases.FirstOrDefault(r => r.Version == version);
    if (release is null)
    {
        return Result.Fail($"Release for version {version} not found.");
    }

    AnsiConsole.MarkupLine("[green]Installing...[/]");

    var appInstaller = new KUPReportGenerator.Installer.AppInstaller();
    var install = await appInstaller.Install(release!, Constants.CurrentOSPlatform, cancellationToken);
    if (install.IsFailed)
    {
        return install;
    }

    AnsiConsole.MarkupLine($"[green]Done.[/] Version [greenyellow]{release.Version}[/] has been successfully installed.");
    return Result.Ok();
}