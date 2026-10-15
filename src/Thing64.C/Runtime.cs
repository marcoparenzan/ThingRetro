namespace Thing64.C;

// The library routines a module uses, written into it once: 16-bit multiplication and division. They take their
// operands in module__arg1 and module__arg2 and return the result in A (low byte) and X (high byte)
static class Runtime
{
    public static string Routine(string name, string module)
    {
        string text = name switch
        {
            "mul16" => Multiply,
            "divu16" => DivideUnsigned,
            "divs16" => DivideSigned,
            _ => throw new ArgumentException(name),
        };
        return text.Replace("MODULE", module);
    }

    // The storage of the routines, after the code
    public static string Data(string module) => """

        MODULE__arg1:               !word 0
        MODULE__arg2:               !word 0
        MODULE__product:            !word 0
        MODULE__remainder:          !word 0
        MODULE__sign:               !byte 0

        """.Replace("MODULE", module);

    // Shift and add: for each bit of arg2, from the lowest, arg1 (doubled each time) is added to the product
    const string Multiply = """

        ; arg1 * arg2 (the low 16 bits of the product)
        MODULE__mul16:
                    LDA #$00
                    STA MODULE__product
                    STA MODULE__product+1
                    LDY #16
        @bit:       LSR MODULE__arg2+1
                    ROR MODULE__arg2
                    BCC @next
                    CLC
                    LDA MODULE__product
                    ADC MODULE__arg1
                    STA MODULE__product
                    LDA MODULE__product+1
                    ADC MODULE__arg1+1
                    STA MODULE__product+1
        @next:      ASL MODULE__arg1
                    ROL MODULE__arg1+1
                    DEY
                    BNE @bit
                    LDA MODULE__product
                    LDX MODULE__product+1
                    RTS

        """;

    // Long division, one bit at a time: the dividend's bits enter the remainder from the left; when the divisor
    // fits, it is subtracted and the quotient's bit (in arg1, shifted in) is 1
    const string DivideUnsigned = """

        ; arg1 / arg2 unsigned: the quotient in A/X, the remainder in remainder
        MODULE__divu16:
                    LDA #$00
                    STA MODULE__remainder
                    STA MODULE__remainder+1
                    LDY #16
        @bit:       ASL MODULE__arg1
                    ROL MODULE__arg1+1
                    ROL MODULE__remainder
                    ROL MODULE__remainder+1
                    LDA MODULE__remainder
                    SEC
                    SBC MODULE__arg2
                    TAX
                    LDA MODULE__remainder+1
                    SBC MODULE__arg2+1
                    BCC @next
                    STA MODULE__remainder+1
                    STX MODULE__remainder
                    INC MODULE__arg1
        @next:      DEY
                    BNE @bit
                    LDA MODULE__arg1
                    LDX MODULE__arg1+1
                    RTS

        """;

    // Signed: the sizes are divided, then the quotient is negative when the signs differ, the remainder has the
    // dividend's sign (as in C: -7 / 2 is -3, -7 % 2 is -1)
    const string DivideSigned = """

        ; arg1 / arg2 signed: the quotient in A/X, the remainder in remainder
        MODULE__divs16:
                    LDA MODULE__arg1+1                  ; sign: bit 7 the dividend's, bit 6 the divisor's
                    AND #$80
                    STA MODULE__sign
                    BPL @dividend
                    LDA #$00
                    SEC
                    SBC MODULE__arg1
                    STA MODULE__arg1
                    LDA #$00
                    SBC MODULE__arg1+1
                    STA MODULE__arg1+1
        @dividend:  LDA MODULE__arg2+1
                    BPL @divisor
                    LDA #$00
                    SEC
                    SBC MODULE__arg2
                    STA MODULE__arg2
                    LDA #$00
                    SBC MODULE__arg2+1
                    STA MODULE__arg2+1
                    LDA MODULE__sign
                    EOR #$40
                    STA MODULE__sign
        @divisor:   JSR MODULE__divu16
                    BIT MODULE__sign
                    BPL @remainder
                    LDA #$00
                    SEC
                    SBC MODULE__remainder
                    STA MODULE__remainder
                    LDA #$00
                    SBC MODULE__remainder+1
                    STA MODULE__remainder+1
        @remainder: LDA MODULE__sign
                    ASL
                    EOR MODULE__sign
                    BPL @positive
                    LDA #$00
                    SEC
                    SBC MODULE__arg1
                    STA MODULE__arg1
                    LDA #$00
                    SBC MODULE__arg1+1
                    STA MODULE__arg1+1
        @positive:  LDA MODULE__arg1
                    LDX MODULE__arg1+1
                    RTS

        """;
}
