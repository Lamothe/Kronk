using System.Text.Json.Serialization;

namespace Kronk.Models.Tools;

public class CreateDirectoryProperties
{
    [JsonPropertyName("path")]
    public StringToolProperty Path { get; set; } = new("The directory path to create");

    [JsonPropertyName("parents")]
    public BooleanToolProperty Parents { get; set; } = new("Create parent directories if they don't exist (default: true)");
}

public class CreateDirectoryToolInputSchema : ToolParameters<CreateDirectoryProperties>
{
    public CreateDirectoryToolInputSchema() : base([nameof(CreateDirectoryProperties.Path)])
    {
    }
}

public class CreateDirectoryToolArguments
{
    public required string Path { get; set; }

    public bool Parents { get; set; } = true;
}

public class CreateDirectoryTool : Tool<CreateDirectoryToolArguments, CreateDirectoryToolInputSchema>
{
    public CreateDirectoryTool() : base(
        "create_directory",
        "Creates a directory. By default creates parent directories as needed. Set 'parents' to false to require existing parents."
    )
    {
    }

    public override async Task<string> Run(CreateDirectoryToolArguments arguments, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        if (Directory.Exists(arguments.Path))
        {
            return $"Directory already exists at {arguments.Path}.";
        }

        try
        {
            if (!arguments.Parents)
            {
                var parent = Path.GetDirectoryName(arguments.Path);
                if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent))
                {
                    return $"SYSTEM ERROR: Parent directory does not exist. Set 'parents' to true to create it.";
                }

                Directory.CreateDirectory(arguments.Path);
            }
            else
            {
                Directory.CreateDirectory(arguments.Path);
            }

            return $"Successfully created directory {arguments.Path}.";
        }
        catch (Exception ex)
        {
            return $"SYSTEM ERROR: Failed to create directory: {ex.Message}";
        }
    }
}
