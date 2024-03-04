using System.Runtime.InteropServices;
using CliWrap;
using Serilog;
using Spectre.Console;

namespace Helpers;

public static class ConsoleHelpers
{
    public static bool HasErrors(ResultBase result) =>
        result.IsFailed && !result.HasException<OperationCanceledException>(e => e.CancellationToken.IsCancellationRequested);

    public static void WriteErrors(ResultBase result)
    {
        if (!HasErrors(result))
        {
            return;
        }

        AnsiConsole.MarkupLine("[red]Errors[/] have occurred:");

        var table = new Table();
        table.AddColumn("N");
        table.AddColumn(new TableColumn("Error").Centered());

        for (var index = 0; index < result.Errors.Count; index++)
        {
            var error = result.Errors[index];

            var grid = new Grid();
            grid.AddColumn(new GridColumn().LeftAligned().NoWrap());
            grid.AddRow($"[red]{error.Message}[/]");

            var reasons = error.Reasons
                .Where(r => r is not ExceptionalError)
                .ToArray();
            if (reasons.Length > 0)
            {
                grid.AddEmptyRow();
                grid.AddRow("Reasons: ");
                foreach (var reason in reasons)
                {
                    grid.AddRow($"[orangered1]{reason!.Message}[/]");
                }
            }

            table.AddRow(new Text($"{index + 1}"), grid);

            foreach (var exceptionalError in error.Reasons.OfType<ExceptionalError>())
            {
                Log.Logger.Error(exceptionalError.Exception, exceptionalError.Exception.Message);
            }
        }

        AnsiConsole.Write(table);
    }

    public static async Task OpenDirectory(string directory, CancellationToken cancellationToken)
    {
        var shell = "cmd";
        var arguments = $"/c start {directory}";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            shell = "bash";
            arguments = $"-c open {directory}";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            shell = "bash";
            arguments = $"-c xdg-open {directory}";
        }

        await Cli.Wrap(shell).WithArguments(arguments).ExecuteAsync(cancellationToken);
    }
}