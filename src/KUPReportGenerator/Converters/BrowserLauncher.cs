using PuppeteerSharp;
using PuppeteerSharp.BrowserData;

namespace KUPReportGenerator.Converters;

internal static class BrowserLauncher
{
    public static async Task<IBrowser> Launch()
    {
        var installedBrowser = await GetInstalledBrowser();
        var executablePath = installedBrowser.GetExecutablePath();

        return await Puppeteer.LaunchAsync(new LaunchOptions
        {
            ExecutablePath = executablePath,
            Headless = true,
        });
    }

    private static async Task<InstalledBrowser> GetInstalledBrowser()
    {
        using var browserFetcher = new BrowserFetcher(SupportedBrowser.Chrome);

        var installedBrowser = browserFetcher.GetInstalledBrowsers()
            .FirstOrDefault(b => b.Browser is SupportedBrowser.Chrome);

        return installedBrowser ?? await browserFetcher.DownloadAsync(BrowserTag.Stable);
    }
}