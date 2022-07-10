using System.CommandLine;
using System.Globalization;
using FluentResults;
using KUPReportGenerator.Generators;
using KUPReportGenerator.Helpers;
using Spectre.Console;

namespace KUPReportGenerator;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            AnsiConsole.Write(new FigletText("KUP Report Generator").Centered().Color(Color.Green1));
            AnsiConsole.MarkupLine("Started, Press [green]Ctrl-C[/] to stop.");

            var rootCommand = CommandLineHelper.CreateRootCommand(
                async (FileInfo fileInfo, CancellationToken cancellationToken) =>
                {
                    const string actionRun = "Run";
                    const string actionInstall = "Install";

                    var action = AnsiConsole.Prompt(
                        new SelectionPrompt<string>()
                            .Title("What do you want [green]to do[/]?")
                            .MoreChoicesText("[grey](Move up and down to choose an action)[/]")
                            .AddChoices(new[] { actionRun, actionInstall }));

                    if (action is actionRun)
                    {
                        var reportSettings = await LoadReportSettingsAsync(fileInfo, cancellationToken);
                        if (reportSettings.IsFailed)
                        {
                            WriteErrors(reportSettings.ToResult());
                            return;
                        }

                        await AnsiConsole.Progress()
                            .AutoClear(true)
                            .Columns(new ProgressColumn[]
                            {
                                new TaskDescriptionColumn(),
                                new ProgressBarColumn(),
                                new PercentageColumn(),
                                new RemainingTimeColumn()
                            })
                            .StartAsync(async ctx =>
                            {
                                var result = await RunAsync(reportSettings.Value, ctx, cancellationToken);
                                if (result.IsSuccess)
                                {
                                    AnsiConsole.WriteLine("Done. Reports are successfully generated: ");
                                    AnsiConsole.MarkupLine($"- [green]{Constants.ReportFilePath}[/]");
                                    AnsiConsole.MarkupLine($"- [green]{Constants.CommitsHistoryFilePath}[/]");
                                }
                                else
                                {
                                    WriteErrors(result);
                                }
                            });
                    }
                    else if (action is actionInstall)
                    {
                        var result = await InstallAsync(fileInfo, cancellationToken);
                        if (result.IsSuccess)
                        {
                            AnsiConsole.WriteLine("Done. Now you can run.");
                        }
                        else
                        {
                            WriteErrors(result);
                        }
                    }
                });

            return await rootCommand.InvokeAsync(args);
        }
        finally
        {
            Console.ReadKey();
        }
    }

    private static async Task<Result> InstallAsync(FileInfo fileInfo, CancellationToken cancellationToken)
    {
        ReportSettings? reportSettings = null;

        var installationPrompt = true;
        if (fileInfo.Exists)
        {
            installationPrompt = AnsiConsole.Confirm("The tool is already installed. Do you want to [green]reinstall[/]?");

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

        AnsiConsole.WriteLine("Okay. Let's follow step by step and please be attentive: ");

        var employeeFullName = AnsiConsole.Prompt(
            new TextPrompt<string>("1. What's your [green]name and surname[/]?")
                .DefaultValue(reportSettings?.EmployeeFullName ?? "Vasya Pupkin")
                .PromptStyle("yellow"));

        var employeeEmail = AnsiConsole.Prompt(
            new TextPrompt<string>("2. What's your [green]corporate email[/]?")
                .DefaultValue(reportSettings?.EmployeeEmail ?? "vasya.pupkin@google.com")
                .PromptStyle("yellow"));

        var employeePosition = AnsiConsole.Prompt(
            new TextPrompt<string>("3. What's your [green]job position[/]?")
                .DefaultValue(reportSettings?.EmployeePosition ?? "Software Engineer")
                .PromptStyle("yellow"));

        var employeeFolderPath = AnsiConsole.Prompt(
            new TextPrompt<string>("4. What's your [green]remote folder[/]?")
                .DefaultValue(reportSettings?.EmployeeFolderName ?? $"\\\\gda-file-07\\{employeeFullName.ToLower().Replace(' ', '.')}")
                .PromptStyle("yellow"));

        var controlerFullName = AnsiConsole.Prompt(
            new TextPrompt<string>("5. What's your [green]controler name and surname[/]?")
                .DefaultValue(reportSettings?.ControlerFullName ?? "Petya Galupkin")
                .PromptStyle("yellow"));

        var controlerPosition = AnsiConsole.Prompt(
            new TextPrompt<string>("6. What's your [green]controler job position[/]?")
                .DefaultValue(reportSettings?.ControlerFullName ?? "Dir Software Engineer")
                .PromptStyle("yellow"));

        var projectName = AnsiConsole.Prompt(
            new TextPrompt<string>("7. What's your [green]project name[/]?")
                .DefaultValue(reportSettings?.ProjectName ?? "GaleraProject 1.0.0")
                .PromptStyle("yellow"));

        var projectAdoOrganizationName = AnsiConsole.Prompt(
            new TextPrompt<string>("8. What's your [green]organization name in the Azure DevOps[/]?")
                .DefaultValue(reportSettings?.ProjectAdoOrganizationName ?? "galera-company")
                .PromptStyle("yellow"));

        string? rapidApiKey = null;
        if (AnsiConsole.Confirm("9. Do you want to automatically get the number of working days in a month?"))
        {
            rapidApiKey = AnsiConsole.Prompt(
                new TextPrompt<string>("10. Great. Go to the [blue]https://rapidapi.com/joursouvres-api/api/working-days/[/] -> sign up -> copy the [green]`X-RapidAPI-Key`[/].")
                    .DefaultValue(reportSettings?.RapidApiKey ?? "29f54a418emshebe82a11ac98e27p1ed562jxcff7fb4f95751")
                    .PromptStyle("yellow"));
        }

        var newReportSettings = new ReportSettings()
        {
            EmployeeEmail = employeeEmail,
            EmployeeFullName = employeeFullName,
            EmployeePosition = employeePosition,
            EmployeeFolderName = employeeFolderPath,
            ControlerFullName = controlerFullName,
            ControlerPosition = controlerPosition,
            ProjectName = projectName,
            ProjectAdoOrganizationName = projectAdoOrganizationName,
            RapidApiKey = rapidApiKey
        };

        var initResult = await newReportSettings.SaveAsync(fileInfo.ToString(), cancellationToken);
        return initResult.ToResult();
    }

    private static async Task<Result<ReportSettings>> LoadReportSettingsAsync(FileInfo fileInfo, CancellationToken cancellationToken)
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

        if (!string.IsNullOrEmpty(reportSettings.Value.RapidApiKey))
        {
            using var rapidApi = new RapidApi(reportSettings.Value.RapidApiKey);
            var workingDays = await rapidApi.GetWorkingDays(cancellationToken: cancellationToken);
            if (workingDays.IsFailed)
            {
                return workingDays.ToResult();
            }

            reportSettings.Value.WorkingDays = workingDays.Value;
        }
        else
        {
            reportSettings.Value.WorkingDays = AnsiConsole.Prompt(
                new TextPrompt<ushort>($"How many working days are there in {DateTime.UtcNow.ToString("MMMM", CultureInfo.InvariantCulture)}?")
                    .DefaultValue(reportSettings.Value.WorkingDays ?? 21)
                    .PromptStyle("yellow"));
        }

        reportSettings.Value.AbsencesDays = AnsiConsole.Prompt(
            new TextPrompt<ushort>($"How many absences days are there in {DateTime.UtcNow.ToString("MMMM", CultureInfo.InvariantCulture)}?")
                .DefaultValue(reportSettings?.Value.AbsencesDays ?? 0)
                .PromptStyle("yellow"));

        return reportSettings!;
    }

    private static async Task<Result> RunAsync(ReportSettings reportSettings, ProgressContext progressContext,
        CancellationToken cancellationToken)
    {
        var reportsGenerator = new IReportGenerator[]
        {
            new CommitsHistoryReportGenerator(),
            new HtmlReportGenerator()
        };

        var reportGenerator = new ReportGeneratorComposite(reportsGenerator);
        var reportResult = await reportGenerator.Generate(reportSettings, progressContext, cancellationToken);
        return reportResult;
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
            return Result.Fail(new Error($"Initialization failed. Could't create output directory: {Constants.OutputDirectory}.").CausedBy(exc));
        }
    }

    private static void WriteErrors(Result result)
    {
        AnsiConsole.MarkupLine("Oops, something goes wrong with [red]errors[/]: ");

        var grid = new Grid();
        grid.AddColumn(new GridColumn().PadLeft(2).PadRight(0).NoWrap());

        for (var index = 0; index < result.Errors.Count; index++)
        {
            var error = result.Errors[index];
            grid.AddRow($"{index + 1}. [darkorange]{error.Message}[/]");
        }

        AnsiConsole.Write(grid);
    }
}