namespace KUPReportGenerator.TaskProgress;

public interface IProgressContext
{
    IProgressContextTask AddTask(string description, bool autoStart = true, double maxValue = 100);
}