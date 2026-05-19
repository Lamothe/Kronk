using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Kronk.Models.Tools;

public class SearchInFilesProperties
{
    [JsonPropertyName("pattern")]
    public StringToolProperty Pattern { get; set; } = new("The regex pattern to search for");

    [JsonPropertyName("paths")]
    public StringArrayToolProperty Paths { get; set; } = new("An array of file paths or glob patterns to search within");

    [JsonPropertyName("case_sensitive")]
    public BooleanToolProperty CaseSensitive { get; set; } = new("Whether the search is case sensitive (default: false)");

    [JsonPropertyName("include_line_numbers")]
    public BooleanToolProperty IncludeLineNumbers { get; set; } = new("Whether to include line numbers in the output (default: true)");

    [JsonPropertyName("context_lines")]
    public IntegerToolProperty ContextLines { get; set; } = new("Number of context lines to include before and after each match (default: 0)");

    [JsonPropertyName("file_pattern")]
    public StringToolProperty FilePattern { get; set; } = new("A glob pattern to filter files (e.g. '*.cs', '*.ts'). Alternative to specifying individual paths.");
}

public class SearchInFilesToolInputSchema : ToolParameters<SearchInFilesProperties>
{
    public SearchInFilesToolInputSchema() : base([nameof(SearchInFilesProperties.Pattern)])
    {
    }
}

public class SearchInFilesToolArguments
{
    public required string Pattern { get; set; }

    public string[]? Paths { get; set; }

    public bool CaseSensitive { get; set; } = false;

    public bool IncludeLineNumbers { get; set; } = true;

    public int ContextLines { get; set; } = 0;

    public string? FilePattern { get; set; }
}

public class SearchInFilesTool : Tool<SearchInFilesToolArguments, SearchInFilesToolInputSchema>
{
    private const int MaxResults = 500;
    private const int MaxLineLength = 500;

    public SearchInFilesTool() : base(
        "search_in_files",
        "Searches for a regex pattern across files. Use 'paths' for specific files or 'file_pattern' for glob-based file selection. Returns matching lines with optional context."
    )
    {
    }

    private static readonly int BinaryCheckSize = 8192;

    public override async Task<string> Run(SearchInFilesToolArguments arguments, CancellationToken cancellationToken)
    {
        var resultBuilder = new StringBuilder();
        var regexOptions = arguments.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
        regexOptions |= RegexOptions.Compiled;

        Regex regex;
        try
        {
            regex = new Regex(arguments.Pattern, regexOptions);
        }
        catch (Exception ex)
        {
            return $"SYSTEM ERROR: Invalid regex pattern '{arguments.Pattern}': {ex.Message}";
        }

        var targetFiles = new List<string>();

        if (!string.IsNullOrWhiteSpace(arguments.FilePattern))
        {
            var searchPath = Directory.GetCurrentDirectory();
            try
            {
                var options = new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true
                };

                var pattern = arguments.FilePattern;

                targetFiles.AddRange(Directory.GetFiles(searchPath, pattern, options).OrderBy(x => x));

                if (!targetFiles.Any() && pattern.Contains("*"))
                {
                    targetFiles.AddRange(Directory.GetFiles(searchPath, "*", options).Where(f => MatchGlob(Path.GetFileName(f), pattern)).OrderBy(x => x));
                }
            }
            catch (Exception ex)
            {
                return $"SYSTEM ERROR: Failed to resolve file pattern: {ex.Message}";
            }
        }
        else if (arguments.Paths != null && arguments.Paths.Length > 0)
        {
            foreach (var path in arguments.Paths)
            {
                if (File.Exists(path))
                {
                    targetFiles.Add(path);
                }
                else if (Directory.Exists(path))
                {
                    try
                    {
                        targetFiles.AddRange(Directory.GetFiles(path, "*", new EnumerationOptions
                        {
                            RecurseSubdirectories = true,
                            IgnoreInaccessible = true
                        }).OrderBy(x => x));
                    }
                    catch
                    {
                        // skip
                    }
                }
                else if (path.Contains("*") || path.Contains("?"))
                {
                    var searchDir = Path.GetDirectoryName(path)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                   ?? Directory.GetCurrentDirectory();
                    var searchPattern = Path.GetFileName(path);

                    try
                    {
                        targetFiles.AddRange(Directory.GetFiles(searchDir, searchPattern, new EnumerationOptions
                        {
                            RecurseSubdirectories = true,
                            IgnoreInaccessible = true
                        }).OrderBy(x => x));
                    }
                    catch
                    {
                        // skip
                    }
                }
            }
        }
        else
        {
            return "SYSTEM ERROR: Provide either 'paths' or 'file_pattern'.";
        }

        if (!targetFiles.Any())
        {
            return "No files found matching the given criteria.";
        }

        var matchCount = 0;
        var filesWithMatches = 0;

        foreach (var filePath in targetFiles)
        {
            if (cancellationToken.IsCancellationRequested)
                return "Search cancelled.";

            if (matchCount >= MaxResults)
            {
                resultBuilder.AppendLine($"\n[Results truncated at {MaxResults} matches]");
                break;
            }

            try
            {
                if (await IsBinaryFileAsync(filePath, cancellationToken))
                {
                    continue;
                }

                var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
                var fileMatches = new List<(int lineNumber, string line)>();

                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (line.Length > MaxLineLength)
                        line = line[..MaxLineLength] + "...";

                    if (regex.IsMatch(line))
                    {
                        fileMatches.Add((i + 1, lines[i]));
                        matchCount++;
                    }
                }

                if (fileMatches.Any())
                {
                    if (filesWithMatches > 0)
                        resultBuilder.AppendLine();

                    resultBuilder.AppendLine($"--- {filePath} ({fileMatches.Count} match{(fileMatches.Count == 1 ? "" : "es")}) ---");

                    var printed = new HashSet<int>();
                    foreach (var (lineNum, line) in fileMatches)
                    {
                        var startLine = Math.Max(0, lineNum - arguments.ContextLines - 1);
                        var endLine = Math.Min(lines.Length - 1, lineNum + arguments.ContextLines - 1);

                        if (printed.Contains(lineNum) && arguments.ContextLines == 0)
                        {
                            PrintLine(resultBuilder, lineNum, line, arguments.IncludeLineNumbers);
                        }
                        else
                        {
                            for (var i = startLine; i <= endLine; i++)
                            {
                                if (!printed.Contains(i + 1))
                                {
                                    var displayLine = lines[i];
                                    if (displayLine.Length > MaxLineLength)
                                        displayLine = displayLine[..MaxLineLength] + "...";

                                    if (i == startLine && i > 0 && !fileMatches.Exists(m => m.lineNumber == i))
                                    {
                                        resultBuilder.AppendLine("...");
                                    }

                                    PrintLine(resultBuilder, i + 1, displayLine, arguments.IncludeLineNumbers);
                                    printed.Add(i + 1);
                                }
                            }
                        }
                    }

                    filesWithMatches++;
                }
            }
            catch (Exception ex)
            {
                resultBuilder.AppendLine($"[Error reading {filePath}: {ex.Message}]");
            }
        }

        resultBuilder.AppendLine();
        resultBuilder.AppendLine($"({matchCount} total match{(matchCount == 1 ? "" : "es")} across {filesWithMatches} file{(filesWithMatches == 1 ? "" : "s")})");

        return resultBuilder.ToString();
    }

    private static void PrintLine(StringBuilder sb, int lineNum, string line, bool includeLineNumbers)
    {
        if (includeLineNumbers)
        {
            sb.Append($"  {lineNum,5}: ");
        }

        sb.AppendLine(line);
    }

    private static async Task<bool> IsBinaryFileAsync(string path, CancellationToken cancellationToken)
    {
        var fileInfo = new FileInfo(path);

        if (fileInfo.Length > 10 * 1024 * 1024)
            return true;

        var checkSize = Math.Min(BinaryCheckSize, (int)fileInfo.Length);

        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        var buffer = new byte[checkSize];
        var bytesRead = await fs.ReadAsync(buffer, cancellationToken);

        for (var i = 0; i < bytesRead; i++)
        {
            if (buffer[i] == 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchGlob(string fileName, string pattern)
    {
        try
        {
            var regexPattern = "^" + Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";
            return Regex.IsMatch(fileName, regexPattern, RegexOptions.IgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
