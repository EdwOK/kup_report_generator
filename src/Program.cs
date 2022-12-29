using System.CommandLine;
using CliWrap;
using FluentResults;
using FluentValidation;
using KUPReportGenerator;
using KUPReportGenerator.CommandLine;
using KUPReportGenerator.Converters;
using KUPReportGenerator.Generators;
using KUPReportGenerator.GitCommitsHistory;
using KUPReportGenerator.Helpers;
using KUPReportGenerator.Report;
using KUPReportGenerator.TaskProgress;
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
    var rootCommand = CommandLineBuilder.BuildRootCommand(
        async (fileInfo) =>
        {
            using var cancellationTokenSource = new CancellationTokenSource();

            var cancellationToken = cancellationTokenSource.Token;
            Console.CancelKeyPress += (_, e) =>
            {
                cancellationTokenSource.Cancel();
                e.Cancel = true;
            };

            AnsiConsole.Write(new FigletText("KUP Report Generator").Centered().Color(Color.Green1));
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
                            AnsiConsole.MarkupLine("[green]Done[/]. Reports are successfully generated: ");
                            AnsiConsole.MarkupLine($"- [green]{Constants.HtmlReportFilePath}[/]");
                            AnsiConsole.MarkupLine($"- [green]{Constants.PdfReportFilePath}[/]");
                            AnsiConsole.MarkupLine($"- [green]{Constants.CommitsHistoryFilePath}[/]");

                            if (EnvironmentUtils.IsWindowsPlatform())
                            {
                                await Cli.Wrap("cmd")
                                    .WithArguments($"/c start {Constants.OutputDirectory}")
                                    .ExecuteAsync(cancellationToken);
                            }
                        }
                        else if (HasErrors(result))
                        {
                            WriteErrors(result);
                        }

                        break;
                    }
                case nameof(CommandLineActions.Install):
                    {
                        var result = await InstallAsync(fileInfo, cancellationToken);
                        if (result.IsSuccess)
                        {
                            AnsiConsole.MarkupLine("[green]Done[/]. Now you can run.");
                        }
                        else if (HasErrors(result))
                        {
                            WriteErrors(result);
                        }

                        break;
                    }
            }
        });

    return await rootCommand.InvokeAsync(args);
}
finally
{
    Log.CloseAndFlush();

    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("Press [green]any[/] key to exit.");
    Console.ReadKey();
}

async Task<Result> RunAsync(FileInfo fileInfo, CancellationToken cancellationToken)
{
    var initialize = Initialize(cancellationToken);
    if (initialize.IsFailed)
    {
        return initialize;
    }

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
    var currentMonthWorkingDays = await GetCurrentMonthWorkingDays(reportSettings.Value, cancellationToken);
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

            var reportGenerator = new ReportGeneratorComposite(new IReportGenerator[]
            {
                    new CommitsHistoryReportGenerator(spectralProgressContext, new AdoGitCommitHistoryProvider(spectralProgressContext)),
                    new FileHtmlReportGenerator(spectralProgressContext),
                    new FilePdfReportGenerator(spectralProgressContext, new GoogleChromePdfConvert())
            });

            return await reportGenerator.Generate(reportContext, cancellationToken);
        });
}

async Task<Result<ushort>> GetCurrentMonthWorkingDays(ReportSettings reportSettings,
    CancellationToken cancellationToken)
{
    const ushort defaultWorkingDays = 21;
    if (string.IsNullOrEmpty(reportSettings.RapidApiKey))
    {
        return defaultWorkingDays;
    }

    using var rapidApi = new RapidApi(reportSettings.RapidApiKey);

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

Result Initialize(CancellationToken cancellationToken)
{
    try
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Directory.Exists(Constants.OutputDirectory))
        {
            Directory.Delete(Constants.OutputDirectory, true);
        }

        Directory.CreateDirectory(Constants.OutputDirectory);
        return Result.Ok();
    }
    catch (Exception exc)
    {
        return Result.Fail(
            new Error($"Initialization failed. Could\'t create output directory: {Constants.OutputDirectory}.")
                .CausedBy(exc));
    }
}

bool HasErrors(Result result) =>
    result.IsFailed && !result.HasException<OperationCanceledException>(e => e.CancellationToken.IsCancellationRequested);

void WriteErrors(Result result)
{
    AnsiConsole.MarkupLine("[red]Done[/]. Reports are generated with [red]errors[/]: ");

    var table = new Table();
    table.AddColumn("N");
    table.AddColumn(new TableColumn("Error").Centered());

    for (var index = 0; index < result.Errors.Count; index++)
    {
        var error = result.Errors[index];
        var reasons = error.Reasons
            .Select(r => r is ExceptionalError exc ? exc : null)
            .Where(r => r is not null)
            .ToArray();

        var grid = new Grid();
        grid.AddColumn(new GridColumn().LeftAligned().NoWrap());
        grid.AddRow($"[red]{error.Message}[/]");
        Log.Error("Error: {error}", error.Message);

        if (reasons.Any())
        {
            grid.AddEmptyRow();
            foreach (var reason in reasons)
            {
                grid.AddRow($"[orangered1]{$"{reason!.Exception.Source}: {reason!.Exception.Message}"}[/]");
                Log.Error(reason.Exception, "Reason: {reason}", reason.Exception.Message);
            }
        }

        table.AddRow(new Text($"{index + 1}"), grid);
    }

    AnsiConsole.Write(table);
    AnsiConsole.WriteLine("See details in logs.");
}