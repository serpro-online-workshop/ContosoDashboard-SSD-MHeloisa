using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace ContosoDashboard.Services;

public interface ILocalMalwareScanner
{
    Task<MalwareScanResult> ScanAsync(string filePath, CancellationToken cancellationToken = default);
}

public sealed record MalwareScanResult(bool IsClean, bool WasDetected, string? FailureCode)
{
    public bool IsFailure => !IsClean && !WasDetected;
}

public sealed class LocalClamAvScanner : ILocalMalwareScanner
{
    private readonly DocumentOptions _options;
    private readonly ILogger<LocalClamAvScanner> _logger;

    public LocalClamAvScanner(IOptions<DocumentOptions> options, ILogger<LocalClamAvScanner> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<MalwareScanResult> ScanAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            return new MalwareScanResult(false, false, "scanner-error");

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _options.ClamScanPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            },
            EnableRaisingEvents = true
        };
        process.StartInfo.ArgumentList.Add("--no-summary");
        process.StartInfo.ArgumentList.Add("--");
        process.StartInfo.ArgumentList.Add(Path.GetFullPath(filePath));

        try
        {
            if (!process.Start())
                return new MalwareScanResult(false, false, "scanner-unavailable");
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            _logger.LogWarning("Local malware scanner could not start ({FailureType}).", exception.GetType().Name);
            return new MalwareScanResult(false, false, "scanner-unavailable");
        }

        // Drain both pipes so the scanner cannot deadlock on a full output buffer. Never expose its output.
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.ScannerTimeoutSeconds));

        try
        {
            await process.WaitForExitAsync(timeout.Token);
            await Task.WhenAll(stdout, stderr);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            await DrainAfterExitAsync(stdout, stderr);
            _logger.LogWarning("Local malware scan exceeded its configured timeout.");
            return new MalwareScanResult(false, false, "scanner-error");
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            await DrainAfterExitAsync(stdout, stderr);
            throw;
        }
        catch (Exception exception)
        {
            TryKill(process);
            _logger.LogWarning("Local malware scanner failed ({FailureType}).", exception.GetType().Name);
            return new MalwareScanResult(false, false, "scanner-error");
        }

        return process.ExitCode switch
        {
            0 => new MalwareScanResult(true, false, null),
            1 => new MalwareScanResult(false, true, "malware-detected"),
            _ => new MalwareScanResult(false, false, "scanner-error")
        };
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }

    private static async Task DrainAfterExitAsync(Task<string> stdout, Task<string> stderr)
    {
        try { await Task.WhenAll(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(2)); }
        catch (Exception) { /* Output is intentionally discarded; the scan already failed closed. */ }
    }
}
