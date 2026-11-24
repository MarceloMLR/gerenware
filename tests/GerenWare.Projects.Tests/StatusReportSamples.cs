using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using GerenWare.Projects.Application;
using GerenWare.Projects.Contracts;
using GerenWare.Projects.Domain;
using GerenWare.Projects.Infrastructure.StatusReports;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace GerenWare.Projects.Tests;

/// <summary>The data of a fictitious status report and the ways the tests read the PowerPoint it makes.</summary>
internal static class StatusReportSamples
{
    public const long Emu = 914400;

    public static readonly DateOnly Today = new(2026, 11, 20);

    private static uint _next = 100;

    public static PresentationDocument Render(StatusReportData data) =>
        Open(new PptxStatusReportRenderer().RenderAsync(data, TestContext.Current.CancellationToken).GetAwaiter().GetResult());

    public static PresentationDocument Open(byte[] content) => PresentationDocument.Open(new MemoryStream(content), false);

    public static List<P.Slide> Slides(PresentationDocument document)
    {
        var presentation = document.PresentationPart!;
        return [.. presentation.Presentation!.SlideIdList!.Elements<P.SlideId>()
            .Select(id => ((SlidePart)presentation.GetPartById(id.RelationshipId!)).Slide!)];
    }

    /// <summary>The name of each slide, as the template gives it.</summary>
    public static List<string> NamesOf(PresentationDocument document) =>
        [.. Slides(document).Select(slide => slide.CommonSlideData!.Name!.Value!)];

    public static P.Shape ShapeNamed(OpenXmlElement root, string name) =>
        root.Descendants<P.Shape>().First(shape => NameOf(shape) == name);

    public static bool HasShape(OpenXmlElement root, string name) => root.Descendants<P.Shape>().Any(shape => NameOf(shape) == name);

    public static P.Picture PictureNamed(OpenXmlElement root, string name) =>
        root.Descendants<P.Picture>().First(picture => picture.NonVisualPictureProperties!.NonVisualDrawingProperties!.Name == name);

    public static List<P.Picture> PicturesNamed(OpenXmlElement root, string name) =>
        [.. root.Descendants<P.Picture>().Where(picture => picture.NonVisualPictureProperties!.NonVisualDrawingProperties!.Name == name)];

    /// <summary>The blocks of a list on a slide, in order.</summary>
    public static List<P.GroupShape> Blocks(OpenXmlElement root, string name) =>
        [.. root.Descendants<P.GroupShape>().Where(group => group.NonVisualGroupShapeProperties!.NonVisualDrawingProperties!.Name == name)];

    public static string NameOf(P.Shape shape) => shape.NonVisualShapeProperties!.NonVisualDrawingProperties!.Name!.Value!;

    public static string TextOf(OpenXmlElement root) =>
        string.Join("\n", root.Descendants<A.Paragraph>().Select(paragraph => paragraph.InnerText));

    /// <summary>The solid fill of a shape, as <c>RRGGBB</c>.</summary>
    public static string FillOf(P.Shape shape) => shape.ShapeProperties!.GetFirstChild<A.SolidFill>()!.RgbColorModelHex!.Val!.Value!;

    /// <summary>The color of the stops of a gradient of a shape.</summary>
    public static List<string> GradientOf(P.Shape shape) =>
        [.. shape.ShapeProperties!.GetFirstChild<A.GradientFill>()!.Descendants<A.RgbColorModelHex>().Select(color => color.Val!.Value!)];

    public static (long X, long Y, long Width, long Height) BoxOf(P.Shape shape)
    {
        var transform = shape.ShapeProperties!.Transform2D!;
        return (transform.Offset!.X!, transform.Offset!.Y!, transform.Extents!.Cx!, transform.Extents!.Cy!);
    }

    public static (long X, long Y, long Width, long Height) BoxOf(P.Picture picture)
    {
        var transform = picture.ShapeProperties!.Transform2D!;
        return (transform.Offset!.X!, transform.Offset!.Y!, transform.Extents!.Cx!, transform.Extents!.Cy!);
    }

    public static (long X, long Y) OffsetOf(P.GroupShape group)
    {
        var offset = group.GroupShapeProperties!.TransformGroup!.Offset!;
        return (offset.X!, offset.Y!);
    }

    /// <summary>The SVG of a picture, from the part its <c>svgBlip</c> points to.</summary>
    public static string SvgOf(P.Picture picture)
    {
        var part = picture.Ancestors<P.Slide>().Single().SlidePart!;
        var svg = picture.BlipFill!.Blip!.Descendants().Single(element => element.LocalName == "svgBlip");
        var id = svg.GetAttribute("embed", "http://schemas.openxmlformats.org/officeDocument/2006/relationships").Value!;
        using var reader = new StreamReader(part.GetPartById(id).GetStream());
        return reader.ReadToEnd();
    }

    public static byte[] Png(int width, int height)
    {
        var png = new byte[24];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(png, 0);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(16), (uint)width);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(20), (uint)height);
        return png;
    }

    /// <summary>A template of one slide with the shapes given, for the filling of a template of a company.</summary>
    public static byte[] TemplateWith(params OpenXmlElement[] shapes)
    {
        using var stream = new MemoryStream();
        stream.Write(DefaultStatusReportTemplate.Build(null, null));
        using (var document = PresentationDocument.Open(stream, true))
        {
            var presentation = document.PresentationPart!;
            var ids = presentation.Presentation!.SlideIdList!.Elements<P.SlideId>().ToList();
            foreach (var id in ids.Skip(1))
            {
                var part = presentation.GetPartById(id.RelationshipId!);
                id.Remove();
                presentation.DeletePart(part);
            }

            var slide = ((SlidePart)presentation.GetPartById(ids[0].RelationshipId!)).Slide!;
            var tree = slide.CommonSlideData!.ShapeTree!;
            foreach (var child in tree.ChildElements.Skip(2).ToList())
            {
                child.Remove();
            }

            tree.Append(shapes);
            slide.Save();
            presentation.Presentation.Save();
        }

        return stream.ToArray();
    }

    /// <summary>A shape of a template, in inches, with a text and a fill when given.</summary>
    public static P.Shape Shape(string name, double x, double y, double width, double height, string? text = null, string? fill = null) =>
        new(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = Interlocked.Increment(ref _next), Name = name },
                new P.NonVisualShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.ShapeProperties(
                new A.Transform2D(new A.Offset { X = E(x), Y = E(y) }, new A.Extents { Cx = E(width), Cy = E(height) }),
                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle },
                fill is null ? new A.NoFill() : new A.SolidFill(new A.RgbColorModelHex { Val = fill })),
            new P.TextBody(
                new A.BodyProperties { LeftInset = (int)E(0.3), RightInset = (int)E(0.05) },
                new A.ListStyle(),
                new A.Paragraph(new A.Run(new A.RunProperties { Language = "pt-BR", FontSize = 1000 }, new A.Text(text ?? string.Empty)))));

    /// <summary>A group of shapes of a template, such as a block of a list.</summary>
    public static P.GroupShape Group(string name, double x, double y, double width, double height, params OpenXmlElement[] children)
    {
        var group = new P.GroupShape(
            new P.NonVisualGroupShapeProperties(
                new P.NonVisualDrawingProperties { Id = Interlocked.Increment(ref _next), Name = name },
                new P.NonVisualGroupShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.GroupShapeProperties(new A.TransformGroup(
                new A.Offset { X = E(x), Y = E(y) },
                new A.Extents { Cx = E(width), Cy = E(height) },
                new A.ChildOffset { X = E(x), Y = E(y) },
                new A.ChildExtents { Cx = E(width), Cy = E(height) })));
        group.Append(children);
        return group;
    }

    public static long E(double inches) => (long)Math.Round(inches * Emu, MidpointRounding.AwayFromZero);

    public static StatusReportRiskItem Risk(int number) => new(
        number,
        $"Rejeição {number} na emissão",
        ReportTicketStatus.InProgress,
        ReportTicketPriority.High,
        true,
        new ReportVendorTicket("Fornecedor Fictício", "77812", ReportVendorStatus.InAnalysis),
        RiskLevel.High,
        "Depende do retorno do fornecedor",
        [new StatusReportMilestone("Homologação", new DateOnly(2026, 12, 4), 10, null)],
        0);

    public static ReportHours Hours(decimal? contracted) => new(
        contracted,
        contracted is null ? 0m : 20m,
        180m,
        contracted is null ? null : 40m,
        contracted is null ? ReportHoursState.NoContract : ReportHoursState.Within,
        [new ReportTaskHours(Guid.CreateVersion7(), "Configurar plano de contas", 40m, 32m)],
        6m);

    public static StatusReportData Data()
    {
        var projection = new ScheduleProjection(
            Guid.CreateVersion7(),
            new DateOnly(2026, 11, 20),
            new DateOnly(2026, 11, 25),
            3,
            [
                new ProjectedMilestone(Guid.CreateVersion7(), "Homologação", new DateOnly(2026, 12, 4), new DateOnly(2026, 12, 8), 2),
                new ProjectedMilestone(Guid.CreateVersion7(), "Go-live", new DateOnly(2026, 12, 11), new DateOnly(2026, 12, 15), 2),
            ],
            null);
        List<StatusReportPhase> phases =
        [
            new("Configuração", new DateOnly(2026, 10, 13), new DateOnly(2026, 10, 30), 30, 50),
            new("Integrações", new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 20), 40, 60),
            new("Testes", new DateOnly(2026, 11, 23), new DateOnly(2026, 12, 4), 0, 0),
            new("Implantação", new DateOnly(2026, 12, 7), new DateOnly(2026, 12, 11), 0, null),
        ];
        return new StatusReportData(
            Today,
            new StatusReportBrand("Consultoria Fictícia", null, null, null),
            "ERP-FIN",
            "Implantação do ERP financeiro",
            ProjectStatus.InProgress,
            "Distribuidora Exemplo",
            null,
            ["Marina Costa"],
            new OverviewProgress(62, 70, true),
            new OverviewDeadline(new DateOnly(2026, 12, 4), new DateOnly(2026, 12, 2), 2),
            new StatusReportList<StatusReportPhase>(phases, 0),
            phases,
            [new StatusReportMilestone("Homologação", new DateOnly(2026, 12, 4), 10, null)],
            new StatusReportList<StatusReportLateTask>(
            [
                new("Integrar emissão de NF-e", new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 17), 3, false, projection),
                new("Treinar usuários-chave", new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 27), 0, true, null),
            ], 2),
            7,
            1,
            new StatusReportTexts("Cadastros validados", "Implantação do ERP financeiro", "Homologar", "Aprovar o roteiro"),
            [],
            null);
    }
}
