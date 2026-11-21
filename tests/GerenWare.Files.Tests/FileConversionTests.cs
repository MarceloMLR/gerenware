using System.Net;
using System.Text;
using GerenWare.Files.Contracts;
using GerenWare.Files.Infrastructure;
using GerenWare.Testing;
using GerenWare.Users.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;

namespace GerenWare.Files.Tests;

/// <summary>The conversion of a stored file into PDF (ADR-0165): by Microsoft Graph in SharePoint, never in the local folder.</summary>
// Spec: armazenamento-de-arquivos, requirement "Conversão em PDF".
public sealed class FileConversionTests
{
    private readonly FakeSharePoint _site = new();
    private readonly SharePointFileStorage _storage;

    public FileConversionTests()
    {
        _storage = new SharePointFileStorage(new Clients(_site), new FixedTokens(), new SharePointStorageOptions { SiteUrl = FakeSharePoint.SiteUrl });
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact(DisplayName = "Converter um PowerPoint no SharePoint")]
    public async Task AStoredPowerPoint_ComesBackAsPdf_WithNothingNewInItsFolder()
    {
        var folder = await _storage.EnsureFolderAsync(null, "ERP-FIN - Implantação/Relatórios de status", Cancellation);
        var stored = await UploadAsync(folder.Id, "ERP-FIN - Relatório de status - 2026-11-21.pptx", 300 * 1024);

        _storage.ConvertsToPdf.Should().BeTrue();
        await using var pdf = await _storage.ToPdfAsync(stored.Id, Cancellation);

        pdf.Should().NotBeNull();
        pdf!.Name.Should().Be("ERP-FIN - Relatório de status - 2026-11-21.pdf");
        pdf.ContentType.Should().Be("application/pdf");
        var content = await ReadAsync(pdf);
        content.Should().Equal(FakeSharePoint.PdfOf(stored.Name, 300 * 1024));
        pdf.Size.Should().Be(content.Length);
        _site.Conversions.Should().Equal(stored.Id);
        _site.NamesIn("Documentos", "ERP-FIN - Implantação/Relatórios de status").Should().Equal(stored.Name);
    }

    [Fact(DisplayName = "Arquivo acima do limite da conversão")]
    public async Task AFileLargerThanTheLimit_IsRefused_WithoutAskingGraph()
    {
        var folder = await _storage.EnsureFolderAsync(null, "Relatórios de status", Cancellation);
        var stored = await UploadAsync(folder.Id, "Grande.pptx", 3 * 1024 * 1024);

        var convert = () => _storage.ToPdfAsync(stored.Id, Cancellation);

        (await convert.Should().ThrowAsync<FileConversionException>()).Which.Failure.Should().Be(FileConversionFailure.TooLarge);
        _site.Conversions.Should().BeEmpty();
    }

    [Fact(DisplayName = "Conversão recusada pelo Graph")]
    public async Task ARefusalOfGraph_SaysItsStatusAndCode_AfterTheThrottlingIsWaited()
    {
        var folder = await _storage.EnsureFolderAsync(null, "Relatórios de status", Cancellation);
        var stored = await UploadAsync(folder.Id, "ERP-FIN - Relatório de status - 2026-11-21.pptx", 1024);
        _site.ConversionFailure = (HttpStatusCode.NotAcceptable, "notSupported");
        _site.ThrottleNext = 2;

        var convert = () => _storage.ToPdfAsync(stored.Id, Cancellation);

        var refusal = (await convert.Should().ThrowAsync<FileConversionException>()).Which;
        refusal.Failure.Should().Be(FileConversionFailure.Refused);
        refusal.Status.Should().Be(406);
        refusal.Code.Should().Be("notSupported");
        refusal.Message.Should().NotContain("ERP-FIN", "the failure never says the names of the files");
        _site.Conversions.Should().Equal(stored.Id);
    }

    [Fact(DisplayName = "PDF de um arquivo que não existe")]
    public async Task TheFileMissing_HasNoPdf()
    {
        await _storage.EnsureFolderAsync(null, "Relatórios de status", Cancellation);
        var outside = _site.AddFile("Arquivo morto", string.Empty, "De fora.pptx", Encoding.UTF8.GetBytes("de fora"));

        (await _storage.ToPdfAsync(outside, Cancellation)).Should().BeNull();
        (await _storage.ToPdfAsync("01UNKNOWNITEM", Cancellation)).Should().BeNull();
        _site.Conversions.Should().BeEmpty();
    }

    [Fact(DisplayName = "Sem conversão na implementação local")]
    public async Task TheLocalStorage_DoesNotConvert()
    {
        var root = Path.Combine(Path.GetTempPath(), "gerenware-conversion-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var services = ServicesOf(new()
            {
                ["FileStorage:Provider"] = "Local",
                ["FileStorage:Local:RootPath"] = root,
            });
            var conversion = services.GetRequiredService<IFileConversion>();
            var storage = services.GetRequiredService<IFileStorage>();
            var folder = await storage.EnsureFolderAsync(null, "Relatórios de status", Cancellation);
            await using var content = new MemoryStream(Encoding.UTF8.GetBytes("powerpoint"));
            var stored = await storage.UploadAsync(folder.Id, "Relatório.pptx", content, SameNameAction.NewVersion, Cancellation);

            conversion.ConvertsToPdf.Should().BeFalse();
            var convert = () => conversion.ToPdfAsync(stored.Id, Cancellation);
            await convert.Should().ThrowAsync<NotSupportedException>();
            (await storage.ListFoldersAsync(null, Cancellation)).Select(item => item.Name).Should().Equal("Relatórios de status");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact(DisplayName = "Conversão do SharePoint configurado")]
    public void TheConfiguredSharePoint_IsTheStorageThatConverts()
    {
        using var services = ServicesOf(new()
        {
            ["FileStorage:Provider"] = "SharePoint",
            ["FileStorage:SharePoint:SiteUrl"] = FakeSharePoint.SiteUrl,
        });

        var conversion = services.GetRequiredService<IFileConversion>();

        conversion.ConvertsToPdf.Should().BeTrue();
        conversion.Should().BeSameAs(services.GetRequiredService<IFileStorage>());
    }

    private static ServiceProvider ServicesOf(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new HostingEnvironment
        {
            EnvironmentName = Environments.Development,
            ContentRootPath = Path.GetTempPath(),
        });
        services.AddSingleton<IGraphTokens>(new FixedTokens());
        services.AddFilesModule(configuration);
        return services.BuildServiceProvider();
    }

    private async Task<StoredFile> UploadAsync(string folderId, string name, int size)
    {
        await using var content = new MemoryStream(new byte[size]);
        return await _storage.UploadAsync(folderId, name, content, SameNameAction.NewVersion, Cancellation);
    }

    private static async Task<byte[]> ReadAsync(FileDownload download)
    {
        using var buffer = new MemoryStream();
        await download.Content.CopyToAsync(buffer, Cancellation);
        return buffer.ToArray();
    }

    private sealed class Clients(FakeSharePoint site) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(site, disposeHandler: false);
    }

    private sealed class FixedTokens : IGraphTokens
    {
        public Task<string> GetAsync(CancellationToken cancellationToken) => Task.FromResult(FakeSharePoint.Token);
    }
}
