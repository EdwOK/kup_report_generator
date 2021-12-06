using FluentResults;
using System;
using System.IO;

namespace KUPReportGenerator.Helpers
{
    internal static class ConsoleHelper
    {
        public static void PrintResults(Result result)
        {
            if (File.Exists(Constants.ReportFilePath))
            {
                Console.Write("KUP report: ");
                PrintWithColor(() => Console.Write(Constants.ReportFilePath), ConsoleColor.Green);
                Console.WriteLine();
            }

            if (File.Exists(Constants.CommitsHistoryFilePath))
            {
                Console.Write("Commits history: ");
                PrintWithColor(() => Console.Write(Constants.CommitsHistoryFilePath), ConsoleColor.Green);
                Console.WriteLine();
            }
        }

        public static void PrintErrors(Result result)
        {
            Console.WriteLine("Errors: ");
            foreach (var error in result.Errors)
            {
                PrintWithColor(() => Console.WriteLine(error.Message), ConsoleColor.DarkRed);
            }
        }

        private static void PrintWithColor(Action callback, ConsoleColor? consoleColor = null)
        {
            var previousColor = Console.ForegroundColor;
            Console.ForegroundColor = consoleColor ?? previousColor;
            callback();
            Console.ForegroundColor = previousColor;
        }
    }
}
