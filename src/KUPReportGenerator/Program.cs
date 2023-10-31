using System.CommandLine;
using System.Runtime.InteropServices;
using CliWrap;
using FluentResults;
using KUPReportGenerator;
using KUPReportGenerator.Converters;
using KUPReportGenerator.Generators;
using KUPReportGenerator.GitCommitsHistory;
using KUPReportGenerator.Helpers;
using KUPReportGenerator.Helpers.TaskProgress;
using KUPReportGenerator.Report;
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

    var rootCommand = BuildRootCommand(
        async (fileInfo) =>
        {
            AnsiConsole.Write(new FigletText($"KUP Report Generator").Centered().Color(Color.Green1));
            AnsiConsole.MarkupLine("Started, Press [blue]Ctrl-C[/] to stop.");

            var action = await new SelectionPrompt<string>()
                .Title("What do you want [blue]to do[/]?")
                .MoreChoicesText("[grey](Move up and down to choose an action)[/]")
                .AddChoices(nameof(CommandLineActions.Run), nameof(CommandLineActions.Install))
                .ShowAsync(AnsiConsole.Console, cancellationToken);

            switch (action)
            {
                case nameof(CommandLineActions.Run):
                    {
                        var result = await RunAsync(fileInfo, cancellationToken);
                        if (result.IsSuccess)
                        {
                            if (HasGeneratedReports())
                            {
                                AnsiConsole.MarkupLine($"[blue]Done[/]. Reports are successfully generated.");
                                AnsiConsole.MarkupLine($"Open [blue]{Constants.OutputDirectory}[/] folder to check the reports.");
                                await OpenOutputDirectory(cancellationToken);
                            }
                            else
                            {
                                AnsiConsole.MarkupLine($"[blue]Done[/]. No reports were created.");
                            }
                        }
                        else if (ConsoleHelpers.HasErrors(result))
                        {
                            AnsiConsole.MarkupLine("[red]Errors[/] have occurred:");
                            ConsoleHelpers.WriteErrors(result);
                        }

                        break;
                    }
                case nameof(CommandLineActions.Install):
                    {
                        var result = await InstallAsync(fileInfo, cancellationToken);
                        if (result.IsSuccess)
                        {
                            AnsiConsole.MarkupLine("[blue]Done[/]. Now you can run.");
                        }
                        else if (ConsoleHelpers.HasErrors(result))
                        {
                            AnsiConsole.MarkupLine("[red]Errors[/] have occurred:");
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
    AnsiConsole.WriteException(exc);
    Log.Error(exc, exc.Message);
}
finally
{
    Log.CloseAndFlush();

    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("Press [blue]any[/] key to exit.");
    Console.ReadKey();
}

async Task OpenOutputDirectory(CancellationToken cancellationToken)
{
    var shell = "cmd";
    var arguments = $"/c start {Constants.OutputDirectory}";
    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
        shell = "bash";
        arguments = $"-c open {Constants.OutputDirectory}";
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
        shell = "bash";
        arguments = $"-c xdg-open {Constants.OutputDirectory}";
    }

    await Cli.Wrap(shell).WithArguments(arguments).ExecuteAsync(cancellationToken);
}

bool HasGeneratedReports()
{
    return Directory.Exists(Constants.OutputDirectory) && Directory.GetFiles(Constants.OutputDirectory).Any();
}

async Task<Result> RunAsync(FileInfo fileInfo, CancellationToken cancellationToken)
{
    var reportSettings = await ReportSettings.OpenAsync(fileInfo.ToString(), cancellationToken);
    if (reportSettings.IsFailed)
    {
        return Result.Fail("The tool was installed with errors.").WithReasons(reportSettings.Errors);
    }

    var validationResult = await new ReportSettingsValidator().ValidateAsync(reportSettings.Value, cancellationToken);
    if (!validationResult.IsValid)
    {
        return Result.Fail($"The tool was installed with validation errors in the setting file: {Constants.SettingsFilePath}.")
            .WithReasons(validationResult.Errors.Select(e => new Error(e.ErrorMessage)));
    }

    var workingMonth = DatetimeHelper.GetCurrentMonthName();

    var monthlyWorkingDays = await GetWorkingDaysInMonth(reportSettings.Value.RapidApiKey, workingMonth, cancellationToken);
    if (monthlyWorkingDays.IsFailed)
    {
        return monthlyWorkingDays.ToResult();
    }

    var workingDays =
        await new TextPrompt<ushort>($"How many [blue]working days[/] are there in {workingMonth}?")
            .DefaultValue(monthlyWorkingDays.Value)
            .PromptStyle("yellow")
            .ShowAsync(AnsiConsole.Console, cancellationToken);

    var absencesDays =
        await new TextPrompt<ushort>($"How many [blue]absences days[/] are there in {workingMonth}?")
            .DefaultValue((ushort)0)
            .PromptStyle("yellow")
            .ShowAsync(AnsiConsole.Console, cancellationToken);

    var reportContext = new ReportGeneratorContext(reportSettings.Value, workingMonth, absencesDays, workingDays);

    return await AnsiConsole.Progress()
        .AutoClear(true)
        .Columns(
            new TaskDescriptionColumn(),
            new ProgressBarColumn(),
            new PercentageColumn(),
            new RemainingTimeColumn())
        .StartAsync(async progressContext =>
        {
            var spectralProgressContext = new SpectreConsoleProgressContext(progressContext);

            IGitCommitHistoryProvider commitHistoryProvider = reportContext.ReportSettings.GitCommitHistoryProvider switch
            {
                GitCommitHistoryProviders.AzureDevOps => new AdoGitCommitHistoryProvider(spectralProgressContext),
                GitCommitHistoryProviders.Local => new LocalGitCommitHistoryProvider(spectralProgressContext),
                _ => throw new ArgumentException("Invalid commit history provider!")
            };

            var reportGeneratorPipeline = new ReportGeneratorPipeline(new IReportGenerator[]
            {
                new CommitsHistoryReportGenerator(spectralProgressContext, commitHistoryProvider),
                new FileHtmlReportGenerator(spectralProgressContext),
                new FilePdfReportGenerator(spectralProgressContext, new GoogleChromePdfConvert())
            });

            return await reportGeneratorPipeline.Generate(reportContext, cancellationToken);
        });
}

async Task<Result<ushort>> GetWorkingDaysInMonth(string? rapidApiKey, string workingMonth, CancellationToken cancellationToken)
{
    const ushort defaultWorkingDays = 21;
    if (string.IsNullOrEmpty(rapidApiKey))
    {
        return defaultWorkingDays;
    }

    using var rapidApi = new RapidApi(rapidApiKey);

    var startDate = DatetimeHelper.GetFirstDateOfMonth(workingMonth);
    var endDate = DatetimeHelper.GetLastDateOfMonth(workingMonth);

    var workingDaysResult = await rapidApi.GetWorkingDays(startDate, endDate, cancellationToken: cancellationToken);
    if (workingDaysResult.IsFailed)
    {
        return workingDaysResult.ToResult();
    }

    return workingDaysResult.Value;
}

async Task<Result> InstallAsync(FileInfo fileInfo, CancellationToken cancellationToken)
{
    ReportSettings? reportSettings = null;

    var installationPrompt = true;
    if (fileInfo.Exists)
    {
        installationPrompt =
            await new ConfirmationPrompt("The tool is already installed. Do you want to [blue]re-install[/]?")
                .ShowAsync(AnsiConsole.Console, cancellationToken);

        var reportSettingsResult = await ReportSettings.OpenAsync(fileInfo.ToString(), cancellationToken);
        if (reportSettingsResult.IsFailed)
        {
            return reportSettingsResult.ToResult();
        }

        reportSettings = reportSettingsResult.Value;
    }

    if (!installationPrompt)
    {
        return Result.Ok();
    }

    AnsiConsole.MarkupLine("Okay. Let's follow step by step and please be attentive: ");

    var employeeFullName = await new TextPrompt<string>("1. What's your [blue]name and surname[/]?")
        .DefaultValue(reportSettings?.EmployeeFullName ?? "Vasya Pupkin")
        .PromptStyle("yellow")
        .ShowAsync(AnsiConsole.Console, cancellationToken);

    var formattedEmployeeFullName = employeeFullName.ToLower().Replace(' ', '.');

    var employeeEmail = await new TextPrompt<string>("2. What's your [blue]corporate email[/]?")
        .DefaultValue(reportSettings?.EmployeeEmail ?? $"{formattedEmployeeFullName}@google.com")
        .PromptStyle("yellow")
        .ShowAsync(AnsiConsole.Console, cancellationToken);

    var employeePosition = await new TextPrompt<string>("3. What's your [blue]job position[/]?")
        .DefaultValue(reportSettings?.EmployeeJobPosition ?? "Software Engineer")
        .PromptStyle("yellow")
        .ShowAsync(AnsiConsole.Console, cancellationToken);

    var employeeFolderPath = await new TextPrompt<string>("4. What's your [blue]remote folder[/]?")
        .DefaultValue(reportSettings?.EmployeeFolderName ?? $"\\\\gda-file-07\\{formattedEmployeeFullName}")
        .PromptStyle("yellow")
        .ShowAsync(AnsiConsole.Console, cancellationToken);

    var controlerFullName = await new TextPrompt<string>("5. What's your [blue]controler name and surname[/]?")
        .DefaultValue(reportSettings?.ControlerFullName ?? "Petya Galupkin")
        .PromptStyle("yellow")
        .ShowAsync(AnsiConsole.Console, cancellationToken);

    var controlerPosition = await new TextPrompt<string>("6. What's your [blue]controler job position[/]?")
        .DefaultValue(reportSettings?.ControlerJobPosition ?? "Dir Software Engineer")
        .PromptStyle("yellow")
        .ShowAsync(AnsiConsole.Console, cancellationToken);

    var projectName = await new TextPrompt<string>("7. What's your [blue]project name[/]?")
        .DefaultValue(reportSettings?.ProjectName ?? "GaleraProject 1.0.0")
        .PromptStyle("yellow")
        .ShowAsync(AnsiConsole.Console, cancellationToken);

    var commitProviderChoise = await new SelectionPrompt<GitCommitHistoryProviders>()
        .Title("Which [blue]commit provider[/] do you want?")
        .MoreChoicesText("[grey](Move up and down to choose an action)[/]")
        .AddChoices(GitCommitHistoryProviders.AzureDevOps, GitCommitHistoryProviders.Local)
        .ShowAsync(AnsiConsole.Console, cancellationToken);

    string? projectGitDirectory = null;
    string? projectAdoOrganizationName = null;

    if (commitProviderChoise == GitCommitHistoryProviders.Local)
    {
        projectGitDirectory = await new TextPrompt<string>("8. What's your [blue]project root directory on your local system[/]?")
            .DefaultValue(reportSettings?.ProjectGitDirectory ?? "D://GaleraProject//")
            .PromptStyle("yellow")
            .ShowAsync(AnsiConsole.Console, cancellationToken);
    }
    else if (commitProviderChoise == GitCommitHistoryProviders.AzureDevOps)
    {
        projectAdoOrganizationName = await new TextPrompt<string>("8. What's your [blue]organization name in the Azure DevOps[/]?")
            .DefaultValue(reportSettings?.ProjectAdoOrganizationName ?? "galera-company")
            .PromptStyle("yellow")
            .ShowAsync(AnsiConsole.Console, cancellationToken);
    }

    string? rapidApiKey = null;
    if (await new ConfirmationPrompt("9. Do you want to automatically get the number of working days in a month?")
            .ShowAsync(AnsiConsole.Console, cancellationToken))
    {
        rapidApiKey =
            await new TextPrompt<string>(
                    "10. Great. Go to the [blue]https://rapidapi.com/joursouvres-api/api/working-days/[/] -> sign up -> copy the [blue]`X-RapidAPI-Key`[/].")
                .DefaultValue(reportSettings?.RapidApiKey ?? "")
                .PromptStyle("yellow")
                .ShowAsync(AnsiConsole.Console, cancellationToken);
    }

    var newReportSettings = new ReportSettings
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
        GitCommitHistoryProvider = commitProviderChoise,
    };

    var saveNewReportSettings = await newReportSettings.SaveAsync(fileInfo.ToString(), cancellationToken);
    return saveNewReportSettings.ToResult();
}