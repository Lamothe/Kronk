using System.Text;
using System.Text.Json.Serialization;

namespace Kronk.Models.Tools;

public class ListDirectoryProperties
{
    [JsonPropertyName("path")]
    public StringToolProperty Path { get; set; } = new("The directory path to list");

    [JsonPropertyName("recursive")]
    public BooleanToolProperty Recursive { get; set; } = new("Whether to list files recursively");

    [JsonPropertyName("depth")]
    public IntegerToolProperty Depth { get; set; } = new("Maximum recursion depth (only when recursive is true)");
}

public class ListDirectoryToolInputSchema : ToolParameters<ListDirectoryProperties>
{
    public ListDirectoryToolInputSchema() : base([nameof(ListDirectoryProperties.Path)])
    {
    }
}

public class ListDirectoryToolArguments
{
    public required string Path { get; set; }

    public bool Recursive { get; set; }

    public int Depth { get; set; } = 3;
}

public class ListDirectoryTool : Tool<ListDirectoryToolArguments, ListDirectoryToolInputSchema>
{
    public ListDirectoryTool() : base(
        "list_directory",
        "Lists the contents of a directory. Optionally recurses into subdirectories up to a given depth."
    )
    {
    }

    public override async Task<string> Run(ListDirectoryToolArguments arguments, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        if (!Directory.Exists(arguments.Path))
        {
            return $"SYSTEM ERROR: Directory not found at {arguments.Path}.";
        }

        var resultBuilder = new StringBuilder();
        resultBuilder.AppendLine($"--- DIRECTORY: {arguments.Path} ---");

        try
        {
            var normalizedBasePath = Path.GetFullPath(arguments.Path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var maxDepth = arguments.Depth;

            var allDirectories = Directory.GetDirectories(arguments.Path, "*", new EnumerationOptions
            {
                RecurseSubdirectories = arguments.Recursive,
                IgnoreInaccessible = true
            })
            .Where(d =>
            {
                if (!arguments.Recursive || maxDepth <= 0)
                    return true;

                var relPath = Path.GetFullPath(d)[normalizedBasePath.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return relPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length <= maxDepth;
            })
            .OrderBy(x => x);

            var allFiles = Directory.GetFiles(arguments.Path, "*", new EnumerationOptions
            {
                RecurseSubdirectories = arguments.Recursive,
                IgnoreInaccessible = true
            })
            .Where(f =>
            {
                if (!arguments.Recursive || maxDepth <= 0)
                    return true;

                var relPath = Path.GetFullPath(f)[normalizedBasePath.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return relPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length - 1 <= maxDepth;
            })
            .OrderBy(x => x);

            foreach (var dir in allDirectories)
            {
                resultBuilder.AppendLine($"DIR  {dir}/");
            }

            resultBuilder.AppendLine();

            foreach (var file in allFiles)
            {
                resultBuilder.AppendLine($"FILE {file}");
            }

            if (!allFiles.Any() && !allDirectories.Any())
            {
                resultBuilder.AppendLine("(empty directory)");
            }
        }
        catch (Exception ex)
        {
            return $"SYSTEM ERROR: Failed to list directory: {ex.Message}";
        }

        return resultBuilder.ToString();
    }
}
