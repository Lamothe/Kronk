using System.Diagnostics;
using System.Text;
using System.Text.Json.Serialization;

namespace Kronk.Models.Tools;

public class GitDiffProperties
{
    [JsonPropertyName("path")]
    public StringToolProperty Path { get; set; } = new("The repository root directory (defaults to current working directory)");

    [JsonPropertyName("file")]
    public StringToolProperty File { get; set; } = new("A specific file to diff (relative to repo root)");

    [JsonPropertyName("staged")]
    public BooleanToolProperty Staged { get; set; } = new("Show staged changes instead of unstaged (default: false)");

    [JsonPropertyName("stat_only")]
    public BooleanToolProperty StatOnly { get; set; } = new("Show only diffstat summary, not full diff (default: false)");
}

public class GitDiffToolInputSchema : ToolParameters<GitDiffProperties>
{
    public GitDiffToolInputSchema() : base(Array.Empty<string>())
    {
    }
}

public class GitDiffToolArguments
{
    public string? Path { get; set; }

    public string? File { get; set; }

    public bool Staged { get; set; } = false;

    public bool StatOnly { get; set; } = false;
}

public class GitDiffTool : Tool<GitDiffToolArguments, GitDiffToolInputSchema>
{
    public GitDiffTool() : base(
        "git_diff",
        "Shows differences between commits, the working tree, and the index. Use 'staged' to see staged changes, or specify a 'file' for a single-file diff."
    )
    {
    }

    public override async Task<string> Run(GitDiffToolArguments arguments, CancellationToken cancellationToken)
    {
        var repoPath = string.IsNullOrWhiteSpace(arguments.Path) ? Directory.GetCurrentDirectory() : arguments.Path;

        if (!Directory.Exists(repoPath))
        {
            return $"SYSTEM ERROR: Directory not found at {repoPath}.";
        }

        var gitArgs = new List<string>();

        if (arguments.Staged)
        {
            gitArgs.Add("diff --cached");
        }
        else
        {
            gitArgs.Add("diff");
        }

        if (arguments.StatOnly)
        {
            gitArgs.Add("--stat");
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

        var label = arguments.Staged ? "STAGED DIFF" : "DIFF";
        if (arguments.StatOnly) label += " (STAT)";
        if (!string.IsNullOrWhiteSpace(arguments.File)) label += $": {arguments.File}";

        return $"--- GIT {label}: {repoPath} ---\n{result}\n";
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

        // diff exits 1 when there are differences, which is normal
        if (process.ExitCode > 1)
        {
            return $"SYSTEM ERROR: git exited with code {process.ExitCode}\n{error.Trim()}";
        }

        return string.IsNullOrWhiteSpace(output) ? "(no changes)" : output.Trim();
    }
}
