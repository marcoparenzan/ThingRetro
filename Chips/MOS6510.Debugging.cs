using System;
using System.Collections.Generic;

namespace Chips;

public partial class MOS6510
{
    private HashSet<ushort> breakpoints = new(); // Breakpoint virtuali
    private bool breakpointHit = false; // Indica se un breakpoint è stato raggiunto

    public void AddBreakpoint(ushort address)
    {
        breakpoints.Add(address);
    }

    public void RemoveBreakpoint(ushort address)
    {
        breakpoints.Remove(address);
    }

    public bool IsBreakpointHit() => breakpointHit;
}