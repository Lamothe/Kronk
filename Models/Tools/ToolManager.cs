namespace Kronk.Models.Tools;

public static class ToolManager
{
    public static readonly object[] Tools =
    [
        // Web
        new FetchUrlTool(),

        // File I/O
        new ReadFilesTool(),
        new ReadFileRangeTool(),
        new WriteFileTool(),
        new ReplaceInFileTool(),
        new AppendToFileTool(),

        // Directory
        new ListDirectoryTool(),
        new CreateDirectoryTool(),
        new SearchFilesTool(),
        new GetFileInfoTool(),
        new DeleteFileTool(),

        // Search
        new SearchInFilesTool(),

        // Shell
        new RunCommandTool(),
        new RunTestsTool(),

        // Git
        new GitStatusTool(),
        new GitDiffTool(),
        new GitLogTool(),
    ];

    public static ITool GetTool(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentNullException(nameof(name));
        }

        return Tools.Cast<ITool>().SingleOrDefault(x => x.Name == name) ?? throw new Exception($"Unable to find tool '{name}'");
    }

    public static Task<string> Run(string? toolName, string? json, CancellationToken cancellationToken)
    {
        return GetTool(toolName).Run(json, cancellationToken);
    }
}