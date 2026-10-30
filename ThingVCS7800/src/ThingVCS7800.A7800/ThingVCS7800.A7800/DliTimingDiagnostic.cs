using ThingRetroEmu.Video;
using ThingVCS;
namespace ThingVCS7800.A7800;

public sealed record DliEdge(long MasterClock, int Scanline, int Dot, bool Halt);
public sealed record DliTimingResult(string Kind, VideoSystem System, int Frames, long ExpectedEdges, long ObservedEdges, byte ExpectedHandlerCounter, byte ObservedHandlerCounter, long OverheadClocks, long ClampedLines, long TruncatedLines, bool Passed)
{
    public bool StartupDli { get; init; }
    public long ServicedInterrupts { get; init; }
    public long UnservicedInterrupts => ObservedEdges - ServicedInterrupts;
    public long? MinimumEdgeSpacing { get; init; }
    public IReadOnlyList<DliEdge> Edges { get; init; } = [];
    // Times are observed after Board.Tick, not electrical bus timestamps or RTI completion.
    public IReadOnlyList<long> HandlerObservationClocks { get; init; } = [];
    public long? MinimumHandlerLatency { get; init; }
    public long? MaximumHandlerLatency { get; init; }
}

/// <summary>Synthetic diagnostic; it measures this core, not physical hardware timing.</summary>
public sealed class DliTimingDiagnostic
{
    public DliTimingResult Run(int frames = 2, bool startupDli = false, VideoSystem system = VideoSystem.Ntsc)
    {
        if (frames is < 1 or > 120) throw new ArgumentOutOfRangeException(nameof(frames));
        var timing = MariaTiming.For(system);
        var host = new DevelopmentComputer(); host.Board.Video.SystemSelection = system;
        host.Insert(DevelopmentRoms.DisplayListInterrupts(startupDli, timing.ActiveLines));
        var maria = (Maria)host.Board.MariaSocket.Chip!;
        List<DliEdge> edges = [];
        List<long> handlerClocks = [];
        long serviced = 0; byte previous = host.Board.Read(0x1800);
        while (maria.Frames < frames)
        {
            long before = maria.DisplayListInterrupts;
            host.Board.Tick();
            if (maria.DisplayListInterrupts != before)
            {
                long clock = maria.LastDliMasterClock;
                edges.Add(new(clock, (int)(clock / Maria.DotsPerLine % timing.Lines),
                    (int)(clock % Maria.DotsPerLine), maria.DmaBusy));
            }
            byte current = host.Board.Read(0x1800);
            int delta = unchecked((byte)(current - previous));
            serviced += delta;
            for (int i = 0; i < delta; i++) handlerClocks.Add(maria.MasterClocks);
            previous = current;
        }
        long expected = (timing.ActiveLines - (startupDli ? 1L : 2L)) * frames;
        byte expectedCounter = unchecked((byte)expected);
        bool passed = maria.DisplayListInterrupts == expected && serviced == expected && previous == expectedCounter &&
            maria.DliOverheadMasterClocks == expected * Maria.DliOverheadClocks &&
            maria.DliOverheadClampedLines == 0 && maria.TruncatedLines == 0;
        long? spacing = edges.Count < 2 ? null : edges.Zip(edges.Skip(1), (a,b) => b.MasterClock - a.MasterClock).Min();
        // Pair by order only when every generated request was serviced; otherwise
        // attribution is ambiguous and latency must not be reported as verified.
        long[] latencies = edges.Count == handlerClocks.Count && edges.Count > 0
            ? edges.Zip(handlerClocks, (edge, clock) => clock - edge.MasterClock).ToArray() : [];
        return new("Synthetic DLI / INC $1800 / RTI — not hardware certification", system, frames, expected,
            maria.DisplayListInterrupts, expectedCounter, previous, maria.DliOverheadMasterClocks,
            maria.DliOverheadClampedLines, maria.TruncatedLines, passed)
        {
            StartupDli = startupDli, ServicedInterrupts = serviced, MinimumEdgeSpacing = spacing,
            Edges = edges.AsReadOnly(), HandlerObservationClocks = handlerClocks.AsReadOnly(),
            MinimumHandlerLatency = latencies.Length > 0 ? latencies.Min() : null,
            MaximumHandlerLatency = latencies.Length > 0 ? latencies.Max() : null
        };
    }
}
