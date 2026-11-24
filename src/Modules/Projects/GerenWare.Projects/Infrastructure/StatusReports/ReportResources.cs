using System.Globalization;
using System.Text.Json;

namespace GerenWare.Projects.Infrastructure.StatusReports;

/// <summary>
/// The texts of the status report, by key, from the translation file of the report (ADR-0013): the labels of the
/// default template and the words of the values. Nothing of a company is in them (ADR-0003).
/// </summary>
internal sealed class ReportTexts
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private readonly JsonElement _texts;

    private ReportTexts(JsonElement texts)
    {
        _texts = texts;
    }

    public static ReportTexts Default { get; } = new(ReportResource.Load("GerenWare.Projects.StatusReports.pt-BR.json"));

    /// <summary>The text of a key such as <c>labels.progress</c>.</summary>
    public string this[string key]
    {
        get
        {
            var element = _texts;
            foreach (var part in key.Split('.'))
            {
                if (!element.TryGetProperty(part, out element))
                {
                    throw new KeyNotFoundException($"The status report has no text {key}.");
                }
            }

            return element.GetString() ?? throw new KeyNotFoundException($"The text {key} is not a text.");
        }
    }

    /// <summary>The text of a key with its places filled, as <c>{0}</c> and <c>{1}</c>.</summary>
    public string Format(string key, params object[] values) => string.Format(PtBr, this[key], values);

    /// <summary>The text of a key with one or many, as "1 dia útil" and "3 dias úteis".</summary>
    public string Count(string key, int count) =>
        count == 1 ? this[$"{key}One"] : Format($"{key}Many", count.ToString(PtBr));

    /// <summary>A day as the screens show it, 13/10/2026.</summary>
    public static string Day(DateOnly day) => day.ToString("dd/MM/yyyy", PtBr);

    /// <summary>Hours as the screens show them, with up to two decimals: 1.234,5.</summary>
    public static string Hours(decimal hours) => hours.ToString("#,##0.##", PtBr);

    public static string Number(int value) => value.ToString(PtBr);
}

/// <summary>
/// The colors, the corners and the shadows of the design system (ADR-0062 to ADR-0067, ADR-0159), from the same tokens
/// file the screens use, in the light theme and in the dark one, for the report.
/// </summary>
internal sealed class ReportTokens
{
    private readonly Dictionary<string, (string Light, string Dark)> _values;

    private ReportTokens(Dictionary<string, (string Light, string Dark)> values)
    {
        _values = values;
    }

    public static ReportTokens Default { get; } = Load();

    /// <summary>A color as <c>RRGGBB</c>, as the PowerPoint file keeps it, without its transparency.</summary>
    public string Color(string name, bool dark = false) => ValueOf(name, dark)[1..7].ToUpperInvariant();

    /// <summary>The opacity of a color, in thousandths of a percent, as the file keeps it: 100000 for an opaque one.</summary>
    public int Alpha(string name, bool dark = false)
    {
        var value = ValueOf(name, dark);
        return value.Length == 9
            ? (int)Math.Round(int.Parse(value.AsSpan(7, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0 * 100000)
            : 100000;
    }

    /// <summary>A corner, in pixels.</summary>
    public int Radius(string name) =>
        _values.TryGetValue(name, out var value)
            ? int.Parse(value.Light.Replace("px", string.Empty, StringComparison.Ordinal), CultureInfo.InvariantCulture)
            : throw new KeyNotFoundException($"The design system has no corner {name}.");

    /// <summary>The first shadow of a token of the light theme: how far down and how blurred, in pixels, and its color and opacity.</summary>
    public (double Down, double Blur, string Color, int Alpha) Shadow(string name)
    {
        if (!_values.TryGetValue(name, out var value))
        {
            throw new KeyNotFoundException($"The design system has no shadow {name}.");
        }

        var parts = value.Light.Split(',')[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var color = parts[^1];
        return (Pixels(parts[1]), Pixels(parts[2]), color[1..7].ToUpperInvariant(), color.Length == 9
            ? (int)Math.Round(int.Parse(color.AsSpan(7, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0 * 100000)
            : 100000);
    }

    private static double Pixels(string value) =>
        double.Parse(value.Replace("px", string.Empty, StringComparison.Ordinal), CultureInfo.InvariantCulture);

    /// <summary>The value of a color, following a reference to another token (<c>{border}</c>).</summary>
    private string ValueOf(string name, bool dark)
    {
        if (!_values.TryGetValue(name, out var values))
        {
            throw new KeyNotFoundException($"The design system has no color {name}.");
        }

        var value = dark ? values.Dark : values.Light;
        return value.StartsWith('{') ? ValueOf(value[1..^1], dark)
            : value.StartsWith('#') && value.Length is 7 or 9 ? value
            : throw new KeyNotFoundException($"The design system has no color {name}.");
    }

    private static ReportTokens Load()
    {
        var tokens = ReportResource.Load("GerenWare.Projects.StatusReports.tokens.json");
        var values = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
        foreach (var group in new[] { "color", "radius", "shadow" })
        {
            foreach (var token in tokens.GetProperty(group).GetProperty("tokens").EnumerateArray())
            {
                var value = token.GetProperty("value");
                values[token.GetProperty("name").GetString()!] = value.ValueKind == JsonValueKind.Object
                    ? (value.GetProperty("light").GetString()!, value.GetProperty("dark").GetString()!)
                    : (value.GetString()!, value.GetString()!);
            }
        }

        return new ReportTokens(values);
    }
}

internal static class ReportResource
{
    public static JsonElement Load(string name)
    {
        using var stream = typeof(ReportResource).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"The resource {name} is missing.");
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.Clone();
    }
}
