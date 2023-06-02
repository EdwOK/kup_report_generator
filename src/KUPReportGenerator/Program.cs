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
    var rootCommand = BuildRootCommand(
        async (fileInfo) =>
        {
            using var cancellationTokenSource = new CancellationTokenSource();

            var cancellationToken = cancellationTokenSource.Token;
            Console.CancelKeyPress += (_, e) =>
            {
                cancellationTokenSource.Cancel();
                e.Cancel = true;
            };

            AnsiConsole.Write(new FigletText($"KUP Report Generator").Centered().Color(Color.Green1));
            AnsiConsole.MarkupLine("Started, Press [green]Ctrl-C[/] to stop.");

            var action = await new SelectionPrompt<string>()
                .Title("What do you want [green]to do[/]?")
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
                            AnsiConsole.MarkupLine("[green]Done[/]. Reports are successfully generated.");
                        }
                        else if (ConsoleHelpers.HasErrors(result))
                        {
                            AnsiConsole.MarkupLine("[red]Done[/]. Reports are generated with [red]errors[/]: ");
                            ConsoleHelpers.WriteErrors(result);
                        }

                        AnsiConsole.MarkupLine($"Open [green]{Constants.OutputDirectory}[/] folder to see the report results.");
                        await OpenOutputDirectory(cancellationToken);

                        break;
                    }
                case nameof(CommandLineActions.Install):
                    {
                        var result = await InstallAsync(fileInfo, cancellationToken);
                        if (result.IsSuccess)
                        {
                            AnsiConsole.MarkupLine("[green]Done[/]. Now you can run.");
                        }
                        else if (ConsoleHelpers.HasErrors(result))
                        {
                            AnsiConsole.MarkupLine("[red]Done[/]. Reports are generated with [red]errors[/]: ");
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
    AnsiConsole.MarkupLine("Press [green]any[/] key to exit.");
    Console.ReadKey();
}

string CreateOutputDirectory(string directoryPath)
{
    if (Directory.Exists(directoryPath))
    {
        Directory.Delete(directoryPath, true);
    }

    Directory.CreateDirectory(directoryPath);
    return directoryPath;
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

async Task<Result> RunAsync(FileInfo fileInfo, CancellationToken cancellationToken)
{
    var outputDirectoryPath = CreateOutputDirectory(Constants.OutputDirectory);

    var reportSettings = await ReportSettings.OpenAsync(fileInfo.ToString(), cancellationToken);
    if (reportSettings.IsFailed)
    {
        return reportSettings.ToResult();
    }

    var validator = new ReportSettingsValidator();
    var validationResult = await validator.ValidateAsync(reportSettings.Value, cancellationToken);
    if (!validationResult.IsValid)
    {
        return Result.Fail("Validation failed for the report settings file.")
            .WithErrors(validationResult.Errors.Select(e => new Error(e.ErrorMessage)));
    }

    var currentMonthName = DatetimeHelper.GetCurrentMonthName();
    var currentMonthWorkingDays = await GetCurrentMonthWorkingDays(reportSettings.Value.RapidApiKey, cancellationToken);
    if (currentMonthWorkingDays.IsFailed)
    {
        return currentMonthWorkingDays.ToResult();
    }

    var workingDays =
        await new TextPrompt<ushort>($"How many [green]working days[/] are there in {currentMonthName}?")
            .DefaultValue(currentMonthWorkingDays.Value)
            .PromptStyle("yellow")
            .ShowAsync(AnsiConsole.Console, cancellationToken);

    var absencesDays =
        await new TextPrompt<ushort>($"How many [green]absences days[/] are there in {currentMonthName}?")
            .DefaultValue((ushort)0)
            .PromptStyle("yellow")
            .ShowAsync(AnsiConsole.Console, cancellationToken);

    var reportContext = new ReportGeneratorContext(reportSettings.Value, absencesDays, workingDays);

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

            var reportGeneratorPipeline = new ReportGeneratorPipeline(new IReportGenerator[]
            {
                new CommitsHistoryReportGenerator(spectralProgressContext, new AdoGitCommitHistoryProvider(spectralProgressContext)),
                new FileHtmlReportGenerator(spectralProgressContext),
                new FilePdfReportGenerator(spectralProgressContext, new GoogleChromePdfConvert())
            });

            return await reportGeneratorPipeline.Generate(reportContext, cancellationToken);
        });
}

async Task<Result<ushort>> GetCurrentMonthWorkingDays(string? rapidApiKey,
    CancellationToken cancellationToken)
{
    const ushort defaultWorkingDays = 21;
    if (string.IsNullOrEmpty(rapidApiKey))
    {
        return defaultWorkingDays;
    }

    using var rapidApi = new RapidApi(rapidApiKey);

    var startDate = DatetimeHelper.GetFirstDateOfCurrentMonth();
    var endDate = DatetimeHelper.GetLastDateOfCurrentMonth();

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
            await new ConfirmationPrompt("The tool is already installed. Do you want to [green]re-install[/]?")
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

    var employeeFullName = await new TextPrompt<string>("1. What's your [green]name and surname[/]?")
        .DefaultValue(reportSettings?.EmployeeFullName ?? "Vasya Pupkin")
        .PromptStyle("yellow")
        .ShowAsync(AnsiConsole.Console, cancellationToken);

    var formattedEmployeeFullName = employeeFullName.ToLower().Replace(' ', '.');

    var employeeEmail = await new TextPrompt<string>("2. What's your [green]corporate email[/]?")
        .DefaultValue(reportSettings?.EmployeeEmail ?? $"{formattedEmployeeFullName}@google.com")
        .PromptStyle("yellow")
        .ShowAsync(AnsiConsole.Console, cancellationToken);

    var employeePosition = await new TextPrompt<string>("3. What's your [green]job position[/]?")
        .DefaultValue(reportSettings?.EmployeeJobPosition ?? "Software Engineer")
        .PromptStyle("yellow")
        .ShowAsync(AnsiConsole.Console, cancellationToken);

    var employeeFolderPath = await new TextPrompt<string>("4. What's your [green]remote folder[/]?")
        .DefaultValue(reportSettings?.EmployeeFolderName ?? $"\\\\gda-file-07\\{formattedEmployeeFullName}")
        .PromptStyle("yellow")
        .ShowAsync(AnsiConsole.Console, cancellationToken);

    var controlerFullName = await new TextPrompt<string>("5. What's your [green]controler name and surname[/]?")
        .DefaultValue(reportSettings?.ControlerFullName ?? "Petya Galupkin")
        .PromptStyle("yellow")
        .ShowAsync(AnsiConsole.Console, cancellationToken);

    var controlerPosition = await new TextPrompt<string>("6. What's your [green]controler job position[/]?")
        .DefaultValue(reportSettings?.ControlerJobPosition ?? "Dir Software Engineer")
        .PromptStyle("yellow")
        .ShowAsync(AnsiConsole.Console, cancellationToken);

    var projectName = await new TextPrompt<string>("7. What's your [green]project name[/]?")
        .DefaultValue(reportSettings?.ProjectName ?? "GaleraProject 1.0.0")
        .PromptStyle("yellow")
        .ShowAsync(AnsiConsole.Console, cancellationToken);

    var projectAdoOrganizationName = await new TextPrompt<string>("8. What's your [green]organization name in the Azure DevOps[/]?")
        .DefaultValue(reportSettings?.ProjectAdoOrganizationName ?? "galera-company")
        .PromptStyle("yellow")
        .ShowAsync(AnsiConsole.Console, cancellationToken);

    string? rapidApiKey = null;
    if (await new ConfirmationPrompt("9. Do you want to automatically get the number of working days in a month?")
            .ShowAsync(AnsiConsole.Console, cancellationToken))
    {
        rapidApiKey =
            await new TextPrompt<string>(
                    "10. Great. Go to the [blue]https://rapidapi.com/joursouvres-api/api/working-days/[/] -> sign up -> copy the [green]`X-RapidAPI-Key`[/].")
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
        ProjectAdoOrganizationName = projectAdoOrganizationName,
        RapidApiKey = rapidApiKey
    };

    var saveNewReportSettings = await newReportSettings.SaveAsync(fileInfo.ToString(), cancellationToken);
    return saveNewReportSettings.ToResult();
}