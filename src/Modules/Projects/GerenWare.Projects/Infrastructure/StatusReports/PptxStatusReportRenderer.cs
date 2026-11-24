using System.Globalization;
using GerenWare.Projects.Application;
using GerenWare.Projects.Contracts;
using GerenWare.Projects.Domain;
using GerenWare.SharedKernel;

namespace GerenWare.Projects.Infrastructure.StatusReports;

/// <summary>
/// The status report in PowerPoint (ADR-0165, ADR-0168): the default template with the colors of the installation
/// (<see cref="DefaultStatusReportTemplate"/>), filled (<see cref="StatusReportFiller"/>) with the data written as the
/// screens write it, by the texts of the report, and each state with the tone and the icon it has in the screens.
/// </summary>
internal sealed class PptxStatusReportRenderer : IStatusReportRenderer
{
    /// <summary>How many characters of a name fit in one line of each block of the default template (ADR-0168).</summary>
    private const int PhaseName = 38;
    private const int StageName = 22;
    private const int MilestoneName = 26;
    private const int LateTaskName = 45;
    private const int TaskHoursName = 55;

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private static ReportTexts Texts => ReportTexts.Default;

    public Task<byte[]> RenderAsync(StatusReportData data, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(data);
        var template = DefaultStatusReportTemplate.Build(data.Brand.MainColor, data.Brand.SecondaryColor);
        return Task.FromResult(StatusReportFiller.Fill(template, ValuesOf(data)));
    }

    public string FileNameOf(string projectCode, DateOnly day, StatusReportFormat format) =>
        Texts["fileName"]
            .Replace("{code}", projectCode, StringComparison.Ordinal)
            .Replace("{day}", day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), StringComparison.Ordinal)
        + (format == StatusReportFormat.Pdf ? ".pdf" : ".pptx");

    /// <summary>The values of the fields, as the screens write them (ADR-0013): days, business days, statuses and hours.</summary>
    public static StatusReportValues ValuesOf(StatusReportData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var report = new ReportScope();
        var fields = report.Fields;
        fields["instalacao.nome"] = data.Brand.Name;
        fields["projeto.codigo"] = data.ProjectCode;
        fields["projeto.nome"] = data.ProjectName;
        fields["projeto.situacao"] = Texts[$"projectStatuses.{data.Status}"];
        report.Tags["etiqueta.situacao"] = TagOf(data.Status);
        fields["projeto.gerente"] = string.Join(", ", data.Managers);
        fields["cliente.nome"] = data.ClientName ?? Texts["values.noClient"];
        fields["relatorio.data"] = ReportTexts.Day(data.Today);
        fields["relatorio.dataExtensa"] = data.Today.ToString(Texts["values.longDay"], PtBr);
        report.Pictures[StatusReportFields.InstallationLogo] = InstallationLogoOf(data.Brand);
        report.Pictures[StatusReportFields.ClientLogo] = PictureOf(data.ClientLogo);

        Progress(report, data);
        Deadline(report, data.Deadline);
        Milestones(report, data.Milestones);
        fields["chamados.abertos"] = ReportTexts.Number(data.OpenTickets);
        fields["chamados.vencidos"] = data.OverdueTickets == 0 ? Texts["values.overdueNone"] : Texts.Count("values.overdue", data.OverdueTickets);
        report.Tags["etiqueta.vencidos"] = data.OverdueTickets == 0
            ? new ReportTag(ReportTone.Success, ReportIcon.CheckCircle)
            : new ReportTag(ReportTone.Danger, ReportIcon.WarningAmber);
        fields["texto.resumo"] = data.Texts.Summary;
        fields["texto.foco"] = data.Texts.Focus;
        fields["texto.proximosPassos"] = data.Texts.NextSteps;
        fields["texto.cliente"] = data.Texts.ClientNeeds;

        Phases(report, data);
        LateTasks(report, data.LateTasks);
        if (data.Hours is { } hours)
        {
            Hours(report, hours);
        }

        fields["riscos.total"] = ReportTexts.Number(data.Risks.Count);
        return new StatusReportValues(
            report,
            [.. data.Risks.Select((risk, index) => RiskOf(risk, index + 1))],
            data.Hours is not null,
            ReportColors.Of(data.Brand.MainColor));
    }

    private static void Progress(ReportScope report, StatusReportData data)
    {
        report.Fields["andamento.real"] = data.Progress is { } progress ? Percent(progress.Actual) : Texts["values.none"];
        report.Fields["andamento.previsto"] = data.Progress switch
        {
            null => Texts["values.noSchedule"],
            { Planned: { } planned } => Texts.Format("values.plannedProgress", planned),
            _ => string.Empty,
        };
        report.Bars[StatusReportFields.ProgressBar] = new ReportBar((data.Progress?.Actual ?? 0) / 100m);
        if (data.Progress?.Planned is { } share)
        {
            report.Marks["marca.andamento"] = share / 100m;
        }
    }

    /// <summary>The end against the baseline (ADR-0021), as the overview: after it a warning; before or on it, on time.</summary>
    private static void Deadline(ReportScope report, OverviewDeadline? deadline)
    {
        report.Fields["prazo.fim"] = deadline is null ? Texts["values.none"] : ReportTexts.Day(deadline.End);
        report.Fields["prazo.desvio"] = DeviationOf(deadline);
        report.Fields["prazo.linhaDeBase"] = deadline switch
        {
            null => Texts["values.noSchedule"],
            { BaselineEnd: { } baseline } => Texts.Format("values.baselineDay", ReportTexts.Day(baseline)),
            _ => Texts["values.noBaselineShort"],
        };
        report.Tags["etiqueta.prazo"] = deadline switch
        {
            { Deviation: > 0 } => new ReportTag(ReportTone.Warning, ReportIcon.HourglassEmpty),
            { BaselineEnd: not null, Deviation: not null } => new ReportTag(ReportTone.Success, ReportIcon.CheckCircle),
            _ => new ReportTag(ReportTone.Neutral, ReportIcon.CalendarToday),
        };
    }

    private static void Milestones(ReportScope report, IReadOnlyList<StatusReportMilestone> milestones)
    {
        var next = milestones.FirstOrDefault();
        report.Fields["marco.proximo"] = next is null ? Texts["values.noMilestone"] : Cut(next.Name, 60);
        report.Fields["marco.proximo.quando"] = next is null ? string.Empty : Texts.Format("values.dayAndWhen", ShortDay(next.Day), WhenOf(next));
        report.Tags["etiqueta.proximoMarco"] = next?.DaysLate is > 0
            ? new ReportTag(ReportTone.Danger, ReportIcon.WarningAmber)
            : new ReportTag(ReportTone.Neutral, ReportIcon.CalendarToday);
        report.Lists[StatusReportFields.Milestone] = [.. milestones.Select(milestone => MilestoneOf(milestone, StatusReportFields.Milestone))];
        report.Fields["marcos.vazio"] = milestones.Count == 0 ? Texts["values.noMilestones"] : string.Empty;
    }

    /// <summary>A milestone of a list: its name, its day in full and short, and when it is, with the day in a tag: late, a danger; else, the accent.</summary>
    private static ReportScope MilestoneOf(StatusReportMilestone milestone, string list)
    {
        var scope = Scope(
            ($"{list}.nome", Cut(milestone.Name, MilestoneName)),
            ($"{list}.dia", ReportTexts.Day(milestone.Day)),
            ($"{list}.diaCurto", ShortDay(milestone.Day)),
            ($"{list}.situacao", WhenOf(milestone)));
        scope.Tags[$"{StatusReportFields.Tag}{list}"] = milestone.DaysLate is > 0
            ? new ReportTag(ReportTone.Danger, ReportIcon.WarningAmber)
            : new ReportTag(ReportTone.Accent);
        return scope;
    }

    private static void Phases(ReportScope report, StatusReportData data)
    {
        report.Lists[StatusReportFields.Phase] = [.. data.Phases.Items.Select(phase =>
        {
            var scope = Scope(
                ("fase.nome", Cut(phase.Name, PhaseName)),
                ("fase.periodo", Texts.Format("values.period", ReportTexts.Day(phase.Start), ReportTexts.Day(phase.End))),
                ("fase.andamento", Percent(phase.Progress)),
                ("fase.previsto", phase.Planned is { } planned ? Percent(planned) : Texts["values.none"]));
            scope.Bars["barra.fase"] = new ReportBar(phase.Progress / 100m);
            if (phase.Planned is { } share)
            {
                scope.Marks["marca.fase"] = share / 100m;
            }

            return scope;
        })];
        report.Fields["fases.vazio"] = data.Phases.Items.Count == 0 ? Texts["values.noPhases"] : string.Empty;
        report.Fields["fases.mais"] = data.Phases.More > 0 ? Texts.Count("values.morePhases", data.Phases.More) : string.Empty;

        // The sequence on the first slide (ADR-0168): done in success, in progress in information, not started neutral.
        report.Lists[StatusReportFields.Stage] = [.. data.Sequence.Select(phase =>
        {
            var scope = Scope(
                ("etapa.nome", Cut(phase.Name, StageName)),
                ("etapa.periodo", Texts.Format("values.period", ShortDay(phase.Start), ShortDay(phase.End))),
                ("etapa.andamento", Percent(phase.Progress)));
            scope.Bars["barra.etapa"] = new ReportBar(
                phase.Progress / 100m,
                phase.Progress >= 100 ? ReportTone.Success : phase.Progress > 0 ? ReportTone.Info : ReportTone.Neutral);
            return scope;
        })];
        report.Fields["etapas.vazio"] = data.Sequence.Count == 0 ? Texts["values.noPhases"] : string.Empty;
    }

    private static void LateTasks(ReportScope report, StatusReportList<StatusReportLateTask> tasks)
    {
        report.Lists[StatusReportFields.LateTask] = [.. tasks.Items.Select(task =>
        {
            var scope = Scope(
                ("atraso.tarefa", Cut(task.Name, LateTaskName)),
                ("atraso.atraso", task.NotStarted
                    ? Texts.Format("values.shouldHaveStarted", ShortDay(task.Start))
                    : Texts.Format("values.daysLate", DaysOf(task.DaysLate))),
                ("atraso.empurra", PushesOf(task.Projection)));
            scope.Tags["etiqueta.atraso"] = new ReportTag(ReportTone.Danger, ReportIcon.WarningAmber);
            return scope;
        })];
        var total = tasks.Items.Count + tasks.More;
        report.Fields["atrasos.total"] = ReportTexts.Number(total);
        report.Tags["etiqueta.atrasos"] = total == 0
            ? new ReportTag(ReportTone.Success, ReportIcon.CheckCircle)
            : new ReportTag(ReportTone.Danger, ReportIcon.WarningAmber);
        report.Fields["atrasos.vazio"] = tasks.Items.Count == 0 ? Texts["values.noLateTasks"] : string.Empty;
        report.Fields["atrasos.mais"] = tasks.More > 0 ? Texts.Count("values.moreLateTasks", tasks.More) : string.Empty;
    }

    private static void Hours(ReportScope report, ReportHours hours)
    {
        var fields = report.Fields;
        fields["horas.contratadas"] = hours.Contracted is { } contracted ? HoursOf(contracted) : Texts["values.none"];
        fields["horas.adicionais"] = HoursOf(hours.Additional);
        fields["horas.apontadas"] = HoursOf(hours.Logged);
        fields["horas.saldo"] = hours.Balance is { } balance ? HoursOf(balance) : Texts["values.none"];
        fields["horas.situacao"] = Texts[$"hoursStates.{hours.State}"];
        report.Tags["etiqueta.horas"] = hours.State switch
        {
            ReportHoursState.Over => new ReportTag(ReportTone.Danger, ReportIcon.ErrorOutline),
            ReportHoursState.NearLimit => new ReportTag(ReportTone.Warning, ReportIcon.WarningAmber),
            ReportHoursState.Within => new ReportTag(ReportTone.Success, ReportIcon.CheckCircle),
            _ => new ReportTag(ReportTone.Neutral, ReportIcon.RadioButtonUnchecked),
        };

        // The consumption against the contracted and the additional hours, as the hours of the projects show it.
        var available = hours.Contracted is { } hired ? hired + hours.Additional : 0m;
        fields["horas.consumo"] = available > 0
            ? Texts.Format(
                "values.consumption",
                ReportTexts.Hours(hours.Logged),
                ReportTexts.Hours(available),
                (int)Math.Round(hours.Logged / available * 100, MidpointRounding.AwayFromZero))
            : Texts.Format("values.consumptionNoContract", ReportTexts.Hours(hours.Logged));
        report.Bars["barra.horas"] = new ReportBar(
            available > 0 ? hours.Logged / available : 0m,
            hours.State switch
            {
                ReportHoursState.Over => ReportTone.Danger,
                ReportHoursState.NearLimit => ReportTone.Warning,
                _ => null,
            });

        var tasks = StatusReportList<ReportTaskHours>.Of(hours.Tasks, StatusReportLimits.TaskHours);
        report.Lists[StatusReportFields.TaskHours] = [.. tasks.Items.Select(task =>
        {
            var scope = Scope(
                ("horasTarefa.nome", Cut(task.Name, TaskHoursName)),
                ("horasTarefa.previstas", task.Planned is { } planned ? HoursOf(planned) : Texts["values.none"]),
                ("horasTarefa.apontadas", HoursOf(task.Logged)));
            scope.Bars["barra.horasTarefa"] = task.Planned is > 0
                ? new ReportBar(task.Logged / task.Planned.Value, task.Logged > task.Planned.Value ? ReportTone.Danger : null)
                : new ReportBar(0m);
            return scope;
        })];
        fields["horasTarefas.vazio"] = tasks.Items.Count == 0 ? Texts["values.noTaskHours"] : string.Empty;
        var rest = hours.Tasks.Skip(StatusReportLimits.TaskHours).ToList();
        fields["horasTarefas.mais"] = rest.Count switch
        {
            0 => string.Empty,
            1 => Texts.Format(
                "values.moreTaskHoursOne", ReportTexts.Hours(rest.Sum(task => task.Planned ?? 0m)), ReportTexts.Hours(rest.Sum(task => task.Logged))),
            _ => Texts.Format(
                "values.moreTaskHoursMany",
                rest.Count,
                ReportTexts.Hours(rest.Sum(task => task.Planned ?? 0m)),
                ReportTexts.Hours(rest.Sum(task => task.Logged))),
        };
        fields["horas.fora"] = hours.Outside > 0 ? Texts.Format("values.outsideHours", ReportTexts.Hours(hours.Outside)) : string.Empty;
    }

    private static ReportScope RiskOf(StatusReportRiskItem risk, int order)
    {
        var scope = new ReportScope();
        var fields = scope.Fields;
        fields["risco.ordem"] = ReportTexts.Number(order);
        fields["risco.numero"] = ReportTexts.Number(risk.Number);
        fields["risco.titulo"] = risk.Title;
        var status = Texts[$"ticketStatuses.{risk.Status}"];
        fields["risco.status"] = risk.Overdue ? Texts.Format("values.overdue", status) : status;
        scope.Tags["etiqueta.status"] = TagOf(risk.Status);
        fields["risco.prioridade"] = Texts[$"priorities.{risk.Priority}"];
        scope.Tags["etiqueta.prioridade"] = risk.Priority switch
        {
            ReportTicketPriority.Critical => new ReportTag(ReportTone.Danger, ReportIcon.KeyboardDoubleArrowUp),
            ReportTicketPriority.High => new ReportTag(ReportTone.Warning, ReportIcon.KeyboardArrowUp),
            ReportTicketPriority.Medium => new ReportTag(ReportTone.Neutral, ReportIcon.DragHandle),
            _ => new ReportTag(ReportTone.Neutral, ReportIcon.KeyboardArrowDown),
        };
        fields["risco.classificacao"] = Texts[$"riskLevels.{risk.Level}"];
        scope.Tags["etiqueta.classificacao"] = risk.Level switch
        {
            RiskLevel.High => new ReportTag(ReportTone.Danger, ReportIcon.ErrorOutline),
            RiskLevel.Medium => new ReportTag(ReportTone.Warning, ReportIcon.WarningAmber),
            _ => new ReportTag(ReportTone.Neutral, ReportIcon.Info),
        };
        fields["risco.pontoDeAtencao"] = risk.Attention;
        fields["risco.fornecedor"] = risk.Vendor is { } vendor
            ? Texts.Format("values.vendor", vendor.Vendor, vendor.Code, Texts[$"vendorStatuses.{vendor.Status}"])
            : Texts["values.noVendor"];
        scope.Lists[StatusReportFields.RiskMilestone] = [.. risk.Milestones.Select(milestone => MilestoneOf(milestone, StatusReportFields.RiskMilestone))];
        fields["riscoMarcos.vazio"] = risk.Milestones.Count == 0 ? Texts["values.noRiskMilestones"] : string.Empty;
        fields["riscoMarcos.mais"] = risk.MoreMilestones > 0 ? Texts.Count("values.moreRiskMilestones", risk.MoreMilestones) : string.Empty;
        return scope;
    }

    /// <summary>The status of a project, with the tone and the icon of the screens (<c>projectStatus.ts</c>).</summary>
    private static ReportTag TagOf(ProjectStatus status) => status switch
    {
        ProjectStatus.InProgress => new ReportTag(ReportTone.Info, ReportIcon.Autorenew),
        ProjectStatus.Paused => new ReportTag(ReportTone.Warning, ReportIcon.PauseCircle),
        ProjectStatus.Completed => new ReportTag(ReportTone.Success, ReportIcon.CheckCircle),
        ProjectStatus.Cancelled => new ReportTag(ReportTone.Neutral, ReportIcon.Block),
        _ => new ReportTag(ReportTone.Neutral, ReportIcon.EventNote),
    };

    /// <summary>The status of a ticket, with the tone and the icon of the screens (<c>TicketTags.tsx</c>).</summary>
    private static ReportTag TagOf(ReportTicketStatus status) => status switch
    {
        ReportTicketStatus.Triage => new ReportTag(ReportTone.Neutral, ReportIcon.FilterAlt),
        ReportTicketStatus.InProgress => new ReportTag(ReportTone.Info, ReportIcon.Autorenew),
        ReportTicketStatus.WaitingForRequester => new ReportTag(ReportTone.Warning, ReportIcon.Schedule),
        _ => new ReportTag(ReportTone.Neutral, ReportIcon.Inbox),
    };

    /// <summary>
    /// The logo of the installation over the band (ADR-0168): the one for dark backgrounds; without any, the one of the
    /// GerenWare when the installation has the name of the product; the one for light backgrounds, in its frame; or the
    /// name, written.
    /// </summary>
    private static ReportPicture? InstallationLogoOf(StatusReportBrand brand) =>
        brand.DarkLogo is { } dark ? PictureOf(dark)
        : brand.Logo is null && brand.Name == Product.Name ? ReportImages.ProductLogo
        : brand.Logo is { } light ? PictureOf(light)! with { Framed = true }
        : null;

    /// <summary>The end against the baseline (ADR-0021): after, before or on it, or that there is none.</summary>
    private static string DeviationOf(OverviewDeadline? deadline) => deadline switch
    {
        null => Texts["values.noSchedule"],
        { BaselineEnd: null } or { Deviation: null } => Texts["values.noBaseline"],
        { Deviation: > 0 } => Texts.Format("values.baselineLate", DaysOf(deadline.Deviation.Value)),
        { Deviation: < 0 } => Texts.Format("values.baselineEarly", DaysOf(-deadline.Deviation.Value)),
        _ => Texts["values.baselineOnTime"],
    };

    /// <summary>When a milestone is: late, today or in some business days (ADR-0086).</summary>
    private static string WhenOf(StatusReportMilestone milestone) =>
        milestone.DaysLate is > 0
            ? Texts.Format("values.milestoneLate", DaysOf(milestone.DaysLate.Value))
            : milestone.BusinessDaysLeft == 0
                ? Texts["values.milestoneToday"]
                : Texts.Format("values.milestoneLeft", DaysOf(milestone.BusinessDaysLeft));

    /// <summary>What the projection of a late task pushes (ADR-0164): the nearest milestone and how many more, or the end of the project.</summary>
    private static string PushesOf(ScheduleProjection? projection)
    {
        if (projection is null)
        {
            return string.Empty;
        }

        if (projection.Milestones.Count > 0)
        {
            var first = projection.Milestones[0];
            var text = Texts.Format("values.pushesMilestone", first.Name, DaysOf(first.Days));
            return projection.Milestones.Count switch
            {
                1 => text,
                2 => Texts.Format("values.pushesMoreOne", text),
                _ => Texts.Format("values.pushesMoreMany", text, projection.Milestones.Count - 1),
            };
        }

        return projection.ProjectEnd is { } end
            ? Texts.Format("values.pushesEnd", DaysOf(end.Days))
            : Texts["values.pushesNothing"];
    }

    private static string DaysOf(int days) => Texts.Count("values.businessDays", days);

    private static string HoursOf(decimal hours) => Texts.Format("values.hours", ReportTexts.Hours(hours));

    private static string Percent(int value) => Texts.Format("values.percent", value);

    private static string ShortDay(DateOnly day) => day.ToString("dd/MM", CultureInfo.InvariantCulture);

    private static string Cut(string text, int length) => text.Length <= length ? text : text[..(length - 1)].TrimEnd() + "…";

    private static ReportPicture? PictureOf(StatusReportImage? image) =>
        image is null ? null : new ReportPicture(image.Content, image.ContentType);

    private static ReportScope Scope(params (string Name, string Value)[] fields)
    {
        var scope = new ReportScope();
        foreach (var (name, value) in fields)
        {
            scope.Fields[name] = value;
        }

        return scope;
    }
}
