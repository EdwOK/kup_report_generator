using Spectre.Console;

namespace Helpers.TaskProgress;

public class SpectreConsoleProgressContext(ProgressContext progressContext) : IProgressContext
{
    public IProgressContextTask AddTask(string description, bool autoStart = true, double maxValue = 100)
    {
        var task = progressContext.AddTask(description, autoStart, maxValue);
        return new SpectreConsoleProgressContextTask(task);
    }
}