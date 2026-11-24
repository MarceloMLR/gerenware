namespace GerenWare.Projects.Infrastructure.StatusReports;

/// <summary>
/// The fields of the status report (ADR-0165, ADR-0168), in one place: the lists, whose row of a table or whose block
/// repeats for each item; the slide repeated for each risk; the slide only with "Mostrar horas"; and the shapes by name.
/// The conference of the template of the company (Status-6) checks them.
/// </summary>
internal static class StatusReportFields
{
    /// <summary>The fields of the slide repeated for each risk start with it.</summary>
    public const string Risk = "risco";

    /// <summary>The fields of the slide only with "Mostrar horas" start with it.</summary>
    public const string Hours = "horas";

    public const string Phase = "fase";

    /// <summary>The sequence of the phases on the first slide.</summary>
    public const string Stage = "etapa";

    public const string Milestone = "marco";

    public const string LateTask = "atraso";

    public const string TaskHours = "horasTarefa";

    public const string RiskMilestone = "riscoMarco";

    /// <summary>The lists whose rows repeat in a table.</summary>
    public static IReadOnlyList<string> Lists { get; } = [Phase, Stage, Milestone, LateTask, TaskHours, RiskMilestone];

    /// <summary>A group of shapes with this name and the one of a list repeats for each item: <c>lista.fase</c>.</summary>
    public const string Block = "lista.";

    /// <summary>A block whose name ends with it repeats to the side, and not down.</summary>
    public const string Sideways = ".lado";

    /// <summary>A bar takes the share of its track (<see cref="TrackOf"/>) the value gives: <c>barra.fase</c>.</summary>
    public const string Bar = "barra.";

    /// <summary>A mark goes to the point of the track of its bar the value gives, or leaves without one: <c>marca.fase</c>.</summary>
    public const string Mark = "marca.";

    /// <summary>A tag takes the colors and the icon (<see cref="IconOf"/>) of the tone of its value: <c>etiqueta.situacao</c>.</summary>
    public const string Tag = "etiqueta.";

    /// <summary>The shape of the logo of the installation: a picture with the logo or, without it, the name.</summary>
    public const string InstallationLogo = "logo.instalacao";

    /// <summary>The shape of the logo of the client: a picture with the logo or, without it, the name.</summary>
    public const string ClientLogo = "logo.cliente";

    public const string ProgressBar = "barra.andamento";

    /// <summary>The name of the track of a bar.</summary>
    public static string TrackOf(string bar) => $"{bar}.trilho";

    /// <summary>The name of the bar whose track a mark follows.</summary>
    public static string BarOfMark(string mark) => Bar + mark[Mark.Length..];

    /// <summary>The name of the icon of a tag.</summary>
    public static string IconOf(string tag) => $"{tag}.icone";

    /// <summary>The name of the frame of a logo, kept only when the logo needs it.</summary>
    public static string FrameOf(string logo) => $"{logo}.quadro";

    /// <summary>The prefix of a field, the part before the first dot.</summary>
    public static string PrefixOf(string field) => field.Split('.')[0];
}

/// <summary>
/// How a tag or a bar shows a state (ADR-0065): by the colors of the design system, which never change, with an icon and
/// a text; the accent, the color of the installation, only for what is not a state.
/// </summary>
internal enum ReportTone
{
    Danger = 1,
    Warning = 2,
    Success = 3,
    Neutral = 4,
    Info = 5,
    Accent = 6,
}

/// <summary>The tone of a tag and its icon; the accent has none.</summary>
internal sealed record ReportTag(ReportTone Tone, ReportIcon? Icon = null);

/// <summary>The share of a bar, from 0 to 1, and its tone; without one, it keeps the color of the template.</summary>
internal sealed record ReportBar(decimal Share, ReportTone? Tone = null);

/// <summary>
/// An image: its content and its type, whether it goes in the frame of its shape (a logo for light backgrounds over the
/// band) and, for an SVG, the PNG for the versions of the PowerPoint that do not show it.
/// </summary>
internal sealed record ReportPicture(byte[] Content, string ContentType, bool Framed = false, byte[]? Fallback = null);

/// <summary>
/// The values of one place of the report: the fields, the lists of items, the tags, the bars, the marks and the logos.
/// A value not found here is looked for in the place around it (the report, for a risk or an item of a list).
/// </summary>
internal sealed class ReportScope
{
    public Dictionary<string, string> Fields { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, IReadOnlyList<ReportScope>> Lists { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, ReportTag> Tags { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, ReportBar> Bars { get; } = new(StringComparer.Ordinal);

    /// <summary>The point of each mark on the track of its bar, from 0 to 1.</summary>
    public Dictionary<string, decimal> Marks { get; } = new(StringComparer.Ordinal);

    /// <summary>The logo of each shape; none keeps the text of the shape, the name.</summary>
    public Dictionary<string, ReportPicture?> Pictures { get; } = new(StringComparer.Ordinal);
}

/// <summary>
/// The values of a status report: of the report, of each risk, whether the hours go in (ADR-0165, ADR-0166) and the
/// colors of the tags and the bars (ADR-0168).
/// </summary>
internal sealed record StatusReportValues(ReportScope Report, IReadOnlyList<ReportScope> Risks, bool ShowHours, ReportColors Colors);
