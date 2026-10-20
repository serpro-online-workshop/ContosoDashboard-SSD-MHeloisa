using System.IO.Compression;
using System.Text;
using ContosoDashboard.Services;
using Xunit;

namespace ContosoDashboard.Tests.Unit;

public sealed class DocumentUploadValidationTests
{
    private readonly DocumentFileValidator _validator = new();
    private const long MaximumSize = DocumentOptions.MaximumFileSizeBytes;

    [Theory]
    [InlineData("../bad.pdf")]
    [InlineData("folder\\bad.pdf")]
    [InlineData("bad\nname.pdf")]
    [InlineData("")]
    public void RejectsPathOrControlCharactersInDisplayName(string name)
    {
        using var stream = new MemoryStream("%PDF-1.7"u8.ToArray());
        var result = _validator.Validate(stream, name, "application/pdf", MaximumSize);
        Assert.False(result.IsValid);
        Assert.Equal("invalid-metadata", result.ErrorCode);
    }

    [Fact]
    public void AcceptsOrdinarySpecialCharactersInDisplayName()
    {
        using var stream = new MemoryStream("%PDF-1.7"u8.ToArray());
        var result = _validator.Validate(stream, "Q4 Report (2025) - Finance & Ops.pdf", "application/pdf", MaximumSize);
        Assert.True(result.IsValid);
        Assert.Equal("application/pdf", result.DetectedContentType);
    }

    [Fact]
    public void RejectsEmptyFiles()
    {
        using var stream = new MemoryStream();
        var result = _validator.Validate(stream, "empty.pdf", "application/pdf", MaximumSize);
        Assert.False(result.IsValid);
        Assert.Equal("empty-file", result.ErrorCode);
    }

    [Fact]
    public void RejectsFilesOverLimitAndAcceptsTheExactMaximum()
    {
        var oversized = new byte[(int)MaximumSize + 1];
        oversized[0] = (byte)'%';
        using var tooLarge = new MemoryStream(oversized);
        Assert.Equal("size-exceeded", _validator.Validate(tooLarge, "large.pdf", "application/pdf", MaximumSize).ErrorCode);

        var maximum = new byte[(int)MaximumSize];
        Encoding.ASCII.GetBytes("%PDF-1.7").CopyTo(maximum, 0);
        using var allowed = new MemoryStream(maximum);
        Assert.True(_validator.Validate(allowed, "maximum.pdf", "application/pdf", MaximumSize).IsValid);
    }

    [Fact]
    public void RejectsExtensionAndContentMismatch()
    {
        using var stream = new MemoryStream("%PDF-1.7"u8.ToArray());
        var result = _validator.Validate(stream, "image.jpg", "image/jpeg", MaximumSize);
        Assert.False(result.IsValid);
        Assert.Equal("content-type-mismatch", result.ErrorCode);
    }

    [Fact]
    public void RejectsUnsupportedExtensionAndUntrustedDeclaredTypeMismatch()
    {
        using var stream = new MemoryStream("%PDF-1.7"u8.ToArray());
        Assert.Equal("unsupported-type", _validator.Validate(stream, "file.exe", null, MaximumSize).ErrorCode);

        using var other = new MemoryStream("%PDF-1.7"u8.ToArray());
        Assert.Equal("content-type-mismatch", _validator.Validate(other, "file.pdf", "text/plain", MaximumSize).ErrorCode);
    }

    [Fact]
    public void DetectsOpenXmlFamilyByPackageContentsInsteadOfFilenameAlone()
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
            archive.CreateEntry("word/document.xml");
        memory.Position = 0;

        var result = _validator.Validate(memory, "report.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document", MaximumSize);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void RejectsZipPayloadThatDoesNotContainTheDeclaredOfficeFamily()
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
            archive.CreateEntry("xl/workbook.xml");
        memory.Position = 0;

        var result = _validator.Validate(memory, "report.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document", MaximumSize);
        Assert.False(result.IsValid);
        Assert.Equal("content-type-mismatch", result.ErrorCode);
    }
}
