using System.Text;

namespace GerenWare.Projects.Infrastructure.StatusReports;

/// <summary>
/// The icons of the report (ADR-0065, ADR-0168), the same of the screens: each value of a tag has the icon its status
/// has there (<c>projectStatus.ts</c>, <c>TicketTags.tsx</c>, <c>DueDate.tsx</c>, <c>projectHours.ts</c>), and the
/// cards of the next steps have theirs.
/// </summary>
internal enum ReportIcon
{
    Autorenew = 1,
    Block,
    CalendarToday,
    CheckCircle,
    Checklist,
    DragHandle,
    ErrorOutline,
    EventNote,
    FilterAlt,
    Handshake,
    HourglassEmpty,
    Inbox,
    Info,
    KeyboardArrowDown,
    KeyboardArrowUp,
    KeyboardDoubleArrowUp,
    PauseCircle,
    RadioButtonUnchecked,
    Schedule,
    TrackChanges,
    WarningAmber,
}

/// <summary>
/// The images the report brings with it: the icons, as SVG with the paths of <c>@mui/icons-material</c> (MIT), in the
/// color asked, and the logo of the GerenWare for dark backgrounds, from <c>docs/marca/</c>, with its PNG for the
/// versions of the PowerPoint that do not show SVG (ADR-0160).
/// </summary>
internal static class ReportImages
{
    private const string Placeholder = "#000000";

    /// <summary>The logo of the GerenWare for dark backgrounds, never redrawn (ADR-0159).</summary>
    public static ReportPicture ProductLogo { get; } = new(
        Resource("GerenWare.Projects.StatusReports.gerenware-dark.svg"),
        "image/svg+xml",
        Fallback: Resource("GerenWare.Projects.StatusReports.gerenware-dark.png"));

    /// <summary>The icon in the color given, as <c>RRGGBB</c>.</summary>
    public static ReportPicture Icon(ReportIcon icon, string color)
    {
        var svg = Encoding.UTF8.GetString(Resource($"GerenWare.Projects.StatusReports.Icons.{FileOf(icon)}.svg"));
        return new ReportPicture(Encoding.UTF8.GetBytes(svg.Replace(Placeholder, $"#{color}", StringComparison.Ordinal)), "image/svg+xml");
    }

    /// <summary>The name of the file of an icon: its name in kebab case, as <c>check-circle</c>.</summary>
    public static string FileOf(ReportIcon icon)
    {
        var name = icon.ToString();
        var file = new StringBuilder();
        for (var index = 0; index < name.Length; index++)
        {
            if (index > 0 && char.IsUpper(name[index]))
            {
                file.Append('-');
            }

            file.Append(char.ToLowerInvariant(name[index]));
        }

        return file.ToString();
    }

    private static byte[] Resource(string name)
    {
        using var stream = typeof(ReportImages).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"The resource {name} is missing.");
        using var content = new MemoryStream();
        stream.CopyTo(content);
        return content.ToArray();
    }
}
