namespace Silen.App;

internal static class RibbonMotionTimeline
{
    public const double IntroDuration = 1.8;
    public const double AmbientPeriodSeconds = 8;

    public static RibbonMotionFrame Evaluate(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) seconds = 0;
        var blend = SmoothStep(seconds, .65, IntroDuration);
        var content = SmoothStep(seconds, .4, .95);
        var artwork = SmoothStep(seconds, 0, .3) * (1 - .5 * blend);
        var phase = Math.Tau * ((seconds % AmbientPeriodSeconds) / AmbientPeriodSeconds);
        return new(blend, content, 28 * blend, artwork, .75 * Math.Max(blend, content),
            phase, seconds >= IntroDuration);
    }

    public static bool CanRun(bool loaded, bool visible, bool active, bool minimized, bool closing,
        bool reducedMotion, bool highContrast, bool busy, bool introComplete) =>
        loaded && visible && active && !minimized && !closing && !reducedMotion && !highContrast && (!busy || !introComplete);

    private static double SmoothStep(double seconds, double start, double end)
    {
        var progress = Math.Clamp((seconds - start) / (end - start), 0, 1);
        return progress * progress * (3 - 2 * progress);
    }
}

internal readonly record struct RibbonMotionFrame(double BackdropBlend, double ContentOpacity,
    double BlurRadius, double ArtworkOpacity, double VeilOpacity, double Phase, bool IntroComplete);
