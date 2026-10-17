using System.Text;

namespace Thing65.Asm;

public static partial class Assembler
{
    // A statement of the source, with where it comes from. Block holds the statements between { and } that follow
    // it (for !for, !if, else, !macro, !zone...)
    sealed class Node(string text, string file, string? path, int line, string lineText)
    {
        public string Text { get; } = text;
        public string File { get; } = file;
        public string? Path { get; } = path;
        public int Line { get; } = line;
        public string LineText { get; } = lineText;
        public List<Node>? Block { get; set; }
    }

    // Source files read by !source and !binary, relative to the including file's folder (or to the base folder),
    // each parsed once
    sealed class SourceFiles(string? directory)
    {
        readonly Dictionary<string, List<Node>> parsed = new(StringComparer.OrdinalIgnoreCase);

        public string Resolve(string fileName, Node from)
        {
            if (Path.IsPathRooted(fileName)) return fileName;
            string? folder = from.Path is not null ? Path.GetDirectoryName(from.Path) : directory;
            if (folder is null) throw new FileNotFoundException($"no folder to look for \"{fileName}\" in");
            return Path.GetFullPath(Path.Combine(folder, fileName));
        }

        public List<Node> Include(string path)
        {
            if (!parsed.TryGetValue(path, out var nodes))
                parsed[path] = nodes = Parse(File.ReadAllText(path), System.IO.Path.GetFileName(path), path);
            return nodes;
        }

        public byte[] ReadBinary(string path) => File.ReadAllBytes(path);

        // Splits the text into statements: a new line, ':' (outside strings), '{' and '}' end a statement;
        // comments go from ';' to the end of the line. Braces build the blocks
        public List<Node> Parse(string text, string file, string? path)
        {
            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var root = new List<Node>();
            var stack = new Stack<List<Node>>();
            var current = root;
            var statement = new StringBuilder();
            int statementLine = 1;

            void Flush(int lineNumber)
            {
                string content = statement.ToString().Trim();
                statement.Clear();
                if (content.Length > 0) current.Add(new Node(content, file, path, statementLine, lines[statementLine - 1]));
                statementLine = lineNumber;
            }

            for (int l = 0; l < lines.Length; l++)
            {
                string line = lines[l];
                int lineNumber = l + 1;
                statementLine = lineNumber;
                bool inString = false;
                for (int i = 0; i < line.Length; i++)
                {
                    char c = line[i];
                    if (inString)
                    {
                        statement.Append(c);
                        if (c == '"') inString = false;
                        continue;
                    }
                    if (c == '"') { inString = true; statement.Append(c); continue; }
                    if (c == '\'' && i + 2 < line.Length && line[i + 2] == '\'')
                    {
                        statement.Append(line, i, 3);
                        i += 2;
                        continue;
                    }
                    if (c == ';') break;
                    if (c == ':') { Flush(lineNumber); continue; }
                    if (c == '{')
                    {
                        Flush(lineNumber);
                        if (current.Count == 0)
                            throw new AssemblerException(file, lineNumber, line, "'{' without a statement before it", path);
                        var block = new List<Node>();
                        current[^1].Block = block;
                        stack.Push(current);
                        current = block;
                        continue;
                    }
                    if (c == '}')
                    {
                        Flush(lineNumber);
                        if (stack.Count == 0) throw new AssemblerException(file, lineNumber, line, "'}' without '{'", path);
                        current = stack.Pop();
                        continue;
                    }
                    statement.Append(c);
                }
                Flush(lineNumber + 1);
            }
            if (stack.Count > 0) throw new AssemblerException(file, lines.Length, "", "missing '}' at the end of the file", path);
            return root;
        }
    }
}
