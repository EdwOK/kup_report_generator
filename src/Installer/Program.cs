using Helpers;
using Helpers.Releases;
using Helpers.TaskProgress;
using Installer;
using Serilog;
using Spectre.Console;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File("logs.txt",
        rollingInterval: RollingInterval.Day,
        rollOnFileSizeLimit: true)
    .CreateLogger();

try
{
    using var cts = new CancellationTokenSource();

    var cancellationToken = cts.Token;
    Console.CancelKeyPress += (_, e) =>
    {
        cts?.Cancel();
        e.Cancel = true;
    };

    AnsiConsole.Write(new FigletText("AppInstaller").Centered().Color(Color.Green1));
    AnsiConsole.MarkupLine($"Started v[blue]{AppHelper.AppVersion}[/], Press [green]Ctrl-C[/] to stop.");

    var install = await AnsiConsole.Progress()
        .AutoClear(false)
        .Columns(
            new TaskDescriptionColumn(),
            new ProgressBarColumn(),
            new PercentageColumn(),
            new RemainingTimeColumn())
        .StartAsync(async progressContext =>
            await Install(new SpectreConsoleProgressContext(progressContext), cancellationToken));

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

async Task<Result> Install(IProgressContext progressContext, CancellationToken cancellationToken)
{
    var repository = new GithubRepository(AppConstants.RepositoryOwner, AppConstants.RepositoryName);

    var releases = await repository.GetReleases(cancellationToken);
    if (releases.IsFailed)
    {
        return releases.ToResult();
    }

    var releaseVersions = releases.Value.Select(r => r.Version).ToArray();

    var version = await new SelectionPrompt<string>()
        .Title("What's [green]release version[/] would you like to update to?")
        .MoreChoicesText("[grey](Move up and down to reveal more release versions)[/]")
        .AddChoices(releaseVersions)
        .ShowAsync(AnsiConsole.Console, cancellationToken);

    var release = releases.Value.FirstOrDefault(r => r.Version == version);
    if (release is null)
    {
        return Result.Fail($"Release for version {version} not found.");
    }

    var install = await AppInstaller.Install(release, AppHelper.CurrentOsPlatform, cancellationToken);
    if (install.IsFailed)
    {
        return install;
    }

    AnsiConsole.MarkupLine($"[green]Done.[/] Version [greenyellow]{release.Version}[/] has been successfully installed.");
    return Result.Ok();
}