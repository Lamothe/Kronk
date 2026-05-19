using System.Text.Json.Serialization;

namespace Kronk.Models;

public record JsonRpcError
{
    public required int Code { get; set; }
    public required string Message { get; set; }
}

public record JsonRpcResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc => "2.0";

    public object? Id { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; set; } = null;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonRpcError? Error { get; set; } = null;
}

public class JsonRpcRequest
{
    public string? Method { get; set; }
    public object? Params { get; set; }
    public string? JsonRpc { get; set; }
    public object? Id { get; set; }
}