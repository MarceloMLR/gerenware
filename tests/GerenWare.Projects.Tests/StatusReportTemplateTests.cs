using System.Text;
using DocumentFormat.OpenXml.Validation;
using GerenWare.Projects.Application;
using GerenWare.Projects.Domain;
using GerenWare.Projects.Infrastructure.StatusReports;
using GerenWare.Testing;
using static GerenWare.Projects.Tests.StatusReportSamples;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace GerenWare.Projects.Tests;

// Spec: relatorio-de-status, requirements "Modelo padrão do relatório" and "Campos do modelo" (ADR-0165, ADR-0168).
public sealed class StatusReportTemplateTests
{
    [Fact]
    public void DefaultTemplate_PassesTheValidator_WithTheShapesTheFillingExpects()
    {
        using var document = Open(DefaultStatusReportTemplate.Build(null, null));

        new OpenXmlValidator().Validate(document, TestContext.Current.CancellationToken).Should().BeEmpty();
        var slides = Slides(document);
        foreach (var name in new[]
        {
            "logo.instalacao", "logo.instalacao.quadro", "logo.cliente", "etiqueta.situacao", "etiqueta.situacao.icone",
            "barra.andamento", "barra.andamento.trilho", "marca.andamento", "etiqueta.prazo", "etiqueta.proximoMarco", "etiqueta.vencidos",
        })
        {
            HasShape(slides[0], name).Should().BeTrue(name);
        }

        Blocks(slides[0], "lista.marco").Should().ContainSingle();
        Blocks(slides[0], "lista.etapa.lado").Should().ContainSingle();
        Blocks(slides[1], "lista.fase").Should().ContainSingle();
        Blocks(slides[1], "lista.atraso").Should().ContainSingle();
        Blocks(slides[3], "lista.riscoMarco").Should().ContainSingle();
        Blocks(slides[4], "lista.horasTarefa").Should().ContainSingle();
        foreach (var name in new[] { "etiqueta.classificacao", "etiqueta.status", "etiqueta.prioridade" })
        {
            HasShape(slides[3], name).Should().BeTrue(name);
        }

        HasShape(slides[4], "etiqueta.horas").Should().BeTrue();
        slides.SelectMany(slide => slide.Descendants<A.LatinFont>()).Select(font => font.Typeface!.Value).Distinct().Should().Equal("+mn-lt");
        document.PresentationPart!.SlideMasterParts.Single().ThemePart!.Theme!.ThemeElements!.FontScheme!.MinorFont!.LatinFont!.Typeface!.Value
            .Should().Be("Calibri");
    }

    [Fact]
    public void Cards_HaveTheCornerTheBorderAndTheLightShadowOfTheDesignSystem()
    {
        using var document = Open(DefaultStatusReportTemplate.Build(null, null));

        var card = Slides(document)[1].Descendants<P.Shape>().First(shape => NameOf(shape) == "Cartão");
        FillOf(card).Should().Be("FFFFFF");
        card.ShapeProperties!.GetFirstChild<A.Outline>()!.GetFirstChild<A.SolidFill>()!.RgbColorModelHex!.Val!.Value.Should().Be("E1E4E8");
        var shadow = card.ShapeProperties!.GetFirstChild<A.EffectList>()!.GetFirstChild<A.OuterShadow>()!;
        (shadow.Distance!.Value, shadow.BlurRadius!.Value, shadow.Direction!.Value).Should().Be((E(4 / 96.0), E(12 / 96.0), 5400000));
        shadow.RgbColorModelHex!.Val!.Value.Should().Be("1C1F24");
        shadow.RgbColorModelHex!.GetFirstChild<A.Alpha>()!.Val!.Value.Should().Be(12157);
        Slides(document)[1].Descendants<P.Shape>().Where(shape => NameOf(shape) == "Anel da faixa")
            .Should().OnlyContain(shape => shape.ShapeProperties!.GetFirstChild<A.PresetGeometry>()!.Preset! == A.ShapeTypeValues.Arc);
    }

    [Fact(DisplayName = "Cores da GerenWare no relatório")]
    public void WithoutColors_TheBandIsTheGraphiteOfThePanel_AndTheBarsTheEmerald()
    {
        using var document = Render(Data());

        var first = Slides(document)[0];
        GradientOf(ShapeNamed(first, "Faixa")).Should().Equal("1C1F24", "3B3F46");
        FillOf(ShapeNamed(first, "barra.andamento")).Should().Be("047857");
        FillOf(ShapeNamed(Blocks(Slides(document)[1], "lista.fase")[0], "barra.fase")).Should().Be("047857");
    }

    [Fact(DisplayName = "Cores da instalação no relatório")]
    public void InstallationColor_PaintsTheBarsAndADarkBandOfItsHue_ButNotTheStates()
    {
        using var document = Render(Data() with { Brand = Data().Brand with { MainColor = "#0F766E" } });

        var first = Slides(document)[0];
        FillOf(ShapeNamed(first, "barra.andamento")).Should().Be("0F766E");
        FillOf(ShapeNamed(Blocks(Slides(document)[1], "lista.fase")[0], "barra.fase")).Should().Be("0F766E");
        var band = GradientOf(ShapeNamed(first, "Faixa"));
        band.Should().OnlyContain(color => Oklch.Luminance(color) < 0.06, "the band stays dark");
        Oklch.Of(band[1]).H.Should().BeApproximately(Oklch.Of("0F766E").H, 3);
        FillOf(ShapeNamed(first, "etiqueta.situacao")).Should().Be("1C3340", "the states keep their colors");
        FillOf(ShapeNamed(first, "etiqueta.vencidos")).Should().Be("FCE4E4");
        document.PresentationPart!.SlideMasterParts.Single().ThemePart!.Theme!.ThemeElements!.ColorScheme!.Accent1Color!.RgbColorModelHex!.Val!.Value
            .Should().Be("0F766E");
    }

    [Fact(DisplayName = "Etiqueta na faixa escura")]
    public void TagOverTheBand_TakesTheColorsOfTheDarkTheme_WithTheIconOfTheState()
    {
        using var document = Render(Data());

        var first = Slides(document)[0];
        var tag = ShapeNamed(first, "etiqueta.situacao");
        tag.InnerText.Should().Be("Em andamento");
        FillOf(tag).Should().Be("1C3340");
        tag.Descendants<A.Run>().Single().RunProperties!.GetFirstChild<A.SolidFill>()!.RgbColorModelHex!.Val!.Value.Should().Be("7DD3FC");
        SvgOf(PictureNamed(first, "etiqueta.situacao.icone")).Should().Contain("#7DD3FC").And.Contain(PathOf(ReportIcon.Autorenew));
    }

    [Fact(DisplayName = "Logo da instalação para fundo escuro")]
    public void DarkLogo_GoesOverTheBand_WithoutAFrame()
    {
        var brand = Data().Brand with
        {
            Logo = new StatusReportImage(Png(400, 100), "image/png"),
            DarkLogo = new StatusReportImage(Png(300, 100), "image/png"),
        };

        using var document = Render(Data() with { Brand = brand });

        var first = Slides(document)[0];
        var logo = BoxOf(PictureNamed(first, "logo.instalacao"));
        (logo.Width / (double)logo.Height).Should().BeApproximately(3.0, 0.01, "it is the logo for dark backgrounds");
        HasShape(first, "logo.instalacao.quadro").Should().BeFalse();
    }

    [Fact(DisplayName = "Só o logo claro da instalação")]
    public void OnlyTheLightLogo_GoesInAWhiteFrame_HuggingIt_AtTheRight()
    {
        var svg = Encoding.UTF8.GetBytes("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 200 100"><rect width="200" height="100"/></svg>""");

        using var document = Render(Data() with { Brand = Data().Brand with { Logo = new StatusReportImage(svg, "image/svg+xml") } });

        var first = Slides(document)[0];
        var logo = PictureNamed(first, "logo.instalacao");
        logo.BlipFill!.Blip!.OuterXml.Should().Contain("svgBlip");
        var picture = BoxOf(logo);
        var frame = BoxOf(ShapeNamed(first, "logo.instalacao.quadro"));
        FillOf(ShapeNamed(first, "logo.instalacao.quadro")).Should().Be("FFFFFF");
        (frame.X, frame.Width).Should().Be((picture.X - E(0.12), picture.Width + E(0.24)));
        (picture.X + picture.Width).Should().BeCloseTo(E(13.333 - 0.6), 2, "the logo keeps to the right, as its text");
    }

    [Fact(DisplayName = "Instalação com o nome do produto e sem logo")]
    public void InstallationWithTheNameOfTheProductAndNoLogo_ShowsTheLogoOfTheGerenWareForDarkBackgrounds()
    {
        using var document = Render(Data() with { Brand = Data().Brand with { Name = "GerenWare" } });

        var first = Slides(document)[0];
        var logo = PictureNamed(first, "logo.instalacao");
        SvgOf(logo).Should().Be(File.ReadAllText(RepositoryFiles.PathOf("docs", "marca", "svg", "gerenware-horizontal-fundo-escuro.svg")));
        HasShape(first, "logo.instalacao.quadro").Should().BeFalse();
    }

    [Fact(DisplayName = "Instalação e cliente sem logo")]
    public void WithoutLogos_TheNamesAreWritten()
    {
        using var document = Render(Data());

        var first = Slides(document)[0];
        ShapeNamed(first, "logo.instalacao").InnerText.Should().Be("Consultoria Fictícia");
        ShapeNamed(first, "logo.instalacao").Descendants<A.Run>().Single().RunProperties!.GetFirstChild<A.SolidFill>()!.RgbColorModelHex!.Val!.Value
            .Should().Be("FFFFFF");
        ShapeNamed(first, "logo.cliente").InnerText.Should().Be("Distribuidora Exemplo");
        HasShape(first, "logo.instalacao.quadro").Should().BeFalse();
        FillOf(ShapeNamed(first, "Quadro do cliente")).Should().Be("FFFFFF");
        first.Descendants<P.Picture>().Should().NotContain(picture => picture.NonVisualPictureProperties!.NonVisualDrawingProperties!.Name!.Value!.StartsWith("logo.", StringComparison.Ordinal));
    }

    [Fact]
    public void ClientLogo_BecomesAPictureFittedInItsShape_InTheWhiteFrame()
    {
        using var document = Render(Data() with { ClientLogo = new StatusReportImage(Png(200, 100), "image/png") });

        var client = PictureNamed(Slides(document)[0], "logo.cliente");
        client.NonVisualPictureProperties!.NonVisualDrawingProperties!.Description!.Value.Should().Be("Distribuidora Exemplo");
        var size = BoxOf(client);
        (size.Width / (double)size.Height).Should().BeApproximately(2.0, 0.01);
        HasShape(Slides(document)[0], "Quadro do cliente").Should().BeTrue();
    }

    [Fact(DisplayName = "Campo quebrado em pedaços")]
    public void FieldKeptInPieces_TakesTheValue_WithTheFormatOfWhereItStarts()
    {
        var run = new A.Run(new A.RunProperties { Language = "pt-BR", FontSize = 1000 }, new A.Text());
        var shape = Shape("Título", 1, 1, 4, 0.5);
        var paragraph = shape.TextBody!.Elements<A.Paragraph>().Single();
        paragraph.RemoveAllChildren<A.Run>();
        foreach (var (text, bold) in new[] { ("{{proj", true), ("eto.no", false), ("me}}", false) })
        {
            var piece = (A.Run)run.CloneNode(true);
            piece.RunProperties!.Bold = bold;
            piece.Text!.Text = text;
            paragraph.Append(piece);
        }

        using var filled = Open(StatusReportFiller.Fill(TemplateWith(shape), PptxStatusReportRenderer.ValuesOf(Data())));

        var runs = ShapeNamed(Slides(filled)[0], "Título").Descendants<A.Run>().ToList();
        runs.Should().ContainSingle();
        (runs[0].Text!.Text, runs[0].RunProperties!.Bold!.Value).Should().Be(("Implantação do ERP financeiro", true));
    }

    [Fact]
    public void LineBreaksOfAText_BecomeParagraphs()
    {
        using var document = Render(Data() with
        {
            Texts = new StatusReportTexts("Primeira linha\nSegunda linha", string.Empty, string.Empty, string.Empty),
        });

        ShapeNamed(Slides(document)[0], "{{texto.resumo}}").Descendants<A.Paragraph>().Select(paragraph => paragraph.InnerText)
            .Should().Equal("Primeira linha", "Segunda linha");
    }

    [Fact(DisplayName = "Lista numa tabela")]
    public void ListInATable_RepeatsTheModelRowForEachItem()
    {
        var table = Table(["Fase", "Andamento"], ["{{fase.nome}}", "{{fase.andamento}}"]);

        using var document = Open(StatusReportFiller.Fill(TemplateWith(table), PptxStatusReportRenderer.ValuesOf(Data())));

        var rows = Slides(document)[0].Descendants<A.TableRow>().Select(row => row.Elements<A.TableCell>().Select(cell => cell.InnerText).ToList()).ToList();
        rows.Should().HaveCount(5, "the titles and one row for each of the four phases");
        rows[1].Should().Equal("Configuração", "30%");
        rows[4].Should().Equal("Implantação", "0%");
    }

    [Fact(DisplayName = "Lista em blocos")]
    public void ListInBlocks_RepeatsTheBlockForEachItem_DownByItsHeight()
    {
        using var document = Render(Data());

        var blocks = Blocks(Slides(document)[1], "lista.fase");
        blocks.Select(block => ShapeNamed(block, "{{fase.nome}}").InnerText).Should().Equal("Configuração", "Integrações", "Testes", "Implantação");
        var step = blocks[0].GroupShapeProperties!.TransformGroup!.Extents!.Cy!.Value;
        blocks.Select(block => OffsetOf(block).Y - OffsetOf(blocks[0]).Y).Should().Equal(0, step, step * 2, step * 3);
        blocks.Select(block => OffsetOf(block).X).Distinct().Should().ContainSingle();
    }

    [Fact]
    public void BlockOfACompany_RepeatsToTheSide_AndAnEmptyListTakesItOut()
    {
        var template = TemplateWith(
            Group("lista.fase.lado", 1, 1, 2, 0.5, Shape("{{fase.nome}}", 1, 1, 1.8, 0.3, "{{fase.nome}}")),
            Group("lista.marco", 1, 3, 4, 0.4, Shape("{{marco.nome}}", 1, 3, 3, 0.3, "{{marco.nome}}")));

        using var document = Open(StatusReportFiller.Fill(template, PptxStatusReportRenderer.ValuesOf(Data() with { Milestones = [] })));

        var slide = Slides(document)[0];
        var blocks = Blocks(slide, "lista.fase.lado");
        blocks.Select(block => OffsetOf(block).X).Should().Equal(E(1), E(3), E(5), E(7));
        blocks.Select(block => block.InnerText).Should().Equal("Configuração", "Integrações", "Testes", "Implantação");
        Blocks(slide, "lista.marco").Should().BeEmpty();
        new OpenXmlValidator().Validate(document, TestContext.Current.CancellationToken).Should().BeEmpty();
    }

    [Fact(DisplayName = "Barra e marca de um item")]
    public void BarAndMarkOfAnItem_FollowItsTrack()
    {
        var phases = Data().Phases.Items.ToList();
        phases[0] = phases[0] with { Progress = 35, Planned = 70 };

        using var document = Render(Data() with { Phases = new StatusReportList<StatusReportPhase>(phases, 0) });

        var block = Blocks(Slides(document)[1], "lista.fase")[0];
        var track = BoxOf(ShapeNamed(block, "barra.fase.trilho"));
        ((double)BoxOf(ShapeNamed(block, "barra.fase")).Width / track.Width).Should().BeApproximately(0.35, 0.001);
        var mark = BoxOf(ShapeNamed(block, "marca.fase"));
        ((mark.X + (mark.Width / 2.0) - track.X) / track.Width).Should().BeApproximately(0.7, 0.001);
        HasShape(Blocks(Slides(document)[1], "lista.fase")[3], "marca.fase").Should().BeFalse("a phase without the planned progress has no mark");
    }

    [Fact]
    public void ProgressBar_IsAsWideAsTheShareOfItsTrack_WithTheMarkOfThePlannedOne()
    {
        using var document = Render(Data());

        var first = Slides(document)[0];
        var track = BoxOf(ShapeNamed(first, "barra.andamento.trilho"));
        ((double)BoxOf(ShapeNamed(first, "barra.andamento")).Width / track.Width).Should().BeApproximately(0.62, 0.001);
        var mark = BoxOf(ShapeNamed(first, "marca.andamento"));
        ((mark.X + (mark.Width / 2.0) - track.X) / track.Width).Should().BeApproximately(0.7, 0.001);
    }

    [Fact(DisplayName = "Etiqueta pelo tom do valor")]
    public void TagOfADay_IsInTheToneOfDangerWhenLate_AndInTheAccentOtherwise()
    {
        using var document = Render(Data() with
        {
            Milestones =
            [
                new StatusReportMilestone("Cadastro validado", new DateOnly(2026, 11, 13), 0, 4),
                new StatusReportMilestone("Homologação", new DateOnly(2026, 12, 4), 10, null),
            ],
        });

        var blocks = Blocks(Slides(document)[0], "lista.marco");
        var late = ShapeNamed(blocks[0], "etiqueta.marco");
        (late.InnerText, FillOf(late)).Should().Be(("13/11", "FCE4E4"));
        SvgOf(PictureNamed(blocks[0], "etiqueta.marco.icone")).Should().Contain("#A12222").And.Contain(PathOf(ReportIcon.WarningAmber));
        var onTime = ShapeNamed(blocks[1], "etiqueta.marco");
        (onTime.InnerText, FillOf(onTime)).Should().Be(("04/12", "E6F4ED"));
        PicturesNamed(blocks[1], "etiqueta.marco.icone").Should().BeEmpty("the accent has no icon");
        var body = onTime.TextBody!.BodyProperties!;
        body.LeftInset!.Value.Should().Be(body.RightInset!.Value, "the text takes the place of the icon back");
    }

    [Fact]
    public void TagOfACompany_TakesThePaletteOfTheBackgroundItWasDrawnWith()
    {
        var scope = new ReportScope();
        scope.Fields["projeto.situacao"] = "Pausado";
        scope.Tags["etiqueta.claro"] = new ReportTag(ReportTone.Warning, ReportIcon.PauseCircle);
        scope.Tags["etiqueta.escuro"] = new ReportTag(ReportTone.Warning, ReportIcon.PauseCircle);
        var template = TemplateWith(
            Shape("etiqueta.claro", 1, 1, 2, 0.3, "{{projeto.situacao}}", "EFF1F4"),
            Shape("etiqueta.claro.icone", 1.1, 1.05, 0.2, 0.2),
            Shape("etiqueta.escuro", 1, 2, 2, 0.3, "{{projeto.situacao}}", "2B3038"),
            Shape("etiqueta.escuro.icone", 1.1, 2.05, 0.2, 0.2),
            Shape("etiqueta.vazia", 1, 3, 2, 0.3, "{{nada}}", "EFF1F4"),
            Shape("etiqueta.vazia.icone", 1.1, 3.05, 0.2, 0.2));

        using var document = Open(StatusReportFiller.Fill(template, new StatusReportValues(scope, [], false, ReportColors.Of(null))));

        var slide = Slides(document)[0];
        FillOf(ShapeNamed(slide, "etiqueta.claro")).Should().Be("FDF1DC");
        SvgOf(PictureNamed(slide, "etiqueta.claro.icone")).Should().Contain("#8A5300");
        FillOf(ShapeNamed(slide, "etiqueta.escuro")).Should().Be("3A2D12");
        SvgOf(PictureNamed(slide, "etiqueta.escuro.icone")).Should().Contain("#F2C46D");
        HasShape(slide, "etiqueta.vazia").Should().BeFalse("a tag left without text leaves");
        HasShape(slide, "etiqueta.vazia.icone").Should().BeFalse();
    }

    [Fact]
    public void ColorsOfTheTags_AreTheOnesOfTheStates_InEachTheme_AndTheAccentOfTheInstallation()
    {
        var colors = ReportColors.Of(null);

        colors.TagOf(ReportTone.Danger, false).Should().Be(("FCE4E4", "A12222"));
        colors.TagOf(ReportTone.Danger, true).Should().Be(("44191A", "F49A93"));
        colors.TagOf(ReportTone.Warning, false).Should().Be(("FDF1DC", "8A5300"));
        colors.TagOf(ReportTone.Success, true).Should().Be(("16332D", "6EE7B7"));
        colors.TagOf(ReportTone.Info, false).Should().Be(("E3F2FB", "0369A1"));
        colors.TagOf(ReportTone.Neutral, true).Should().Be(("2B3038", "C9CDD3"));
        colors.TagOf(ReportTone.Accent, false).Should().Be(("E6F4ED", "047857"));
        colors.TagOf(ReportTone.Accent, true).Should().Be(("17352B", "6EE7B7"));
        (colors.GraphOf(ReportTone.Danger, false), colors.GraphOf(ReportTone.Danger, true)).Should().Be(("B42318", "D33A34"));
        (colors.GraphOf(ReportTone.Success, false), colors.GraphOf(ReportTone.Info, true)).Should().Be(("059669", "7DD3FC"));
        ReportColors.IsDark("2B3038").Should().BeTrue();
        ReportColors.IsDark("EFF1F4").Should().BeFalse();
    }

    [Theory]
    [InlineData("#0F766E")]
    [InlineData("#1F4E8C")]
    [InlineData("#FDE047")]
    [InlineData("#F9FAFB")]
    public void ColorOfTheInstallation_GivesADarkBandOfItsHue_AndAReadableAccent(string main)
    {
        var colors = ReportColors.Of(main);

        colors.Accent.Should().Be(main[1..]);
        Oklch.Contrast(colors.AccentText, colors.AccentBackground).Should().BeGreaterThanOrEqualTo(4.5);
        Oklch.Contrast(colors.AccentText, colors.Surface).Should().BeGreaterThanOrEqualTo(4.5);
        new[] { colors.BandStart, colors.BandEnd }.Should().OnlyContain(color => Oklch.Luminance(color) < 0.06);
        Oklch.Contrast(colors.BandText, colors.BandEnd).Should().BeGreaterThanOrEqualTo(4.5);
        Oklch.Contrast(colors.BandAccent, colors.BandEnd).Should().BeGreaterThanOrEqualTo(4.5);
        ReportColors.IsDark(colors.BandTrack).Should().BeTrue();
        Oklch.Luminance(colors.AccentBackground).Should().BeGreaterThan(0.8, "the background of the accent stays a light surface");
    }

    [Fact]
    public void WithoutAColor_OrWithTheEmeraldItself_TheColorsAreTheOnesOfTheBrand()
    {
        ReportColors.Of("#047857").Should().Be(ReportColors.Of(null));
        var colors = ReportColors.Of(null);
        (colors.Page, colors.Accent, colors.AccentBackground, colors.BandStart, colors.BandEnd, colors.BandAccent)
            .Should().Be(("F6F7F9", "047857", "E6F4ED", "1C1F24", "3B3F46", "6EE7B7"));
        (colors.BandShape, colors.BandShapeAlpha).Should().Be(("FFFFFF", 12157));
    }

    [Fact]
    public void LongText_ShrinksToItsLines_DownToTheSmallestScale()
    {
        using var document = Render(Data() with
        {
            Texts = new StatusReportTexts(new string('a', 300) + " " + new string('b', 300), new string('c', 1000), "Homologar", string.Empty),
        });

        var summary = ShapeNamed(Slides(document)[0], "{{texto.resumo}}").TextBody!.BodyProperties!.GetFirstChild<A.NormalAutoFit>()!;
        summary.FontScale!.Value.Should().BeInRange(StatusReportFiller.MinimumFontScale, 97500);
        var focus = ShapeNamed(Slides(document)[2], "{{texto.foco}}").TextBody!.BodyProperties!.GetFirstChild<A.NormalAutoFit>()!;
        focus.FontScale!.Value.Should().Be(StatusReportFiller.MinimumFontScale, "a text of 1.000 characters takes the smallest scale");
        ShapeNamed(Slides(document)[2], "{{texto.proximosPassos}}").TextBody!.BodyProperties!.GetFirstChild<A.NormalAutoFit>()!.FontScale
            .Should().BeNull("a short text keeps its size");
    }

    [Fact]
    public void FontScale_GoesDownInStepsUntilTheLinesFit()
    {
        StatusReportFiller.FontScaleOf([(100, 12, 1.15)], 6.4, 1.42).Should().Be(100000);
        StatusReportFiller.FontScaleOf([(700, 12, 1.15)], 6.4, 1.42).Should().BeInRange(80000, 97500);
        StatusReportFiller.FontScaleOf([(5000, 12, 1.15)], 6.4, 1.42).Should().Be(80000);
        StatusReportFiller.FontScaleOf([(10, 12, 1), (10, 12, 1), (10, 12, 1)], 6.4, 0.45).Should().BeLessThan(100000, "each paragraph takes a line");
    }

    [Fact]
    public void Icons_AreTheOnesOfTheScreens_InTheColorAsked_AndNoFileIsLeftOver()
    {
        foreach (var icon in Enum.GetValues<ReportIcon>())
        {
            var svg = Encoding.UTF8.GetString(ReportImages.Icon(icon, "A12222").Content);
            svg.Should().Contain("viewBox=\"0 0 24 24\"").And.Contain("#A12222").And.NotContain("#000000");
        }

        var folder = RepositoryFiles.PathOf("src", "Modules", "Projects", "GerenWare.Projects", "Infrastructure", "StatusReports", "Icons");
        Directory.GetFiles(folder, "*.svg").Select(Path.GetFileNameWithoutExtension)
            .Should().BeEquivalentTo(Enum.GetValues<ReportIcon>().Select(ReportImages.FileOf));
    }

    [Fact]
    public void LogoOfTheProduct_IsTheFileOfTheBrand_WithItsPng()
    {
        ReportImages.ProductLogo.Content.Should().Equal(File.ReadAllBytes(RepositoryFiles.PathOf("docs", "marca", "svg", "gerenware-horizontal-fundo-escuro.svg")));
        ReportImages.ProductLogo.Fallback.Should().Equal(File.ReadAllBytes(RepositoryFiles.PathOf("docs", "marca", "png", "gerenware-horizontal-fundo-escuro-256px.png")));
    }

    /// <summary>The path of an icon, as its file has it.</summary>
    private static string PathOf(ReportIcon icon)
    {
        var svg = Encoding.UTF8.GetString(ReportImages.Icon(icon, "000000").Content);
        var start = svg.IndexOf(" d=\"", StringComparison.Ordinal) + 4;
        return svg[start..svg.IndexOf('"', start)];
    }

    /// <summary>A table of a template: the row of the titles and the model row.</summary>
    private static P.GraphicFrame Table(string[] titles, string[] fields)
    {
        static A.TableRow Row(string[] texts) =>
            new(texts.Select(text => new A.TableCell(
                new A.TextBody(new A.BodyProperties(), new A.ListStyle(), new A.Paragraph(new A.Run(new A.RunProperties { Language = "pt-BR" }, new A.Text(text)))),
                new A.TableCellProperties())))
            { Height = E(0.3) };

        return new P.GraphicFrame(
            new P.NonVisualGraphicFrameProperties(
                new P.NonVisualDrawingProperties { Id = 90, Name = "fases" },
                new P.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoGrouping = true }),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.Transform(new A.Offset { X = E(1), Y = E(1) }, new A.Extents { Cx = E(6), Cy = E(0.6) }),
            new A.Graphic(new A.GraphicData(new A.Table(
                new A.TableProperties { FirstRow = true },
                new A.TableGrid(titles.Select(_ => new A.GridColumn { Width = E(3) })),
                Row(titles),
                Row(fields)))
            {
                Uri = "http://schemas.openxmlformats.org/drawingml/2006/table",
            }));
    }
}
