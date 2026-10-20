namespace ContosoDashboard.Services;

public sealed class DocumentOptions
{
    public const string SectionName = "DocumentStorage";
    public const long MaximumFileSizeBytes = 26_214_400;

    // Relative paths resolve from the application content root, never the current working directory.
    public string StorageRoot { get; set; } = "App_Data/Documents";
    public string ClamScanPath { get; set; } = "clamscan";
    public int ScannerTimeoutSeconds { get; set; } = 30;
    public long MaxFileSizeBytes { get; set; } = MaximumFileSizeBytes;
    public string[] AllowedExtensions { get; set; } =
        ["pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx", "txt", "jpg", "jpeg", "png"];

    public string[] AllowedContentTypes { get; set; } =
    [
        "application/pdf", "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.ms-powerpoint",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "text/plain", "text/csv", "image/jpeg", "image/png"
    ];

    public static string ResolveAndValidateStorageRoot(string contentRoot, string webRoot, string configuredRoot)
    {
        if (string.IsNullOrWhiteSpace(configuredRoot))
            throw new InvalidOperationException("DocumentStorage:StorageRoot must be configured.");

        var fullRoot = Path.GetFullPath(Path.IsPathRooted(configuredRoot)
            ? configuredRoot
            : Path.Combine(contentRoot, configuredRoot));
        var fullWebRoot = Path.GetFullPath(webRoot);
        Directory.CreateDirectory(fullRoot);

        var resolvedRoot = ResolveFinalDirectoryLink(fullRoot);
        var resolvedWebRoot = ResolveFinalDirectoryLink(fullWebRoot);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (IsWithin(resolvedRoot, resolvedWebRoot, comparison))
            throw new InvalidOperationException("DocumentStorage:StorageRoot must resolve outside wwwroot.");

        return resolvedRoot;
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ClamScanPath))
            throw new InvalidOperationException("DocumentStorage:ClamScanPath must name the required local ClamAV executable.");
        if (ScannerTimeoutSeconds is < 1 or > 600)
            throw new InvalidOperationException("DocumentStorage:ScannerTimeoutSeconds must be between 1 and 600.");
        if (MaxFileSizeBytes != MaximumFileSizeBytes)
            throw new InvalidOperationException($"DocumentStorage:MaxFileSizeBytes must be {MaximumFileSizeBytes}.");
        if (AllowedExtensions.Length == 0 || AllowedContentTypes.Length == 0)
            throw new InvalidOperationException("The document type allow-list cannot be empty.");
    }

    private static string ResolveFinalDirectoryLink(string directoryPath)
    {
        var fullPath = Path.GetFullPath(directoryPath);
        var root = Path.GetPathRoot(fullPath)!;
        var relative = fullPath[root.Length..];
        var segments = relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);
        var resolved = root;
        foreach (var segment in segments)
        {
            resolved = Path.Combine(resolved, segment);
            if (!Directory.Exists(resolved) && !File.Exists(resolved))
                continue;
            FileSystemInfo info = Directory.Exists(resolved)
                ? new DirectoryInfo(resolved)
                : new FileInfo(resolved);
            var target = info.ResolveLinkTarget(true);
            if (target is not null)
                resolved = Path.GetFullPath(target.FullName);
        }

        return Path.GetFullPath(resolved);
    }

    private static bool IsWithin(string path, string parent, StringComparison comparison)
    {
        var relative = Path.GetRelativePath(parent, path);
        return relative == "." ||
               (!Path.IsPathRooted(relative) &&
                relative != ".." &&
                !relative.StartsWith(".." + Path.DirectorySeparatorChar, comparison) &&
                !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, comparison));
    }
}
