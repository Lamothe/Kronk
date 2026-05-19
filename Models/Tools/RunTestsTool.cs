using System.Diagnostics;
using System.Text;
using System.Text.Json.Serialization;

namespace Kronk.Models.Tools;

public class RunTestsProperties
{
    [JsonPropertyName("path")]
    public StringToolProperty Path { get; set; } = new("The path to the project or solution file (defaults to current working directory)");

    [JsonPropertyName("filter")]
    public StringToolProperty Filter { get; set; } = new("Test filter expression (e.g. 'FullyQualifiedName~MyTest', 'DisplayName=MyTest')");

    [JsonPropertyName("framework")]
    public StringToolProperty Framework { get; set; } = new("Target framework (e.g. 'net10.0', 'net9.0')");

    [JsonPropertyName("verbosity")]
    public StringToolProperty Verbosity { get; set; } = new("Build verbosity: quiet, minimal, normal, detailed (default: minimal)");

    [JsonPropertyName("no_build")]
    public BooleanToolProperty NoBuild { get; set; } = new("Skip building the project before running tests (default: false)");

    [JsonPropertyName("test_runner")]
    public StringToolProperty TestRunner { get; set; } = new("The test runner to use: dotnet, npm, yarn (default: dotnet)");

    [JsonPropertyName("npm_script")]
    public StringToolProperty NpmScript { get; set; } = new("The npm/yarn script to run when test_runner is npm or yarn (default: test)");
}

public class RunTestsToolInputSchema : ToolParameters<RunTestsProperties>
{
    public RunTestsToolInputSchema() : base(Array.Empty<string>())
    {
    }
}

public class RunTestsToolArguments
{
    public string? Path { get; set; }

    public string? Filter { get; set; }

    public string? Framework { get; set; }

    public string? Verbosity { get; set; }

    public bool NoBuild { get; set; } = false;

    public string? TestRunner { get; set; }

    public string? NpmScript { get; set; }
}

public class RunTestsTool : Tool<RunTestsToolArguments, RunTestsToolInputSchema>
{
    public RunTestsTool() : base(
        "run_tests",
        "Runs tests using dotnet test, npm test, or yarn test. Supports filtering by test name, target framework, and verbosity. Use 'test_runner' to select the tool."
    )
    {
    }

    public override async Task<string> Run(RunTestsToolArguments arguments, CancellationToken cancellationToken)
    {
        var workPath = string.IsNullOrWhiteSpace(arguments.Path) ? Directory.GetCurrentDirectory() : arguments.Path;

        if (!Directory.Exists(workPath) && !File.Exists(workPath))
        {
            return $"SYSTEM ERROR: Path not found: {workPath}";
        }

        var runner = string.IsNullOrWhiteSpace(arguments.TestRunner) ? "dotnet" : arguments.TestRunner.ToLowerInvariant();

        return runner switch
        {
            "dotnet" => await RunDotnetTests(workPath, arguments, cancellationToken),
            "npm" => await RunNpmTests(workPath, arguments, cancellationToken),
            "yarn" => await RunYarnTests(workPath, arguments, cancellationToken),
            _ => $"SYSTEM ERROR: Unknown test runner '{runner}'. Use dotnet, npm, or yarn."
        };
    }

    private static async Task<string> RunDotnetTests(string workPath, RunTestsToolArguments arguments, CancellationToken cancellationToken)
    {
        var dotnetArgs = new List<string> { "test" };

        if (File.Exists(workPath))
        {
            dotnetArgs.Add($"\"{workPath}\"");
        }

        if (!string.IsNullOrWhiteSpace(arguments.Filter))
        {
            dotnetArgs.Add($"--filter \"{arguments.Filter}\"");
        }

        if (!string.IsNullOrWhiteSpace(arguments.Framework))
        {
            dotnetArgs.Add($"--framework {arguments.Framework}");
        }

        if (arguments.NoBuild)
        {
            dotnetArgs.Add("--no-build");
        }

        if (!string.IsNullOrWhiteSpace(arguments.Verbosity))
        {
            dotnetArgs.Add($"--verbosity {arguments.Verbosity}");
        }

        return await RunCommandInternal("dotnet", string.Join(" ", dotnetArgs), workPath, "DOTNET TEST", cancellationToken);
    }

    private static async Task<string> RunNpmTests(string workPath, RunTestsToolArguments arguments, CancellationToken cancellationToken)
    {
        var script = string.IsNullOrWhiteSpace(arguments.NpmScript) ? "test" : arguments.NpmScript;
        return await RunCommandInternal("npx", $"npm run {script}", workPath, "NPM TEST", cancellationToken);
    }

    private static async Task<string> RunYarnTests(string workPath, RunTestsToolArguments arguments, CancellationToken cancellationToken)
    {
        var script = string.IsNullOrWhiteSpace(arguments.NpmScript) ? "test" : arguments.NpmScript;
        return await RunCommandInternal("yarn", $"run {script}", workPath, "YARN TEST", cancellationToken);
    }

    private static async Task<string> RunCommandInternal(string fileName, string args, string workPath, string label, CancellationToken cancellationToken)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Directory.Exists(workPath) ? workPath : Path.GetDirectoryName(workPath)
            }
        };

        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var output = await outputTask;
        var error = await errorTask;

        var result = output.Trim();
        if (!string.IsNullOrWhiteSpace(error))
        {
            result += $"\nSTDERR:\n{error.Trim()}";
        }

        var status = process.ExitCode == 0 ? "PASSED" : $"FAILED (exit code {process.ExitCode})";

        return $"--- {label}: {status} ---\n{result}\n";
    }
}
