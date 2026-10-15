namespace Thing64.WebStudio;

// The files of a new project
static class Templates
{
    public static string Manifest(string name) => $$"""
        // Build manifest of {{name}}: what F7 builds (paths relative to this folder); F5 builds and runs the first output.
        // The same file builds from the command line: thing64 build thing64.json
        {
          "outputs": [
            { "file": "{{FileName(name)}}.prg", "source": "main.asm", "description": "{{name}}" }
          ]
        }

        """;

    public static string MainAsm(string name) => $$"""
        ; ----------------------------------------------------------------------------------------------------------
        ; {{name}}
        ;
        ; A BASIC line, 10 SYS 2061, starts the machine language that follows it: the program is loaded and
        ; started with RUN like a BASIC program.
        ; ----------------------------------------------------------------------------------------------------------

        BORDER      = $D020
        BACKGROUND  = $D021
        CHROUT      = $FFD2
        RASTER      = $D012

                    * = $0801

                    .word basicEnd, 10          ; next line, line number 10
                    .byte $9E                   ; SYS
                    .text "2061"
                    .byte 0
        basicEnd:   .word 0                     ; end of the BASIC program

        start:      LDA #6
                    STA BACKGROUND              ; blue
                    LDX #0
        print:      LDA message,X
                    BEQ colors
                    JSR CHROUT
                    INX
                    BNE print

        ; The border changes color once per frame, when the raster reaches line 0
        colors:     LDA RASTER
                    BNE colors
        wait:       LDA RASTER
                    BEQ wait
                    INC BORDER
                    JMP colors

        message:    .byte 147                   ; clear the screen
                    .text "HELLO FROM THING64!"
                    .byte 13, 0

        """;

    // The map of the project: what it is, its files, its memory, its conventions and its editor regions. RalfAI
    // reads it before working and keeps it up to date
    public static string Structure(string name) => $$"""
        # {{name}} — structure

        What the program does, in two or three lines.

        ## Files

        | File | Role |
        | --- | --- |
        | `main.asm` | Entry point (BASIC line `10 SYS 2061`), setup, main loop |
        | `thing64.json` | Build manifest: what F7 builds, the first output is what F5 runs |

        The main file is `main.asm`.

        ## Memory map

        | Address | Content | Where |
        | --- | --- | --- |
        | `$0400-$07E7` | Screen RAM | |
        | `$0801-` | BASIC line and program | `main.asm` |

        ## Data and conventions

        - Comments are in English and explain the C64 side of what the code does.
        - Data tables are explicit `.byte` rows.

        ## Regions with an editor

        | File | Region | Editor | Edits |
        | --- | --- | --- | --- |

        A region is marked by `;@ <editor> parameters` lines and closed by `;@ end`; the studio shows "✎ Edit" above it.
        `;@ screen width= height= screen= colors= charset= first= background= multicolor= multicolor1= multicolor2=`
        edits a screen (screen codes and colors); `;@ charset first=` edits the characters of a charset;
        `;@ sprites` edits sprites and their animations (parameters are numbers or symbols).

        """;

    public static string EmptyAsm(string fileName) => $"""
        ; {fileName}

        """;

    // The project's name as a file name: letters, digits and dashes
    static string FileName(string name)
    {
        var chars = name.ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-').ToArray();
        string file = new string(chars).Trim('-');
        return file.Length > 0 ? file : "main";
    }
}
