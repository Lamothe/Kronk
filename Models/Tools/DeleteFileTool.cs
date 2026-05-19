using System.Text.Json.Serialization;

namespace Kronk.Models.Tools;

public class DeleteFileProperties
{
    [JsonPropertyName("path")]
    public StringToolProperty Path { get; set; } = new("The file or directory path to delete");

    [JsonPropertyName("recursive")]
    public BooleanToolProperty Recursive { get; set; } = new("Recursively delete a directory and all its contents (default: false)");

    [JsonPropertyName("force")]
    public BooleanToolProperty Force { get; set; } = new("Delete read-only files and ignore errors on missing paths (default: false)");
}

public class DeleteFileToolInputSchema : ToolParameters<DeleteFileProperties>
{
    public DeleteFileToolInputSchema() : base([nameof(DeleteFileProperties.Path)])
    {
    }
}

public class DeleteFileToolArguments
{
    public required string Path { get; set; }

    public bool Recursive { get; set; } = false;

    public bool Force { get; set; } = false;
}

public class DeleteFileTool : Tool<DeleteFileToolArguments, DeleteFileToolInputSchema>
{
    public DeleteFileTool() : base(
        "delete_file",
        "Deletes a file or directory. Use 'recursive' to remove directories and all their contents. Use 'force' to ignore missing paths and delete read-only files."
    )
    {
    }

    public override async Task<string> Run(DeleteFileToolArguments arguments, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        var path = arguments.Path;

        if (!File.Exists(path) && !Directory.Exists(path))
        {
            if (arguments.Force)
            {
                return $"Path {path} does not exist. Nothing to delete.";
            }

            return $"SYSTEM ERROR: Path not found at {path}.";
        }

        try
        {
            if (File.Exists(path))
            {
                if (arguments.Force)
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                }

                File.Delete(path);
                return $"Successfully deleted file {path}.";
            }

            if (Directory.Exists(path))
            {
                if (!arguments.Recursive)
                {
                    var hasContents = Directory.GetFiles(path).Any() || Directory.GetDirectories(path).Any();
                    if (hasContents)
                    {
                        return $"SYSTEM ERROR: Directory {path} is not empty. Use 'recursive: true' to delete it and all contents.";
                    }
                }

                if (arguments.Recursive)
                {
                    var directoryInfo = new DirectoryInfo(path);
                    foreach (var file in directoryInfo.EnumerateFiles("*", new EnumerationOptions
                    {
                        RecurseSubdirectories = true,
                        IgnoreInaccessible = true
                    }))
                    {
                        try
                        {
                            if (arguments.Force)
                            {
                                file.Attributes = FileAttributes.Normal;
                            }

                            file.Delete();
                        }
                        catch
                        {
                            // Ignore individual file errors when force is set
                            if (!arguments.Force) throw;
                        }
                    }

                    foreach (var subDir in directoryInfo.EnumerateDirectories("*", new EnumerationOptions
                    {
                        RecurseSubdirectories = true,
                        IgnoreInaccessible = true
                    }).OrderByDescending(x => x.FullName))
                    {
                        try
                        {
                            subDir.Delete(true);
                        }
                        catch
                        {
                            if (!arguments.Force) throw;
                        }
                    }

                    try
                    {
                        Directory.Delete(path, false);
                    }
                    catch
                    {
                        // May fail if still in use
                    }

                    return $"Successfully deleted directory {path} and all contents.";
                }
                else
                {
                    Directory.Delete(path, false);
                    return $"Successfully deleted empty directory {path}.";
                }
            }

            return $"SYSTEM ERROR: Cannot determine type of path {path}.";
        }
        catch (Exception ex)
        {
            return $"SYSTEM ERROR: Failed to delete: {ex.Message}";
        }
    }
}
