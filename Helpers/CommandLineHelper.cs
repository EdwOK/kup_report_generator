using System.CommandLine;

namespace KUPReportGenerator.Helpers;

internal static class CommandLineHelper
{
    private static readonly Option<FileInfo> SettingsFileOption = new("--settings-file")
    {
        IsRequired = false,
        Description = "Path to the settings file. Defaults is current directory."
    };

    static CommandLineHelper()
    {
        SettingsFileOption.SetDefaultValue(new FileInfo(Constants.SettingsFilePath));
        SettingsFileOption.AddValidator(result =>
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

    public static RootCommand CreateRootCommand(Func<FileInfo, CancellationToken, Task> handler)
    {
        var rootCommand = new RootCommand();
        rootCommand.AddGlobalOption(SettingsFileOption);
        rootCommand.SetHandler(handler, SettingsFileOption);
        return rootCommand;
    }
}