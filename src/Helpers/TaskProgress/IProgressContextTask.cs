namespace Helpers.TaskProgress;

public interface IProgressContextTask : IProgress<double>
{
    void Start();

    void Stop();

    void Increment(double value);
}