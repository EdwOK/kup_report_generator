using System.CommandLine;
using Helpers;
using Helpers.TaskProgress;
using KUPReportGenerator;
using KUPReportGenerator.Converters;
using KUPReportGenerator.Generators;
using KUPReportGenerator.GitCommitsHistory.DataProviders;
using KUPReportGenerator.Report;
using KUPReportGenerator.Utils;
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

    var rootCommand = CommandLine.CreateRootCommand(async fileInfo =>
    {
        AnsiConsole.Write(new FigletText("KUP Report Generator").Centered().Color(Color.Green1));
        AnsiConsole.MarkupLine($"Started v[blue]{AppHelper.AppVersion}[/], Press [blue]Ctrl-C[/] to stop.");

        var checkPrerequisites = await AppHelper.CheckPrerequisites(cancellationToken);
        if (checkPrerequisites.IsFailed)
        {
            ConsoleHelpers.WriteErrors(checkPrerequisites);
            return;
        }

        var newAppVersion = await AppHelper.CheckAppVersionForUpdate(AppHelper.AppVersion, cancellationToken);
        if (newAppVersion is not null)
        {
            AnsiConsole.MarkupLine(
                $"New v[greenyellow]{newAppVersion.Version}[/] of the tool is available, now you can use [blue]installer.exe[/] in the current folder to upgrade the tool to the new version.");
        }

        var action = await new SelectionPrompt<string>()
            .Title("What do you want [blue]to do[/]?")
            .MoreChoicesText("[grey](Move up and down to choose an action)[/]")
            .AddChoices(
                nameof(CommandLine.CommandLineActions.Run),
                nameof(CommandLine.CommandLineActions.Install))
            .ShowAsync(AnsiConsole.Console, cancellationToken);

        switch (action)
        {
            case nameof(CommandLine.CommandLineActions.Run):
                {
                    var result = await Run(fileInfo, cancellationToken);
                    if (result.IsSuccess)
                    {
                        if (FileHelper.AnyFiles(Constants.OutputDirectory))
                        {
                            AnsiConsole.MarkupLine("[blue]Done[/]. Reports are successfully generated.");
                            AnsiConsole.MarkupLine($"Open [blue]{Constants.OutputDirectory}[/] folder to check the reports.");
                            await ConsoleHelpers.OpenDirectory(Constants.OutputDirectory, cancellationToken);
                        }
                        else
                        {
                            AnsiConsole.MarkupLine("[blue]Done[/]. No reports were created.");
                        }
                    }
                    else
                    {
                        ConsoleHelpers.WriteErrors(result);
                    }

                    break;
                }
            case nameof(CommandLine.CommandLineActions.Install):
                {
                    var result = await Install(fileInfo, cancellationToken);
                    if (result.IsSuccess)
                    {
                        AnsiConsole.MarkupLine("[blue]Done[/]. Now you can run.");
                    }
                    else
                    {
                        ConsoleHelpers.WriteErrors(result);
                    }

                    break;
                }
        }
    });

    await rootCommand.InvokeAsync(args);
}
catch (Exception exc)
{
    AnsiConsole.WriteException(exc, ExceptionFormats.ShortenEverything);
    Log.Error(exc, exc.Message);
}
finally
{
    Log.CloseAndFlush();

    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("Press [blue]any[/] key to exit.");
    Console.ReadKey();
}

async Task<Result> Run(FileInfo fileInfo, CancellationToken cancellationToken)
{
    var reportSettings = await ReportSettings.OpenAsync(fileInfo.ToString(), cancellationToken);
    if (reportSettings.IsFailed)
    {
        return new Error("The tool was installed with errors.").CausedBy(reportSettings.Errors);
    }

    var reportSettingsValidator = new ReportSettingsValidator();
    var validationResult = await reportSettingsValidator.ValidateAsync(reportSettings.Value, cancellationToken);
    if (!validationResult.IsValid)
    {
        return new Error($"The tool was installed with validation errors in the setting file: {Constants.SettingsFilePath}.")
            .CausedBy(validationResult.Errors.Select(e => new Error(e.ErrorMessage)));
    }

    var workingMonth = DatetimeHelper.GetCurrentMonthName();

    var workingDaysCalculator = new WorkingDaysCalculator(reportSettings.Value.RapidApiKey);
    var workingDaysInMonth = await workingDaysCalculator.GetWorkingDaysInMonth(workingMonth, cancellationToken);
    if (workingDaysInMonth.IsFailed)
    {
        return workingDaysInMonth.ToResult();
    }

    var workingDays =
        await new TextPrompt<ushort>($"How many [blue]working days[/] are there in {workingMonth}?")
            .DefaultValue(workingDaysInMonth.Value)
            .PromptStyle("yellow")
            .ShowAsync(AnsiConsole.Console, cancellationToken);

    var absencesDays =
        await new TextPrompt<ushort>($"How many [blue]absences days[/] are there in {workingMonth}?")
            .DefaultValue((ushort)0)
            .PromptStyle("yellow")
            .ShowAsync(AnsiConsole.Console, cancellationToken);

    var reportContext = new ReportGeneratorContext(reportSettings.Value, workingMonth, absencesDays, workingDays);

    return await AnsiConsole.Progress()
        .AutoClear(false)
        .Columns(
            new TaskDescriptionColumn(),
            new ProgressBarColumn(),
            new PercentageColumn(),
            new RemainingTimeColumn()
        )
        .StartAsync(async progressContext =>
        {
            var spectralProgressContext = new SpectreConsoleProgressContext(progressContext);

            var gitCommitsHistoryProvider = new GitCommitsHistoryProviderFactory()
                .Create(reportContext.ReportSettings.GitCommitHistoryProvider, spectralProgressContext);

            var reportGeneratorPipeline = new ReportGeneratorPipeline(new IReportGenerator[]
            {
                new CommitsHistoryReportGenerator(spectralProgressContext, gitCommitsHistoryProvider),
                new FileHtmlReportGenerator(spectralProgressContext),
                new FilePdfReportGenerator(spectralProgressContext, new GoogleChromePdfConvert())
            });

            return await reportGeneratorPipeline.Generate(reportContext, cancellationToken);
        });
}

async Task<ResultBase> Install(FileInfo fileInfo, CancellationToken cancellationToken)
{
    ReportSettings? existingReportSettings = null;

    var installationPrompt = true;
    if (fileInfo.Exists)
    {
        installationPrompt =
            await new ConfirmationPrompt("The tool is already installed. Do you want to [blue]re-install[/]?")
                .ShowAsync(AnsiConsole.Console, cancellationToken);

        var reportSettingsResult = await ReportSettings.OpenAsync(fileInfo.ToString(), cancellationToken);
        if (reportSettingsResult.IsFailed)
        {
            return reportSettingsResult;
        }

        existingReportSettings = reportSettingsResult.Value;
    }

    if (!installationPrompt)
    {
        return Result.Ok();
    }

    AnsiConsole.MarkupLine("Okay. Let's follow step by step and please be attentive: ");

    var employeeFullName = await new TextPrompt<string>("1. What's your [blue]name and surname[/]?")
        .DefaultValue(existingReportSettings?.EmployeeFullName ?? "Vasya Pupkin")
        .PromptStyle("yellow")
        .ShowAsync(AnsiConsole.Console, cancellationToken);

    var formattedEmployeeFullName = employeeFullName.ToLower().Replace(' ', '.');

    var employeeEmail = await new TextPrompt<string>("2. What's your [blue]corporate email[/]?")
        .DefaultValue(existingReportSettings?.EmployeeEmail ?? $"{formattedEmployeeFullName}@google.com")
        .PromptStyle("yellow")
        .ShowAsync(AnsiConsole.Console, cancellationToken);

    var employeePosition = await new TextPrompt<string>("3. What's your [blue]job position[/]?")
        .DefaultValue(existingReportSettings?.EmployeeJobPosition ?? "Software Engineer")
        .PromptStyle("yellow")
        .ShowAsync(AnsiConsole.Console, cancellationToken);

    var employeeFolderPath = await new TextPrompt<string>("4. What's your [blue]remote folder[/]?")
        .DefaultValue(existingReportSettings?.EmployeeFolderName ?? $"\\\\gda-file-07\\{formattedEmployeeFullName}")
        .PromptStyle("yellow")
        .ShowAsync(AnsiConsole.Console, cancellationToken);

    var controlerFullName = await new TextPrompt<string>("5. What's your [blue]controler name and surname[/]?")
        .DefaultValue(existingReportSettings?.ControlerFullName ?? "Petya Galupkin")
        .PromptStyle("yellow")
        .ShowAsync(AnsiConsole.Console, cancellationToken);

    var controlerPosition = await new TextPrompt<string>("6. What's your [blue]controler job position[/]?")
        .DefaultValue(existingReportSettings?.ControlerJobPosition ?? "Dir Software Engineer")
        .PromptStyle("yellow")
        .ShowAsync(AnsiConsole.Console, cancellationToken);

    var projectName = await new TextPrompt<string>("7. What's your [blue]project name[/]?")
        .DefaultValue(existingReportSettings?.ProjectName ?? "GaleraProject 1.0.0")
        .PromptStyle("yellow")
        .ShowAsync(AnsiConsole.Console, cancellationToken);

    var gitCommitsHistoryProvider = await new SelectionPrompt<GitCommitsHistoryProvider>()
        .Title("Which [blue]commit provider[/] do you want?")
        .MoreChoicesText("[grey](Move up and down to choose an action)[/]")
        .AddChoices(GitCommitsHistoryProvider.AzureDevOps, GitCommitsHistoryProvider.Local)
        .ShowAsync(AnsiConsole.Console, cancellationToken);

    string? projectGitDirectory = null;
    string? projectAdoOrganizationName = null;

    switch (gitCommitsHistoryProvider)
    {
        case GitCommitsHistoryProvider.Local:
            projectGitDirectory = await new TextPrompt<string>("8. What's your [blue]project root directory on your local system[/]?")
                    .DefaultValue(existingReportSettings?.ProjectGitDirectory ?? "D://GaleraProject//")
                    .PromptStyle("yellow")
                    .ShowAsync(AnsiConsole.Console, cancellationToken);
            break;
        case GitCommitsHistoryProvider.AzureDevOps:
            projectAdoOrganizationName = await new TextPrompt<string>("8. What's your [blue]organization name in the Azure DevOps[/]?")
                    .DefaultValue(existingReportSettings?.ProjectAdoOrganizationName ?? "galera-company")
                    .PromptStyle("yellow")
                    .ShowAsync(AnsiConsole.Console, cancellationToken);
            break;
        default:
            return Result.Fail("Unsupported commit provider!");
    }

    string? rapidApiKey = null;

    if (await new ConfirmationPrompt("9. Do you want to automatically get the number of working days in a month?")
            .ShowAsync(AnsiConsole.Console, cancellationToken))
    {
        rapidApiKey = await new TextPrompt<string>(
                "10. Great. Go to the [blue]https://rapidapi.com/joursouvres-api/api/working-days/[/] -> sign up -> copy the [blue]`X-RapidAPI-Key`[/].")
            .DefaultValue(existingReportSettings?.RapidApiKey ?? "")
            .PromptStyle("yellow")
            .ShowAsync(AnsiConsole.Console, cancellationToken);
    }

    var reportSettings = new ReportSettings
    {
        EmployeeEmail = employeeEmail,
        EmployeeFullName = employeeFullName,
        EmployeeJobPosition = employeePosition,
        EmployeeFolderName = employeeFolderPath,
        ControlerFullName = controlerFullName,
        ControlerJobPosition = controlerPosition,
        ProjectName = projectName,
        ProjectGitDirectory = projectGitDirectory,
        ProjectAdoOrganizationName = projectAdoOrganizationName,
        RapidApiKey = rapidApiKey,
        GitCommitHistoryProvider = gitCommitsHistoryProvider,
    };

    return await reportSettings.SaveAsync(fileInfo.ToString(), cancellationToken);
}