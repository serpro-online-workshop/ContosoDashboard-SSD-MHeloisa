using System.IO.Compression;
using System.Text;

namespace ContosoDashboard.Services;

public sealed record DocumentValidationResult(
    bool IsValid,
    string? ErrorCode,
    string? ErrorMessage,
    string? DetectedContentType,
    string? NormalizedExtension);

public interface IDocumentFileValidator
{
    DocumentValidationResult Validate(Stream payload, string fileName, string? declaredContentType, long maximumBytes);
}

public class DocumentFileValidator : IDocumentFileValidator
{
    private static readonly IReadOnlyDictionary<string, string> Types = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["pdf"] = "application/pdf",
        ["doc"] = "application/msword",
        ["docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ["xls"] = "application/vnd.ms-excel",
        ["xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ["ppt"] = "application/vnd.ms-powerpoint",
        ["pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ["txt"] = "text/plain",
        ["csv"] = "text/csv",
        ["jpg"] = "image/jpeg",
        ["jpeg"] = "image/jpeg",
        ["png"] = "image/png"
    };

    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];
    private static readonly byte[] OleSignature = [0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1];

    public DocumentValidationResult Validate(Stream payload, string fileName, string? declaredContentType, long maximumBytes)
    {
        var name = fileName?.Trim() ?? string.Empty;
        if (name.Length is 0 or > 255 || name.Any(char.IsControl) ||
            name.Contains('/') || name.Contains('\\') || name is "." or "..")
            return Invalid("invalid-metadata", "The original filename is invalid.");

        var extension = Path.GetExtension(name).TrimStart('.').ToLowerInvariant();
        if (!Types.TryGetValue(extension, out var expectedType))
            return Invalid("unsupported-type", "This file type is not supported.", extension);

        if (payload.CanSeek)
        {
            if (payload.Length == 0) return Invalid("empty-file", "The selected file is empty.", extension);
            if (payload.Length > maximumBytes) return Invalid("size-exceeded", "The file exceeds the 25 MiB limit.", extension);
            payload.Position = 0;
        }

        var detected = Detect(payload, extension);
        if (payload.CanSeek) payload.Position = 0;
        if (detected == null)
            return Invalid("content-type-mismatch", "The file content does not match a supported file type.", extension);
        if (!string.Equals(detected, expectedType, StringComparison.OrdinalIgnoreCase))
            return Invalid("content-type-mismatch", "The filename extension does not match the file content.", extension);

        var declared = NormalizeDeclaredType(declaredContentType);
        if (declared != null && !string.Equals(declared, detected, StringComparison.OrdinalIgnoreCase))
            return Invalid("content-type-mismatch", "The supplied content type does not match the file content.", extension);

        return new DocumentValidationResult(true, null, null, detected, extension);
    }

    private static string? Detect(Stream stream, string extension)
    {
        var start = new byte[16];
        var read = ReadUpTo(stream, start);
        if (StartsWith(start, read, PdfSignature)) return "application/pdf";
        if (StartsWith(start, read, PngSignature)) return "image/png";
        if (read >= 3 && start[0] == 0xff && start[1] == 0xd8 && start[2] == 0xff) return "image/jpeg";

        if (StartsWith(start, read, OleSignature))
        {
            return extension switch
            {
                "doc" => "application/msword",
                "xls" => "application/vnd.ms-excel",
                "ppt" => "application/vnd.ms-powerpoint",
                _ => null
            };
        }

        if (read >= 4 && start[0] == 0x50 && start[1] == 0x4b && start[2] == 0x03 && start[3] == 0x04)
            return DetectOpenXml(stream, extension);

        if (extension is "txt" or "csv" && IsUtf8Text(stream))
            return extension == "csv" ? "text/csv" : "text/plain";
        return null;
    }

    private static string? DetectOpenXml(Stream stream, string extension)
    {
        if (!stream.CanSeek) return null;
        stream.Position = 0;
        try
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            var entries = archive.Entries.Select(e => e.FullName).ToHashSet(StringComparer.Ordinal);
            return extension switch
            {
                "docx" when entries.Contains("word/document.xml") => Types[extension],
                "xlsx" when entries.Contains("xl/workbook.xml") => Types[extension],
                "pptx" when entries.Contains("ppt/presentation.xml") => Types[extension],
                _ => null
            };
        }
        catch (InvalidDataException)
        {
            return null;
        }
        finally
        {
            stream.Position = 0;
        }
    }

    private static bool IsUtf8Text(Stream stream)
    {
        if (!stream.CanSeek) return false;
        var oldPosition = stream.Position;
        try
        {
            stream.Position = 0;
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            var bytes = reader.ReadBytes(8192);
            if (bytes.Length == 0 || bytes.Contains((byte)0)) return false;
            _ = new UTF8Encoding(false, true).GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
        finally
        {
            stream.Position = oldPosition;
        }
    }

    private static string? NormalizeDeclaredType(string? declared)
    {
        if (string.IsNullOrWhiteSpace(declared) ||
            declared.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
            return null;
        return declared.Split(';', 2)[0].Trim();
    }

    private static int ReadUpTo(Stream stream, byte[] buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = stream.Read(buffer, total, buffer.Length - total);
            if (read == 0) break;
            total += read;
        }
        return total;
    }

    private static bool StartsWith(byte[] value, int length, byte[] signature) =>
        length >= signature.Length && value.AsSpan(0, signature.Length).SequenceEqual(signature);

    private static DocumentValidationResult Invalid(string code, string message, string? extension = null) =>
        new(false, code, message, null, extension);
}
