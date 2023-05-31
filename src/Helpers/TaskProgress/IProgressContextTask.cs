namespace KUPReportGenerator.Helpers.TaskProgress;

public interface IProgressContextTask : IProgress<double>
{
    void Increment(double value);
}