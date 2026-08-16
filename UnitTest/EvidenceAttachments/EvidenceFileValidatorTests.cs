using FluentAssertions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.EvidenceAttachments.Application;

namespace UnitTest.EvidenceAttachments;

public sealed class EvidenceFileValidatorTests
{
    private readonly EvidenceFileValidator _validator = new();

    [Fact]
    public void AcceptsAllowedImageAndSanitizesFileName()
    {
        EvidenceFileValidationResult result = _validator.Validate("my photo.jpg", "image/jpeg", 10, 100);

        result.SafeFileName.Should().Be("my-photo.jpg");
        result.ContentType.Should().Be("image/jpeg");
    }

    [Theory]
    [InlineData("evil.exe", "application/octet-stream")]
    [InlineData("script.js", "text/javascript")]
    [InlineData("page.html", "text/html")]
    [InlineData("vector.svg", "image/svg+xml")]
    [InlineData("archive.zip", "application/zip")]
    public void RejectsUnsafeExtensionsAndContentTypes(string fileName, string contentType)
    {
        Assert.Throws<ValidationAppException>(() => _validator.Validate(fileName, contentType, 10, 100));
    }

    [Theory]
    [InlineData("../photo.jpg")]
    [InlineData("folder/photo.jpg")]
    [InlineData("folder\\photo.jpg")]
    public void RejectsPathTraversalAndPathSegments(string fileName)
    {
        Assert.Throws<ValidationAppException>(() => _validator.Validate(fileName, "image/jpeg", 10, 100));
    }

    [Fact]
    public void RejectsEmptyAndTooLargeFiles()
    {
        Assert.Throws<ValidationAppException>(() => _validator.Validate("photo.jpg", "image/jpeg", 0, 100));
        Assert.Throws<ValidationAppException>(() => _validator.Validate("photo.jpg", "image/jpeg", 101, 100));
    }

    [Fact]
    public void CreatesSafeObjectKey()
    {
        string key = _validator.CreateObjectKey("evidence/production", "Testing", "incident", "evidence-id", "photo.jpg");

        key.Should().StartWith("evidence/production/Testing/incident/evidence-id/").And.EndWith("-photo.jpg").And.NotContain("..").And.NotContain("\\");
    }

    [Fact]
    public async Task ComputesSha256AndResetsStream()
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("hello"));

        string hash = await EvidenceFileValidator.ComputeSha256Async(stream, CancellationToken.None);

        hash.Should().Be("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824");
        stream.Position.Should().Be(0);
    }
}
