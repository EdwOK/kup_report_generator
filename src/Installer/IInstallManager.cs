using System.Runtime.InteropServices;
using FluentResults;

namespace KUPReportGenerator.Installer;

public interface IInstallManager
{
    Task<IEnumerable<Release>> GetReleases(CancellationToken cancellationToken);

    Task<Result> Install(Release release, OSPlatform osPlatform, CancellationToken cancellationToken);
}