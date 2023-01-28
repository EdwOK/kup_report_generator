namespace KUPReportGenerator.TaskProgress;

public interface IProgressContextTask : IProgress<double>
{
    void Increment(double value);
}