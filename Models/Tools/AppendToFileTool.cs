using System.Text;
using System.Text.Json.Serialization;

namespace Kronk.Models.Tools;

public class AppendToFileProperties
{
    [JsonPropertyName("path")]
    public StringToolProperty Path { get; set; } = new("The file path to append to");

    [JsonPropertyName("content")]
    public StringToolProperty Content { get; set; } = new("The content to append");

    [JsonPropertyName("prepend")]
    public BooleanToolProperty Prepend { get; set; } = new("If true, prepend the content instead of appending (default: false)");

    [JsonPropertyName("insert_before_line")]
    public IntegerToolProperty InsertBeforeLine { get; set; } = new("Insert the content before this line number (1-based). Takes priority over prepend if specified.");

    [JsonPropertyName("insert_after_line")]
    public IntegerToolProperty InsertAfterLine { get; set; } = new("Insert the content after this line number (1-based).");
}

public class AppendToFileToolInputSchema : ToolParameters<AppendToFileProperties>
{
    public AppendToFileToolInputSchema() : base([
        nameof(AppendToFileProperties.Path),
        nameof(AppendToFileProperties.Content)])
    {
    }
}

public class AppendToFileToolArguments
{
    public required string Path { get; set; }

    public required string Content { get; set; }

    public bool Prepend { get; set; } = false;

    public int InsertBeforeLine { get; set; } = 0;

    public int InsertAfterLine { get; set; } = 0;
}

public class AppendToFileTool : Tool<AppendToFileToolArguments, AppendToFileToolInputSchema>
{
    public AppendToFileTool() : base(
        "append_to_file",
        "Appends, prepends, or inserts content into a file. Use 'prepend' to add at the start, or 'insert_before_line' / 'insert_after_line' for precise placement. Creates the file if it doesn't exist."
    )
    {
    }

    public override async Task<string> Run(AppendToFileToolArguments arguments, CancellationToken cancellationToken)
    {
        try
        {
            var directory = Path.GetDirectoryName(arguments.Path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string resultContent;

            if (!File.Exists(arguments.Path))
            {
                resultContent = arguments.Content;
            }
            else if (arguments.InsertBeforeLine > 0)
            {
                var lines = await File.ReadAllLinesAsync(arguments.Path, cancellationToken);

                if (arguments.InsertBeforeLine < 1 || arguments.InsertBeforeLine > lines.Length + 1)
                {
                    return $"SYSTEM ERROR: insert_before_line must be between 1 and {lines.Length + 1}.";
                }

                var sb = new StringBuilder();
                for (var i = 0; i < lines.Length; i++)
                {
                    if (i == arguments.InsertBeforeLine - 1)
                    {
                        sb.AppendLine(arguments.Content);
                    }

                    sb.AppendLine(lines[i]);
                }

                resultContent = sb.ToString();
            }
            else if (arguments.InsertAfterLine > 0)
            {
                var lines = await File.ReadAllLinesAsync(arguments.Path, cancellationToken);

                if (arguments.InsertAfterLine < 1 || arguments.InsertAfterLine > lines.Length)
                {
                    return $"SYSTEM ERROR: insert_after_line must be between 1 and {lines.Length}.";
                }

                var sb = new StringBuilder();
                for (var i = 0; i < lines.Length; i++)
                {
                    sb.AppendLine(lines[i]);

                    if (i == arguments.InsertAfterLine - 1)
                    {
                        sb.AppendLine(arguments.Content);
                    }
                }

                resultContent = sb.ToString();
            }
            else if (arguments.Prepend)
            {
                var existing = await File.ReadAllTextAsync(arguments.Path, cancellationToken);
                resultContent = arguments.Content + (existing.StartsWith("\n") ? existing : "\n" + existing);
            }
            else
            {
                // Default: append
                var existing = await File.ReadAllTextAsync(arguments.Path, cancellationToken);
                resultContent = existing + (existing.EndsWith("\n") ? "" : "\n") + arguments.Content;
            }

            await File.WriteAllTextAsync(arguments.Path, resultContent, cancellationToken);
            return $"Successfully wrote {resultContent.Length} characters to {arguments.Path}.";
        }
        catch (Exception ex)
        {
            return $"SYSTEM ERROR: Failed to write file: {ex.Message}";
        }
    }
}
