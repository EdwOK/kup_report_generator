using System.Threading;
using System.Threading.Tasks;

namespace KUPReportGenerator.Helpers
{
    internal static class TaskExtensions
    {
        public static async Task WithCancellation(this Task task, CancellationToken cancellationToken)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            try
            {
                var waitOnCancellation = Task.Delay(Timeout.Infinite, cts.Token);
                await Task.WhenAny(task, waitOnCancellation).Unwrap().ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    cts.Cancel();
                }
                catch
                {
                }
            }
        }

        public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
        {
            await WithCancellation((Task)task, cancellationToken);
            return task.Result;
        }
    }
}
