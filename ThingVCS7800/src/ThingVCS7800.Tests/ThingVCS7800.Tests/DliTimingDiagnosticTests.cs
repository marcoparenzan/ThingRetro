using ThingVCS7800.A7800;
using ThingRetroEmu.Video;
namespace ThingVCS7800.Tests;
public class DliTimingDiagnosticTests
{
    [Fact] public void GeneratedPostRenderDiagnosticServicesAllEdgesAndReturnsFromNmi()
    {
        var result = new DliTimingDiagnostic().Run();
        Assert.True(result.Passed);
        Assert.Equal(480, result.ObservedEdges);
        Assert.Equal(224, result.ObservedHandlerCounter);
        Assert.Equal(8160, result.OverheadClocks);
        Assert.Equal(0, result.ClampedLines); Assert.Equal(0, result.TruncatedLines);
        Assert.Contains("Synthetic", result.Kind);
    }
    [Fact] public void DenseStartupFixtureServicesInitialAndSuccessorOnSeparateLines()
    {
        var host = new DevelopmentComputer(); host.Insert(ThingVCS.DevelopmentRoms.DisplayListInterrupts());
        var maria = (ThingVCS.Maria)host.Board.MariaSocket.Chip!;
        while (maria.Frames < 1) host.Board.Tick();
        Assert.Equal(241, maria.DisplayListInterrupts);
        Assert.Equal(241, host.Board.Read(0x1800));
        Assert.Equal(4097, maria.DliOverheadMasterClocks);
    }
    [Theory]
    [InlineData(VideoSystem.Ntsc, 480)]
    [InlineData(VideoSystem.Pal, 580)]
    public void MatrixCountsServicedInterruptsWithoutByteCounterWrapAmbiguity(VideoSystem system, long expected)
    {
        var result = new DliTimingDiagnostic().Run(system: system);
        Assert.True(result.Passed); Assert.Equal(expected, result.ServicedInterrupts);
        Assert.Equal(0, result.UnservicedInterrupts);
        Assert.Equal(result.ObservedEdges, result.Edges.Count);
        Assert.All(result.Edges, edge => Assert.True(edge.Halt));
    }
    [Theory][InlineData(VideoSystem.Ntsc)][InlineData(VideoSystem.Pal)]
    public void StartupMatrixHasNoCoalescenceAndNoDoubleDliOnBootstrapLine(VideoSystem system)
    {
        var result = new DliTimingDiagnostic().Run(startupDli: true, system: system);
        Assert.True(result.Passed); Assert.Equal(0, result.UnservicedInterrupts);
        Assert.Equal(452, result.MinimumEdgeSpacing);
        Assert.Equal(result.ExpectedEdges, result.ObservedEdges);
        Assert.Equal(result.ObservedEdges * 17, result.OverheadClocks);
        Assert.Equal(2, result.Edges.Count(edge => edge.Scanline == 16)); // Exactly one initial edge per frame.
    }
    [Theory]
    [InlineData(VideoSystem.Ntsc, false)]
    [InlineData(VideoSystem.Ntsc, true)]
    [InlineData(VideoSystem.Pal, false)]
    [InlineData(VideoSystem.Pal, true)]
    public void HandlerObservationsFollowEdgesAndFinishBeforeNextRequest(VideoSystem system, bool startup)
    {
        var result = new DliTimingDiagnostic().Run(startupDli: startup, system: system);
        Assert.True(result.Passed);
        Assert.Equal(result.Edges.Count, result.HandlerObservationClocks.Count);
        Assert.NotNull(result.MinimumHandlerLatency); Assert.NotNull(result.MaximumHandlerLatency);
        Assert.True(result.MinimumHandlerLatency >= ThingVCS.Maria.DliOverheadClocks);
        Assert.True(result.MaximumHandlerLatency < result.MinimumEdgeSpacing);
        for (int i = 0; i < result.Edges.Count; i++)
        {
            Assert.True(result.HandlerObservationClocks[i] > result.Edges[i].MasterClock);
            if (i + 1 < result.Edges.Count)
                Assert.True(result.HandlerObservationClocks[i] < result.Edges[i + 1].MasterClock);
        }
    }
    [Theory][InlineData(0)][InlineData(121)]
    public void UnboundedOrEmptyRunsAreRejected(int frames) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new DliTimingDiagnostic().Run(frames));
}
