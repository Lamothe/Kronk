using System.Text;
using System.Text.Json.Serialization;

namespace Kronk.Models.Tools;

public class GetFileInfoProperties
{
    [JsonPropertyName("path")]
    public StringToolProperty Path { get; set; } = new("The file or directory path to get info for");
}

public class GetFileInfoToolInputSchema : ToolParameters<GetFileInfoProperties>
{
    public GetFileInfoToolInputSchema() : base([nameof(GetFileInfoProperties.Path)])
    {
    }
}

public class GetFileInfoToolArguments
{
    public required string Path { get; set; }
}

public class GetFileInfoTool : Tool<GetFileInfoToolArguments, GetFileInfoToolInputSchema>
{
    public GetFileInfoTool() : base(
        "get_file_info",
        "Returns metadata about a file or directory, including type, size, creation time, last modified time, and permissions."
    )
    {
    }

    public override async Task<string> Run(GetFileInfoToolArguments arguments, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        if (!File.Exists(arguments.Path) && !Directory.Exists(arguments.Path))
        {
            return $"SYSTEM ERROR: Path not found: {arguments.Path}";
        }

        try
        {
            var resultBuilder = new StringBuilder();
            var info = new FileInfo(arguments.Path);
            var dirInfo = new DirectoryInfo(arguments.Path);

            if (File.Exists(arguments.Path))
            {
                resultBuilder.AppendLine($"--- FILE INFO: {arguments.Path} ---");
                resultBuilder.AppendLine($"Type:        file");
                resultBuilder.AppendLine($"Size:        {FormatBytes(info.Length)} ({info.Length} bytes)");
                resultBuilder.AppendLine($"Created:     {info.CreationTimeUtc:yyyy-MM-dd HH:mm:ss UTC}");
                resultBuilder.AppendLine($"Modified:    {info.LastWriteTimeUtc:yyyy-MM-dd HH:mm:ss UTC}");
                resultBuilder.AppendLine($"Accessed:    {info.LastAccessTimeUtc:yyyy-MM-dd HH:mm:ss UTC}");
                resultBuilder.AppendLine($"Name:        {info.Name}");
                resultBuilder.AppendLine($"Extension:   {info.Extension}");
                resultBuilder.AppendLine($"Directory:   {info.DirectoryName}");
                resultBuilder.AppendLine($"FullPath:    {info.FullName}");

                if (OperatingSystem.IsLinux())
                {
                    try
                    {
                        var unixMode = info.UnixFileMode;
                        resultBuilder.AppendLine($"UnixMode:    {((int)unixMode):X4}");
                    }
                    catch
                    {
                        // Ignore if UnixFileMode is not supported
                    }
                }

                var lineCount = File.ReadLines(arguments.Path).Count();
                resultBuilder.AppendLine($"Lines:       {lineCount}");
            }
            else if (Directory.Exists(arguments.Path))
            {
                resultBuilder.AppendLine($"--- DIRECTORY INFO: {arguments.Path} ---");
                resultBuilder.AppendLine($"Type:        directory");
                resultBuilder.AppendLine($"Created:     {dirInfo.CreationTimeUtc:yyyy-MM-dd HH:mm:ss UTC}");
                resultBuilder.AppendLine($"Modified:    {dirInfo.LastWriteTimeUtc:yyyy-MM-dd HH:mm:ss UTC}");
                resultBuilder.AppendLine($"Name:        {dirInfo.Name}");
                resultBuilder.AppendLine($"FullPath:    {dirInfo.FullName}");

                var fileCount = dirInfo.EnumerateFiles("*", new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true
                }).Count();

                var dirCount = dirInfo.EnumerateDirectories("*", new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true
                }).Count();

                resultBuilder.AppendLine($"Files:       {fileCount}");
                resultBuilder.AppendLine($"Subdirs:     {dirCount}");

                if (OperatingSystem.IsLinux())
                {
                    try
                    {
                        var unixMode = dirInfo.UnixFileMode;
                        resultBuilder.AppendLine($"UnixMode:    {((int)unixMode):X4}");
                    }
                    catch
                    {
                        // Ignore
                    }
                }
            }

            return resultBuilder.ToString();
        }
        catch (Exception ex)
        {
            return $"SYSTEM ERROR: Failed to get file info: {ex.Message}";
        }
    }

    private static string FormatBytes(long bytes)
    {
        var units = new[] { "B", "KB", "MB", "GB", "TB" };
        var unitIndex = 0;
        var size = (double)bytes;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        var decimals = unitIndex == 0 ? 0 : 1;
        return $"{size.ToString($"F{decimals}")} {units[unitIndex]}";
    }
}
