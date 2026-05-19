using System.Diagnostics;
using System.Text;
using System.Text.Json.Serialization;

namespace Kronk.Models.Tools;

public class GitStatusProperties
{
    [JsonPropertyName("path")]
    public StringToolProperty Path { get; set; } = new("The repository root directory (defaults to current working directory)");

    [JsonPropertyName("short")]
    public BooleanToolProperty Short { get; set; } = new("Use short/compact output format (default: true)");
}

public class GitStatusToolInputSchema : ToolParameters<GitStatusProperties>
{
    public GitStatusToolInputSchema() : base(Array.Empty<string>())
    {
    }
}

public class GitStatusToolArguments
{
    public string? Path { get; set; }

    public bool Short { get; set; } = true;
}

public class GitStatusTool : Tool<GitStatusToolArguments, GitStatusToolInputSchema>
{
    public GitStatusTool() : base(
        "git_status",
        "Shows the working tree status of a git repository. Displays staged, unstaged, and untracked files."
    )
    {
    }

    public override async Task<string> Run(GitStatusToolArguments arguments, CancellationToken cancellationToken)
    {
        var repoPath = string.IsNullOrWhiteSpace(arguments.Path) ? Directory.GetCurrentDirectory() : arguments.Path;

        if (!Directory.Exists(repoPath))
        {
            return $"SYSTEM ERROR: Directory not found at {repoPath}.";
        }

        var formatArgs = arguments.Short ? "--short --branch" : "--long --branch";

        var result = await RunGit(repoPath, $"status {formatArgs}", cancellationToken);

        if (result.Contains("fatal: not a git repository"))
        {
            return $"SYSTEM ERROR: {repoPath} is not a git repository.";
        }

        return result;
    }

    private static async Task<string> RunGit(string repoPath, string gitArgs, CancellationToken cancellationToken)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"-C \"{repoPath}\" {gitArgs}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = repoPath
            }
        };

        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            return $"SYSTEM ERROR: git exited with code {process.ExitCode}\n{error.Trim()}";
        }

        return $"--- GIT STATUS: {repoPath} ---\n{output.Trim()}\n";
    }
}
