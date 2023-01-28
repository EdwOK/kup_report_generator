using FluentResults;
using Spectre.Console;

namespace KUPReportGenerator.Helpers;

public static class ConsoleHelpers
{
    public static bool HasErrors(ResultBase result) =>
        result.IsFailed && !result.HasException<OperationCanceledException>(e => e.CancellationToken.IsCancellationRequested);

    public static void WriteErrors(ResultBase result)
    {
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

            if (reasons.Any())
            {
                grid.AddEmptyRow();
                foreach (var reason in reasons)
                {
                    grid.AddRow($"[orangered1]{$"{reason!.Exception.Source}: {reason!.Exception.Message}"}[/]");
                }
            }

            table.AddRow(new Text($"{index + 1}"), grid);
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine("See details in logs.txt");
    }
}