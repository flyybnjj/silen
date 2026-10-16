using Silen.App;
using Xunit;

namespace Silen.Tests;

public class RibbonMotionTimelineTests
{
    [Fact]
    public void MascotStartsItsOwnRevealWithoutAMonogramStage()
    {
        var frame = RibbonMotionTimeline.Evaluate(0);

        Assert.Equal(new RibbonMotionFrame(0, 0, 0, 0, 0, 0, false), frame);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.MinValue)]
    [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidElapsedTimesSafelyReturnToTheInitialFrame(double seconds)
    {
        Assert.Equal(new RibbonMotionFrame(0, 0, 0, 0, 0, 0, false), RibbonMotionTimeline.Evaluate(seconds));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(.075, .15625)]
    [InlineData(.15, .5)]
    [InlineData(.225, .84375)]
    [InlineData(.3, 1)]
    public void MascotAppearsImmediatelyWithAQuietEasedFade(double seconds, double expectedOpacity)
    {
        Assert.Equal(expectedOpacity, RibbonMotionTimeline.Evaluate(seconds).ArtworkOpacity, 12);
    }

    [Theory]
    [InlineData(.4, 0)]
    [InlineData(.675, .5)]
    [InlineData(.95, 1)]
    public void AppBecomesReadableBeforeTheArtworkFinishesSettling(double seconds, double expectedOpacity)
    {
        Assert.Equal(expectedOpacity, RibbonMotionTimeline.Evaluate(seconds).ContentOpacity, 12);
    }

    [Fact]
    public void ReadableContentHasFullPaperBackingWhileTheMascotIsStillProminent()
    {
        var frame = RibbonMotionTimeline.Evaluate(.95);

        Assert.Equal(1, frame.ContentOpacity);
        Assert.True(frame.ArtworkOpacity > .9);
        Assert.Equal(.75, frame.VeilOpacity);
    }

    [Fact]
    public void PaperBackingAppearsWithTheContentRatherThanFollowingTheSlowerArtwork()
    {
        var frame = RibbonMotionTimeline.Evaluate(.675);

        Assert.Equal(.5, frame.ContentOpacity, 12);
        Assert.Equal(.375, frame.VeilOpacity, 12);
    }

    [Theory]
    [InlineData(.65, 0, 0, 1, .3240045078888054)]
    [InlineData(1.225, .5, 14, .75, .75)]
    [InlineData(1.8, 1, 28, .5, .75)]
    public void SettlingSoftensTheArtworkAndAddsPaperBehindTheContent(double seconds, double blend, double blur, double artwork, double veil)
    {
        var frame = RibbonMotionTimeline.Evaluate(seconds);

        Assert.Equal(blend, frame.BackdropBlend, 12);
        Assert.Equal(blur, frame.BlurRadius, 12);
        Assert.Equal(artwork, frame.ArtworkOpacity, 12);
        Assert.Equal(veil, frame.VeilOpacity, 12);
    }

    [Fact]
    public void IntroCompletionSwitchesExactlyWhenTheFinalBackdropIsReady()
    {
        Assert.False(RibbonMotionTimeline.Evaluate(1.8 - 1e-8).IntroComplete);
        var final = RibbonMotionTimeline.Evaluate(1.8);

        Assert.True(final.IntroComplete);
        Assert.Equal(1, final.ContentOpacity);
        Assert.Equal(1, final.BackdropBlend);
        Assert.True(RibbonMotionTimeline.Evaluate(double.MaxValue).IntroComplete);
    }

    [Fact]
    public void BackdropProgressesWithoutOvershootWhileTheMascotAppearsThenRecedes()
    {
        var previous = RibbonMotionTimeline.Evaluate(0);
        for (var milliseconds = 1; milliseconds <= 4000; milliseconds++)
        {
            var frame = RibbonMotionTimeline.Evaluate(milliseconds / 1000d);
            Assert.InRange(frame.BackdropBlend, previous.BackdropBlend, 1);
            Assert.InRange(frame.ContentOpacity, previous.ContentOpacity, 1);
            Assert.InRange(frame.BlurRadius, previous.BlurRadius, 28);
            Assert.InRange(frame.ArtworkOpacity, 0, 1);
            if (milliseconds <= 300) Assert.InRange(frame.ArtworkOpacity, previous.ArtworkOpacity, 1);
            if (milliseconds > 650) Assert.InRange(frame.ArtworkOpacity, .5, previous.ArtworkOpacity);
            Assert.InRange(frame.VeilOpacity, previous.VeilOpacity, .75);
            Assert.True(double.IsFinite(frame.Phase));
            Assert.InRange(frame.Phase, 0, Math.Tau);
            previous = frame;
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(.3)]
    [InlineData(.4)]
    [InlineData(.65)]
    [InlineData(.95)]
    [InlineData(1.8)]
    public void StageBoundariesDoNotJumpVisualValues(double seconds)
    {
        var before = RibbonMotionTimeline.Evaluate(seconds - 1e-8);
        var after = RibbonMotionTimeline.Evaluate(seconds + 1e-8);
        var differences = new[] { after.BackdropBlend - before.BackdropBlend,
            after.ContentOpacity - before.ContentOpacity, after.BlurRadius - before.BlurRadius,
            after.ArtworkOpacity - before.ArtworkOpacity, after.VeilOpacity - before.VeilOpacity,
            after.Phase - before.Phase };

        Assert.All(differences, difference => Assert.InRange(Math.Abs(difference), 0, 1e-5));
    }

    [Fact]
    public void MascotMotionStartsAtLaunchAndRepeatsWithoutUnboundedPhase()
    {
        Assert.Equal(0, RibbonMotionTimeline.Evaluate(0).Phase);
        Assert.Equal(Math.PI, RibbonMotionTimeline.Evaluate(4).Phase, 12);
        Assert.Equal(0, RibbonMotionTimeline.Evaluate(8).Phase, 12);
        Assert.Equal(RibbonMotionTimeline.Evaluate(5).Phase, RibbonMotionTimeline.Evaluate(13).Phase, 12);
        Assert.InRange(RibbonMotionTimeline.Evaluate(double.MaxValue).Phase, 0, Math.Tau);
    }

    [Theory]
    [InlineData(false, true, true, false, false, false, false)]
    [InlineData(true, false, true, false, false, false, false)]
    [InlineData(true, true, false, false, false, false, false)]
    [InlineData(true, true, true, true, false, false, false)]
    [InlineData(true, true, true, false, true, false, false)]
    [InlineData(true, true, true, false, false, true, false)]
    [InlineData(true, true, true, false, false, false, true)]
    public void EveryWindowAndAccessibilityPauseConditionStopsRendering(bool loaded, bool visible, bool active,
        bool minimized, bool closing, bool reducedMotion, bool highContrast)
    {
        Assert.False(RibbonMotionTimeline.CanRun(loaded, visible, active, minimized, closing, reducedMotion, highContrast, false, false));
        Assert.False(RibbonMotionTimeline.CanRun(loaded, visible, active, minimized, closing, reducedMotion, highContrast, false, true));
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void BusyOperationsPauseAmbientMotionWithoutFreezingTheStartupReveal(bool busy, bool introComplete, bool expected)
    {
        Assert.Equal(expected, RibbonMotionTimeline.CanRun(true, true, true, false, false, false, false, busy, introComplete));
    }
}
