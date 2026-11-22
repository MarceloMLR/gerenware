using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GerenWare.Testing;
using GerenWare.Users.Contracts;
using Microsoft.Extensions.DependencyInjection;
using static GerenWare.Api.Tests.ProjectTeam;

namespace GerenWare.Api.Tests;

// Spec: relatorio-de-status (API side), requirements "Relatório em PDF" and "Limite e falha da conversão" (ADR-0165).
public sealed class StatusReportPdfTests(SqlServerFixture sqlServer)
{
    private const string Reports = "Relatórios de status";
    private const string PdfName = "ERP-FIN - Relatório de status - 2026-10-19.pdf";
    private const string PowerPointName = "ERP-FIN - Relatório de status - 2026-10-19.pptx";

    /// <summary>The team of ERP-FIN (<see cref="ProjectTeam"/>) with its files in the simulated SharePoint, or in the local folder.</summary>
    private sealed record Scene(ProjectTeam Team, HttpClient Marina, FakeSharePoint? Site) : IAsyncDisposable
    {
        public string PathOf(Guid project) => $"/api/projects/{project}/status-report";

        public string Path => PathOf(Team.Erp);

        public ValueTask DisposeAsync() => Team.DisposeAsync();
    }

    private async Task<Scene> StartAsync(bool sharePoint = true)
    {
        var site = sharePoint ? new FakeSharePoint() : null;
        var team = site is null
            ? await ProjectTeam.StartAsync(sqlServer)
            : await ProjectTeam.StartAsync(sqlServer, builder =>
            {
                builder.Configuration["FileStorage:Provider"] = "SharePoint";
                builder.Configuration["FileStorage:SharePoint:SiteUrl"] = FakeSharePoint.SiteUrl;
                builder.Services.AddSingleton<IGraphTokens>(new SiteToken());
                builder.Services.AddHttpClient("sharepoint").ConfigurePrimaryHttpMessageHandler(() => site);
            });
        return new Scene(team, await team.ClientOfAsync(team.Marina), site);
    }

    private static object Body(string format, string summary = "Cadastros validados") => new
    {
        summary,
        focus = "Integração fiscal",
        nextSteps = "Homologar a emissão",
        clientNeeds = "Aprovar o roteiro de testes",
        risks = Array.Empty<object>(),
        showHours = false,
        format,
    };

    private static async Task<JsonElement> GeneratedAsync(Scene scene, object body)
    {
        var generated = await SendAsync(scene.Marina, HttpMethod.Post, scene.Path, body);
        generated.StatusCode.Should().Be(HttpStatusCode.OK, await generated.Content.ReadAsStringAsync(Cancellation));
        return await generated.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
    }

    private static async Task<JsonElement> ProblemOfAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);

    private static Task<JsonElement> DraftAsync(Scene scene, Guid? project = null) =>
        scene.Marina.GetFromJsonAsync<JsonElement>(scene.PathOf(project ?? scene.Team.Erp), Cancellation);

    /// <summary>The documents of the folder of the reports of the project, by the name, with the version.</summary>
    private static async Task<List<(string Name, int Version)>> ReportsOfAsync(Scene scene, Guid? project = null)
    {
        var id = project ?? scene.Team.Erp;
        var folders = (await scene.Marina.GetFromJsonAsync<JsonElement>($"/api/projects/{id}/documents/folders", Cancellation))
            .GetProperty("folders").EnumerateArray();
        var reports = folders.FirstOrDefault(folder => folder.GetProperty("isStatusReport").GetBoolean());
        if (reports.ValueKind == JsonValueKind.Undefined)
        {
            return [];
        }

        var items = (await scene.Marina.GetFromJsonAsync<JsonElement>(
            $"/api/projects/{id}/documents?folderId={reports.GetProperty("id").GetGuid()}", Cancellation)).GetProperty("items");
        return [.. items.EnumerateArray().Select(item => (item.GetProperty("name").GetString()!, item.GetProperty("version").GetInt32()))];
    }

    /// <summary>What is in the folder of the reports in the storage of SharePoint, by the name.</summary>
    private static async Task<IReadOnlyList<string>> StoredReportsAsync(Scene scene, Guid? project = null)
    {
        var folder = (await scene.Marina.GetFromJsonAsync<JsonElement>($"/api/projects/{project ?? scene.Team.Erp}/folder", Cancellation))
            .GetProperty("name").GetString();
        return scene.Site!.NamesIn("Documentos", $"{folder}/Documentos/{Reports}");
    }

    [Fact(DisplayName = "PDF guardado na pasta dos relatórios")]
    public async Task Pdf_IsTheOnlyFileKeptInTheFolderOfTheReports()
    {
        await using var scene = await StartAsync();

        var report = await GeneratedAsync(scene, Body("Pdf"));

        (report.GetProperty("name").GetString(), report.GetProperty("folderName").GetString(), report.GetProperty("version").GetInt32())
            .Should().Be((PdfName, Reports, 1));
        (await ReportsOfAsync(scene)).Should().Equal((PdfName, 1));
        (await StoredReportsAsync(scene)).Should().Equal(PdfName);
        var content = await scene.Marina.GetByteArrayAsync(
            $"/api/projects/{scene.Team.Erp}/documents/{report.GetProperty("documentId").GetGuid()}/content?download=true", Cancellation);
        var pdf = Encoding.UTF8.GetString(content);
        pdf.Should().StartWith("%PDF-").And.Contain(".pptx", "the PDF was made from the PowerPoint");
        scene.Site!.Conversions.Should().ContainSingle();
        var items = (await scene.Marina.GetFromJsonAsync<JsonElement>(
            $"/api/projects/{scene.Team.Erp}/documents?folderId={report.GetProperty("folderId").GetGuid()}", Cancellation)).GetProperty("items");
        items.EnumerateArray().Single().GetProperty("updated").GetProperty("name").GetString().Should().Be("Marina Costa");
    }

    [Fact(DisplayName = "PDF gerado de novo no mesmo dia")]
    public async Task PdfGeneratedAgainOnTheSameDay_IsANewVersion()
    {
        await using var scene = await StartAsync();

        var first = await GeneratedAsync(scene, Body("Pdf"));
        var second = await GeneratedAsync(scene, Body("Pdf", "Testes iniciados"));

        second.GetProperty("documentId").GetGuid().Should().Be(first.GetProperty("documentId").GetGuid());
        second.GetProperty("version").GetInt32().Should().Be(2);
        (await ReportsOfAsync(scene)).Should().Equal((PdfName, 2));
        (await StoredReportsAsync(scene)).Should().Equal(PdfName);
    }

    [Fact(DisplayName = "Formato do último relatório")]
    public async Task Draft_ComesInTheFormatOfTheLastReport()
    {
        await using var scene = await StartAsync();
        var first = await DraftAsync(scene);

        await GeneratedAsync(scene, Body("Pdf"));
        var next = await DraftAsync(scene);
        await GeneratedAsync(scene, Body("PowerPoint"));
        var after = await DraftAsync(scene);

        (first.GetProperty("format").GetString(), first.GetProperty("pdfAvailable").GetBoolean()).Should().Be(("PowerPoint", true));
        next.GetProperty("format").GetString().Should().Be("Pdf");
        after.GetProperty("format").GetString().Should().Be("PowerPoint");
        (await ReportsOfAsync(scene)).Should().BeEquivalentTo([(PdfName, 1), (PowerPointName, 1)], "each format is a document of its own");
    }

    [Fact(DisplayName = "PDF sem o SharePoint")]
    public async Task Pdf_IsRefused_WhereTheFilesAreInTheLocalFolder()
    {
        await using var scene = await StartAsync(sharePoint: false);

        var draft = await DraftAsync(scene);
        var refused = await SendAsync(scene.Marina, HttpMethod.Post, scene.Path, Body("Pdf"));
        var withoutFormat = await SendAsync(scene.Marina, HttpMethod.Post, scene.Path, new { summary = "Sem formato", risks = Array.Empty<object>() });

        (draft.GetProperty("format").GetString(), draft.GetProperty("pdfAvailable").GetBoolean()).Should().Be(("PowerPoint", false));
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ProblemOfAsync(refused)).GetProperty("errors").TryGetProperty("format", out _).Should().BeTrue();
        withoutFormat.StatusCode.Should().Be(HttpStatusCode.OK, "without the format, it is the PowerPoint");
        (await ReportsOfAsync(scene)).Should().Equal((PowerPointName, 1));
    }

    [Fact(DisplayName = "PowerPoint grande demais para o PDF")]
    public async Task APowerPointLargerThanTheConversionTakes_IsRefused_BeforeAnythingIsKept()
    {
        await using var scene = await StartAsync();
        var project = await ProjectWithLargeLogosAsync(scene);

        var refused = await SendAsync(scene.Marina, HttpMethod.Post, scene.PathOf(project), Body("Pdf"));

        refused.StatusCode.Should().Be(HttpStatusCode.Conflict, await refused.Content.ReadAsStringAsync(Cancellation));
        var problem = await ProblemOfAsync(refused);
        problem.GetProperty("type").GetString().Should().Be("urn:gerenware:problem:status-report-too-large-for-pdf");
        problem.GetProperty("maxBytes").GetInt64().Should().Be(2 * 1024 * 1024);
        (await ReportsOfAsync(scene, project)).Should().BeEmpty();
        scene.Site!.Conversions.Should().BeEmpty();
        (await DraftAsync(scene, project)).GetProperty("last").ValueKind.Should().Be(JsonValueKind.Null);
        var powerPoint = await SendAsync(scene.Marina, HttpMethod.Post, scene.PathOf(project), Body("PowerPoint"));
        powerPoint.StatusCode.Should().Be(HttpStatusCode.OK, "the same report goes out in PowerPoint");
    }

    [Fact(DisplayName = "Conversão que falha")]
    public async Task AConversionThatFails_KeepsNothing()
    {
        await using var scene = await StartAsync();
        await GeneratedAsync(scene, Body("PowerPoint"));
        scene.Site!.ConversionFailure = (HttpStatusCode.InternalServerError, "generalException");

        var failed = await SendAsync(scene.Marina, HttpMethod.Post, scene.Path, Body("Pdf", "Outro resumo"));

        failed.StatusCode.Should().Be(HttpStatusCode.BadGateway, await failed.Content.ReadAsStringAsync(Cancellation));
        (await ProblemOfAsync(failed)).GetProperty("type").GetString().Should().Be("urn:gerenware:problem:status-report-pdf-failed");
        (await ReportsOfAsync(scene)).Should().Equal((PowerPointName, 1));
        (await StoredReportsAsync(scene)).Should().Equal(PowerPointName);
        var draft = await DraftAsync(scene);
        (draft.GetProperty("summary").GetString(), draft.GetProperty("format").GetString()).Should().Be(("Cadastros validados", "PowerPoint"));
    }

    /// <summary>
    /// A project of a client whose logo, as the one of the installation, has 1 MB that does not compress, so the
    /// PowerPoint with the two passes the 2 MB of the conversion.
    /// </summary>
    private static async Task<Guid> ProjectWithLargeLogosAsync(Scene scene)
    {
        var admin = scene.Team.Admin;
        var installation = await SendFileAsync(admin, "/api/installation/logo", LargePng(seed: 1), "marca.png");
        installation.StatusCode.Should().Be(HttpStatusCode.NoContent, await installation.Content.ReadAsStringAsync(Cancellation));
        var added = await SendAsync(admin, HttpMethod.Post, "/api/clients", new { name = "Kappa Energia", cnpj = (string?)null, observation = (string?)null });
        added.StatusCode.Should().Be(HttpStatusCode.Created, await added.Content.ReadAsStringAsync(Cancellation));
        var client = (await added.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("id").GetGuid();
        var logo = await SendFileAsync(admin, $"/api/clients/{client}/logo", LargePng(seed: 2), "kappa.png");
        logo.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
        var created = await SendAsync(admin, HttpMethod.Post, "/api/projects", new
        {
            code = "FAT-02",
            name = "Faturamento",
            managerId = scene.Team.Marina,
            techLeadId = scene.Team.Rafael,
            clientId = client,
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync(Cancellation));
        return (await created.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("id").GetGuid();
    }

    /// <summary>The header of a PNG of 400 by 100 and then bytes at random up to 1 MB, which a zip does not shrink.</summary>
    private static byte[] LargePng(int seed)
    {
        var content = new byte[1024 * 1024];
        new Random(seed).NextBytes(content);
        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(content, 0);
        BinaryPrimitives.WriteUInt32BigEndian(content.AsSpan(8), 13);
        "IHDR"u8.CopyTo(content.AsSpan(12));
        BinaryPrimitives.WriteUInt32BigEndian(content.AsSpan(16), 400);
        BinaryPrimitives.WriteUInt32BigEndian(content.AsSpan(20), 100);
        return content;
    }

    private static async Task<HttpResponseMessage> SendFileAsync(HttpClient client, string path, byte[] content, string fileName)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, path);
        request.Headers.Add("X-CSRF", "1");
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        request.Content = new MultipartFormDataContent { { file, "file", fileName } };
        return await client.SendAsync(request, Cancellation);
    }

    private sealed class SiteToken : IGraphTokens
    {
        public Task<string> GetAsync(CancellationToken cancellationToken) => Task.FromResult(FakeSharePoint.Token);
    }
}
