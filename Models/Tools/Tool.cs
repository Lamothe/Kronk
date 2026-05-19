using System.Text.Json;

namespace Kronk.Models.Tools;

public interface ITool
{
    string Name { get; }
    Task<string> Run(string? json, CancellationToken cancellationToken);
}

public class ToolProperty(string type, string description)
{
    public string Type { get; } = type;

    public string Description { get; } = description;
}

public class StringToolProperty(string description) : ToolProperty("string", description)
{
}

public class ArrayToolPropertyItem(string type)
{
    public string Type { get; } = type;
}

public class StringArrayToolProperty(string description) : ToolProperty("array", description)
{
    public ArrayToolPropertyItem Items { get; } = new("string");
}

public class BooleanToolProperty(string description) : ToolProperty("boolean", description)
{
}

public class IntegerToolProperty(string description) : ToolProperty("integer", description)
{
}

public class ToolParameters<TProperties>(string[] required) where TProperties : new()
{
    public string Type { get; } = "object";

    public TProperties Properties { get; set; } = new TProperties();

    public IEnumerable<string> Required { get; set; } = required.Select(x => JsonNamingPolicy.SnakeCaseLower.ConvertName(x));
}

public abstract class Tool<TArguments, TInputSchema>(string name, string description) : ITool where TInputSchema : new()
{
    public string Name => name;
    public string Description => description;

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public TInputSchema InputSchema { get; set; } = new TInputSchema();

    private static TArguments GetArguments(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentNullException(nameof(json));
        }

        return JsonSerializer.Deserialize<TArguments>(json, JsonSerializerOptions)
            ?? throw new Exception("No response from deserialiser");
    }

    public Task<string> Run(string? json, CancellationToken cancellationToken)
    {
        var arguments = GetArguments(json);
        return Run(arguments, cancellationToken);
    }

    public abstract Task<string> Run(TArguments arguments, CancellationToken cancellationToken);
}
