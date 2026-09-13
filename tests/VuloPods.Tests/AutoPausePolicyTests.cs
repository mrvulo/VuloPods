using Microsoft.Extensions.Time.Testing;
using VuloPods.Core.Media;

namespace VuloPods.Tests;

public class AutoPausePolicyTests
{
    private sealed class FakeMedia : IMediaController
    {
        public bool Playing;
        public int PauseCalls;
        public int ResumeCalls;

        public Task<bool> PauseIfPlayingAsync()
        {
            PauseCalls++;
            if (!Playing) return Task.FromResult(false);
            Playing = false;
            return Task.FromResult(true);
        }

        public Task ResumeAsync()
        {
            ResumeCalls++;
            Playing = true;
            return Task.CompletedTask;
        }
    }

    private readonly FakeTimeProvider _time = new();
    private readonly FakeMedia _media = new();

    [Fact]
    public void FirstObservation_DoesNothing()
    {
        using var policy = new AutoPausePolicy(_media, _time);
        _media.Playing = true;
        policy.OnEarState(false, false);
        _time.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(0, _media.PauseCalls);
    }

    [Fact]
    public void RemovingOnePod_PausesAfterDebounce()
    {
        using var policy = new AutoPausePolicy(_media, _time);
        _media.Playing = true;
        policy.OnEarState(true, true);
        policy.OnEarState(false, true);

        _time.Advance(TimeSpan.FromMilliseconds(900));
        Assert.Equal(0, _media.PauseCalls);
        _time.Advance(TimeSpan.FromMilliseconds(200));
        Assert.Equal(1, _media.PauseCalls);
        Assert.False(_media.Playing);
    }

    [Fact]
    public void RemoveAndReinsertWithinDebounce_DoesNothing()
    {
        using var policy = new AutoPausePolicy(_media, _time);
        _media.Playing = true;
        policy.OnEarState(true, true);
        policy.OnEarState(false, true);
        _time.Advance(TimeSpan.FromMilliseconds(500));
        policy.OnEarState(true, true);
        _time.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal(0, _media.PauseCalls);
        Assert.Equal(0, _media.ResumeCalls);
        Assert.True(_media.Playing);
    }

    [Fact]
    public void Reinserting_ResumesOnlyIfWePaused()
    {
        using var policy = new AutoPausePolicy(_media, _time);
        _media.Playing = true;
        policy.OnEarState(true, true);
        policy.OnEarState(false, true);
        _time.Advance(TimeSpan.FromSeconds(1.1));
        Assert.False(_media.Playing);

        policy.OnEarState(true, true);
        _time.Advance(TimeSpan.FromSeconds(1.1));

        Assert.Equal(1, _media.ResumeCalls);
        Assert.True(_media.Playing);
    }

    [Fact]
    public void Reinserting_DoesNotResume_WhenNothingWasPlaying()
    {
        using var policy = new AutoPausePolicy(_media, _time);
        _media.Playing = false;
        policy.OnEarState(true, true);
        policy.OnEarState(false, true);
        _time.Advance(TimeSpan.FromSeconds(1.1));
        policy.OnEarState(true, true);
        _time.Advance(TimeSpan.FromSeconds(1.1));

        Assert.Equal(1, _media.PauseCalls);
        Assert.Equal(0, _media.ResumeCalls);
    }

    [Fact]
    public void RemovingBothPods_PausesOnce()
    {
        using var policy = new AutoPausePolicy(_media, _time);
        _media.Playing = true;
        policy.OnEarState(true, true);
        policy.OnEarState(false, false);
        _time.Advance(TimeSpan.FromSeconds(1.1));
        Assert.Equal(1, _media.PauseCalls);
    }

    [Fact]
    public void ResumeOnlyOnce_AfterMultipleReinserts()
    {
        using var policy = new AutoPausePolicy(_media, _time);
        _media.Playing = true;
        policy.OnEarState(true, true);
        policy.OnEarState(false, false);
        _time.Advance(TimeSpan.FromSeconds(1.1));
        policy.OnEarState(true, false);
        _time.Advance(TimeSpan.FromSeconds(1.1));
        policy.OnEarState(true, true);
        _time.Advance(TimeSpan.FromSeconds(1.1));

        Assert.Equal(1, _media.ResumeCalls);
    }

    [Fact]
    public void Reset_ClearsBaseline_SoNextObservationDoesNothing()
    {
        using var policy = new AutoPausePolicy(_media, _time);
        _media.Playing = true;
        policy.OnEarState(true, true);
        policy.Reset();
        policy.OnEarState(false, false);
        _time.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(0, _media.PauseCalls);
    }
}
