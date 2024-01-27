using Spectre.Console;

namespace Helpers.TaskProgress;

public class SpectreConsoleProgressContextTask : IProgressContextTask
{
    private readonly ProgressTask _task;
    private readonly IProgress<double> _progress;

    public SpectreConsoleProgressContextTask(ProgressTask task)
    {
        _task = task;
        _progress = _task;
    }

    public void Start() => _task.StartTask();

    public void Stop() => _task.StopTask();

    public void Increment(double value) => _task.Increment(value);

    public void Report(double value) => _progress.Report(value);
}