using System.Text.Json;
using Kronk.Models;
using Kronk.Models.Tools;
using Microsoft.AspNetCore.Mvc;

namespace Kronk.Controllers;

[Route("/")]
public class McpController : ControllerBase
{

    [HttpPost]
    public async Task<object?> Post([FromBody] JsonRpcRequest request, CancellationToken cancellationToken)
    {
        return request.Method switch
        {
            "initialize" => Initialise(request),
            "notifications/initialized" => null,
            "tools/list" => ListTools(request),
            "tools/call" => await CallTools(request, cancellationToken),
            _ => DefaultResponse(request)
        };
    }

    private static JsonRpcResponse Initialise(JsonRpcRequest request)
    {
        return new JsonRpcResponse
        {
            Id = request.Id,
            Result = new
            {
                protocolVersion = "2024-11-05",
                capabilities = new
                {
                    tools = new { }
                },
                serverInfo = new
                {
                    name = "Kronk MCP Server",
                    version = "1.0.0"
                }
            }
        };
    }

    private static JsonRpcResponse ListTools(JsonRpcRequest request)
    {
        return new JsonRpcResponse
        {
            Id = request.Id,
            Result = new
            {
                tools = ToolManager.Tools
            }
        };
    }

    private static async Task<object> CallTools(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        if (request.Params is not JsonElement paramsElement)
        {
            return new JsonRpcResponse
            {
                Id = request.Id!,
                Error = new JsonRpcError { Code = -32602, Message = "Invalid params format" }
            };
        }

        if (!paramsElement.TryGetProperty("name", out var nameElement))
        {
            return new JsonRpcResponse
            {
                Id = request.Id!,
                Error = new JsonRpcError { Code = -32602, Message = "Missing tool name" }
            };
        }
        var name = nameElement.GetString()!;

        var arguments = paramsElement.TryGetProperty("arguments", out var argsElement)
            ? argsElement.GetRawText()
            : "{}";

        return await Call(request.Id!, name, arguments, cancellationToken);
    }

    private static async Task<object> Call(object id, string name, string arguments, CancellationToken cancellationToken)
    {
        string contentResult;
        var isError = false;

        try
        {
            contentResult = await ToolManager.Run(name, arguments, cancellationToken);
        }
        catch (Exception ex)
        {
            isError = true;
            contentResult = $"Execution failed: {ex.Message}";
        }

        return new JsonRpcResponse
        {
            Id = id,
            Result = new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = contentResult
                    }
                },
                isError
            }
        };
    }

    private static JsonRpcResponse DefaultResponse(JsonRpcRequest request)
    {
        return new JsonRpcResponse
        {
            Id = request.Id,
            Error = new JsonRpcError
            {
                Code = -32601,
                Message = $"Unknown method {request.Method}"
            }
        };
    }
}
