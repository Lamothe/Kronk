using System.Text;
using System.Text.Json.Serialization;

namespace Kronk.Models.Tools;

public class ReadFileRangeProperties
{
    [JsonPropertyName("path")]
    public StringToolProperty Path { get; set; } = new("The file path to read");

    [JsonPropertyName("start_line")]
    public IntegerToolProperty StartLine { get; set; } = new("The starting line number (1-based, default: 1)");

    [JsonPropertyName("end_line")]
    public IntegerToolProperty EndLine { get; set; } = new("The ending line number (1-based, inclusive, default: end of file)");
}

public class ReadFileRangeToolInputSchema : ToolParameters<ReadFileRangeProperties>
{
    public ReadFileRangeToolInputSchema() : base([nameof(ReadFileRangeProperties.Path)])
    {
    }
}

public class ReadFileRangeToolArguments
{
    public required string Path { get; set; }

    public int StartLine { get; set; } = 1;

    public int EndLine { get; set; } = int.MaxValue;
}

public class ReadFileRangeTool : Tool<ReadFileRangeToolArguments, ReadFileRangeToolInputSchema>
{
    public ReadFileRangeTool() : base(
        "read_file_range",
        "Reads a specific range of lines from a file. Line numbers are 1-based and inclusive. Useful for reading large files without loading the entire content."
    )
    {
    }

    public override async Task<string> Run(ReadFileRangeToolArguments arguments, CancellationToken cancellationToken)
    {
        if (!File.Exists(arguments.Path))
        {
            return $"SYSTEM ERROR: File not found at {arguments.Path}.";
        }

        if (arguments.StartLine < 1)
        {
            return "SYSTEM ERROR: start_line must be >= 1.";
        }

        if (arguments.EndLine < arguments.StartLine)
        {
            return "SYSTEM ERROR: end_line must be >= start_line.";
        }

        try
        {
            var resultBuilder = new StringBuilder();
            resultBuilder.AppendLine($"--- FILE: {arguments.Path} (lines {arguments.StartLine}-{arguments.EndLine}) ---");

            var lineCount = 0;
            var startLine = arguments.StartLine;
            var endLine = arguments.EndLine;

            using var stream = File.OpenRead(arguments.Path);
            using var reader = new StreamReader(stream);

            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
            {
                lineCount++;

                if (lineCount >= startLine && lineCount <= endLine)
                {
                    resultBuilder.AppendLine($"{lineCount,6}: {line}");
                }

                if (lineCount > endLine)
                    break;
            }

            var totalLabel = lineCount >= endLine ? "(more may exist)" : lineCount.ToString();
            resultBuilder.AppendLine($"(total file lines: {totalLabel})");

            return resultBuilder.ToString();
        }
        catch (Exception ex)
        {
            return $"SYSTEM ERROR: Failed to read file: {ex.Message}";
        }
    }
}
