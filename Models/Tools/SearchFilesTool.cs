using System.Text;
using System.Text.Json.Serialization;

namespace Kronk.Models.Tools;

public class SearchFilesProperties
{
    [JsonPropertyName("pattern")]
    public StringToolProperty Pattern { get; set; } = new("The glob pattern to search for (e.g. '*.cs', '**/test/**')");

    [JsonPropertyName("path")]
    public StringToolProperty Path { get; set; } = new("The root directory to search within (defaults to current working directory)");
}

public class SearchFilesToolInputSchema : ToolParameters<SearchFilesProperties>
{
    public SearchFilesToolInputSchema() : base([nameof(SearchFilesProperties.Pattern)])
    {
    }
}

public class SearchFilesToolArguments
{
    public required string Pattern { get; set; }

    public string? Path { get; set; }
}

public class SearchFilesTool : Tool<SearchFilesToolArguments, SearchFilesToolInputSchema>
{
    public SearchFilesTool() : base(
        "search_files",
        "Finds files matching a glob pattern. Supports * (any chars except /), ** (any path), and ? (single char). Example patterns: '*.cs', '**/*.ts', 'src/**/*.cs'"
    )
    {
    }

    public override async Task<string> Run(SearchFilesToolArguments arguments, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        var searchPath = string.IsNullOrWhiteSpace(arguments.Path) ? Directory.GetCurrentDirectory() : arguments.Path;

        if (!Directory.Exists(searchPath))
        {
            return $"SYSTEM ERROR: Directory not found at {searchPath}.";
        }

        try
        {
            var patternSegments = arguments.Pattern.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            var results = GlobMatch(searchPath, patternSegments, 0).OrderBy(x => x).ToList();

            var resultBuilder = new StringBuilder();
            resultBuilder.AppendLine($"--- GLOB: {arguments.Pattern} in {searchPath} ---");

            if (results.Count == 0)
            {
                resultBuilder.AppendLine("(no matches found)");
            }
            else
            {
                foreach (var filePath in results)
                {
                    resultBuilder.AppendLine(filePath);
                }
            }

            resultBuilder.AppendLine();
            resultBuilder.AppendLine($"({results.Count} match{(results.Count == 1 ? "" : "es")})");

            return resultBuilder.ToString();
        }
        catch (Exception ex)
        {
            return $"SYSTEM ERROR: Failed to search files: {ex.Message}";
        }
    }

    private static List<string> GlobMatch(string currentDir, string[] segments, int segmentIndex)
    {
        var results = new List<string>();

        if (segmentIndex >= segments.Length)
        {
            if (Directory.Exists(currentDir) || File.Exists(currentDir))
            {
                results.Add(currentDir);
            }

            return results;
        }

        var segment = segments[segmentIndex];

        if (segment == "**")
        {
            // ** matches zero or more directories
            // Try matching at current level (skip **)
            results.AddRange(GlobMatch(currentDir, segments, segmentIndex + 1));

            // Try descending into each subdirectory
            try
            {
                foreach (var subDir in Directory.EnumerateDirectories(currentDir, "*", new EnumerationOptions { IgnoreInaccessible = true }))
                {
                    results.AddRange(GlobMatch(subDir, segments, segmentIndex));
                }
            }
            catch
            {
                // Ignore inaccessible directories
            }
        }
        else
        {
            var searchPattern = segment.Replace("**", "*");

            try
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(currentDir, searchPattern, new EnumerationOptions { IgnoreInaccessible = true }))
                {
                    if (segmentIndex == segments.Length - 1)
                    {
                        if (File.Exists(entry) || Directory.Exists(entry))
                        {
                            results.Add(entry);
                        }
                    }
                    else if (Directory.Exists(entry))
                    {
                        results.AddRange(GlobMatch(entry, segments, segmentIndex + 1));
                    }
                }
            }
            catch
            {
                // Ignore inaccessible directories
            }
        }

        return results;
    }
}
