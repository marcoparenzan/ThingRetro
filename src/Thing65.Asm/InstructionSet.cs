namespace Thing65.Asm;

// What each instruction does, in short: the editor shows it when the mouse hovers over a mnemonic.
// Flags: N negative, V overflow, D decimal, I interrupts disabled, Z zero, C carry
public static class InstructionSet
{
    public static string? Describe(string mnemonic) =>
        Descriptions.TryGetValue(mnemonic.ToUpperInvariant(), out var text) ? text : null;

    public static IReadOnlyDictionary<string, string> Descriptions { get; } = new Dictionary<string, string>
    {
        // Load and store
        ["LDA"] = "Load A from memory (or an immediate value). Flags: N Z",
        ["LDX"] = "Load X. Flags: N Z",
        ["LDY"] = "Load Y. Flags: N Z",
        ["STA"] = "Store A in memory",
        ["STX"] = "Store X in memory",
        ["STY"] = "Store Y in memory",

        // Register transfers
        ["TAX"] = "Copy A to X. Flags: N Z",
        ["TAY"] = "Copy A to Y. Flags: N Z",
        ["TXA"] = "Copy X to A. Flags: N Z",
        ["TYA"] = "Copy Y to A. Flags: N Z",
        ["TSX"] = "Copy the stack pointer to X. Flags: N Z",
        ["TXS"] = "Copy X to the stack pointer (page $01)",

        // Stack
        ["PHA"] = "Push A onto the stack",
        ["PHP"] = "Push the flags onto the stack (with B and bit 5 set)",
        ["PLA"] = "Pull a byte from the stack into A. Flags: N Z",
        ["PLP"] = "Pull a byte from the stack into the flags",

        // Arithmetic and logic
        ["ADC"] = "A = A + operand + C (in BCD when D = 1). Flags: N V Z C",
        ["SBC"] = "A = A - operand - (1 - C): use SEC before subtracting. Flags: N V Z C",
        ["AND"] = "A = A AND operand: clears the bits that are 0 in the operand. Flags: N Z",
        ["ORA"] = "A = A OR operand: sets the bits that are 1 in the operand. Flags: N Z",
        ["EOR"] = "A = A XOR operand: flips the bits that are 1 in the operand. Flags: N Z",
        ["BIT"] = "Test A AND memory without changing A: Z from the result, N and V from bits 7 and 6 of memory",
        ["CMP"] = "Compare A with the operand (A - operand, not stored). C = 1 if A >= operand. Flags: N Z C",
        ["CPX"] = "Compare X with the operand. C = 1 if X >= operand. Flags: N Z C",
        ["CPY"] = "Compare Y with the operand. C = 1 if Y >= operand. Flags: N Z C",

        // Increments and decrements
        ["INC"] = "Add 1 to a byte in memory. Flags: N Z",
        ["DEC"] = "Subtract 1 from a byte in memory. Flags: N Z",
        ["INX"] = "X = X + 1. Flags: N Z",
        ["INY"] = "Y = Y + 1. Flags: N Z",
        ["DEX"] = "X = X - 1. Flags: N Z",
        ["DEY"] = "Y = Y - 1. Flags: N Z",

        // Shifts
        ["ASL"] = "Shift left (x 2): bit 7 goes to C, 0 enters bit 0. Flags: N Z C",
        ["LSR"] = "Shift right (/ 2): bit 0 goes to C, 0 enters bit 7. Flags: N Z C",
        ["ROL"] = "Rotate left through C: bit 7 goes to C, C enters bit 0. Flags: N Z C",
        ["ROR"] = "Rotate right through C: bit 0 goes to C, C enters bit 7. Flags: N Z C",

        // Jumps
        ["JMP"] = "Jump to the address (or, with (address), to the address stored in memory)",
        ["JSR"] = "Call a subroutine: push the return address and jump",
        ["RTS"] = "Return from a subroutine called with JSR",
        ["RTI"] = "Return from an interrupt: pull flags and address from the stack",
        ["BRK"] = "Software interrupt: push address and flags (with B = 1) and jump through the IRQ vector ($FFFE)",
        ["NOP"] = "Do nothing (2 cycles)",

        // Conditional branches (relative: -128 to +127 bytes)
        ["BEQ"] = "Branch if Z = 1 (result zero, or compared values equal)",
        ["BNE"] = "Branch if Z = 0 (result not zero, or compared values different)",
        ["BCS"] = "Branch if C = 1 (after a compare: greater or equal)",
        ["BCC"] = "Branch if C = 0 (after a compare: less)",
        ["BMI"] = "Branch if N = 1 (bit 7 of the result set)",
        ["BPL"] = "Branch if N = 0 (bit 7 of the result clear)",
        ["BVS"] = "Branch if V = 1 (signed overflow)",
        ["BVC"] = "Branch if V = 0",

        // Flags
        ["CLC"] = "C = 0 (before ADC)",
        ["SEC"] = "C = 1 (before SBC)",
        ["CLD"] = "D = 0: binary arithmetic",
        ["SED"] = "D = 1: ADC and SBC work in decimal (BCD)",
        ["CLI"] = "I = 0: enable interrupts (IRQ)",
        ["SEI"] = "I = 1: disable interrupts (IRQ; NMI still gets through)",
        ["CLV"] = "V = 0",

        // Undocumented (stable on the 6510)
        ["LAX"] = "Undocumented: load the same value into A and X. Flags: N Z",
        ["SAX"] = "Undocumented: store A AND X in memory",
        ["SLO"] = "Undocumented: ASL in memory, then ORA with the result",
        ["RLA"] = "Undocumented: ROL in memory, then AND with the result",
        ["SRE"] = "Undocumented: LSR in memory, then EOR with the result",
        ["RRA"] = "Undocumented: ROR in memory, then ADC with the result",
        ["DCP"] = "Undocumented: DEC in memory, then CMP with the result",
        ["ISC"] = "Undocumented: INC in memory, then SBC with the result",
        ["ANC"] = "Undocumented: immediate AND, then C = bit 7 of the result",
        ["ALR"] = "Undocumented: immediate AND, then LSR A",
        ["ARR"] = "Undocumented: immediate AND, then ROR A with peculiar flags",
        ["AXS"] = "Undocumented: X = (A AND X) - operand, without borrow. Flags: N Z C",
        ["LAS"] = "Undocumented: A, X and stack pointer = memory AND stack pointer",
        ["JAM"] = "Undocumented: halts the CPU until reset",

        // Undocumented and unstable: the result depends on the individual chip
        ["XAA"] = "Undocumented, unstable: A = (A OR constant) AND X AND operand",
        ["LXA"] = "Undocumented, unstable: A = X = (A OR constant) AND operand",
        ["SHA"] = "Undocumented, unstable: store A AND X AND (high byte of the address + 1)",
        ["SHX"] = "Undocumented, unstable: store X AND (high byte of the address + 1)",
        ["SHY"] = "Undocumented, unstable: store Y AND (high byte of the address + 1)",
        ["TAS"] = "Undocumented, unstable: stack pointer = A AND X, then store like SHA",
    };
}
