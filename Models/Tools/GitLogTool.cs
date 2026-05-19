using System.Diagnostics;
using System.Text;
using System.Text.Json.Serialization;

namespace Kronk.Models.Tools;

public class GitLogProperties
{
    [JsonPropertyName("path")]
    public StringToolProperty Path { get; set; } = new("The repository root directory (defaults to current working directory)");

    [JsonPropertyName("max_count")]
    public IntegerToolProperty MaxCount { get; set; } = new("Maximum number of commits to show (default: 10)");

    [JsonPropertyName("file")]
    public StringToolProperty File { get; set; } = new("Show history for a specific file only");

    [JsonPropertyName("author")]
    public StringToolProperty Author { get; set; } = new("Filter by author name or email");

    [JsonPropertyName("since")]
    public StringToolProperty Since { get; set; } = new("Show commits since this date (e.g. '2 weeks ago', '2024-01-01')");

    [JsonPropertyName("oneline")]
    public BooleanToolProperty Oneline { get; set; } = new("Use compact one-line output format (default: true)");
}

public class GitLogToolInputSchema : ToolParameters<GitLogProperties>
{
    public GitLogToolInputSchema() : base(Array.Empty<string>())
    {
    }
}

public class GitLogToolArguments
{
    public string? Path { get; set; }

    public int MaxCount { get; set; } = 10;

    public string? File { get; set; }

    public string? Author { get; set; }

    public string? Since { get; set; }

    public bool Oneline { get; set; } = true;
}

public class GitLogTool : Tool<GitLogToolArguments, GitLogToolInputSchema>
{
    public GitLogTool() : base(
        "git_log",
        "Shows the commit history of a git repository. Supports filtering by file, author, and date range."
    )
    {
    }

    public override async Task<string> Run(GitLogToolArguments arguments, CancellationToken cancellationToken)
    {
        var repoPath = string.IsNullOrWhiteSpace(arguments.Path) ? Directory.GetCurrentDirectory() : arguments.Path;

        if (!Directory.Exists(repoPath))
        {
            return $"SYSTEM ERROR: Directory not found at {repoPath}.";
        }

        var gitArgs = new List<string> { "log" };

        if (arguments.Oneline)
        {
            gitArgs.Add("--oneline --decorate");
        }
        else
        {
            gitArgs.Add("--format=%H %an <%ae> %ai%n    %s");
        }

        gitArgs.Add($"-n {arguments.MaxCount}");

        if (!string.IsNullOrWhiteSpace(arguments.Author))
        {
            gitArgs.Add($"--author={arguments.Author}");
        }

        if (!string.IsNullOrWhiteSpace(arguments.Since))
        {
            gitArgs.Add($"--since={arguments.Since}");
        }

        if (!string.IsNullOrWhiteSpace(arguments.File))
        {
            gitArgs.Add($"-- \"{arguments.File}\"");
        }

        var result = await RunGit(repoPath, string.Join(" ", gitArgs), cancellationToken);

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

        return $"--- GIT LOG: {repoPath} ---\n{output.Trim()}\n";
    }
}
