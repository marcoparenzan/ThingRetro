using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text;

namespace Thing65.Mos;

public partial class MOS6526
{
    public void Debug(TextWriter writer)
    {
        if (writer == Console.Out)
        {
            Console.SetCursorPosition(0, 0);
        }
        // Data Direction and Ports
        writer.WriteLine($"Data Direction A: ${PortA.Direction:X2}");
        writer.WriteLine($"Data Direction B: ${PortB.Direction:X2}");
        writer.WriteLine($"Port A Pins: ${PortA.Pins:X2}");
        writer.WriteLine($"Port B Pins: ${PortB.Pins:X2}");

        // Timers
        writer.WriteLine($"Timer A: ${timerA:X4} (Latch: ${timerALatch:X4}) [Running: {timerARunning}]");
        writer.WriteLine($"Timer B: ${timerB:X4} (Latch: ${timerBLatch:X4}) [Running: {timerBRunning}]");

        // Control registers
        writer.WriteLine($"Control Register A: ${controlRegA:X2} ({DecodeControlRegA()})");
        writer.WriteLine($"Control Register B: ${controlRegB:X2} ({DecodeControlRegB()})");

        // TOD clock
        writer.WriteLine($"TOD: {todHr & 0x1F:D2}:{todMin:D2}:{todSec:D2}.{tod10thsec:D1} [Running: {todRunning}]");
        writer.WriteLine($"TOD Alarm: {todAlarmHr & 0x1F:D2}:{todAlarmMin:D2}:{todAlarmSec:D2}.{todAlarm10thsec:D1}");

        // Serial port
        writer.WriteLine($"Serial Shift Register: ${serialShift:X2} [Serial Running: {serialRunning}]");

        // Interrupts
        writer.WriteLine($"Interrupt Control: ${interruptControl:X2} ({DecodeInterruptControl()})");
        writer.WriteLine($"Interrupt Status: ${interruptStatus:X2} ({DecodeInterruptStatus()})");

        // Pending states
        writer.WriteLine($"Pending Timer A Underflow: {pendingTimerAUnderflow}");
        writer.WriteLine($"Pending Timer B Underflow: {pendingTimerBUnderflow}");
        writer.WriteLine($"Pending TOD Alarm: {pendingTodAlarm}");
        writer.WriteLine($"Pending Interrupts: ${pendingInterrupts:X2}");
        writer.WriteLine($"TOD Cycle Divider: {todCycleDivider}/{TOD_CYCLE_INTERVAL}");

        writer.WriteLine("=======================================");
    }

    private string DecodeControlRegA()
    {
        var sb = new StringBuilder();

        if ((controlRegA & 0x01) != 0) sb.Append("START ");
        if ((controlRegA & 0x02) != 0) sb.Append("PBON ");
        if ((controlRegA & 0x04) != 0) sb.Append("OUTMODE ");
        if ((controlRegA & 0x08) != 0) sb.Append("RUNMODE ");
        if ((controlRegA & 0x10) != 0) sb.Append("LOAD ");
        if ((controlRegA & 0x20) != 0) sb.Append("INMODE ");
        if ((controlRegA & 0x40) != 0) sb.Append("SPMODE ");
        if ((controlRegA & 0x80) != 0) sb.Append("TODIN ");

        return sb.ToString().Trim();
    }

    private string DecodeControlRegB()
    {
        var sb = new StringBuilder();

        if ((controlRegB & 0x01) != 0) sb.Append("START ");
        if ((controlRegB & 0x02) != 0) sb.Append("PBON ");
        if ((controlRegB & 0x04) != 0) sb.Append("OUTMODE ");
        if ((controlRegB & 0x08) != 0) sb.Append("RUNMODE ");
        if ((controlRegB & 0x10) != 0) sb.Append("LOAD ");
        if ((controlRegB & 0x20) != 0) sb.Append("INMODE ");

        // Timer B specific modes
        if ((controlRegB & 0x40) != 0) sb.Append("COUNT_TA ");
        if ((controlRegB & 0x80) != 0) sb.Append("TOD_ALARM ");

        return sb.ToString().Trim();
    }

    private string DecodeInterruptControl()
    {
        var sb = new StringBuilder();

        if ((interruptControl & 0x01) != 0) sb.Append("TA_EN ");
        if ((interruptControl & 0x02) != 0) sb.Append("TB_EN ");
        if ((interruptControl & 0x04) != 0) sb.Append("ALARM_EN ");
        if ((interruptControl & 0x08) != 0) sb.Append("SP_EN ");
        if ((interruptControl & 0x10) != 0) sb.Append("FLAG_EN ");
        if ((interruptControl & 0x80) != 0) sb.Append("MASK ");

        return sb.ToString().Trim();
    }

    private string DecodeInterruptStatus()
    {
        var sb = new StringBuilder();

        if ((interruptStatus & 0x01) != 0) sb.Append("TA_INT ");
        if ((interruptStatus & 0x02) != 0) sb.Append("TB_INT ");
        if ((interruptStatus & 0x04) != 0) sb.Append("ALARM_INT ");
        if ((interruptStatus & 0x08) != 0) sb.Append("SP_INT ");
        if ((interruptStatus & 0x10) != 0) sb.Append("FLAG_INT ");
        if ((interruptStatus & 0x80) != 0) sb.Append("IRQ ");

        return sb.ToString().Trim();
    }
}