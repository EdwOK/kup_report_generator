using System.Runtime.InteropServices;
using FluentResults;

namespace KUPReportGenerator.Installer;

public interface IInstallManager
{
    Task<Result<IEnumerable<Release>>> GetReleases(CancellationToken cancellationToken);

    Task<Result> Install(Release release, OSPlatform osPlatform, CancellationToken cancellationToken);
}