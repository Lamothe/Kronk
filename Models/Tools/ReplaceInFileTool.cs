using System.Text.Json.Serialization;

namespace Kronk.Models.Tools;

public class ReplaceInFileProperties
{
    [JsonPropertyName("path")]
    public StringToolProperty Path { get; set; } = new("The file path");

    [JsonPropertyName("search_text")]
    public StringToolProperty SearchText { get; set; } = new("The exact existing text to find and remove");

    [JsonPropertyName("replace_text")]
    public StringToolProperty ReplaceText { get; set; } = new("The new text to insert in its place");
}

public class ReplaceInFileToolInputSchema : ToolParameters<ReplaceInFileProperties>
{
    public ReplaceInFileToolInputSchema()
    : base([
        nameof(ReplaceInFileProperties.Path),
        nameof(ReplaceInFileProperties.SearchText),
        nameof(ReplaceInFileProperties.ReplaceText)])
    {
    }
}

public class ReplaceInFileToolArguments
{
    public required string Path { get; set; }

    public required string SearchText { get; set; }

    public required string ReplaceText { get; set; }
}

public class ReplaceInFileTool : Tool<ReplaceInFileToolArguments, ReplaceInFileToolInputSchema>
{
    public ReplaceInFileTool() : base(
        "replace_in_file",
        "Replaces a specific block of text in an existing file. CRITICAL: The search_text MUST exactly match the existing file content, including all whitespace, indentation, and blank lines."
    )
    {
    }

    public override async Task<string> Run(ReplaceInFileToolArguments arguments, CancellationToken cancellationToken)
    {
        string? toolResult;

        if (!File.Exists(arguments.Path))
        {
            toolResult = $"SYSTEM ERROR: File not found at {arguments.Path}.";
        }
        else
        {
            var content = await File.ReadAllTextAsync(arguments.Path, cancellationToken);

            if (!content.Contains(arguments.SearchText))
            {
                toolResult = "SYSTEM ERROR: The 'search_text' was not found in the file. You likely missed some whitespace, indentation, or included too much text. Read the file again and provide a smaller, strictly exact match.";
            }
            else
            {
                content = content.Replace(arguments.SearchText, arguments.ReplaceText);
                await File.WriteAllTextAsync(arguments.Path, content, cancellationToken);
                toolResult = $"Successfully replaced the text block. The file is now {content.Length} characters long.";
            }
        }

        return toolResult;
    }
}