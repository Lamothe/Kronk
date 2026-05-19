using System.Text;
using System.Text.Json.Serialization;

namespace Kronk.Models.Tools;

public class ReadFilesProperties
{
    [JsonPropertyName("paths")]
    public StringArrayToolProperty Paths { get; set; } = new("An array of absolute or relative file paths to read");
}

public class ReadFilesToolInputSchema : ToolParameters<ReadFilesProperties>
{
    public ReadFilesToolInputSchema() : base([nameof(ReadFilesProperties.Paths)])
    {
    }
}

public class ReadFilesToolArguments
{
    public required string[] Paths { get; set; }
}

public class ReadFilesTool : Tool<ReadFilesToolArguments, ReadFilesToolInputSchema>
{
    public ReadFilesTool() : base("read_files", "Reads the content of one or more files on the local file system.")
    {
    }

    public override async Task<string> Run(ReadFilesToolArguments arguments, CancellationToken cancellationToken)
    {
        var resultBuilder = new StringBuilder();

        foreach (var path in arguments.Paths)
        {
            if (File.Exists(path))
            {
                var content = await File.ReadAllTextAsync(path, cancellationToken);
                resultBuilder.AppendLine($"--- FILE: {path} ---");
                resultBuilder.AppendLine(content);
                resultBuilder.AppendLine();
            }
            else
            {
                resultBuilder.AppendLine($"--- FILE: {path} (NOT FOUND) ---");
            }
        }

        return resultBuilder.ToString();
    }
}