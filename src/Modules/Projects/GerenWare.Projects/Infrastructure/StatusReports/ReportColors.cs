using System.Globalization;

namespace GerenWare.Projects.Infrastructure.StatusReports;

/// <summary>
/// The colors of the status report (ADR-0168), in one place: the ones of the design system for the page, the cards and
/// the texts; the band and the accent, from the main color of the installation when it has one (ADR-0146); and the
/// colors of the states, which never take the color of the installation (ADR-0065), in the light theme over a light
/// background and in the dark one over the band. Every color is <c>RRGGBB</c>, as the PowerPoint file keeps it.
/// </summary>
internal sealed record ReportColors
{
    /// <summary>Below this relative luminance a background is dark, as in the screens: the band and its tags.</summary>
    public const double DarkLuminance = 0.18;

    private const double TextContrast = 4.5;

    private static ReportTokens Tokens => ReportTokens.Default;

    public required string Page { get; init; }

    public required string Surface { get; init; }

    public required string Border { get; init; }

    public required string Text { get; init; }

    public required string TextSecondary { get; init; }

    /// <summary>The foot of the slides and the quieter notes.</summary>
    public required string TextMuted { get; init; }

    /// <summary>The track of a bar over a light background.</summary>
    public required string Track { get; init; }

    /// <summary>The bars, the strokes and the icons: the main color of the installation or the emerald of the brand.</summary>
    public required string Accent { get; init; }

    /// <summary>A text in the accent over its background or a card, darker when it needs, to the contrast of 4.5 to 1.</summary>
    public required string AccentText { get; init; }

    /// <summary>The background of the accent: the day of a milestone and the circle of an icon.</summary>
    public required string AccentBackground { get; init; }

    /// <summary>The start of the gradient of the band.</summary>
    public required string BandStart { get; init; }

    /// <summary>The end of the gradient of the band.</summary>
    public required string BandEnd { get; init; }

    /// <summary>The glow at the corner of the band, as on the sign-in page.</summary>
    public required string BandGlow { get; init; }

    /// <summary>The rings and the dots of the band, white with its transparency.</summary>
    public required string BandShape { get; init; }

    /// <summary>The opacity of the rings and the dots, in thousandths of a percent, as the file keeps it.</summary>
    public required int BandShapeAlpha { get; init; }

    public required string BandText { get; init; }

    public required string BandMuted { get; init; }

    /// <summary>The accent over the band: a light tone of the same hue.</summary>
    public required string BandAccent { get; init; }

    /// <summary>The background of the accent over the band.</summary>
    public required string BandAccentBackground { get; init; }

    /// <summary>The track of a bar over the band, and its divider.</summary>
    public required string BandTrack { get; init; }

    /// <summary>The colors of the report, with the main color of the installation (<c>#RRGGBB</c>) or without one.</summary>
    public static ReportColors Of(string? mainColor)
    {
        var colors = new ReportColors
        {
            Page = Tokens.Color("bg-subtle"),
            Surface = Tokens.Color("bg-surface"),
            Border = Tokens.Color("border"),
            Text = Tokens.Color("text"),
            TextSecondary = Tokens.Color("text-secondary"),
            TextMuted = Tokens.Color("text-placeholder"),
            Track = Tokens.Color("progress-track"),
            Accent = Tokens.Color("action"),
            AccentText = Tokens.Color("action-text"),
            AccentBackground = Tokens.Color("bg-selected"),
            BandStart = Tokens.Color("brand-panel-bg"),
            BandEnd = Tokens.Color("brand-panel-bg-2"),
            BandGlow = Tokens.Color("signin-glow"),
            BandShape = Tokens.Color("brand-panel-shape"),
            BandShapeAlpha = Tokens.Alpha("brand-panel-shape"),
            BandText = Tokens.Color("brand-panel-text"),
            BandMuted = Tokens.Color("brand-panel-muted"),
            BandAccent = Tokens.Color("brand-panel-accent"),
            BandAccentBackground = Tokens.Color("bg-selected", dark: true),
            BandTrack = Oklch.Mix(Tokens.Color("brand-panel-bg-2"), "FFFFFF", 0.12),
        };
        var main = HexOf(mainColor);
        if (main is null || main == colors.Accent)
        {
            return colors;
        }

        // The tokens keep their lightness in the design system, with the hue of the main color and a share of its
        // chroma, as the screens do (ADR-0146): a surface takes a little, an accent nearly all of it.
        var background = Oklch.Recolored(Tokens.Color("bg-selected"), main, 0.1, 0.027);
        var bandEnd = Oklch.Recolored(Tokens.Color("brand-panel-bg-2"), main, 1);
        return colors with
        {
            Accent = main,
            AccentBackground = background,
            AccentText = Oklch.DarkerUntil(main, [background, colors.Surface], TextContrast),
            BandStart = Oklch.Recolored(Tokens.Color("brand-panel-bg"), main, 0.43),
            BandEnd = bandEnd,
            BandGlow = Oklch.Recolored(Tokens.Color("signin-glow"), main, 0.81),
            BandAccent = Oklch.Recolored(Tokens.Color("brand-panel-accent"), main, 0.94),
            BandAccentBackground = Oklch.Recolored(Tokens.Color("bg-selected", dark: true), main, 0.27, 0.07),
            BandTrack = Oklch.Mix(bandEnd, "FFFFFF", 0.12),
        };
    }

    /// <summary>Whether a color is a dark background, where the colors of the dark theme read.</summary>
    public static bool IsDark(string color) => Oklch.Luminance(color) < DarkLuminance;

    /// <summary>The background and the text of a tag of a tone, in the light theme or over a dark background.</summary>
    public (string Background, string Text) TagOf(ReportTone tone, bool dark) => tone switch
    {
        ReportTone.Accent => dark ? (BandAccentBackground, BandAccent) : (AccentBackground, AccentText),
        _ => (Tokens.Color($"status-{NameOf(tone)}-bg", dark), Tokens.Color($"status-{NameOf(tone)}-text", dark)),
    };

    /// <summary>The color of a bar of a tone, the colors of the charts of the design system, light or over a dark background.</summary>
    public string GraphOf(ReportTone tone, bool dark) => tone switch
    {
        ReportTone.Accent => dark ? BandAccent : Accent,
        ReportTone.Danger => Tokens.Color("danger", dark),
        ReportTone.Warning => Tokens.Color("warning", dark),
        ReportTone.Success => Tokens.Color("success", dark),
        ReportTone.Info => Tokens.Color("status-info-text", dark),
        _ => Tokens.Color("text-placeholder", dark),
    };

    private static string NameOf(ReportTone tone) => tone switch
    {
        ReportTone.Danger => "danger",
        ReportTone.Warning => "warning",
        ReportTone.Success => "success",
        ReportTone.Info => "info",
        _ => "neutral",
    };

    private static string? HexOf(string? color) =>
        color is { Length: 7 } && color[0] == '#' && int.TryParse(color.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _)
            ? color[1..].ToUpperInvariant()
            : null;
}

/// <summary>
/// The colors in OKLCH (Björn Ottosson, OKLab), as the screens derive the colors of the installation
/// (<c>web/src/theme/brandColors.ts</c>): a lightness that stays, a hue and a chroma that change.
/// </summary>
internal static class Oklch
{
    /// <summary>The token with the hue of the color chosen and the share of its chroma, at most the limit; the lightness of the token stays.</summary>
    public static string Recolored(string token, string chosen, double share, double limit = double.PositiveInfinity)
    {
        var value = Of(token);
        var target = Of(chosen);
        return HexOf((value.L, Math.Min(limit, target.C * share), target.H));
    }

    /// <summary>Makes a color darker until it has the contrast with each of the backgrounds.</summary>
    public static string DarkerUntil(string color, IReadOnlyList<string> backgrounds, double minimum)
    {
        var start = Of(color);
        var candidate = color;
        for (var step = 1; step <= 100 && backgrounds.Any(background => Contrast(candidate, background) < minimum); step++)
        {
            candidate = HexOf((start.L - (step * 0.01), start.C, start.H));
        }

        return candidate;
    }

    /// <summary>The two colors in sRGB, the second one by the share given.</summary>
    public static string Mix(string first, string second, double share)
    {
        var a = RgbOf(first);
        var b = RgbOf(second);
        return RgbHexOf((a.R + ((b.R - a.R) * share), a.G + ((b.G - a.G) * share), a.B + ((b.B - a.B) * share)));
    }

    /// <summary>The relative luminance of a color (WCAG 2.x).</summary>
    public static double Luminance(string color)
    {
        var (r, g, b) = RgbOf(color);
        return (0.2126 * ToLinear(r)) + (0.7152 * ToLinear(g)) + (0.0722 * ToLinear(b));
    }

    /// <summary>The contrast of two colors (WCAG 2.x).</summary>
    public static double Contrast(string first, string second)
    {
        var (light, dark) = (Luminance(first), Luminance(second));
        if (dark > light)
        {
            (light, dark) = (dark, light);
        }

        return (light + 0.05) / (dark + 0.05);
    }

    public static (double L, double C, double H) Of(string color)
    {
        var (r, g, b) = RgbOf(color);
        (r, g, b) = (ToLinear(r), ToLinear(g), ToLinear(b));
        var l = Math.Cbrt((0.4122214708 * r) + (0.5363325363 * g) + (0.0514459929 * b));
        var m = Math.Cbrt((0.2119034982 * r) + (0.6806995451 * g) + (0.1073969566 * b));
        var s = Math.Cbrt((0.0883024619 * r) + (0.2817188376 * g) + (0.6299787005 * b));
        var lightness = (0.2104542553 * l) + (0.793617785 * m) - (0.0040720468 * s);
        var a = (1.9779984951 * l) - (2.428592205 * m) + (0.4505937099 * s);
        var bb = (0.0259040371 * l) + (0.7827717662 * m) - (0.808675766 * s);
        var hue = Math.Atan2(bb, a) * 180 / Math.PI;
        return (lightness, Math.Sqrt((a * a) + (bb * bb)), hue < 0 ? hue + 360 : hue);
    }

    /// <summary>The OKLCH color as <c>RRGGBB</c>, with the chroma reduced until it fits in sRGB.</summary>
    public static string HexOf((double L, double C, double H) color)
    {
        var target = (L: Math.Clamp(color.L, 0, 1), color.C, color.H);
        if (!InGamut(LinearOf(target)))
        {
            var (low, high) = (0.0, target.C);
            for (var step = 0; step < 24; step++)
            {
                var middle = (low + high) / 2;
                if (InGamut(LinearOf((target.L, middle, target.H))))
                {
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }

            target.C = low;
        }

        var (r, g, b) = LinearOf(target);
        return RgbHexOf((FromLinear(r), FromLinear(g), FromLinear(b)));
    }

    private static (double R, double G, double B) LinearOf((double L, double C, double H) color)
    {
        var a = color.C * Math.Cos(color.H * Math.PI / 180);
        var b = color.C * Math.Sin(color.H * Math.PI / 180);
        var l = Math.Pow(color.L + (0.3963377774 * a) + (0.2158037573 * b), 3);
        var m = Math.Pow(color.L - (0.1055613458 * a) - (0.0638541728 * b), 3);
        var s = Math.Pow(color.L - (0.0894841775 * a) - (1.291485548 * b), 3);
        return (
            (4.0767416621 * l) - (3.3077115913 * m) + (0.2309699292 * s),
            (-1.2684380046 * l) + (2.6097574011 * m) - (0.3413193965 * s),
            (-0.0041960863 * l) - (0.7034186147 * m) + (1.707614701 * s));
    }

    private static bool InGamut((double R, double G, double B) rgb) =>
        rgb.R is >= -1e-4 and <= 1 + 1e-4 && rgb.G is >= -1e-4 and <= 1 + 1e-4 && rgb.B is >= -1e-4 and <= 1 + 1e-4;

    private static (double R, double G, double B) RgbOf(string color) =>
        (Channel(color, 0), Channel(color, 2), Channel(color, 4));

    private static double Channel(string color, int start) =>
        int.Parse(color.AsSpan(start, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;

    private static string RgbHexOf((double R, double G, double B) rgb) =>
        string.Concat(new[] { rgb.R, rgb.G, rgb.B }.Select(channel =>
            ((int)Math.Round(Math.Clamp(channel, 0, 1) * 255, MidpointRounding.AwayFromZero)).ToString("X2", CultureInfo.InvariantCulture)));

    private static double ToLinear(double channel) =>
        channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);

    private static double FromLinear(double channel) =>
        channel <= 0.0031308 ? channel * 12.92 : (1.055 * Math.Pow(channel, 1 / 2.4)) - 0.055;
}
