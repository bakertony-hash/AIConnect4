using System.Globalization;

namespace AIConnect4.App.ViewModels;

/// <summary>
/// Formats Last / Total / live thinking durations for the side panels.
/// Under one minute keeps the short fractional form. At one minute and above uses whole seconds as <c>Xm Ys</c>.
/// </summary>
public static class DecisionTimeFormat
{
    public static string Format(TimeSpan span)
    {
        var totalSeconds = Math.Max(0, span.TotalSeconds);
        if (totalSeconds >= 60)
        {
            // Floor, not round: 154.9s → 2m 34s.
            var wholeSeconds = (long)Math.Floor(totalSeconds);
            var minutes = wholeSeconds / 60;
            var seconds = wholeSeconds % 60;
            return $"{minutes}m {seconds}s";
        }

        if (totalSeconds == 0)
        {
            return "0s";
        }

        return totalSeconds < 10
            ? totalSeconds.ToString("0.00", CultureInfo.InvariantCulture) + " s"
            : totalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s";
    }
}
