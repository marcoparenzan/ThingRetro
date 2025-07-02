using System;
using System.Collections.Generic;

namespace Chips;

public partial class MOS6510
{
    private bool loggingEnabled = true; // Abilita/disabilita il logging

    public void EnableLogging(bool enable)
    {
        loggingEnabled = enable;
    }



    private static readonly string[] opcodeMnemonics = new string[]
    {
        "BRK", "ORA", "KIL", "SLO", "NOP", "ORA", "ASL", "SLO", // 0x00-0x07
        "PHP", "ORA", "ASL", "ANC", "NOP", "ORA", "ASL", "SLO", // 0x08-0x0F
        "BPL", "ORA", "KIL", "SLO", "NOP", "ORA", "ASL", "SLO", // 0x10-0x17
        "CLC", "ORA", "NOP", "SLO", "NOP", "ORA", "ASL", "SLO", // 0x18-0x1F
        "JSR", "AND", "KIL", "RLA", "BIT", "AND", "ROL", "RLA", // 0x20-0x27
        "PLP", "AND", "ROL", "ANC", "BIT", "AND", "ROL", "RLA", // 0x28-0x2F
        "BMI", "AND", "KIL", "RLA", "NOP", "AND", "ROL", "RLA", // 0x30-0x37
        "SEC", "AND", "NOP", "RLA", "NOP", "AND", "ROL", "RLA", // 0x38-0x3F
        "RTI", "EOR", "KIL", "SRE", "NOP", "EOR", "LSR", "SRE", // 0x40-0x47
        "PHA", "EOR", "LSR", "ALR", "JMP", "EOR", "LSR", "SRE", // 0x48-0x4F
        "BVC", "EOR", "KIL", "SRE", "NOP", "EOR", "LSR", "SRE", // 0x50-0x57
        "CLI", "EOR", "NOP", "SRE", "NOP", "EOR", "LSR", "SRE", // 0x58-0x5F
        "RTS", "ADC", "KIL", "RRA", "NOP", "ADC", "ROR", "RRA", // 0x60-0x67
        "PLA", "ADC", "ROR", "ARR", "JMP", "ADC", "ROR", "RRA", // 0x68-0x6F
        "BVS", "ADC", "KIL", "RRA", "NOP", "ADC", "ROR", "RRA", // 0x70-0x77
        "SEI", "ADC", "NOP", "RRA", "NOP", "ADC", "ROR", "RRA", // 0x78-0x7F
        "NOP", "STA", "NOP", "SAX", "STY", "STA", "STX", "SAX", // 0x80-0x87
        "DEY", "NOP", "TXA", "XAA", "STY", "STA", "STX", "SAX", // 0x88-0x8F
        "BCC", "STA", "KIL", "AHX", "STY", "STA", "STX", "SAX", // 0x90-0x97
        "TYA", "STA", "TXS", "TAS", "SHY", "STA", "SHX", "AHX", // 0x98-0x9F
        "LDY", "LDA", "LDX", "LAX", "LDY", "LDA", "LDX", "LAX", // 0xA0-0xA7
        "TAY", "LDA", "TAX", "LAX", "LDY", "LDA", "LDX", "LAX", // 0xA8-0xAF
        "BCS", "LDA", "KIL", "LAX", "LDY", "LDA", "LDX", "LAX", // 0xB0-0xB7
        "CLV", "LDA", "TSX", "LAS", "LDY", "LDA", "LDX", "LAX", // 0xB8-0xBF
        "CPY", "CMP", "NOP", "DCP", "CPY", "CMP", "DEC", "DCP", // 0xC0-0xC7
        "INY", "CMP", "DEX", "AXS", "CPY", "CMP", "DEC", "DCP", // 0xC8-0xCF
        "BNE", "CMP", "KIL", "DCP", "NOP", "CMP", "DEC", "DCP", // 0xD0-0xD7
        "CLD", "CMP", "NOP", "DCP", "NOP", "CMP", "DEC", "DCP", // 0xD8-0xDF
        "CPX", "SBC", "NOP", "ISC", "CPX", "SBC", "INC", "ISC", // 0xE0-0xE7
        "INX", "SBC", "NOP", "SBC", "CPX", "SBC", "INC", "ISC", // 0xE8-0xEF
        "BEQ", "SBC", "KIL", "ISC", "NOP", "SBC", "INC", "ISC", // 0xF0-0xF7
        "SED", "SBC", "NOP", "ISC", "NOP", "SBC", "INC", "ISC"  // 0xF8-0xFF
    };

    private static readonly string[] addressingModes = new string[]
    {
        "impl", "X,ind", "impl", "X,ind", "zp", "zp", "zp", "zp", // 0x00-0x07
        "impl", "#", "A", "#", "abs", "abs", "abs", "abs", // 0x08-0x0F
        "rel", "ind,Y", "impl", "ind,Y", "zp,X", "zp,X", "zp,X", "zp,X", // 0x10-0x17
        "impl", "abs,Y", "impl", "abs,Y", "abs,X", "abs,X", "abs,X", "abs,X", // 0x18-0x1F
        "abs", "X,ind", "impl", "X,ind", "zp", "zp", "zp", "zp", // 0x20-0x27
        "impl", "#", "A", "#", "abs", "abs", "abs", "abs", // 0x28-0x2F
        "rel", "ind,Y", "impl", "ind,Y", "zp,X", "zp,X", "zp,X", "zp,X", // 0x30-0x37
        "impl", "abs,Y", "impl", "abs,Y", "abs,X", "abs,X", "abs,X", "abs,X", // 0x38-0x3F
        "impl", "X,ind", "impl", "X,ind", "zp", "zp", "zp", "zp", // 0x40-0x47
        "impl", "#", "A", "#", "abs", "abs", "abs", "abs", // 0x48-0x4F
        "rel", "ind,Y", "impl", "ind,Y", "zp,X", "zp,X", "zp,X", "zp,X", // 0x50-0x57
        "impl", "abs,Y", "impl", "abs,Y", "abs,X", "abs,X", "abs,X", "abs,X", // 0x58-0x5F
        "impl", "X,ind", "impl", "X,ind", "zp", "zp", "zp", "zp", // 0x60-0x67
        "impl", "#", "A", "#", "ind", "abs", "abs", "abs", // 0x68-0x6F
        "rel", "ind,Y", "impl", "ind,Y", "zp,X", "zp,X", "zp,X", "zp,X", // 0x70-0x77
        "impl", "abs,Y", "impl", "abs,Y", "abs,X", "abs,X", "abs,X", "abs,X", // 0x78-0x7F
        "#", "X,ind", "#", "X,ind", "zp", "zp", "zp", "zp", // 0x80-0x87
        "impl", "#", "impl", "#", "abs", "abs", "abs", "abs", // 0x88-0x8F
        "rel", "ind,Y", "impl", "ind,Y", "zp,X", "zp,X", "zp,Y", "zp,Y", // 0x90-0x97
        "impl", "abs,Y", "impl", "abs,Y", "abs,X", "abs,X", "abs,Y", "abs,Y", // 0x98-0x9F
        "#", "X,ind", "#", "X,ind", "zp", "zp", "zp", "zp", // 0xA0-0xA7
        "impl", "#", "impl", "#", "abs", "abs", "abs", "abs", // 0xA8-0xAF
        "rel", "ind,Y", "impl", "ind,Y", "zp,X", "zp,X", "zp,Y", "zp,Y", // 0xB0-0xB7
        "impl", "abs,Y", "impl", "abs,Y", "abs,X", "abs,X", "abs,Y", "abs,Y", // 0xB8-0xBF
        "#", "X,ind", "#", "X,ind", "zp", "zp", "zp", "zp", // 0xC0-0xC7
        "impl", "#", "impl", "#", "abs", "abs", "abs", "abs", // 0xC8-0xCF
        "rel", "ind,Y", "impl", "ind,Y", "zp,X", "zp,X", "zp,X", "zp,X", // 0xD0-0xD7
        "impl", "abs,Y", "impl", "abs,Y", "abs,X", "abs,X", "abs,X", "abs,X", // 0xD8-0xDF
        "#", "X,ind", "#", "X,ind", "zp", "zp", "zp", "zp", // 0xE0-0xE7
        "impl", "#", "impl", "#", "abs", "abs", "abs", "abs", // 0xE8-0xEF
        "rel", "ind,Y", "impl", "ind,Y", "zp,X", "zp,X", "zp,X", "zp,X", // 0xF0-0xF7
        "impl", "abs,Y", "impl", "abs,Y", "abs,X", "abs,X", "abs,X", "abs,X"  // 0xF8-0xFF
    };

    private void LogState(byte opcode)
    {
        string mnemonic = opcodeMnemonics[opcode];
        string addressingMode = addressingModes[opcode];

        string instruction = $"{mnemonic} {addressingMode}";

        // Ottieni i dati dell'operando in base alla modalità di indirizzamento
        string operandData = GetOperandData(opcode);

        Console.WriteLine($"PC={PC - 1:X4} OP={opcode:X2} {instruction} {operandData} A={A:X2} X={X:X2} Y={Y:X2} SP={SP:X2} P={Status:X2}");
    }

    private string GetOperandData(byte opcode)
    {
        ushort pc = (ushort)(PC - 1); // PC è già stato incrementato
        string mode = addressingModes[opcode];

        switch (mode)
        {
            case "#":
            case "zp":
            case "zp,X":
            case "zp,Y":
            case "X,ind":
            case "ind,Y":
                return $"${ReadByte((ushort)(pc + 1)):X2}";

            case "abs":
            case "abs,X":
            case "abs,Y":
            case "ind":
                return $"${ReadByte((ushort)(pc + 2)):X2}{ReadByte((ushort)(pc + 1)):X2}";

            case "rel":
                sbyte offset = (sbyte)ReadByte((ushort)(pc + 1));
                ushort target = (ushort)(pc + 2 + offset);
                return $"${target:X4}";

            default:
                return "";
        }
    }
}