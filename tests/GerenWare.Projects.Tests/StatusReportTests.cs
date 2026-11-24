using DocumentFormat.OpenXml.Validation;
using GerenWare.Projects.Application;
using GerenWare.Projects.Contracts;
using GerenWare.Projects.Domain;
using GerenWare.Projects.Infrastructure.StatusReports;
using static GerenWare.Projects.Tests.StatusReportSamples;
using P = DocumentFormat.OpenXml.Presentation;

namespace GerenWare.Projects.Tests;

// Spec: relatorio-de-status, requirements "Rascunho do relatório", "Textos e riscos do relatório", "O que vem do
// sistema", "Riscos no relatório", "Horas no relatório", "Modelo padrão do relatório" and "Relatório em PDF" (module
// side); the template and its fields are in StatusReportTemplateTests.
public sealed class StatusReportTests
{
    private static readonly HashSet<int> OpenTickets = [539, 541, 545];

    private static (StatusReportForm? Form, IReadOnlyList<StatusReportField> Invalid) FormOf(
        string summary = "Cadastros validados", IReadOnlyList<StatusReportRiskInput>? risks = null) =>
        StatusReportForm.Of(summary, "Integração fiscal", "Homologar", "Aprovar o roteiro", risks ?? [], false, OpenTickets);

    [Fact(DisplayName = "Texto longo demais")]
    public void LongText_IsRefusedInItsField()
    {
        var (form, invalid) = FormOf(new string('a', StatusReportLimits.TextMaxLength + 1));

        form.Should().BeNull();
        invalid.Should().Equal(StatusReportField.Summary);
        FormOf(new string('a', StatusReportLimits.TextMaxLength)).Form.Should().NotBeNull();
    }

    [Fact]
    public void Texts_KeepTheLineBreaks_AndRefuseOtherControlCharacters()
    {
        StatusReportTexts.TextOf("  Primeira linha\r\nSegunda\t linha  ", 100).Should().Be("Primeira linha\nSegunda\t linha");
        StatusReportTexts.TextOf("Texto\u0007", 100).Should().BeNull();
        StatusReportTexts.TextOf(null, 100).Should().BeEmpty();
    }

    [Fact(DisplayName = "Chamado fechado como risco")]
    public void RiskThatIsNotAnOpenTicket_IsRefused()
    {
        var (form, invalid) = FormOf(risks: [new StatusReportRiskInput(538, RiskLevel.High, string.Empty)]);

        form.Should().BeNull();
        invalid.Should().Equal(StatusReportField.Risks);
    }

    [Fact(DisplayName = "Riscos demais")]
    public void MoreThanTenRisks_AreRefused()
    {
        var open = Enumerable.Range(1, 11).ToHashSet();

        var (form, invalid) = StatusReportForm.Of(
            null, null, null, null, [.. open.Select(number => new StatusReportRiskInput(number, RiskLevel.Low, null))], false, open);

        form.Should().BeNull();
        invalid.Should().Equal(StatusReportField.Risks);
    }

    [Fact]
    public void Risks_RefuseARepeatedTicket_AMissingLevel_AndALongPointOfAttention()
    {
        FormOf(risks: [new(539, RiskLevel.High, null), new(539, RiskLevel.Low, null)]).Invalid.Should().Equal(StatusReportField.Risks);
        FormOf(risks: [new(539, null, null)]).Invalid.Should().Equal(StatusReportField.Risks);
        FormOf(risks: [new(539, RiskLevel.High, new string('a', StatusReportLimits.AttentionMaxLength + 1))])
            .Invalid.Should().Equal(StatusReportField.Risks);
        FormOf(risks: [new(539, RiskLevel.High, "Depende do fornecedor"), new(541, RiskLevel.Low, null)])
            .Form!.Risks.Should().Equal(new StatusReportRisk(539, RiskLevel.High, "Depende do fornecedor"), new StatusReportRisk(541, RiskLevel.Low, string.Empty));
    }

    [Fact(DisplayName = "Formato do último relatório")]
    public void NextDraft_ComesInTheFormatOfTheLastReport_AndInPowerPointAtTheFirst()
    {
        var notes = new StatusReportNotes(Guid.CreateVersion7());
        notes.Format.Should().Be(StatusReportFormat.PowerPoint);
        var (pdf, _) = StatusReportForm.Of(
            null, null, null, null, [], false, OpenTickets, StatusReportFormat.Pdf, convertsToPdf: true);
        notes.Keep(pdf!, Guid.CreateVersion7(), DateTime.UtcNow);

        notes.Format.Should().Be(StatusReportFormat.Pdf);
        StatusReportForm.DraftFormatOf(notes.Format, convertsToPdf: true).Should().Be(StatusReportFormat.Pdf);
        StatusReportForm.DraftFormatOf(null, convertsToPdf: true).Should().Be(StatusReportFormat.PowerPoint);
        StatusReportForm.DraftFormatOf(notes.Format, convertsToPdf: false).Should().Be(StatusReportFormat.PowerPoint,
            "where the storage does not convert, the PDF is never offered");
    }

    [Fact(DisplayName = "PDF sem o SharePoint")]
    public void Pdf_IsRefusedInItsField_WhereTheStorageDoesNotConvert()
    {
        var (form, invalid) = StatusReportForm.Of(null, null, null, null, [], false, OpenTickets, StatusReportFormat.Pdf);

        form.Should().BeNull();
        invalid.Should().Equal(StatusReportField.Format);
        StatusReportForm.Of(null, null, null, null, [], false, OpenTickets, (StatusReportFormat)9, convertsToPdf: true)
            .Invalid.Should().Equal(StatusReportField.Format);
        StatusReportForm.Of(null, null, null, null, [], false, OpenTickets).Form!.Format.Should().Be(StatusReportFormat.PowerPoint);
    }

    [Fact(DisplayName = "Rascunho sem relatório anterior")]
    public void FirstDraft_SuggestsTheTicketsThatThreatenMilestones_AsMedium()
    {
        StatusReportSuggestions.Of(null, OpenTickets, [545, 539])
            .Should().Equal(new StatusReportRisk(545, RiskLevel.Medium, string.Empty), new StatusReportRisk(539, RiskLevel.Medium, string.Empty));
    }

    [Fact(DisplayName = "Textos do último relatório")]
    public void NextDraft_KeepsTheRisksOfTheLastReport_FirstAndWithTheirLevel()
    {
        var notes = new StatusReportNotes(Guid.CreateVersion7());
        notes.Keep(FormOf(risks: [new(541, RiskLevel.High, "Depende do fornecedor")]).Form!, Guid.CreateVersion7(), DateTime.UtcNow);

        StatusReportSuggestions.Of(notes, OpenTickets, [539, 541])
            .Should().Equal(new StatusReportRisk(541, RiskLevel.High, "Depende do fornecedor"), new StatusReportRisk(539, RiskLevel.Medium, string.Empty));
        notes.Texts.Summary.Should().Be("Cadastros validados");
    }

    [Fact(DisplayName = "Risco do último relatório já resolvido")]
    public void RiskOfTheLastReportNoLongerOpen_IsNotSuggested()
    {
        var notes = new StatusReportNotes(Guid.CreateVersion7());
        notes.Keep(FormOf(risks: [new(541, RiskLevel.High, null)]).Form!, Guid.CreateVersion7(), DateTime.UtcNow);

        StatusReportSuggestions.Of(notes, new HashSet<int> { 539 }, []).Should().BeEmpty();
    }

    [Fact(DisplayName = "Sequência das fases")]
    public void Sequence_StartsAtTheFirstPhaseNotDone_UpToFive()
    {
        int[] progress = [100, 100, 40, 0, 0, 0, 0];

        StatusReportPhases.SequenceOf([.. Enumerable.Range(0, 7)], index => progress[index] == 100).Should().Equal(2, 3, 4, 5, 6);
    }

    [Fact]
    public void Sequence_CompletesWithThePhasesBefore_AndShowsTheLastOnesWhenAllAreDone()
    {
        List<int> phases = [.. Enumerable.Range(0, 7)];

        StatusReportPhases.SequenceOf(phases, index => index < 5).Should().Equal(2, 3, 4, 5, 6);
        StatusReportPhases.SequenceOf(phases, _ => true).Should().Equal(2, 3, 4, 5, 6);
        StatusReportPhases.SequenceOf([0, 1, 2], _ => false).Should().Equal(0, 1, 2);
        StatusReportPhases.SequenceOf(new List<int>(), _ => false).Should().BeEmpty();
    }

    [Fact(DisplayName = "Relatório abre sem reparo")]
    public void Report_PassesTheValidator_WithTheSlidesInTheOrderOfTheTemplate()
    {
        using var document = Render(Data() with { Risks = [Risk(539), Risk(541)], Hours = Hours(200m) });

        new OpenXmlValidator().Validate(document, TestContext.Current.CancellationToken).Should().BeEmpty();
        NamesOf(document).Should().Equal("Status do projeto", "Cronograma", "Próximos passos", "Risco", "Risco", "Horas do projeto");
        document.PresentationPart!.Presentation!.SlideSize!.Cx!.Value.Should().Be(12192000);
        foreach (var slide in Slides(document))
        {
            slide.Descendants<P.NonVisualDrawingProperties>().Select(properties => properties.Id!.Value)
                .Should().OnlyHaveUniqueItems("the copies of the blocks take ids of their own");
        }
    }

    [Fact(DisplayName = "Relatório sem riscos")]
    public void ReportWithoutRisks_HasNoSlideOfARisk()
    {
        using var document = Render(Data());

        new OpenXmlValidator().Validate(document, TestContext.Current.CancellationToken).Should().BeEmpty();
        NamesOf(document).Should().Equal("Status do projeto", "Cronograma", "Próximos passos");
    }

    [Fact(DisplayName = "Risco com chamado no fornecedor")]
    public void RiskSlide_ShowsTheTicket_TheVendor_AndTheMilestones()
    {
        using var document = Render(Data() with { Risks = [Risk(539)] });

        var slide = Slides(document)[3];
        var text = TextOf(slide);
        text.Should().Contain("Risco 1 de 1").And.Contain("#539 · Rejeição 539 na emissão").And.Contain("Em andamento · prazo vencido");
        text.Should().Contain("Classificação: Alta").And.Contain("Depende do retorno do fornecedor");
        text.Should().Contain("Fornecedor Fictício · 77812 · Em análise").And.Contain("Homologação").And.Contain("04/12").And.Contain("em 10 dias úteis");
        FillOf(ShapeNamed(slide, "etiqueta.classificacao")).Should().Be("FCE4E4");
        FillOf(ShapeNamed(slide, "etiqueta.status")).Should().Be("E3F2FB");
        FillOf(ShapeNamed(slide, "etiqueta.prioridade")).Should().Be("FDF1DC");
        SvgOf(PictureNamed(slide, "etiqueta.classificacao.icone")).Should().Contain("#A12222");
    }

    [Fact]
    public void RiskSlides_CountTheRisks()
    {
        using var document = Render(Data() with { Risks = [Risk(539), Risk(541)] });

        TextOf(Slides(document)[3]).Should().Contain("Risco 1 de 2").And.Contain("#539");
        TextOf(Slides(document)[4]).Should().Contain("Risco 2 de 2").And.Contain("#541");
    }

    [Fact(DisplayName = "Relatório sem horas")]
    public void ReportWithoutHours_HasNoSlideOfTheHours()
    {
        using var document = Render(Data() with { Hours = null });

        NamesOf(document).Should().NotContain("Horas do projeto");
        string.Concat(Slides(document).Select(TextOf)).Should().NotContain("Contratadas").And.NotContain("h apontadas");
    }

    [Fact(DisplayName = "Relatório com horas")]
    public void ReportWithHours_ShowsTheTotals_TheConsumption_AndTheHoursOfTheTasks()
    {
        using var document = Render(Data() with { Hours = Hours(200m) });

        var slide = Slides(document).Last();
        var text = TextOf(slide);
        text.Should().Contain("200 h").And.Contain("20 h").And.Contain("180 h").And.Contain("40 h").And.Contain("Dentro das horas");
        text.Should().Contain("180 de 220 h · 82%");
        text.Should().Contain("Configurar plano de contas").And.Contain("32 h").And.Contain("6 h apontadas fora das tarefas do cronograma");
        FillOf(ShapeNamed(slide, "etiqueta.horas")).Should().Be("E3F5EC");
        var track = BoxOf(ShapeNamed(slide, "barra.horas.trilho")).Width;
        ((double)BoxOf(ShapeNamed(slide, "barra.horas")).Width / track).Should().BeApproximately(180 / 220.0, 0.001);
    }

    [Fact(DisplayName = "Projeto sem horas contratadas")]
    public void ProjectWithoutContractedHours_SaysSo_AndShowsOnlyTheLoggedHours()
    {
        using var document = Render(Data() with { Hours = Hours(null) });

        var slide = Slides(document).Last();
        TextOf(slide).Should().Contain("Sem horas contratadas").And.Contain("180 h apontadas, sem horas contratadas");
        FillOf(ShapeNamed(slide, "etiqueta.horas")).Should().Be("EFF1F4");
        HasShape(slide, "barra.horas").Should().BeFalse("without contracted hours there is nothing to consume");
    }

    [Fact]
    public void HoursOfManyTasks_ShowFifteen_AndTheTotalsOfTheOthers()
    {
        var tasks = Enumerable.Range(1, 18).Select(index => new ReportTaskHours(Guid.CreateVersion7(), $"Tarefa {index}", 10m, 4.5m)).ToList();

        using var document = Render(Data() with { Hours = Hours(200m) with { Tasks = tasks } });

        Blocks(Slides(document).Last(), "lista.horasTarefa").Should().HaveCount(15);
        TextOf(Slides(document).Last()).Should().Contain("e mais 3 tarefas, com 30 h previstas e 13,5 h apontadas");
    }

    [Fact]
    public void TaskAboveItsPlannedHours_HasItsBarInTheToneOfDanger()
    {
        List<ReportTaskHours> tasks =
        [
            new(Guid.CreateVersion7(), "Levantar processos", 40m, 46m),
            new(Guid.CreateVersion7(), "Configurar plano de contas", 60m, 30m),
        ];

        using var document = Render(Data() with { Hours = Hours(200m) with { Tasks = tasks } });

        var blocks = Blocks(Slides(document).Last(), "lista.horasTarefa");
        FillOf(ShapeNamed(blocks[0], "barra.horasTarefa")).Should().Be("B42318");
        BoxOf(ShapeNamed(blocks[0], "barra.horasTarefa")).Width.Should().Be(BoxOf(ShapeNamed(blocks[0], "barra.horasTarefa.trilho")).Width);
        FillOf(ShapeNamed(blocks[1], "barra.horasTarefa")).Should().Be("047857");
    }

    [Fact(DisplayName = "Andamento da fase contra o previsto")]
    public void ScheduleSlide_ShowsEachPhaseWithItsProgressAndThePlannedOne()
    {
        using var document = Render(Data());

        var block = Blocks(Slides(document)[1], "lista.fase")[0];
        TextOf(block).Should().Contain("Configuração").And.Contain("13/10/2026 a 30/10/2026").And.Contain("30%");
        var track = BoxOf(ShapeNamed(block, "barra.fase.trilho"));
        var mark = BoxOf(ShapeNamed(block, "marca.fase"));
        ((mark.X + (mark.Width / 2.0) - track.X) / track.Width).Should().BeApproximately(0.5, 0.001);
    }

    [Fact(DisplayName = "Lista vazia")]
    public void EmptyList_TakesTheBlockOut_AndSaysSo()
    {
        using var document = Render(Data() with { LateTasks = new StatusReportList<StatusReportLateTask>([], 0) });

        Blocks(Slides(document)[1], "lista.atraso").Should().BeEmpty();
        TextOf(Slides(document)[1]).Should().Contain("Nenhuma tarefa atrasada.");
        FillOf(ShapeNamed(Slides(document)[1], "etiqueta.atrasos")).Should().Be("E3F5EC");
    }

    [Fact(DisplayName = "Tarefas atrasadas além do limite")]
    public void LateTasks_ShowWhatTheyPush_AndHowManyMore()
    {
        using var document = Render(Data());

        var text = TextOf(Slides(document)[1]);
        text.Should().Contain("Integrar emissão de NF-e").And.Contain("3 dias úteis de atraso")
            .And.Contain("Empurra o marco Homologação em 2 dias úteis e mais 1 marco").And.Contain("e mais 2 tarefas atrasadas");
        text.Should().Contain("Devia ter começado em 16/11");
        ShapeNamed(Slides(document)[1], "etiqueta.atrasos").InnerText.Should().Be("4");
    }

    [Fact(DisplayName = "Projeto sem cronograma")]
    public void ProjectWithoutSchedule_SaysSoInTheProgressThePhasesAndTheMilestones()
    {
        using var document = Render(Data() with
        {
            Progress = null,
            Deadline = null,
            Phases = new StatusReportList<StatusReportPhase>([], 0),
            Sequence = [],
            Milestones = [],
            LateTasks = new StatusReportList<StatusReportLateTask>([], 0),
        });

        new OpenXmlValidator().Validate(document, TestContext.Current.CancellationToken).Should().BeEmpty();
        var first = Slides(document)[0];
        TextOf(first).Should().Contain("Sem cronograma").And.Contain("Nenhum marco em aberto.");
        Blocks(first, "lista.etapa.lado").Should().BeEmpty();
        HasShape(first, "marca.andamento").Should().BeFalse();
        HasShape(first, "etiqueta.proximoMarco").Should().BeFalse("a tag left without text leaves");
        TextOf(Slides(document)[1]).Should().Contain("Sem cronograma.");
    }

    [Fact]
    public void StatusSlide_ShowsTheProjectTheNumbersAndTheSummary()
    {
        using var document = Render(Data());

        var text = TextOf(Slides(document)[0]);
        text.Should().Contain("Relatório de status · 20 de novembro de 2026").And.Contain("Implantação do ERP financeiro");
        text.Should().Contain("ERP-FIN").And.Contain("Gerente: Marina Costa").And.Contain("Cliente: Distribuidora Exemplo").And.Contain("Em andamento");
        text.Should().Contain("62%").And.Contain("previsto 70%").And.Contain("04/12/2026").And.Contain("2 dias úteis depois da linha de base");
        text.Should().Contain("linha de base: 02/12/2026").And.Contain("04/12 · em 10 dias úteis").And.Contain("1 vencido").And.Contain("Cadastros validados");
        TextOf(Slides(document)[0]).Should().NotContain("{{");
        string.Concat(Slides(document).Select(TextOf)).Should().NotContain("{{");
    }

    [Fact]
    public void StatusSlide_ShowsTheSequenceOfThePhases_InTheirTones()
    {
        List<StatusReportPhase> phases =
        [
            .. Enumerable.Range(1, 7).Select(index => new StatusReportPhase(
                $"Fase {index}", new DateOnly(2026, 10, index), new DateOnly(2026, 10, index + 10), index <= 2 ? 100 : index == 3 ? 40 : 0, null)),
        ];

        using var document = Render(Data() with { Sequence = StatusReportPhases.SequenceOf(phases, phase => phase.Progress >= 100) });

        var blocks = Blocks(Slides(document)[0], "lista.etapa.lado");
        blocks.Select(block => ShapeNamed(block, "{{etapa.nome}}").InnerText).Should().Equal("Fase 3", "Fase 4", "Fase 5", "Fase 6", "Fase 7");
        TextOf(blocks[0]).Should().Contain("03/10 a 13/10").And.Contain("40%");
        blocks.Select(block => OffsetOf(block).Y).Distinct().Should().ContainSingle("the sequence goes to the side");
        (OffsetOf(blocks[1]).X - OffsetOf(blocks[0]).X).Should().Be(blocks[0].GroupShapeProperties!.TransformGroup!.Extents!.Cx!.Value);
        FillOf(ShapeNamed(blocks[0], "barra.etapa")).Should().Be("7DD3FC", "in progress, in information, over the dark band");
        HasShape(blocks[1], "barra.etapa").Should().BeFalse("a phase not started has no bar");
    }

    [Fact]
    public void FileName_HasTheCodeTheDayAndTheExtensionOfTheFormat()
    {
        var renderer = new PptxStatusReportRenderer();

        renderer.FileNameOf("ERP-FIN", Today, StatusReportFormat.PowerPoint).Should().Be("ERP-FIN - Relatório de status - 2026-11-20.pptx");
        renderer.FileNameOf("ERP-FIN", Today, StatusReportFormat.Pdf).Should().Be("ERP-FIN - Relatório de status - 2026-11-20.pdf");
    }
}
