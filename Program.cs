using System.CommandLine;
using System.Globalization;
using CliWrap;
using FluentResults;
using KUPReportGenerator.CommandLine;
using KUPReportGenerator.Generators;
using KUPReportGenerator.Helpers;
using Spectre.Console;

namespace KUPReportGenerator;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            AnsiConsole.Write(new FigletText("KUP Report Generator").Centered().Color(Color.Green1));
            AnsiConsole.MarkupLine("Started, Press [green]Ctrl-C[/] to stop.");

            var rootCommand = CommandLineBuilder.BuildRootCommand(
                async (fileInfo, cancellationToken) =>
                {
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
                                    AnsiConsole.MarkupLine($"- [green]{Constants.ReportFilePath}[/]");
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
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("Press [green]any[/] key to exit.");
            Console.ReadKey();
        }
    }

    private static async Task<Result> RunAsync(FileInfo fileInfo, CancellationToken cancellationToken)
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

        var currentMonthName = DatetimeHelper.GetCurrentMonthName();
        var currentMonthWorkingDays = await GetCurrentMonthWorkingDays(reportSettings.Value, cancellationToken);
        if (currentMonthWorkingDays.IsFailed)
        {
            return currentMonthWorkingDays.ToResult();
        }

        reportSettings.Value.WorkingDays =
            await new TextPrompt<ushort>($"How many [green]working days[/] are there in {currentMonthName}?")
                .DefaultValue(reportSettings.Value.WorkingDays ?? currentMonthWorkingDays.Value)
                .PromptStyle("yellow")
                .ShowAsync(AnsiConsole.Console, cancellationToken);

        reportSettings.Value.AbsencesDays =
            await new TextPrompt<ushort>($"How many [green]absences days[/] are there in {currentMonthName}?")
                .DefaultValue(reportSettings?.Value.AbsencesDays ?? 0)
                .PromptStyle("yellow")
                .ShowAsync(AnsiConsole.Console, cancellationToken);

        return await AnsiConsole.Progress()
            .AutoClear(true)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn())
            .StartAsync(async progressContext =>
            {
                var reportGenerator = new ReportGeneratorComposite(new IReportGenerator[]
                {
                    new CommitsHistoryReportGenerator(),
                    new HtmlReportGenerator()
                });
                return await reportGenerator.Generate(reportSettings!.Value, progressContext, cancellationToken);
            });
    }

    private static async Task<Result<ushort>> GetCurrentMonthWorkingDays(ReportSettings reportSettings,
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

        var workingDaysResult = await rapidApi.GetMonthlyWorkingDays(startDate, endDate, cancellationToken: cancellationToken);
        if (workingDaysResult.IsFailed)
        {
            return workingDaysResult.ToResult();
        }

        return workingDaysResult.Value;
    }

    private static async Task<Result> InstallAsync(FileInfo fileInfo, CancellationToken cancellationToken)
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

    private static Result Initialize(CancellationToken cancellationToken)
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

    private static bool HasErrors(Result result) =>
        result.IsFailed &&
        !result.HasException<OperationCanceledException>(e => e.CancellationToken.IsCancellationRequested);

    private static void WriteErrors(Result result)
    {
        AnsiConsole.MarkupLine("Oops, something goes wrong with [red]errors[/]: ");

        var table = new Table();
        table.AddColumn("N");
        table.AddColumn(new TableColumn("Error").Centered());

        for (var index = 0; index < result.Errors.Count; index++)
        {
            var error = result.Errors[index];
            var reasons = error.Reasons
                .Select(r => r is ExceptionalError exc ? $"{exc.Exception.Source}: {exc.Exception.Message}" : null)
                .Where(r => r is not null)
                .ToArray();

            var grid = new Grid();
            grid.AddColumn(new GridColumn().LeftAligned().NoWrap());
            grid.AddRow($"[red]{error.Message}[/]");
            if (reasons.Any())
            {
                grid.AddEmptyRow();
                foreach (var reason in reasons)
                {
                    grid.AddRow($"[orangered1]{reason}[/]");
                }
            }

            table.AddRow(new Text($"{index + 1}"), grid);
        }

        AnsiConsole.Write(table);
    }
}