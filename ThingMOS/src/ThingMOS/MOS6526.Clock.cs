using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThingMOS;

public partial class MOS6526
{

    void ProcessSignals()
    {
        // Process Timer A
        if (timerARunning)
        {
            // Will timer A underflow in the next cycle?
            if (timerA == 1)
            {
                pendingTimerAUnderflow = true;
            }
        }

        // Process Timer B
        bool timerBClockByUnderflow = (controlRegB & 0x40) != 0;
        if (timerBRunning && (!timerBClockByUnderflow || pendingTimerAUnderflow))
        {
            // Will timer B underflow in the next cycle?
            if ((timerBClockByUnderflow && pendingTimerAUnderflow && timerB == 1) ||
                (!timerBClockByUnderflow && timerB == 1))
            {
                pendingTimerBUnderflow = true;
            }
        }

        // Process TOD clock - only increment at 1/10 second intervals
        todCycleDivider++;
        if (todCycleDivider >= TOD_CYCLE_INTERVAL)
        {
            todCycleDivider = 0;

            // Will TOD match alarm in the next cycle?
            byte next10th = (byte)((tod10thsec + 1) % 10);
            if (todRunning &&
                next10th == todAlarm10thsec &&
                todSec == todAlarmSec &&
                todMin == todAlarmMin &&
                todHr == todAlarmHr)
            {
                pendingTodAlarm = true;
            }
        }

        // Prepare pending interrupts
        if (pendingTimerAUnderflow && (interruptControl & 0x01) != 0)
            pendingInterrupts |= 0x01;

        if (pendingTimerBUnderflow && (interruptControl & 0x02) != 0)
            pendingInterrupts |= 0x02;

        if (pendingTodAlarm && (interruptControl & 0x04) != 0)
            pendingInterrupts |= 0x04;
    }

    void Clock()
    {
        // Timer B (when counting underflows) must see this cycle's underflow of A,
        // even though the Timer A block clears the pending signal
        bool timerAUnderflowed = timerARunning && pendingTimerAUnderflow;

        // Handle Timer A
        if (timerARunning)
        {
            timerA--;

            if (pendingTimerAUnderflow)
            {
                // Timer A underflow
                if ((controlRegA & 0x08) != 0) // One-shot mode (bit 3 = 1)
                {
                    timerARunning = false;
                    controlRegA &= 0xFE; // Clear bit 0
                }

                // Reload from latch
                timerA = timerALatch;

                // Set interrupt flag
                interruptStatus |= 0x01;

                // Clear pending signal
                pendingTimerAUnderflow = false;
            }
        }

        // Handle Timer B
        if (timerBRunning)
        {
            bool timerBClockByUnderflow = (controlRegB & 0x40) != 0;

            // Only decrement if we're not counting underflows, or if we had an underflow
            if (!timerBClockByUnderflow || timerAUnderflowed)
            {
                timerB--;
            }

            if (pendingTimerBUnderflow)
            {
                // Timer B underflow handling
                if ((controlRegB & 0x08) != 0) // One-shot mode (bit 3 = 1)
                {
                    timerBRunning = false;
                    controlRegB &= 0xFE; // Clear bit 0
                }

                // Reload from latch
                timerB = timerBLatch;

                // Set interrupt flag
                interruptStatus |= 0x02;

                // Clear pending signal
                pendingTimerBUnderflow = false;
            }
        }

        // Handle TOD clock update
        if (todCycleDivider == 0 && todRunning)
        {
            // Update TOD clock registers
            tod10thsec = (byte)((tod10thsec + 1) % 10);

            if (tod10thsec == 0)
            {
                todSec = (byte)((todSec + 1) % 60);

                if (todSec == 0)
                {
                    todMin = (byte)((todMin + 1) % 60);

                    if (todMin == 0)
                    {
                        todHr = (byte)((todHr + 1) % 24);
                    }
                }
            }

            if (pendingTodAlarm)
            {
                // Set alarm interrupt
                interruptStatus |= 0x04;
                pendingTodAlarm = false;
            }
        }

        // Process any pending interrupts
        if (pendingInterrupts != 0)
        {
            // Update internal interrupt status
            interruptStatus |= pendingInterrupts;
            pendingInterrupts = 0;

            CheckInterrupts();
        }
    }

    private void CheckInterrupts()
    {
        // The /IRQ line (bit 7) goes active when an enabled event happens and stays active
        // until the ICR is read; whatever is wired to the pin reads it through Irq
        if ((interruptStatus & interruptControl & 0x1F) != 0)
            interruptStatus |= 0x80;
    }
}