using System.CommandLine;
using KUPReportGenerator;

internal static partial class Program
{
    internal enum CommandLineActions
    {
        Run,
        Install,
    }

    private static readonly Option<FileInfo> _settingsFileOption =
        new("--settings-file", "Path to the settings file. Defaults is current directory.");

    static Program()
    {
        _settingsFileOption.SetDefaultValue(new FileInfo(Constants.SettingsFilePath));
        _settingsFileOption.AddValidator(result =>
        {
            foreach (var token in result.Tokens)
            {
                if (!File.Exists(token.Value))
                {
                    result.ErrorMessage = result.LocalizationResources.FileDoesNotExist(token.Value);
                    return;
                }

                if (Path.GetExtension(token.Value) is not ".json")
                {
                    result.ErrorMessage = "File settings must be in JSON format.";
                    return;
                }
            }
        });
    }

    private static RootCommand BuildRootCommand(Func<FileInfo, Task> handler)
    {
        var rootCommand = new RootCommand();
        rootCommand.AddGlobalOption(_settingsFileOption);
        rootCommand.SetHandler(handler, _settingsFileOption);
        return rootCommand;
    }
}