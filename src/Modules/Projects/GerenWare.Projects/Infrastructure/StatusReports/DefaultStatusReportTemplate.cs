using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using GerenWare.Projects.Domain;
using GerenWare.SharedKernel;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace GerenWare.Projects.Infrastructure.StatusReports;

/// <summary>
/// The default template of the status report (ADR-0165, ADR-0168), made by code at each report, only with the fields and
/// the labels of the translation file: a 16:9 PowerPoint in Calibri, in the look of the brand, with the colors of the
/// installation (ADR-0146) and the others from the design system. Each slide has the dark band at the top, with the
/// rings and the dots of the panel of the sign-in page, and the content in white cards over the light page. The slides:
/// "Status do projeto", "Cronograma", "Próximos passos", the one of a risk and "Horas do projeto", in this order.
/// Everything is a native shape, so the company can edit its own (Status-6).
/// </summary>
internal static class DefaultStatusReportTemplate
{
    private const long Emu = 914400;
    private const string Font = "Calibri";
    private const double SlideWidth = 13.333;
    private const double Margin = 0.6;
    private const double Right = SlideWidth - Margin;
    private const double Gap = 0.25;
    private const double BandHeight = 1.1;
    private const double HeroBandHeight = 1.7;
    private const double FootTop = 7.13;
    private const double CardTop = 1.4;
    private const double CardHeight = 5.5;

    private static readonly A.TextAlignmentTypeValues Left = A.TextAlignmentTypeValues.Left;
    private static readonly A.TextAlignmentTypeValues Center = A.TextAlignmentTypeValues.Center;
    private static readonly A.TextAlignmentTypeValues RightAligned = A.TextAlignmentTypeValues.Right;

    private static ReportTexts Texts => ReportTexts.Default;

    private static ReportTokens Tokens => ReportTokens.Default;

    /// <summary>The template, with the main and the secondary colors of the installation (<c>#RRGGBB</c>), when it has them.</summary>
    public static byte[] Build(string? mainColor, string? secondaryColor)
    {
        var colors = ReportColors.Of(mainColor);
        using var stream = new MemoryStream();
        using (var document = PresentationDocument.Create(stream, PresentationDocumentType.Presentation))
        {
            var presentationPart = document.AddPresentationPart();
            presentationPart.Presentation = new P.Presentation();
            var masterPart = presentationPart.AddNewPart<SlideMasterPart>();
            var layoutPart = masterPart.AddNewPart<SlideLayoutPart>();
            layoutPart.SlideLayout = new P.SlideLayout(
                new P.CommonSlideData(EmptyTree()) { Name = "Blank" },
                new P.ColorMapOverride(new A.MasterColorMapping()))
            {
                Type = P.SlideLayoutValues.Blank,
            };
            layoutPart.AddPart(masterPart);
            var themePart = masterPart.AddNewPart<ThemePart>();
            themePart.Theme = Theme(colors, secondaryColor);
            masterPart.SlideMaster = new P.SlideMaster(
                new P.CommonSlideData(
                    new P.Background(new P.BackgroundProperties(new A.SolidFill(Rgb(colors.Page)), new A.EffectList())),
                    EmptyTree()),
                new P.ColorMap
                {
                    Background1 = A.ColorSchemeIndexValues.Light1,
                    Text1 = A.ColorSchemeIndexValues.Dark1,
                    Background2 = A.ColorSchemeIndexValues.Light2,
                    Text2 = A.ColorSchemeIndexValues.Dark2,
                    Accent1 = A.ColorSchemeIndexValues.Accent1,
                    Accent2 = A.ColorSchemeIndexValues.Accent2,
                    Accent3 = A.ColorSchemeIndexValues.Accent3,
                    Accent4 = A.ColorSchemeIndexValues.Accent4,
                    Accent5 = A.ColorSchemeIndexValues.Accent5,
                    Accent6 = A.ColorSchemeIndexValues.Accent6,
                    Hyperlink = A.ColorSchemeIndexValues.Hyperlink,
                    FollowedHyperlink = A.ColorSchemeIndexValues.FollowedHyperlink,
                },
                new P.SlideLayoutIdList(new P.SlideLayoutId { Id = 2147483649U, RelationshipId = masterPart.GetIdOfPart(layoutPart) }),
                new P.TextStyles(new P.TitleStyle(), new P.BodyStyle(), new P.OtherStyle()));
            presentationPart.AddPart(themePart);

            var slideIds = new P.SlideIdList();
            uint id = 256;
            foreach (var canvas in new[] { StatusSlide(colors), ScheduleSlide(colors), NextStepsSlide(colors), RiskSlide(colors), HoursSlide(colors) })
            {
                var slidePart = presentationPart.AddNewPart<SlidePart>();
                slidePart.Slide = canvas.Slide();
                slidePart.AddPart(layoutPart);
                canvas.Attach(slidePart);
                slideIds.Append(new P.SlideId { Id = id++, RelationshipId = presentationPart.GetIdOfPart(slidePart) });
            }

            presentationPart.Presentation.Append(
                new P.SlideMasterIdList(new P.SlideMasterId { Id = 2147483648U, RelationshipId = presentationPart.GetIdOfPart(masterPart) }),
                slideIds,
                new P.SlideSize { Cx = 12192000, Cy = 6858000 },
                new P.NotesSize { Cx = 6858000, Cy = 9144000 });
            presentationPart.Presentation.Save();
        }

        return stream.ToArray();
    }

    /// <summary>
    /// "Status do projeto": the larger band with the client, the project, its situation and the installation; the four
    /// numbers; the summary and the milestones ahead; and the dark band with the end and the sequence of the phases.
    /// </summary>
    private static Canvas StatusSlide(ReportColors colors)
    {
        var canvas = new Canvas(Texts["slides.status"], colors);
        canvas.Band(HeroBandHeight);
        canvas.Rect(Texts["shapes.clientFrame"], Margin, 0.42, 1.35, 0.92, colors.Surface, null, "radius-lg");
        canvas.Text(StatusReportFields.ClientLogo, 0.7, 0.5, 1.15, 0.76, 11, Center, A.TextAnchoringTypeValues.Center,
            Run("{{cliente.nome}}", 11, true, colors.Text));
        canvas.Text(Texts["shapes.eyebrow"], 2.2, 0.36, 7.4, 0.3, 11, Left, A.TextAnchoringTypeValues.Top,
            Run($"{Texts["report"]} · {{{{relatorio.dataExtensa}}}}", 11, false, colors.BandMuted));
        canvas.Text(Texts["shapes.title"], 2.2, 0.66, 7.6, 0.56, 28, Left, A.TextAnchoringTypeValues.Top, true, null, Run("{{projeto.nome}}", 28, true, colors.BandText));
        canvas.Text(Texts["shapes.project"], 2.2, 1.22, 7.4, 0.3, 12, Left, A.TextAnchoringTypeValues.Top,
            Run("{{projeto.codigo}}  ·  ", 12, false, colors.BandMuted),
            Run(Texts["labels.managerLabel"], 12, false, colors.BandMuted),
            Run("{{projeto.gerente}}", 12, false, colors.BandMuted),
            Run("  ·  " + Texts["labels.clientLabel"], 12, false, colors.BandMuted),
            Run("{{cliente.nome}}", 12, false, colors.BandMuted));
        InstallationLogo(canvas, colors, 0.34);
        canvas.Tag("etiqueta.situacao", Right - 1.45, 1.1, 1.45, 0.32, 10.5, Left, dark: true, Run("{{projeto.situacao}}", 10.5, true));

        // The four numbers.
        const double top = 1.95;
        const double height = 1.42;
        var width = (Right - Margin - (3 * Gap)) / 4;
        double X(int index) => Margin + (index * (width + Gap));
        void Label(int index, string text) =>
            canvas.Text(text, X(index) + 0.22, top + 0.16, width - 0.44, 0.26, 10.5, Left, A.TextAnchoringTypeValues.Top,
                Run(text, 10.5, false, colors.TextSecondary));
        for (var index = 0; index < 4; index++)
        {
            canvas.Card(X(index), top, width, height);
        }

        Label(0, Texts["labels.progress"]);
        canvas.Value("{{andamento.real}}", X(0) + 0.22, top + 0.42, 1.4, 0.5, 30, colors.Text);
        canvas.Text("{{andamento.previsto}}", X(0) + 1.25, top + 0.6, width - 1.47, 0.3, 10.5, RightAligned, A.TextAnchoringTypeValues.Top,
            Run("{{andamento.previsto}}", 10.5, false, colors.TextSecondary));
        canvas.Bar("barra.andamento", X(0) + 0.22, top + 1.08, width - 0.44, 0.11, colors.Track, colors.Accent, "marca.andamento");

        Label(1, Texts["labels.deadline"]);
        canvas.Value("{{prazo.fim}}", X(1) + 0.22, top + 0.42, width - 0.44, 0.5, 26, colors.Text);
        canvas.Tag("etiqueta.prazo", X(1) + 0.22, top + 0.98, width - 0.44, 0.28, 9.5, Left, dark: false, Run("{{prazo.desvio}}", 9.5, true));

        Label(2, Texts["labels.nextMilestone"]);
        canvas.Text("{{marco.proximo}}", X(2) + 0.22, top + 0.4, width - 0.44, 0.52, 15, Left, A.TextAnchoringTypeValues.Center, true, null, Run("{{marco.proximo}}", 15, true, colors.Text));
        canvas.Tag("etiqueta.proximoMarco", X(2) + 0.22, top + 0.98, width - 0.44, 0.28, 9.5, Left, dark: false, Run("{{marco.proximo.quando}}", 9.5, true));

        Label(3, Texts["labels.tickets"]);
        canvas.Value("{{chamados.abertos}}", X(3) + 0.22, top + 0.42, width - 0.44, 0.5, 30, colors.Text);
        canvas.Tag("etiqueta.vencidos", X(3) + 0.22, top + 0.98, width - 0.44, 0.28, 9.5, Left, dark: false, Run("{{chamados.vencidos}}", 9.5, true));

        // The summary and the milestones ahead.
        const double row = 3.62;
        const double summaryWidth = 6.9;
        canvas.Card(Margin, row, summaryWidth, 2.15);
        canvas.Heading(Texts["labels.summary"], Margin + 0.25, row + 0.2, summaryWidth - 0.5);
        canvas.Paragraphs("{{texto.resumo}}", Margin + 0.25, row + 0.6, summaryWidth - 0.5, 1.42, 12);
        var milestonesX = Margin + summaryWidth + Gap;
        canvas.Card(milestonesX, row, Right - milestonesX, 2.15);
        canvas.Heading(Texts["labels.milestones"], milestonesX + 0.25, row + 0.2, Right - milestonesX - 0.5);
        canvas.Note("{{marcos.vazio}}", milestonesX + 0.25, row + 0.62, Right - milestonesX - 0.5);
        var inner = Right - milestonesX - 0.5;
        canvas.Block(StatusReportFields.Milestone, milestonesX + 0.25, row + 0.6, inner, 0.3, (x, y) =>
        {
            canvas.Tag("etiqueta.marco", x, y, 0.86, 0.26, 9.5, Center, dark: false, Run("{{marco.diaCurto}}", 9.5, true));
            canvas.Text("{{marco.nome}}", x + 0.98, y, inner - 2.48, 0.26, 10.5, Left, A.TextAnchoringTypeValues.Center,
                Run("{{marco.nome}}", 10.5, true, colors.Text));
            canvas.Text("{{marco.situacao}}", x + inner - 1.45, y, 1.45, 0.26, 9, RightAligned, A.TextAnchoringTypeValues.Center,
                Run("{{marco.situacao}}", 9, false, colors.TextSecondary));
        });

        // The end and the sequence of the phases, in the dark band at the foot.
        const double panel = 5.98;
        canvas.Gradient(Texts["shapes.endBand"], Margin, panel, Right - Margin, 1.0, "radius-lg");
        canvas.Text(Texts["labels.endForecast"], Margin + 0.25, panel + 0.16, 2.2, 0.25, 10, Left, A.TextAnchoringTypeValues.Top,
            Run(Texts["labels.endForecast"], 10, false, colors.BandMuted));
        canvas.Value("{{prazo.fim}}", Margin + 0.25, panel + 0.36, 2.3, 0.4, 22, colors.BandText);
        canvas.Text("{{prazo.linhaDeBase}}", Margin + 0.25, panel + 0.74, 2.3, 0.2, 9, Left, A.TextAnchoringTypeValues.Top,
            Run("{{prazo.linhaDeBase}}", 9, false, colors.BandMuted));
        canvas.Rect(Texts["shapes.divider"], 3.15, panel + 0.18, 0.012, 0.64, colors.BandTrack, null, null);
        canvas.Text("{{etapas.vazio}}", 3.45, panel + 0.36, 4.0, 0.3, 10, Left, A.TextAnchoringTypeValues.Top,
            Run("{{etapas.vazio}}", 10, false, colors.BandMuted));
        var stage = (Right - 3.45 - Gap) / StatusReportLimits.Sequence;
        canvas.Block(StatusReportFields.Stage + StatusReportFields.Sideways, 3.45, panel + 0.16, stage, 0.8, (x, y) =>
        {
            canvas.Text("{{etapa.nome}}", x, y, stage - 0.3, 0.25, 10, Left, A.TextAnchoringTypeValues.Top,
                Run("{{etapa.nome}}", 10, true, colors.BandText));
            canvas.Text("{{etapa.periodo}}", x, y + 0.26, stage - 0.3, 0.22, 9, Left, A.TextAnchoringTypeValues.Top,
                Run("{{etapa.periodo}}", 9, false, colors.BandMuted));
            canvas.Bar("barra.etapa", x, y + 0.58, stage - 0.85, 0.08, colors.BandTrack, colors.BandAccent, null);
            canvas.Text("{{etapa.andamento}}", x + stage - 0.78, y + 0.51, 0.45, 0.22, 9.5, RightAligned, A.TextAnchoringTypeValues.Top,
                Run("{{etapa.andamento}}", 9.5, true, colors.BandAccent));
        });
        canvas.Foot();
        return canvas;
    }

    /// <summary>"Cronograma": the phases in bars against the planned progress, and the late tasks with what they push.</summary>
    private static Canvas ScheduleSlide(ReportColors colors)
    {
        var canvas = new Canvas(Texts["slides.schedule"], colors);
        canvas.Header(Texts["slides.schedule"]);
        const double phasesWidth = 7.3;
        canvas.Card(Margin, CardTop, phasesWidth, CardHeight);
        canvas.Heading(Texts["labels.phases"], Margin + 0.25, CardTop + 0.2, 3.0);
        canvas.Rect(Texts["shapes.legend"], 5.95, CardTop + 0.29, 0.18, 0.09, colors.Accent, null, "radius-sm");
        canvas.Text(Texts["labels.legendProgress"], 6.18, CardTop + 0.22, 0.85, 0.25, 9, Left, A.TextAnchoringTypeValues.Top,
            Run(Texts["labels.legendProgress"], 9, false, colors.TextSecondary));
        canvas.Rect(Texts["shapes.legend"], 7.05, CardTop + 0.23, 0.025, 0.2, colors.Text, null, null);
        canvas.Text(Texts["labels.planned"], 7.13, CardTop + 0.22, 0.65, 0.25, 9, Left, A.TextAnchoringTypeValues.Top,
            Run(Texts["labels.planned"], 9, false, colors.TextSecondary));
        canvas.Note("{{fases.vazio}}", Margin + 0.25, CardTop + 0.65, phasesWidth - 0.5);
        canvas.Block(StatusReportFields.Phase, Margin + 0.25, CardTop + 0.6, phasesWidth - 0.5, 0.46, (x, y) =>
        {
            canvas.Text("{{fase.nome}}", x, y, 3.05, 0.24, 11, Left, A.TextAnchoringTypeValues.Top, Run("{{fase.nome}}", 11, true, colors.Text));
            canvas.Text("{{fase.periodo}}", x, y + 0.23, 3.05, 0.2, 9, Left, A.TextAnchoringTypeValues.Top,
                Run("{{fase.periodo}}", 9, false, colors.TextSecondary));
            canvas.Bar("barra.fase", x + 3.15, y + 0.13, 2.85, 0.12, colors.Track, colors.Accent, "marca.fase");
            canvas.Text("{{fase.andamento}}", x + 6.1, y + 0.06, 0.7, 0.26, 11, RightAligned, A.TextAnchoringTypeValues.Top,
                Run("{{fase.andamento}}", 11, true, colors.Text));
        });
        canvas.Note("{{fases.mais}}", Margin + 0.25, 6.62, phasesWidth - 0.5, 9);

        var lateX = Margin + phasesWidth + Gap;
        var lateWidth = Right - lateX;
        canvas.Card(lateX, CardTop, lateWidth, CardHeight);
        canvas.Heading(Texts["labels.lateTasks"], lateX + 0.25, CardTop + 0.2, 3.0);
        canvas.Tag("etiqueta.atrasos", Right - 0.25 - 0.62, CardTop + 0.2, 0.62, 0.28, 10, Left, dark: false, Run("{{atrasos.total}}", 10, true));
        canvas.Note("{{atrasos.vazio}}", lateX + 0.25, CardTop + 0.65, lateWidth - 0.5);
        canvas.Block(StatusReportFields.LateTask, lateX + 0.25, CardTop + 0.6, lateWidth - 0.5, 0.9, (x, y) =>
        {
            canvas.Rect(Texts["shapes.box"], x, y, lateWidth - 0.5, 0.8, colors.Page, null, "radius-md");
            canvas.Text("{{atraso.tarefa}}", x + 0.18, y + 0.08, lateWidth - 0.86, 0.26, 11, Left, A.TextAnchoringTypeValues.Top,
                Run("{{atraso.tarefa}}", 11, true, colors.Text));
            canvas.Tag("etiqueta.atraso", x + 0.18, y + 0.42, 2.05, 0.28, 9, Left, dark: false, Run("{{atraso.atraso}}", 9, true));
            canvas.Text("{{atraso.empurra}}", x + 2.35, y + 0.36, lateWidth - 0.5 - 2.5, 0.42, 8.5, Left, A.TextAnchoringTypeValues.Center,
                true, null, Run("{{atraso.empurra}}", 8.5, false, colors.TextSecondary));
        });
        canvas.Note("{{atrasos.mais}}", lateX + 0.25, 6.55, lateWidth - 0.5, 9.5);
        canvas.Foot();
        return canvas;
    }

    /// <summary>"Próximos passos": three cards, each with its icon in a circle of the accent, its title, a stroke and the text.</summary>
    private static Canvas NextStepsSlide(ReportColors colors)
    {
        var canvas = new Canvas(Texts["slides.nextSteps"], colors);
        canvas.Header(Texts["slides.nextSteps"]);
        (ReportIcon Icon, string Label, string Field)[] columns =
        [
            (ReportIcon.TrackChanges, Texts["labels.focus"], "{{texto.foco}}"),
            (ReportIcon.Checklist, Texts["labels.nextSteps"], "{{texto.proximosPassos}}"),
            (ReportIcon.Handshake, Texts["labels.clientNeeds"], "{{texto.cliente}}"),
        ];
        var width = (Right - Margin - (2 * Gap)) / 3;
        for (var index = 0; index < columns.Length; index++)
        {
            var x = Margin + (index * (width + Gap));
            canvas.Card(x, CardTop, width, CardHeight);
            canvas.Ellipse(Texts["shapes.iconCircle"], x + 0.3, CardTop + 0.3, 0.72, 0.72, colors.AccentBackground);
            canvas.Image(Texts["shapes.icon"], x + 0.46, CardTop + 0.46, 0.4, 0.4, ReportImages.Icon(columns[index].Icon, colors.Accent));
            canvas.Text(columns[index].Label, x + 0.3, CardTop + 1.22, width - 0.6, 0.35, 15, Left, A.TextAnchoringTypeValues.Top,
                Run(columns[index].Label, 15, true, colors.Text));
            canvas.Rect(Texts["shapes.stroke"], x + 0.3, CardTop + 1.66, 0.5, 0.04, colors.Accent, null, null);
            canvas.Paragraphs(columns[index].Field, x + 0.3, CardTop + 1.9, width - 0.6, 3.4, 12);
        }

        canvas.Foot();
        return canvas;
    }

    /// <summary>
    /// The slide of a risk: "Risco 1 de 2" in the band; the ticket, its level, its status, its priority and the vendor, the
    /// point of attention in a box of the tone of warning; and the milestones that depend on it.
    /// </summary>
    private static Canvas RiskSlide(ReportColors colors)
    {
        var canvas = new Canvas(Texts["slides.risk"], colors);
        canvas.Header(Texts.Format("slides.riskOf", "{{risco.ordem}}", "{{riscos.total}}"));
        const double ticketWidth = 7.7;
        canvas.Card(Margin, CardTop, ticketWidth, CardHeight);
        canvas.Text(Texts["shapes.ticket"], Margin + 0.25, CardTop + 0.22, 5.15, 0.8, 17, Left, A.TextAnchoringTypeValues.Top, true, null, Run("#{{risco.numero}} · {{risco.titulo}}", 17, true, colors.Text));
        canvas.Tag("etiqueta.classificacao", 6.15, CardTop + 0.28, 1.9, 0.34, 10.5, Left, dark: false,
            Run(Texts["labels.classification"], 10.5, false), Run("{{risco.classificacao}}", 10.5, true));

        (string Label, double X, double Width)[] boxes =
        [
            (Texts["labels.riskStatus"], 0.85, 3.25),
            (Texts["labels.riskPriority"], 4.25, 1.2),
            (Texts["labels.riskVendor"], 5.6, 2.45),
        ];
        const double boxTop = 2.55;
        foreach (var (label, x, width) in boxes)
        {
            canvas.Rect(Texts["shapes.box"], x, boxTop, width, 0.85, colors.Page, null, "radius-md");
            canvas.Text(label, x + 0.15, boxTop + 0.1, width - 0.3, 0.22, 9.5, Left, A.TextAnchoringTypeValues.Top,
                Run(label, 9.5, false, colors.TextSecondary));
        }

        canvas.Tag("etiqueta.status", 1.0, boxTop + 0.42, 2.95, 0.3, 9.5, Left, dark: false, Run("{{risco.status}}", 9.5, true));
        canvas.Tag("etiqueta.prioridade", 4.4, boxTop + 0.42, 0.9, 0.3, 9.5, Left, dark: false, Run("{{risco.prioridade}}", 9.5, true));
        canvas.Text("{{risco.fornecedor}}", 5.75, boxTop + 0.36, 2.15, 0.44, 10, Left, A.TextAnchoringTypeValues.Center, true, null, Run("{{risco.fornecedor}}", 10, true, colors.Text));

        var warning = colors.TagOf(ReportTone.Warning, false);
        canvas.Rect(Texts["shapes.attention"], 0.85, 3.65, 7.2, 3.0, warning.Background, null, "radius-lg");
        canvas.Image(Texts["shapes.icon"], 1.05, 3.85, 0.3, 0.3, ReportImages.Icon(ReportIcon.WarningAmber, warning.Text));
        canvas.Text(Texts["labels.attention"], 1.45, 3.85, 4.0, 0.3, 13, Left, A.TextAnchoringTypeValues.Center,
            Run(Texts["labels.attention"], 13, true, warning.Text));
        canvas.Paragraphs("{{risco.pontoDeAtencao}}", 1.05, 4.3, 6.8, 2.2, 12);

        var milestonesX = Margin + ticketWidth + Gap;
        var inner = Right - milestonesX - 0.5;
        canvas.Card(milestonesX, CardTop, Right - milestonesX, CardHeight);
        canvas.Heading(Texts["labels.riskMilestones"], milestonesX + 0.25, CardTop + 0.2, inner);
        canvas.Note("{{riscoMarcos.vazio}}", milestonesX + 0.25, CardTop + 0.65, inner);
        canvas.Block(StatusReportFields.RiskMilestone, milestonesX + 0.25, CardTop + 0.65, inner, 0.62, (x, y) =>
        {
            canvas.Tag("etiqueta.riscoMarco", x, y, 0.86, 0.46, 11, Center, dark: false, Run("{{riscoMarco.diaCurto}}", 11, true));
            canvas.Text("{{riscoMarco.nome}}", x + 0.98, y, inner - 0.98, 0.26, 11, Left, A.TextAnchoringTypeValues.Top,
                Run("{{riscoMarco.nome}}", 11, true, colors.Text));
            canvas.Text("{{riscoMarco.situacao}}", x + 0.98, y + 0.25, inner - 0.98, 0.22, 9.5, Left, A.TextAnchoringTypeValues.Top,
                Run("{{riscoMarco.situacao}}", 9.5, false, colors.TextSecondary));
        });
        canvas.Note("{{riscoMarcos.mais}}", milestonesX + 0.25, 5.85, inner, 9.5);
        canvas.Foot();
        return canvas;
    }

    /// <summary>
    /// "Horas do projeto", only with "Mostrar horas": the four numbers, the state and the consumption in a card, and the
    /// hours of each task with a bar of the logged against the planned ones.
    /// </summary>
    private static Canvas HoursSlide(ReportColors colors)
    {
        var canvas = new Canvas(Texts["slides.hours"], colors);
        canvas.Header(Texts["slides.hours"]);
        const double numbersWidth = 4.4;
        canvas.Card(Margin, CardTop, numbersWidth, CardHeight);
        (string Label, string Field, double X, double Y)[] numbers =
        [
            (Texts["labels.contracted"], "{{horas.contratadas}}", 0.85, 1.62),
            (Texts["labels.additional"], "{{horas.adicionais}}", 2.85, 1.62),
            (Texts["labels.logged"], "{{horas.apontadas}}", 0.85, 2.62),
            (Texts["labels.balance"], "{{horas.saldo}}", 2.85, 2.62),
        ];
        foreach (var (label, field, x, y) in numbers)
        {
            canvas.Text(label, x, y, 1.9, 0.25, 10.5, Left, A.TextAnchoringTypeValues.Top, Run(label, 10.5, false, colors.TextSecondary));
            canvas.Value(field, x, y + 0.27, 1.95, 0.5, 24, colors.Text);
        }

        canvas.Tag("etiqueta.horas", 0.85, 3.55, 2.6, 0.3, 10, Left, dark: false, Run("{{horas.situacao}}", 10, true));
        canvas.Heading(Texts["labels.consumption"], 0.85, 4.1, 3.9, 12);
        canvas.Text("{{horas.consumo}}", 0.85, 4.4, 3.9, 0.26, 10.5, Left, A.TextAnchoringTypeValues.Top,
            Run("{{horas.consumo}}", 10.5, false, colors.TextSecondary));
        canvas.Bar("barra.horas", 0.85, 4.78, 3.9, 0.16, colors.Track, colors.Accent, null);
        canvas.Text("{{horas.fora}}", 0.85, 5.15, 3.9, 0.5, 9.5, Left, A.TextAnchoringTypeValues.Top,
            Run("{{horas.fora}}", 9.5, false, colors.TextSecondary));

        var tasksX = Margin + numbersWidth + Gap;
        var inner = Right - tasksX - 0.5;
        canvas.Card(tasksX, CardTop, Right - tasksX, CardHeight);
        canvas.Heading(Texts["labels.taskHours"], tasksX + 0.25, CardTop + 0.2, 3.0);
        canvas.Text(Texts["labels.plannedHours"], tasksX + 0.25 + inner - 1.78, CardTop + 0.24, 0.8, 0.25, 9, RightAligned,
            A.TextAnchoringTypeValues.Top, Run(Texts["labels.plannedHours"], 9, false, colors.TextSecondary));
        canvas.Text(Texts["labels.loggedHours"], tasksX + 0.25 + inner - 0.88, CardTop + 0.24, 0.88, 0.25, 9, RightAligned,
            A.TextAnchoringTypeValues.Top, Run(Texts["labels.loggedHours"], 9, false, colors.TextSecondary));
        canvas.Note("{{horasTarefas.vazio}}", tasksX + 0.25, CardTop + 0.65, inner);
        canvas.Block(StatusReportFields.TaskHours, tasksX + 0.25, CardTop + 0.65, inner, 0.3, (x, y) =>
        {
            canvas.Text("{{horasTarefa.nome}}", x, y, inner - 3.4, 0.24, 10.5, Left, A.TextAnchoringTypeValues.Top,
                Run("{{horasTarefa.nome}}", 10.5, false, colors.Text));
            canvas.Bar("barra.horasTarefa", x + inner - 3.28, y + 0.08, 1.35, 0.09, colors.Track, colors.Accent, null);
            canvas.Text("{{horasTarefa.previstas}}", x + inner - 1.78, y, 0.8, 0.24, 10.5, RightAligned, A.TextAnchoringTypeValues.Top,
                Run("{{horasTarefa.previstas}}", 10.5, false, colors.Text));
            canvas.Text("{{horasTarefa.apontadas}}", x + inner - 0.88, y, 0.88, 0.24, 10.5, RightAligned, A.TextAnchoringTypeValues.Top,
                Run("{{horasTarefa.apontadas}}", 10.5, false, colors.Text));
        });
        canvas.Note("{{horasTarefas.mais}}", tasksX + 0.25, 6.58, inner, 9);
        canvas.Foot();
        return canvas;
    }

    /// <summary>
    /// The logo of the installation over the band, at the right (ADR-0168): its shape with the name written in white,
    /// for when there is no logo, and the white frame, which the filling keeps only for a logo for light backgrounds.
    /// </summary>
    private static void InstallationLogo(Canvas canvas, ReportColors colors, double y)
    {
        const double width = 2.6;
        const double height = 0.44;
        canvas.Rect(StatusReportFields.FrameOf(StatusReportFields.InstallationLogo), Right - width - 0.12, y - 0.08, width + 0.24, height + 0.16,
            colors.Surface, null, "radius-md");
        canvas.Text(StatusReportFields.InstallationLogo, Right - width, y, width, height, 16, RightAligned, A.TextAnchoringTypeValues.Center,
            Run("{{instalacao.nome}}", 16, true, colors.BandText));
    }

    private static A.Theme Theme(ReportColors colors, string? secondaryColor)
    {
        static string Hex(string? color, string fallback) => color is { Length: 7 } ? color[1..].ToUpperInvariant() : fallback;
        var name = Product.Name;
        return new A.Theme(
            new A.ThemeElements(
                new A.ColorScheme(
                    new A.Dark1Color(Rgb(colors.Text)),
                    new A.Light1Color(Rgb(colors.Surface)),
                    new A.Dark2Color(Rgb(colors.TextSecondary)),
                    new A.Light2Color(Rgb(colors.Page)),
                    new A.Accent1Color(Rgb(colors.Accent)),
                    new A.Accent2Color(Rgb(Hex(secondaryColor, Tokens.Color("bg-selected")))),
                    new A.Accent3Color(Rgb(colors.Border)),
                    new A.Accent4Color(Rgb(colors.Track)),
                    new A.Accent5Color(Rgb(colors.BandStart)),
                    new A.Accent6Color(Rgb(colors.BandEnd)),
                    new A.Hyperlink(Rgb(colors.AccentText)),
                    new A.FollowedHyperlinkColor(Rgb(colors.AccentText)))
                { Name = name },
                new A.FontScheme(
                    new A.MajorFont(new A.LatinFont { Typeface = Font }, new A.EastAsianFont { Typeface = string.Empty }, new A.ComplexScriptFont { Typeface = string.Empty }),
                    new A.MinorFont(new A.LatinFont { Typeface = Font }, new A.EastAsianFont { Typeface = string.Empty }, new A.ComplexScriptFont { Typeface = string.Empty }))
                { Name = name },
                new A.FormatScheme(
                    new A.FillStyleList(PlaceholderFill(), PlaceholderFill(), PlaceholderFill()),
                    new A.LineStyleList(PlaceholderLine(), PlaceholderLine(), PlaceholderLine()),
                    new A.EffectStyleList(new A.EffectStyle(new A.EffectList()), new A.EffectStyle(new A.EffectList()), new A.EffectStyle(new A.EffectList())),
                    new A.BackgroundFillStyleList(PlaceholderFill(), PlaceholderFill(), PlaceholderFill()))
                { Name = name }),
            new A.ObjectDefaults(),
            new A.ExtraColorSchemeList())
        { Name = name };
    }

    private static A.SolidFill PlaceholderFill() => new(new A.SchemeColor { Val = A.SchemeColorValues.PhColor });

    private static A.Outline PlaceholderLine() => new(new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.PhColor })) { Width = 9525 };

    private static P.ShapeTree EmptyTree() =>
        new(
            new P.NonVisualGroupShapeProperties(
                new P.NonVisualDrawingProperties { Id = 1U, Name = string.Empty },
                new P.NonVisualGroupShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.GroupShapeProperties(new A.TransformGroup()));

    private static long E(double inches) => (long)Math.Round(inches * Emu, MidpointRounding.AwayFromZero);

    private static A.RgbColorModelHex Rgb(string color, int? alpha = null)
    {
        var rgb = new A.RgbColorModelHex { Val = color };
        if (alpha is { } value and < 100000)
        {
            rgb.Append(new A.Alpha { Val = value });
        }

        return rgb;
    }

    /// <summary>A piece of text in Calibri, of the size and the color given; without a color, the one of where it is.</summary>
    private static A.Run Run(string text, double size, bool bold, string? color = null)
    {
        var properties = new A.RunProperties { Language = "pt-BR", FontSize = (int)Math.Round(size * 100), Bold = bold, Dirty = false };
        if (color is not null)
        {
            properties.Append(new A.SolidFill(Rgb(color)));
        }

        properties.Append(new A.LatinFont { Typeface = "+mn-lt" });
        return new A.Run(properties, new A.Text(text));
    }

    /// <summary>The shapes of a slide, with their ids in order, and the images to put in it once it has its part.</summary>
    private sealed class Canvas(string name, ReportColors colors)
    {
        private readonly List<(A.Blip Blip, ReportPicture Picture)> _images = [];
        private List<OpenXmlElement> _shapes = [];
        private uint _next = 2;

        public P.Slide Slide()
        {
            var tree = EmptyTree();
            tree.Append(_shapes);
            return new P.Slide(new P.CommonSlideData(tree) { Name = name }, new P.ColorMapOverride(new A.MasterColorMapping()));
        }

        /// <summary>Puts the images in the slide, once it has its part.</summary>
        public void Attach(SlidePart part)
        {
            foreach (var (blip, picture) in _images)
            {
                StatusReportFiller.Embed(part, blip, picture);
            }

            part.Slide!.Save();
        }

        /// <summary>The band of a slide with its title, the project and the logo of the installation.</summary>
        public void Header(string title)
        {
            Band(BandHeight);
            Text(Texts["shapes.title"], Margin, 0.28, 8.6, 0.45, 22, Left, A.TextAnchoringTypeValues.Top, Run(title, 22, true, colors.BandText));
            Text(Texts["shapes.project"], Margin, 0.72, 8.6, 0.28, 11, Left, A.TextAnchoringTypeValues.Top,
                Run("{{projeto.codigo}} · {{projeto.nome}}", 11, false, colors.BandMuted));
            InstallationLogo(this, colors, 0.33);
        }

        /// <summary>
        /// The dark band at the top (D7): the gradient of the panel of the sign-in page, its glow at the corner, the rings
        /// that end at the foot of the band and at the edge of the slide, and the dots.
        /// </summary>
        public void Band(double height)
        {
            Gradient(Texts["shapes.band"], 0, 0, SlideWidth, height, null);
            Add(Texts["shapes.glow"], 0, 0, 7.0, height, A.ShapeTypeValues.Rectangle, new A.GradientFill(
                new A.GradientStopList(
                    new A.GradientStop(Rgb(colors.BandGlow, 90000)) { Position = 0 },
                    new A.GradientStop(Rgb(colors.BandGlow, 0)) { Position = 100000 }),
                new A.PathGradientFill(new A.FillToRectangle { Left = 8000, Top = 100000, Right = 92000, Bottom = 0 }) { Path = A.PathShadeValues.Circle })
            {
                RotateWithShape = true,
            });
            var (centerX, centerY) = (SlideWidth + 0.08, height + 1.0);
            foreach (var radius in new[] { 1.44, 2.2, 2.97, 3.74 })
            {
                // From where the ring meets the foot of the band to where it meets the edge of the slide, clockwise.
                var start = 180 + (Math.Asin((centerY - height) / radius) * 180 / Math.PI);
                var end = 270 - (Math.Asin((centerX - SlideWidth) / radius) * 180 / Math.PI);
                var geometry = new A.PresetGeometry(new A.AdjustValueList(
                    new A.ShapeGuide { Name = "adj1", Formula = $"val {(long)(start * 60000)}" },
                    new A.ShapeGuide { Name = "adj2", Formula = $"val {(long)(end * 60000)}" }))
                {
                    Preset = A.ShapeTypeValues.Arc,
                };
                Add(Texts["shapes.ring"], centerX - radius, centerY - radius, radius * 2, radius * 2, geometry, new A.NoFill(),
                    new A.Outline(new A.SolidFill(Rgb(colors.BandShape, colors.BandShapeAlpha))) { Width = 12700 });
            }

            for (var column = 0; column < 6; column++)
            {
                for (var line = 0; line < 4; line++)
                {
                    Add(Texts["shapes.dot"], 8.647 + (column * 0.183), 0.197 + (line * 0.183), 0.04, 0.04, A.ShapeTypeValues.Ellipse,
                        new A.SolidFill(Rgb(colors.BandShape, colors.BandShapeAlpha)));
                }
            }
        }

        /// <summary>The foot: the project, the report and the day at the left, and the number of the slide at the right.</summary>
        public void Foot()
        {
            Text(Texts["shapes.footer"], Margin, FootTop, 8.0, 0.22, 9, Left, A.TextAnchoringTypeValues.Top,
                Run($"{{{{projeto.codigo}}}} · {Texts["report"]} · {{{{relatorio.data}}}}", 9, false, colors.TextMuted));
            var paragraph = new A.Paragraph(
                new A.ParagraphProperties { Alignment = RightAligned },
                new A.Field(
                    new A.RunProperties(new A.SolidFill(Rgb(colors.TextMuted)), new A.LatinFont { Typeface = "+mn-lt" }) { Language = "pt-BR", FontSize = 900 },
                    new A.Text("‹#›"))
                {
                    Id = "{6D5F2E1A-3C4B-4E7A-9F21-0B8C7D6E5A41}",
                    Type = "slidenum",
                },
                new A.EndParagraphRunProperties { Language = "pt-BR", FontSize = 900 });
            _shapes.Add(TextShape(Texts["shapes.slideNumber"], Right - 1.0, FootTop, 1.0, 0.22, Body(A.TextAnchoringTypeValues.Top, 0, 0, 0, false, true, paragraph)));
        }

        /// <summary>A white card with the border, the corner of the panels and the light shadow of the menus (D7).</summary>
        public void Card(double x, double y, double width, double height)
        {
            var (down, blur, color, alpha) = Tokens.Shadow("shadow-menu");
            var shadow = new A.EffectList(new A.OuterShadow(Rgb(color, alpha))
            {
                BlurRadius = E(blur / 96),
                Distance = E(down / 96),
                Direction = 5400000,
                Alignment = A.RectangleAlignmentValues.Top,
                RotateWithShape = false,
            });
            Add(Texts["shapes.card"], x, y, width, height, Geometry(A.ShapeTypeValues.RoundRectangle, "radius-lg", Math.Min(width, height)),
                new A.SolidFill(Rgb(colors.Surface)), new A.Outline(new A.SolidFill(Rgb(colors.Border))) { Width = 9525 }, shadow);
        }

        public void Rect(string shapeName, double x, double y, double width, double height, string fill, string? line, string? radius) =>
            Add(shapeName, x, y, width, height, Geometry(radius is null ? A.ShapeTypeValues.Rectangle : A.ShapeTypeValues.RoundRectangle, radius, Math.Min(width, height)),
                new A.SolidFill(Rgb(fill)), line is null ? new A.Outline(new A.NoFill()) : new A.Outline(new A.SolidFill(Rgb(line))) { Width = 9525 });

        public void Ellipse(string shapeName, double x, double y, double width, double height, string fill) =>
            Add(shapeName, x, y, width, height, A.ShapeTypeValues.Ellipse, new A.SolidFill(Rgb(fill)));

        /// <summary>The dark gradient of the band, from its start to its end color.</summary>
        public void Gradient(string shapeName, double x, double y, double width, double height, string? radius) =>
            Add(shapeName, x, y, width, height, Geometry(radius is null ? A.ShapeTypeValues.Rectangle : A.ShapeTypeValues.RoundRectangle, radius, Math.Min(width, height)),
                new A.GradientFill(
                    new A.GradientStopList(
                        new A.GradientStop(Rgb(colors.BandStart)) { Position = 0 },
                        new A.GradientStop(Rgb(colors.BandEnd)) { Position = 100000 }),
                    new A.LinearGradientFill { Angle = 600000, Scaled = false })
                {
                    RotateWithShape = true,
                },
                new A.Outline(new A.NoFill()));

        /// <summary>A bar with its track, and the mark of the planned progress when it has one (D4).</summary>
        public void Bar(string barName, double x, double y, double width, double height, string track, string fill, string? mark)
        {
            Rect(StatusReportFields.TrackOf(barName), x, y, width, height, track, null, "radius-sm");
            Rect(barName, x, y, width, height, fill, null, "radius-sm");
            if (mark is not null)
            {
                Rect(mark, x - 0.0125, y - 0.06, 0.025, height + 0.12, colors.Text, null, null);
            }
        }

        /// <summary>
        /// A tag of a state (D4, D5): the neutral colors of the background where it is, light or dark, and the icon before
        /// the text, centered at the height of the small letters, with the text lifted to the center of the tag.
        /// </summary>
        public void Tag(string tagName, double x, double y, double width, double height, double size, A.TextAlignmentTypeValues alignment, bool dark, params A.Run[] runs)
        {
            var (background, text) = colors.TagOf(ReportTone.Neutral, dark);
            foreach (var run in runs)
            {
                run.RunProperties!.InsertAt(new A.SolidFill(Rgb(text)), 0);
            }

            var icon = size * 1.25 / 72;
            const double padding = 0.09;
            var paragraph = Paragraph(alignment, size, null, runs);
            Add(tagName, x, y, width, height, Geometry(A.ShapeTypeValues.RoundRectangle, "radius-sm", Math.Min(width, height)),
                new A.SolidFill(Rgb(background)), new A.Outline(new A.NoFill()), null,
                Body(A.TextAnchoringTypeValues.Center, padding + icon + 0.05, padding, size * 0.285 / 72, false, false, paragraph));
            Add(StatusReportFields.IconOf(tagName), x + padding, y + ((height - icon) / 2), icon, icon, A.ShapeTypeValues.Ellipse,
                new A.SolidFill(Rgb(text)));
        }

        /// <summary>A block of a list (D3): a group as tall (or as wide, to the side) as one item and the space to the next.</summary>
        public void Block(string list, double x, double y, double width, double height, Action<double, double> draw)
        {
            var outer = _shapes;
            _shapes = [];
            Add(Texts["shapes.spacer"], x, y, width, height, A.ShapeTypeValues.Rectangle, new A.NoFill());
            draw(x, y);
            var children = _shapes;
            _shapes = outer;
            var group = new P.GroupShape(
                new P.NonVisualGroupShapeProperties(
                    new P.NonVisualDrawingProperties { Id = _next++, Name = StatusReportFields.Block + list },
                    new P.NonVisualGroupShapeDrawingProperties(),
                    new P.ApplicationNonVisualDrawingProperties()),
                new P.GroupShapeProperties(new A.TransformGroup(
                    new A.Offset { X = E(x), Y = E(y) },
                    new A.Extents { Cx = E(width), Cy = E(height) },
                    new A.ChildOffset { X = E(x), Y = E(y) },
                    new A.ChildExtents { Cx = E(width), Cy = E(height) })));
            group.Append(children);
            _shapes.Add(group);
        }

        /// <summary>An image of the template, such as an icon, put in the slide once it has its part.</summary>
        public void Image(string imageName, double x, double y, double width, double height, ReportPicture picture)
        {
            var blip = new A.Blip();
            _images.Add((blip, picture));
            _shapes.Add(new P.Picture(
                new P.NonVisualPictureProperties(
                    new P.NonVisualDrawingProperties { Id = _next++, Name = imageName, Description = string.Empty },
                    new P.NonVisualPictureDrawingProperties(new A.PictureLocks { NoChangeAspect = true }),
                    new P.ApplicationNonVisualDrawingProperties()),
                new P.BlipFill(blip, new A.Stretch(new A.FillRectangle())),
                new P.ShapeProperties(Transform(x, y, width, height), new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })));
        }

        public void Text(
            string shapeName,
            double x,
            double y,
            double width,
            double height,
            double size,
            A.TextAlignmentTypeValues alignment,
            A.TextAnchoringTypeValues anchor,
            params A.Run[] runs) => Text(shapeName, x, y, width, height, size, alignment, anchor, false, null, runs);

        /// <summary>A text box; one that shrinks on overflow (D8) gets the scale its lines need from the filling.</summary>
        public void Text(
            string shapeName,
            double x,
            double y,
            double width,
            double height,
            double size,
            A.TextAlignmentTypeValues alignment,
            A.TextAnchoringTypeValues anchor,
            bool autoFit,
            double? spacing = null,
            params A.Run[] runs) =>
            _shapes.Add(TextShape(shapeName, x, y, width, height, Body(anchor, 0, 0, 0, autoFit, true, Paragraph(alignment, size, spacing, runs))));

        /// <summary>A large number of a card, in bold.</summary>
        public void Value(string field, double x, double y, double width, double height, double size, string color) =>
            Text(field, x, y, width, height, size, Left, A.TextAnchoringTypeValues.Top, Run(field, size, true, color));

        /// <summary>The title of a card.</summary>
        public void Heading(string text, double x, double y, double width, double size = 13) =>
            Text(text, x, y, width, 0.3, size, Left, A.TextAnchoringTypeValues.Top, Run(text, size, true, colors.Text));

        /// <summary>A long text the manager writes, with a little more space between the lines, that shrinks on overflow.</summary>
        public void Paragraphs(string field, double x, double y, double width, double height, double size) =>
            Text(field, x, y, width, height, size, Left, A.TextAnchoringTypeValues.Top, true, 1.15, Run(field, size, false, colors.Text));

        /// <summary>A short line in the secondary color, such as the text of an empty list or "e mais N".</summary>
        public void Note(string field, double x, double y, double width, double size = 10.5) =>
            Text(field, x, y, width, size < 10 ? 0.25 : 0.3, size, Left, A.TextAnchoringTypeValues.Top, Run(field, size, false, colors.TextSecondary));

        private void Add(string shapeName, double x, double y, double width, double height, A.ShapeTypeValues type, OpenXmlElement fill) =>
            Add(shapeName, x, y, width, height, new A.PresetGeometry(new A.AdjustValueList()) { Preset = type }, fill, new A.Outline(new A.NoFill()));

        private void Add(
            string shapeName,
            double x,
            double y,
            double width,
            double height,
            A.PresetGeometry geometry,
            OpenXmlElement fill,
            OpenXmlElement line,
            OpenXmlElement? effects = null,
            P.TextBody? body = null)
        {
            var properties = new P.ShapeProperties(Transform(x, y, width, height), geometry, fill, line);
            if (effects is not null)
            {
                properties.Append(effects);
            }

            _shapes.Add(new P.Shape(
                new P.NonVisualShapeProperties(
                    new P.NonVisualDrawingProperties { Id = _next++, Name = shapeName },
                    new P.NonVisualShapeDrawingProperties(),
                    new P.ApplicationNonVisualDrawingProperties()),
                properties,
                body ?? Body(A.TextAnchoringTypeValues.Center, 0, 0, 0, false, true, Paragraph(Left, 10, null))));
        }

        private P.Shape TextShape(string shapeName, double x, double y, double width, double height, P.TextBody body) =>
            new(
                new P.NonVisualShapeProperties(
                    new P.NonVisualDrawingProperties { Id = _next++, Name = shapeName },
                    new P.NonVisualShapeDrawingProperties { TextBox = true },
                    new P.ApplicationNonVisualDrawingProperties()),
                new P.ShapeProperties(Transform(x, y, width, height), new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }, new A.NoFill()),
                body);

        private static A.Transform2D Transform(double x, double y, double width, double height) =>
            new(new A.Offset { X = E(x), Y = E(y) }, new A.Extents { Cx = E(width), Cy = E(height) });

        /// <summary>The geometry; a rounded corner in pixels of the design system, at 96 pixels an inch, on its shorter side.</summary>
        private static A.PresetGeometry Geometry(A.ShapeTypeValues type, string? radius, double shorterSide)
        {
            var adjust = new A.AdjustValueList();
            if (type == A.ShapeTypeValues.RoundRectangle && radius is not null && shorterSide > 0)
            {
                var share = (long)Math.Round(Math.Min(0.5, Tokens.Radius(radius) / 96.0 / shorterSide) * 100000, MidpointRounding.AwayFromZero);
                adjust.Append(new A.ShapeGuide { Name = "adj", Formula = $"val {share}" });
            }

            return new A.PresetGeometry(adjust) { Preset = type };
        }

        private static P.TextBody Body(
            A.TextAnchoringTypeValues anchor, double leftInset, double rightInset, double bottomInset, bool autoFit, bool wrap, params A.Paragraph[] paragraphs)
        {
            var properties = new A.BodyProperties
            {
                Wrap = wrap ? A.TextWrappingValues.Square : A.TextWrappingValues.None,
                LeftInset = (int)E(leftInset),
                TopInset = 0,
                RightInset = (int)E(rightInset),
                BottomInset = (int)E(bottomInset),
                Anchor = anchor,
            };
            if (autoFit)
            {
                properties.Append(new A.NormalAutoFit());
            }

            var body = new P.TextBody(properties, new A.ListStyle());
            body.Append(paragraphs);
            return body;
        }

        private static A.Paragraph Paragraph(A.TextAlignmentTypeValues alignment, double size, double? spacing, params A.Run[] runs)
        {
            var properties = new A.ParagraphProperties { Alignment = alignment };
            if (spacing is { } value)
            {
                properties.Append(new A.LineSpacing(new A.SpacingPercent { Val = (int)(value * 100000) }));
            }

            var paragraph = new A.Paragraph(properties);
            paragraph.Append(runs);
            paragraph.Append(new A.EndParagraphRunProperties { Language = "pt-BR", FontSize = (int)Math.Round(size * 100) });
            return paragraph;
        }
    }
}
