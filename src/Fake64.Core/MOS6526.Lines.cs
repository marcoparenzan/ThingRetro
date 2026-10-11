using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fake64;

public partial class MOS6526
{
    // Public
    // Public property accessors for configuration from Board
    public ushort TimerALatch
	{
		get => timerALatch;
		set { timerALatch = value; if (!timerARunning) timerA = value; }
	}

	public byte ControlRegA
	{
		get => controlRegA;
		set
		{
			controlRegA = value;
			timerARunning = (value & 0x01) != 0;
			if (timerARunning && (value & 0x10) != 0)
				timerA = timerALatch;
		}
	}

	public byte InterruptControl
	{
		get => interruptControl;
		set => interruptControl = value;
	}

	// Bit della porta A configurati come uscita (gli ingressi sono tenuti alti dai pull-up)
	public byte DataDirectionA => dataDirectionA;

	// Stato della linea /IRQ (o /NMI per la CIA2): attiva finché l'ICR non viene letto
	public bool IrqAsserted => (interruptStatus & 0x80) != 0;
}