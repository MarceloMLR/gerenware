using System.Buffers.Binary;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace GerenWare.Projects.Infrastructure.StatusReports;

/// <summary>
/// Fills a template of the status report with the values (ADR-0165, ADR-0168), the same for the default template and,
/// later, the one of the company:
/// <list type="bullet">
/// <item>a field between double braces (<c>{{projeto.nome}}</c>) takes the value with the format of where it starts,
/// also when the PowerPoint kept it in pieces, and a line break of the value makes a new paragraph;</item>
/// <item>a group of shapes named <c>lista.{nome}</c> repeats for each item of the list, down by its height or, with
/// <c>.lado</c>, to the side by its width, and a row of a table with fields of a list (<see cref="StatusReportFields.Lists"/>)
/// repeats in the table; an empty list leaves the slide;</item>
/// <item>the slide with fields of a risk repeats for each risk and leaves without risks, and the slide with fields of
/// the hours leaves without "Mostrar horas";</item>
/// <item>a bar takes the share of its track, a mark goes to its point of the track or leaves, and a tag takes the
/// colors and the icon of its tone, light or dark by the background the template drew it with; a tag left without
/// text leaves;</item>
/// <item>a shape of a logo becomes the picture, fitted in it and aligned as its text, in its frame only when it needs
/// one, or keeps its text without the logo;</item>
/// <item>a text that shrinks on overflow takes the scale its lines need, from 100% to 80%.</item>
/// </list>
/// </summary>
internal static partial class StatusReportFiller
{
    private const string SvgExtension = "{96DAC541-7B7A-43D3-8B79-37D633B846F1}";
    private const string SvgNamespace = "http://schemas.microsoft.com/office/drawing/2016/SVG/main";
    private const string RelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const long Emu = 914400;

    /// <summary>The smallest scale of a text that shrinks on overflow (ADR-0168).</summary>
    public const int MinimumFontScale = 80000;

    /// <summary>A PNG of one transparent pixel: the picture the format asks beside an SVG, for older versions.</summary>
    private static readonly byte[] EmptyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    [GeneratedRegex(@"\{\{\s*([A-Za-z0-9.]+)\s*\}\}")]
    private static partial Regex Field();

    /// <summary>The template filled with the values, as a new file.</summary>
    public static byte[] Fill(byte[] template, StatusReportValues values)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(values);
        using var stream = new MemoryStream();
        stream.Write(template);
        using (var document = PresentationDocument.Open(stream, true))
        {
            var presentation = document.PresentationPart ?? throw new InvalidOperationException("The template has no presentation.");
            var slideIds = presentation.Presentation?.SlideIdList?.Elements<P.SlideId>().ToList() ?? [];
            foreach (var slideId in slideIds)
            {
                var slide = (SlidePart)presentation.GetPartById(slideId.RelationshipId!);
                var fields = FieldsOf(slide.Slide!);
                if (fields.Any(field => StatusReportFields.PrefixOf(field) == StatusReportFields.Hours) && !values.ShowHours)
                {
                    Remove(presentation, slideId, slide);
                }
                else if (fields.Any(field => StatusReportFields.PrefixOf(field) == StatusReportFields.Risk))
                {
                    FillRisks(presentation, slideId, slide, values);
                }
                else
                {
                    FillSlide(slide, new Lookup([values.Report]), values.Colors);
                }
            }

            presentation.Presentation!.Save();
        }

        return stream.ToArray();
    }

    /// <summary>The names of the fields of an element, also of the ones kept in pieces.</summary>
    public static IReadOnlyList<string> FieldsOf(OpenXmlElement root) =>
        [.. root.Descendants<A.Paragraph>()
            .SelectMany(paragraph => Field().Matches(string.Concat(paragraph.Elements<A.Run>().Select(run => run.Text?.Text))))
            .Select(match => match.Groups[1].Value)];

    /// <summary>
    /// Puts an image in a picture of a slide: the PNG or the JPG, or the SVG with its PNG for the versions of the
    /// PowerPoint that do not show SVG (a transparent pixel when it has none).
    /// </summary>
    public static void Embed(SlidePart part, A.Blip blip, ReportPicture picture)
    {
        ArgumentNullException.ThrowIfNull(part);
        ArgumentNullException.ThrowIfNull(blip);
        ArgumentNullException.ThrowIfNull(picture);
        if (picture.ContentType == "image/svg+xml")
        {
            blip.Embed = Image(part, picture.Fallback ?? EmptyPng, ImagePartType.Png);
            var svg = new OpenXmlUnknownElement("asvg", "svgBlip", SvgNamespace);
            svg.SetAttribute(new OpenXmlAttribute("r", "embed", RelationshipNamespace, Image(part, picture.Content, ImagePartType.Svg)));
            blip.Append(new A.BlipExtensionList(new A.BlipExtension(svg) { Uri = SvgExtension }));
        }
        else
        {
            blip.Embed = Image(part, picture.Content, picture.ContentType == "image/png" ? ImagePartType.Png : ImagePartType.Jpeg);
        }
    }

    /// <summary>
    /// The scale of the font a text needs to fit in its box, from 100% to the smallest, in steps of 2.5%, by an
    /// estimate of its lines: half of the size for each character and the lines filled to 92%, as a careful guess.
    /// </summary>
    public static int FontScaleOf(IReadOnlyList<(int Characters, double Size, double Spacing)> paragraphs, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(paragraphs);
        for (var scale = 100000; scale > MinimumFontScale; scale -= 2500)
        {
            if (HeightOf(paragraphs, width, scale / 100000.0) <= height)
            {
                return scale;
            }
        }

        return MinimumFontScale;
    }

    private static double HeightOf(IReadOnlyList<(int Characters, double Size, double Spacing)> paragraphs, double width, double scale) =>
        paragraphs.Sum(paragraph =>
        {
            var size = paragraph.Size * scale / 72.0;
            var perLine = Math.Max(1, Math.Floor(width / (size * 0.5) * 0.92));
            var lines = Math.Max(1, Math.Ceiling(paragraph.Characters / perLine));
            return lines * size * 1.2 * paragraph.Spacing;
        });

    private static void FillRisks(PresentationPart presentation, P.SlideId slideId, SlidePart slide, StatusReportValues values)
    {
        if (values.Risks.Count == 0)
        {
            Remove(presentation, slideId, slide);
            return;
        }

        // The copies are made from the slide as the template has it, before any is filled.
        var slides = new List<SlidePart> { slide };
        var previous = slideId;
        foreach (var _ in values.Risks.Skip(1))
        {
            var (copy, copyId) = Copy(presentation, slide, previous);
            slides.Add(copy);
            previous = copyId;
        }

        foreach (var (part, risk) in slides.Zip(values.Risks))
        {
            FillSlide(part, new Lookup([risk, values.Report]), values.Colors);
        }
    }

    private static (SlidePart Part, P.SlideId Id) Copy(PresentationPart presentation, SlidePart source, P.SlideId after)
    {
        var copy = presentation.AddNewPart<SlidePart>();
        using (var content = source.GetStream(FileMode.Open, FileAccess.Read))
        {
            copy.FeedData(content);
        }

        foreach (var related in source.Parts)
        {
            copy.AddPart(related.OpenXmlPart, related.RelationshipId);
        }

        var list = presentation.Presentation!.SlideIdList!;
        var id = new P.SlideId
        {
            Id = list.Elements<P.SlideId>().Max(item => item.Id!.Value) + 1,
            RelationshipId = presentation.GetIdOfPart(copy),
        };
        after.InsertAfterSelf(id);
        return (copy, id);
    }

    private static void Remove(PresentationPart presentation, P.SlideId slideId, SlidePart slide)
    {
        slideId.Remove();
        presentation.DeletePart(slide);
    }

    private static void FillSlide(SlidePart part, Lookup lookup, ReportColors colors)
    {
        var slide = part.Slide!;
        var blocks = FillBlocks(part, slide, lookup, colors);
        FillTables(slide, lookup);
        Fill(part, slide, lookup, colors, blocks);
        slide.Save();
    }

    /// <summary>Repeats each block for the items of its list, each filled with its item; the blocks filled.</summary>
    private static HashSet<P.GroupShape> FillBlocks(SlidePart part, P.Slide slide, Lookup lookup, ReportColors colors)
    {
        var filled = new HashSet<P.GroupShape>();
        foreach (var group in slide.Descendants<P.GroupShape>().ToList())
        {
            var name = NameOf(group);
            if (!name.StartsWith(StatusReportFields.Block, StringComparison.Ordinal) || group.Ancestors<P.GroupShape>().Any(filled.Contains))
            {
                continue;
            }

            var sideways = name.EndsWith(StatusReportFields.Sideways, StringComparison.Ordinal);
            var list = name[StatusReportFields.Block.Length..^(sideways ? StatusReportFields.Sideways.Length : 0)];
            var items = lookup.List(list);
            if (items.Count == 0)
            {
                group.Remove();
                continue;
            }

            var pristine = (P.GroupShape)group.CloneNode(true);
            var extents = group.GroupShapeProperties?.TransformGroup?.Extents;
            var step = (sideways ? extents?.Cx?.Value : extents?.Cy?.Value) ?? 0;
            var copies = new List<P.GroupShape> { group };
            for (var index = 1; index < items.Count; index++)
            {
                var copy = (P.GroupShape)pristine.CloneNode(true);
                if (copy.GroupShapeProperties?.TransformGroup?.Offset is { } offset)
                {
                    offset.X = offset.X!.Value + (sideways ? step * index : 0);
                    offset.Y = offset.Y!.Value + (sideways ? 0 : step * index);
                }

                copies[^1].InsertAfterSelf(copy);
                Renumber(slide, copy);
                copies.Add(copy);
            }

            foreach (var (copy, item) in copies.Zip(items))
            {
                Fill(part, copy, lookup.With(item), colors, []);
                filled.Add(copy);
            }
        }

        return filled;
    }

    /// <summary>Gives the shapes of a copy new ids, after the largest of the slide, so the ids of the template stay.</summary>
    private static void Renumber(P.Slide slide, OpenXmlElement copy)
    {
        var next = slide.Descendants<P.NonVisualDrawingProperties>().Max(properties => properties.Id?.Value ?? 0) + 1;
        foreach (var properties in copy.Descendants<P.NonVisualDrawingProperties>())
        {
            properties.Id = next++;
        }
    }

    /// <summary>Fills the shapes of a slide or a block, but the ones inside the blocks already filled.</summary>
    private static void Fill(SlidePart part, OpenXmlElement root, Lookup lookup, ReportColors colors, HashSet<P.GroupShape> skip)
    {
        bool Outside(OpenXmlElement element) => !element.Ancestors<P.GroupShape>().Any(skip.Contains);

        FillText(root, lookup);
        foreach (var shape in root.Descendants<P.Shape>().Where(Outside).ToList())
        {
            var name = NameOf(shape);
            if (shape.Parent is null)
            {
                continue;
            }

            if (name.StartsWith(StatusReportFields.Tag, StringComparison.Ordinal) && !name.EndsWith(".icone", StringComparison.Ordinal))
            {
                Tag(part, root, shape, name, lookup.Tag(name), colors);
            }
            else if (name.StartsWith(StatusReportFields.Mark, StringComparison.Ordinal))
            {
                PlaceMark(root, shape, name, lookup.Mark(name));
            }
            else if (lookup.Bar(name) is { } bar)
            {
                SizeBar(root, shape, name, bar, colors);
            }
        }

        foreach (var shape in root.Descendants<P.Shape>().Where(Outside).ToList())
        {
            var name = NameOf(shape);
            if (lookup.Picture(name) is { } picture)
            {
                Picture(part, root, shape, name, picture);
            }
            else if (name.EndsWith(".quadro", StringComparison.Ordinal) && lookup.Picture(name[..^".quadro".Length]) is not { Framed: true })
            {
                shape.Remove();
            }
        }

        foreach (var shape in root.Descendants<P.Shape>().Where(Outside).ToList())
        {
            Scale(shape);
        }
    }

    private static void FillTables(P.Slide slide, Lookup lookup)
    {
        foreach (var frame in slide.Descendants<P.GraphicFrame>().ToList())
        {
            if (frame.Descendants<A.Table>().FirstOrDefault() is not { } table)
            {
                continue;
            }

            foreach (var row in table.Elements<A.TableRow>().ToList())
            {
                var list = FieldsOf(row).Select(StatusReportFields.PrefixOf).FirstOrDefault(StatusReportFields.Lists.Contains);
                if (list is null)
                {
                    continue;
                }

                var items = lookup.List(list);
                if (items.Count == 0)
                {
                    frame.Remove();
                    break;
                }

                foreach (var item in items)
                {
                    var copy = (A.TableRow)row.CloneNode(true);
                    FillText(copy, lookup.With(item));
                    row.InsertBeforeSelf(copy);
                }

                row.Remove();
            }
        }
    }

    private static void FillText(OpenXmlElement root, Lookup lookup)
    {
        foreach (var paragraph in root.Descendants<A.Paragraph>().ToList())
        {
            if (Replace(paragraph, lookup))
            {
                SplitLines(paragraph);
            }
        }
    }

    /// <summary>
    /// Puts the value of each field of the paragraph in the piece of text where the field starts, with its format, and
    /// empties the other pieces of the field; whether the paragraph had any field.
    /// </summary>
    private static bool Replace(A.Paragraph paragraph, Lookup lookup)
    {
        var runs = paragraph.Elements<A.Run>().ToList();
        var texts = runs.Select(run => run.Text?.Text ?? string.Empty).ToList();
        var matches = Field().Matches(string.Concat(texts));
        if (matches.Count == 0)
        {
            return false;
        }

        for (var index = matches.Count - 1; index >= 0; index--)
        {
            var match = matches[index];
            var value = lookup.Field(match.Groups[1].Value) ?? string.Empty;
            var (startRun, startOffset) = Locate(texts, match.Index);
            var (endRun, endOffset) = Locate(texts, match.Index + match.Length - 1);
            if (startRun == endRun)
            {
                texts[startRun] = texts[startRun][..startOffset] + value + texts[startRun][(endOffset + 1)..];
                continue;
            }

            texts[startRun] = texts[startRun][..startOffset] + value;
            for (var middle = startRun + 1; middle < endRun; middle++)
            {
                texts[middle] = string.Empty;
            }

            texts[endRun] = texts[endRun][(endOffset + 1)..];
        }

        for (var index = 0; index < runs.Count; index++)
        {
            if (texts[index].Length == 0 && index > 0)
            {
                runs[index].Remove();
            }
            else
            {
                runs[index].Text ??= new A.Text();
                runs[index].Text!.Text = texts[index];
            }
        }

        return true;
    }

    private static (int Run, int Offset) Locate(List<string> texts, int position)
    {
        var start = 0;
        for (var index = 0; index < texts.Count; index++)
        {
            if (position < start + texts[index].Length)
            {
                return (index, position - start);
            }

            start += texts[index].Length;
        }

        return (texts.Count - 1, texts[^1].Length);
    }

    /// <summary>A paragraph whose text has line breaks becomes one paragraph for each line, with the same format.</summary>
    private static void SplitLines(A.Paragraph paragraph)
    {
        if (!paragraph.Elements<A.Run>().Any(run => run.Text?.Text.Contains('\n', StringComparison.Ordinal) == true))
        {
            return;
        }

        var lines = new List<A.Paragraph>();
        var current = EmptyCopy(paragraph);
        foreach (var child in paragraph.ChildElements)
        {
            if (child is A.ParagraphProperties or A.EndParagraphRunProperties)
            {
                continue;
            }

            if (child is not A.Run run || run.Text?.Text.Contains('\n', StringComparison.Ordinal) != true)
            {
                Append(current, child.CloneNode(true));
                continue;
            }

            var parts = run.Text.Text.Split('\n');
            for (var index = 0; index < parts.Length; index++)
            {
                if (index > 0)
                {
                    lines.Add(current);
                    current = EmptyCopy(paragraph);
                }

                var piece = (A.Run)run.CloneNode(true);
                piece.Text!.Text = parts[index];
                Append(current, piece);
            }
        }

        lines.Add(current);
        foreach (var line in lines)
        {
            paragraph.InsertBeforeSelf(line);
        }

        paragraph.Remove();
    }

    private static A.Paragraph EmptyCopy(A.Paragraph paragraph)
    {
        var copy = new A.Paragraph();
        if (paragraph.ParagraphProperties is { } properties)
        {
            copy.Append(properties.CloneNode(true));
        }

        if (paragraph.Elements<A.EndParagraphRunProperties>().FirstOrDefault() is { } end)
        {
            copy.Append(end.CloneNode(true));
        }

        return copy;
    }

    private static void Append(A.Paragraph paragraph, OpenXmlElement child)
    {
        if (paragraph.Elements<A.EndParagraphRunProperties>().FirstOrDefault() is { } end)
        {
            end.InsertBeforeSelf(child);
        }
        else
        {
            paragraph.Append(child);
        }
    }

    /// <summary>
    /// A tag of a state (ADR-0065): the colors of its tone, light or dark by the background the template drew it with,
    /// and its icon, an SVG in the color of the text; a tag without an icon gives its place back to the text, and one
    /// without a value is neutral. A tag without text leaves, with its icon.
    /// </summary>
    private static void Tag(SlidePart part, OpenXmlElement root, P.Shape shape, string name, ReportTag? tag, ReportColors colors)
    {
        var icon = ShapeNamed(root, StatusReportFields.IconOf(name));
        if (string.IsNullOrWhiteSpace(string.Concat(shape.Descendants<A.Text>().Select(text => text.Text))))
        {
            shape.Remove();
            icon?.Remove();
            return;
        }

        tag ??= new ReportTag(ReportTone.Neutral);
        var dark = FillOf(shape) is { } fill && ReportColors.IsDark(fill);
        var (background, text) = colors.TagOf(tag.Tone, dark);
        SetFill(shape, background);
        foreach (var run in shape.Descendants<A.Run>())
        {
            var properties = run.RunProperties ??= new A.RunProperties();
            properties.RemoveAllChildren<A.SolidFill>();
            properties.RemoveAllChildren<A.NoFill>();
            properties.PrependChild(new A.SolidFill(new A.RgbColorModelHex { Val = text }));
        }

        if (icon is null)
        {
            return;
        }

        if (tag.Icon is { } chosen)
        {
            Picture(part, root, icon, StatusReportFields.IconOf(name), ReportImages.Icon(chosen, text));
        }
        else
        {
            icon.Remove();
            if (shape.TextBody?.BodyProperties is { } body)
            {
                body.LeftInset = body.RightInset?.Value ?? 0;
            }
        }
    }

    /// <summary>A bar as wide as the share of its track; with a tone, in the color of the charts, light or dark by the track.</summary>
    private static void SizeBar(OpenXmlElement root, P.Shape bar, string name, ReportBar value, ReportColors colors)
    {
        var track = ShapeNamed(root, StatusReportFields.TrackOf(name)) ?? ShapeNamed(bar.Ancestors<P.Slide>().First(), StatusReportFields.TrackOf(name));
        var width = track?.ShapeProperties?.Transform2D?.Extents?.Cx?.Value;
        if (width is null || bar.ShapeProperties?.Transform2D?.Extents is not { } extents)
        {
            return;
        }

        var share = Math.Clamp(value.Share, 0m, 1m);
        if (share == 0)
        {
            bar.Remove();
            return;
        }

        extents.Cx = (long)Math.Round(width.Value * share, MidpointRounding.AwayFromZero);
        if (value.Tone is { } tone)
        {
            SetFill(bar, colors.GraphOf(tone, FillOf(track!) is { } fill && ReportColors.IsDark(fill)));
        }
    }

    /// <summary>A mark at the point of the track of its bar the value gives; without a value, it leaves.</summary>
    private static void PlaceMark(OpenXmlElement root, P.Shape mark, string name, decimal? share)
    {
        var trackName = StatusReportFields.TrackOf(StatusReportFields.BarOfMark(name));
        var track = ShapeNamed(root, trackName) ?? ShapeNamed(mark.Ancestors<P.Slide>().First(), trackName);
        if (share is null || track?.ShapeProperties?.Transform2D is not { Offset: { } start, Extents: { } length }
            || mark.ShapeProperties?.Transform2D is not { Offset: { } offset, Extents: { } size })
        {
            mark.Remove();
            return;
        }

        offset.X = start.X!.Value + (long)Math.Round(length.Cx!.Value * Math.Clamp(share.Value, 0m, 1m), MidpointRounding.AwayFromZero) - (size.Cx!.Value / 2);
    }

    /// <summary>The color of the solid fill of a shape, when it has one.</summary>
    private static string? FillOf(P.Shape shape) =>
        shape.ShapeProperties?.GetFirstChild<A.SolidFill>()?.RgbColorModelHex?.Val?.Value
        ?? shape.ShapeProperties?.GetFirstChild<A.GradientFill>()?.Descendants<A.RgbColorModelHex>().FirstOrDefault()?.Val?.Value;

    private static void SetFill(P.Shape shape, string color)
    {
        var properties = shape.ShapeProperties ??= new P.ShapeProperties();
        properties.RemoveAllChildren<A.SolidFill>();
        properties.RemoveAllChildren<A.NoFill>();
        properties.RemoveAllChildren<A.GradientFill>();
        var fill = new A.SolidFill(new A.RgbColorModelHex { Val = color });
        if (properties.GetFirstChild<A.PresetGeometry>() is { } geometry)
        {
            geometry.InsertAfterSelf(fill);
        }
        else if (properties.GetFirstChild<A.CustomGeometry>() is { } custom)
        {
            custom.InsertAfterSelf(fill);
        }
        else
        {
            properties.Append(fill);
        }
    }

    /// <summary>
    /// The shape of a logo becomes the picture, as large as fits in it, aligned as its text and centered in its height,
    /// with the text as its description. A logo in a frame (<c>.quadro</c>) takes the frame along, its width hugging it.
    /// </summary>
    private static void Picture(SlidePart part, OpenXmlElement root, P.Shape shape, string name, ReportPicture picture)
    {
        var properties = shape.NonVisualShapeProperties!.NonVisualDrawingProperties!;
        var transform = shape.ShapeProperties?.Transform2D;
        if (transform?.Offset is not { } offset || transform.Extents is not { } extents)
        {
            return;
        }

        var alignment = shape.Descendants<A.ParagraphProperties>().FirstOrDefault()?.Alignment?.Value;
        var (x, y, width, height) = Fit(
            offset.X!.Value,
            offset.Y!.Value,
            extents.Cx!.Value,
            extents.Cy!.Value,
            ImageSize.Of(picture.Content, picture.ContentType),
            alignment == A.TextAlignmentTypeValues.Left ? -1 : alignment == A.TextAlignmentTypeValues.Right ? 1 : 0);
        if (picture.Framed && ShapeNamed(root, StatusReportFields.FrameOf(name)) is { ShapeProperties.Transform2D: { Offset: { } frameOffset, Extents: { } frameExtents } })
        {
            var padding = (frameExtents.Cx!.Value - extents.Cx!.Value) / 2;
            frameOffset.X = x - padding;
            frameExtents.Cx = width + (2 * padding);
        }

        var blip = new A.Blip();
        Embed(part, blip, picture);
        var description = string.Concat(shape.Descendants<A.Text>().Select(text => text.Text));
        var pictureShape = new P.Picture(
            new P.NonVisualPictureProperties(
                new P.NonVisualDrawingProperties { Id = properties.Id, Name = properties.Name, Description = description },
                new P.NonVisualPictureDrawingProperties(new A.PictureLocks { NoChangeAspect = true }),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.BlipFill(blip, new A.Stretch(new A.FillRectangle())),
            new P.ShapeProperties(
                new A.Transform2D(new A.Offset { X = x, Y = y }, new A.Extents { Cx = width, Cy = height }),
                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }));
        shape.InsertBeforeSelf(pictureShape);
        shape.Remove();
    }

    private static string Image(SlidePart part, byte[] content, PartTypeInfo type)
    {
        var image = part.AddImagePart(type);
        using (var stream = new MemoryStream(content, writable: false))
        {
            image.FeedData(stream);
        }

        return part.GetIdOfPart(image);
    }

    /// <summary>
    /// The place of a picture of the size given inside a box, as large as fits, centered in the height and to the left,
    /// the center or the right (-1, 0, 1); the whole box without a size.
    /// </summary>
    private static (long X, long Y, long Width, long Height) Fit(
        long x, long y, long width, long height, (double Width, double Height)? size, int alignment)
    {
        if (size is not { Width: > 0, Height: > 0 } image)
        {
            return (x, y, width, height);
        }

        var scale = Math.Min(width / image.Width, height / image.Height);
        var (fittedWidth, fittedHeight) = ((long)(image.Width * scale), (long)(image.Height * scale));
        var left = alignment switch
        {
            < 0 => x,
            > 0 => x + width - fittedWidth,
            _ => x + ((width - fittedWidth) / 2),
        };
        return (left, y + ((height - fittedHeight) / 2), fittedWidth, fittedHeight);
    }

    /// <summary>A text that shrinks on overflow takes the scale of the font its lines need, because the PowerPoint only works it out on an edit.</summary>
    private static void Scale(P.Shape shape)
    {
        var body = shape.TextBody?.BodyProperties;
        if (body?.GetFirstChild<A.NormalAutoFit>() is not { } autoFit || shape.ShapeProperties?.Transform2D?.Extents is not { } extents)
        {
            return;
        }

        var width = (extents.Cx!.Value - (body.LeftInset?.Value ?? 91440) - (body.RightInset?.Value ?? 91440)) / (double)Emu;
        var height = (extents.Cy!.Value - (body.TopInset?.Value ?? 45720) - (body.BottomInset?.Value ?? 45720)) / (double)Emu;
        var paragraphs = shape.TextBody!.Elements<A.Paragraph>().Select(paragraph =>
        {
            var size = (paragraph.Elements<A.Run>().FirstOrDefault()?.RunProperties?.FontSize?.Value
                ?? paragraph.GetFirstChild<A.EndParagraphRunProperties>()?.FontSize?.Value
                ?? 1800) / 100.0;
            var spacing = (paragraph.ParagraphProperties?.LineSpacing?.SpacingPercent?.Val?.Value ?? 100000) / 100000.0;
            return (string.Concat(paragraph.Elements<A.Run>().Select(run => run.Text?.Text)).Length, size, spacing);
        }).ToList();
        var scale = FontScaleOf(paragraphs, width, height);
        autoFit.FontScale = scale < 100000 ? scale : null;
    }

    private static string NameOf(P.Shape shape) => shape.NonVisualShapeProperties?.NonVisualDrawingProperties?.Name?.Value ?? string.Empty;

    private static string NameOf(P.GroupShape group) =>
        group.NonVisualGroupShapeProperties?.NonVisualDrawingProperties?.Name?.Value ?? string.Empty;

    private static P.Shape? ShapeNamed(OpenXmlElement root, string name) =>
        root.Descendants<P.Shape>().FirstOrDefault(shape => NameOf(shape) == name);

    /// <summary>The values a slide reads: each value is looked for in each place, the innermost first.</summary>
    private sealed class Lookup(IReadOnlyList<ReportScope> scopes)
    {
        public Lookup With(ReportScope item) => new([item, .. scopes]);

        public string? Field(string name) => Find(scope => scope.Fields.GetValueOrDefault(name));

        public IReadOnlyList<ReportScope> List(string name) => Find(scope => scope.Lists.GetValueOrDefault(name)) ?? [];

        public ReportTag? Tag(string name) => Find(scope => scope.Tags.GetValueOrDefault(name));

        public ReportBar? Bar(string name) => Find(scope => scope.Bars.GetValueOrDefault(name));

        public decimal? Mark(string name) =>
            scopes.Select(scope => scope.Marks.TryGetValue(name, out var share) ? share : (decimal?)null).FirstOrDefault(share => share is not null);

        public ReportPicture? Picture(string name) => Find(scope => scope.Pictures.GetValueOrDefault(name));

        private T? Find<T>(Func<ReportScope, T?> value)
            where T : class =>
            scopes.Select(value).FirstOrDefault(found => found is not null);
    }
}

/// <summary>The size of an image, from its content: the header of a PNG, the frame of a JPG or the size of an SVG.</summary>
internal static class ImageSize
{
    public static (double Width, double Height)? Of(byte[] content, string contentType)
    {
        ArgumentNullException.ThrowIfNull(content);
        try
        {
            return contentType switch
            {
                "image/png" when content.Length >= 24 =>
                    (BinaryPrimitives.ReadUInt32BigEndian(content.AsSpan(16)), BinaryPrimitives.ReadUInt32BigEndian(content.AsSpan(20))),
                "image/jpeg" => OfJpeg(content),
                "image/svg+xml" => OfSvg(content),
                _ => null,
            };
        }
        catch (Exception exception) when (exception is XmlException or ArgumentException or FormatException or IndexOutOfRangeException)
        {
            return null;
        }
    }

    private static (double, double)? OfJpeg(byte[] content)
    {
        var index = 2;
        while (index + 9 < content.Length && content[index] == 0xFF)
        {
            var marker = content[index + 1];
            var length = BinaryPrimitives.ReadUInt16BigEndian(content.AsSpan(index + 2));
            // The frames that start a picture, but the ones of the tables (0xC4, 0xC8, 0xCC).
            if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
            {
                return (BinaryPrimitives.ReadUInt16BigEndian(content.AsSpan(index + 7)), BinaryPrimitives.ReadUInt16BigEndian(content.AsSpan(index + 5)));
            }

            index += 2 + length;
        }

        return null;
    }

    private static (double, double)? OfSvg(byte[] content)
    {
        using var stream = new MemoryStream(content, writable: false);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        reader.MoveToContent();
        if (reader.GetAttribute("viewBox") is { } box
            && box.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries) is { Length: 4 } parts)
        {
            return (Number(parts[2]), Number(parts[3]));
        }

        return reader.GetAttribute("width") is { } width && reader.GetAttribute("height") is { } height
            ? (Number(width), Number(height))
            : null;
    }

    private static double Number(string value) =>
        double.Parse(new string([.. value.TakeWhile(character => char.IsDigit(character) || character is '.' or '-')]), CultureInfo.InvariantCulture);
}
