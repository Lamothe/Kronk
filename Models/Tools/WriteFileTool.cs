using System.Text.Json.Serialization;

namespace Kronk.Models.Tools;

public class WriteFileProperties
{
    [JsonPropertyName("path")]
    public StringToolProperty Path { get; set; } = new("The file path");

    [JsonPropertyName("content")]
    public StringToolProperty Content { get; set; } = new("The content to write to the file");
}

public class WriteFileToolInputSchema : ToolParameters<WriteFileProperties>
{
    public WriteFileToolInputSchema() : base([
        nameof(WriteFileProperties.Path),
        nameof(WriteFileProperties.Content)])
    {
    }
}

public class WriteFileToolArguments
{
    public required string Path { get; set; }

    public required string Content { get; set; }
}

public class WriteFileTool : Tool<WriteFileToolArguments, WriteFileToolInputSchema>
{
    public WriteFileTool() : base(
        "write_file",
        "Writes content to a file on the local file system. Creates the file if it doesn't exist, or overwrites it if it does."
    )
    {
    }

    public override async Task<string> Run(WriteFileToolArguments arguments, CancellationToken cancellationToken)
    {
        try
        {
            var directory = Path.GetDirectoryName(arguments.Path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(arguments.Path, arguments.Content, cancellationToken);
            return $"Successfully wrote {arguments.Content.Length} characters to {arguments.Path}.";
        }
        catch (Exception ex)
        {
            return $"SYSTEM ERROR: Failed to write file: {ex.Message}";
        }
    }
}
