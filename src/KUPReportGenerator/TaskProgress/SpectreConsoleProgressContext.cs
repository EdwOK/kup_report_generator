using Spectre.Console;

namespace KUPReportGenerator.TaskProgress;

internal class SpectreConsoleProgressContext : IProgressContext
{
    private readonly ProgressContext _progressContext;

    public SpectreConsoleProgressContext(ProgressContext progressContext) =>
        _progressContext = progressContext;

    public IProgressContextTask AddTask(string description, bool autoStart = true, double maxValue = 100)
    {
        var task = _progressContext.AddTask(description, autoStart, maxValue);
        return new SpectreConsoleProgressContextTask(task);
    }
}