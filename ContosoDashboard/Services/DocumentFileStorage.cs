using Microsoft.Extensions.Options;

namespace ContosoDashboard.Services;

public interface IDocumentFileStorage
{
    string NewStorageKey();
    string NewStagingKey();
    string GetStagedFilePath(string stagingKey);
    Task<long> SaveStagedAsync(
        Stream input,
        string originalFileName,
        string stagingKey,
        long maximumBytes,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default);
    Task FinalizeToStorageAsync(string stagingKey, string storageKey, CancellationToken cancellationToken = default);
    Task DeleteStagedIfExistsAsync(string stagingKey, CancellationToken cancellationToken = default);
    Task DeleteIfExistsAsync(string storageKey, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);
}

public sealed class DocumentSizeLimitException : IOException
{
    public DocumentSizeLimitException() : base("The file exceeds the permitted size.") { }
}

public class DocumentFileStorage : IDocumentFileStorage
{
    private readonly string _root;
    private readonly string _staging;

    public DocumentFileStorage(IOptions<DocumentOptions> options, IWebHostEnvironment environment)
    {
        _root = DocumentOptions.ResolveAndValidateStorageRoot(
            environment.ContentRootPath,
            environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"),
            options.Value.StorageRoot);
        _staging = Path.Combine(_root, "staging");
        Directory.CreateDirectory(_staging);
    }

    public string NewStorageKey() => Guid.NewGuid().ToString("N");
    public string NewStagingKey() => Guid.NewGuid().ToString("N");

    public string GetStagedFilePath(string stagingKey)
    {
        ValidateOpaqueKey(stagingKey);
        return Path.Combine(_staging, stagingKey + ".staging");
    }

    public async Task<long> SaveStagedAsync(
        Stream input,
        string originalFileName,
        string stagingKey,
        long maximumBytes,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ValidateOpaqueKey(stagingKey);
        ValidateOriginalName(originalFileName);
        var path = GetStagedFilePath(stagingKey);
        long total = 0;

        try
        {
            await using var output = new FileStream(path, new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                BufferSize = 64 * 1024,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan
            });

            var buffer = new byte[64 * 1024];
            while (true)
            {
                var count = await input.ReadAsync(buffer.AsMemory(), cancellationToken);
                if (count == 0) break;
                if (total + count > maximumBytes)
                    throw new DocumentSizeLimitException();
                await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                total += count;
                progress?.Report(total);
            }

            await output.FlushAsync(cancellationToken);
            return total;
        }
        catch
        {
            await DeleteStagedIfExistsAsync(stagingKey, CancellationToken.None);
            throw;
        }
    }

    public Task FinalizeToStorageAsync(string stagingKey, string storageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateOpaqueKey(stagingKey);
        ValidateOpaqueKey(storageKey);
        var stagedPath = GetStagedFilePath(stagingKey);
        var finalPath = Path.Combine(_root, storageKey);
        File.Move(stagedPath, finalPath, overwrite: false);
        return Task.CompletedTask;
    }

    public Task DeleteStagedIfExistsAsync(string stagingKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateOpaqueKey(stagingKey);
        var path = GetStagedFilePath(stagingKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public Task DeleteIfExistsAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateOpaqueKey(storageKey);
        var path = Path.Combine(_root, storageKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateOpaqueKey(storageKey);
        var path = Path.Combine(_root, storageKey);
        Stream stream = new FileStream(path, new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,
            BufferSize = 64 * 1024,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan
        });
        return Task.FromResult(stream);
    }

    private static void ValidateOpaqueKey(string key)
    {
        if (key.Length != 32 || key.Any(c => !Uri.IsHexDigit(c)))
            throw new InvalidOperationException("Invalid internal storage key.");
    }

    private static void ValidateOriginalName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) ||
            name.Length > 255 ||
            name.Any(char.IsControl) ||
            name.Contains('/') ||
            name.Contains('\\') ||
            name is "." or "..")
            throw new InvalidDataException("Invalid original filename.");
    }
}
