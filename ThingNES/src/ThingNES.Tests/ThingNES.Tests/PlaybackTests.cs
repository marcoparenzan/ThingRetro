using ThingNES;

namespace ThingNES.Tests;

public class PlaybackTests
{
    [Fact] public void PacerMatchesNtscRateWithoutTimerQuantization()
    {
        var pacer = new FramePacer(); int frames = 0;
        for (int i = 0; i < 1000; i++) frames += pacer.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(60, frames);
        for (int i = 0; i < 9000; i++) frames += pacer.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(600, frames);
    }
    [Fact] public void CatchUpIsBoundedAndResetDropsCredit()
    {
        var pacer = new FramePacer(); Assert.Equal(3, pacer.Advance(TimeSpan.FromSeconds(10)));
        Assert.Equal(0, pacer.Advance(TimeSpan.Zero));
        pacer.Advance(TimeSpan.FromMilliseconds(8)); pacer.Reset();
        Assert.Equal(0, pacer.Advance(TimeSpan.FromMilliseconds(8)));
        Assert.Throws<ArgumentOutOfRangeException>(() => pacer.Advance(TimeSpan.FromTicks(-1)));
    }
    [Fact] public void DcFilterSettlesToZeroAndResetIsDeterministic()
    {
        var filter = new PlaybackFilter(); short first = filter.Process(20000), last = 0;
        for (int i = 0; i < 5000; i++) last = filter.Process(20000);
        Assert.True(first > 0); Assert.InRange((int)last, -1, 1);
        filter.Reset(); Assert.Equal(first, filter.Process(20000));
    }
    [Fact] public void FilterKeepsAlternatingSignalWithinPcmRange()
    {
        var filter = new PlaybackFilter(); int min = 0, max = 0;
        for (int i = 0; i < 5000; i++)
        {
            int value = filter.Process(i % 2 == 0 ? short.MaxValue : short.MinValue);
            min = Math.Min(min, value); max = Math.Max(max, value);
        }
        Assert.True(min < 0 && max > 0); Assert.InRange(min, -32768, -1); Assert.InRange(max, 1, 32767);
    }
}
