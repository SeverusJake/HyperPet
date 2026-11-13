using HyperPet.App.Update;
using Velopack;
using Velopack.Logging;
using Velopack.Sources;

namespace HyperPet.App.Tests.Update;

public class UpdateSourceTests
{
    [Fact]
    public void ProductionSource_UsesPublicReleaseAssetsWithoutGithubApi()
    {
        var source = Assert.IsType<SimpleWebSource>(UpdateService.CreateSource());
        Assert.Equal("https://github.com/SeverusJake/HyperPet/releases/latest/download/", source.BaseUri.AbsoluteUri);
    }

    [Fact]
    public async Task ReadFeed_WhenGithubApiIsRateLimited_UsesPublicManifest()
    {
        var downloader = new RecordingDownloader();
        var feed = await UpdateService.CreateSource(downloader).GetReleaseFeed(NullVelopackLogger.Instance, "HyperPet", "win", null, null);
        Assert.Equal(2, feed.Assets.Length);
        Assert.Equal("0.7.3", feed.Assets[0].Version.ToString());
        var uri = new Uri(Assert.Single(downloader.Urls));
        Assert.Equal("github.com", uri.Host);
        Assert.Equal("/SeverusJake/HyperPet/releases/latest/download/releases.win.json", uri.AbsolutePath);
    }

    [Theory]
    [InlineData("Full", "HyperPet-0.7.3-full.nupkg")]
    [InlineData("Delta", "HyperPet-0.7.3-delta.nupkg")]
    public async Task DownloadPackage_WhenGithubApiIsRateLimited_UsesPublicAsset(string type, string name)
    {
        var downloader = new RecordingDownloader();
        var source = UpdateService.CreateSource(downloader);
        var feed = await source.GetReleaseFeed(NullVelopackLogger.Instance, "HyperPet", "win", null, null);
        downloader.Urls.Clear();
        var asset = Assert.Single(feed.Assets.Where(a => a.Type.ToString() == type));
        await source.DownloadReleaseEntry(NullVelopackLogger.Instance, asset, "unused-test-target", _ => { }, CancellationToken.None);
        var uri = new Uri(Assert.Single(downloader.Urls));
        Assert.Equal("github.com", uri.Host);
        Assert.Equal("/SeverusJake/HyperPet/releases/latest/download/" + name, uri.AbsolutePath);
    }

    private sealed class RecordingDownloader : IFileDownloader
    {
        public List<string> Urls { get; } = new();
        private void Record(string url)
        {
            Urls.Add(url);
            if (new Uri(url).Host == "api.github.com")
                throw new System.Net.Http.HttpRequestException("403 (rate limit exceeded)", null, System.Net.HttpStatusCode.Forbidden);
        }
        public Task<string> DownloadString(string url, IDictionary<string, string>? headers = null, double timeout = 30)
        {
            Record(url);
            return Task.FromResult("""
                {"Assets":[
                  {"PackageId":"HyperPet","Version":"0.7.3","Type":"Full","FileName":"HyperPet-0.7.3-full.nupkg","Size":100},
                  {"PackageId":"HyperPet","Version":"0.7.3","Type":"Delta","FileName":"HyperPet-0.7.3-delta.nupkg","Size":10}
                ]}
                """);
        }
        public Task<byte[]> DownloadBytes(string url, IDictionary<string, string>? headers = null, double timeout = 30) =>
            throw new InvalidOperationException("Unexpected byte download");
        public Task DownloadFile(string url, string targetFile, Action<int>? progress = null,
            IDictionary<string, string>? headers = null, double timeout = 30, CancellationToken cancelToken = default)
        {
            Record(url);
            return Task.CompletedTask;
        }
    }
}
