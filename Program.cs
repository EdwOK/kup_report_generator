using FluentResults;
using KUPReportGenerator.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace KUPReportGenerator
{
    internal class Program
    {
        private static async Task Main()
        {
            Console.WriteLine("Started, Press Ctrl-C to stop.");
            Console.WriteLine();

            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cts.Cancel();
            };

            var result = await RunAsync(cts.Token);
            if (result.IsSuccess)
            {
                ConsoleHelper.PrintResults(result);
            }
            else
            {
                ConsoleHelper.PrintErrors(result);
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to continue.");
            Console.ReadKey();
        }

        private static async Task<Result> RunAsync(CancellationToken cancellationToken)
        {
            var initialize = Initialize(cancellationToken);
            if (initialize.IsFailed)
            {
                return initialize;
            }

            var reportSettings = await ReportSettings.ParseAsync(Constants.SettingsFilePath, cancellationToken);
            if (reportSettings.IsFailed)
            {
                return reportSettings.ToResult();
            }

            var reportsGenerator = new List<IReportGenerator>();
            if (reportSettings.Value.EmployeeHasCommitsHistory)
            {
                reportsGenerator.Add(new CommitsHistoryReportGenerator());
            }
            reportsGenerator.Add(new HtmlReportGenerator());

            var reportGenerator = new ReportGeneratorComposite(reportsGenerator);

            var reportResult = await reportGenerator.Generate(reportSettings.Value, cancellationToken);
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
                return Result.Fail(new Error($"Initialization failed. Could't create output directory {Constants.OutputDirectory}.").CausedBy(exc));
            }
        }
    }
}
