using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Kronk.Models.Tools;

public class RunCommandToolProperties
{
    [JsonPropertyName("command")]
    public StringToolProperty Command { get; set; } = new("The bash command to execute");
}

public class RunCommandToolInputSchema : ToolParameters<RunCommandToolProperties>
{
    public RunCommandToolInputSchema() : base([nameof(RunCommandToolProperties.Command)])
    {
    }
}

public class RunCommandToolArguments
{
    public required string Command { get; set; }
}

public class RunCommandTool : Tool<RunCommandToolArguments, RunCommandToolInputSchema>
{
    public RunCommandTool() : base("run_command", "Executes a shell commands in the Linux terminal.")
    {
    }

    public override async Task<string> Run(RunCommandToolArguments arguments, CancellationToken cancellationToken)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.Start();

        // Write the raw commands directly to standard input to bypass all escaping vulnerabilities
        await process.StandardInput.WriteLineAsync(arguments.Command);
        process.StandardInput.Close(); // Tells Bash we are done sending commands

        // Read StandardOutput and StandardError concurrently to prevent deadlocks
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var errStr = await errorTask;

        var toolResult = await outputTask;
        if (!string.IsNullOrWhiteSpace(errStr))
        {
            toolResult += $"\nSTDERR:\n{errStr}";
        }

        return toolResult;
    }
}