using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StockManager.Models.Exceptions;
using StockManager.Models.Options;
using StockManager.Service.Services;

namespace Tests.UnitTests;

public class FileServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "stockmanager-tests-" + Guid.NewGuid().ToString("N"));
    private readonly FileService _sut;

    public FileServiceTests()
    {
        Directory.CreateDirectory(_root);

        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(e => e.WebRootPath).Returns(_root);
        env.SetupGet(e => e.ContentRootPath).Returns(_root);

        var options = Options.Create(new FileStorageOptions
        {
            MaxFileSizeBytes = 1024,
            AllowedExtensions = [".png", ".jpg"],
            UploadsFolder = "uploads/products"
        });

        _sut = new FileService(env.Object, options, NullLogger<FileService>.Instance);
    }

    private static IFormFile Form(string fileName, string contentType, byte[] content)
    {
        var file = new Mock<IFormFile>();
        file.SetupGet(f => f.FileName).Returns(fileName);
        file.SetupGet(f => f.ContentType).Returns(contentType);
        file.SetupGet(f => f.Length).Returns(content.Length);
        file.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(content));
        return file.Object;
    }

    private static byte[] Png(int totalLength = 64)
    {
        var bytes = new byte[totalLength];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(bytes, 0);
        return bytes;
    }

    [Fact]
    public async Task ValidPng_IsStored_WithRandomName()
    {
        var url = await _sut.SaveImageAsync(Form("holiday photo.PNG", "image/png", Png()));

        Assert.StartsWith("/uploads/products/", url);
        Assert.EndsWith(".png", url);
        Assert.DoesNotContain("holiday", url);
        Assert.True(File.Exists(Path.Combine(_root, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public async Task TooLargeFile_IsRejected()
    {
        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            _sut.SaveImageAsync(Form("big.png", "image/png", Png(2048))));

        Assert.Contains("too large", ex.Message);
    }

    [Fact]
    public async Task DisallowedExtension_IsRejected()
    {
        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            _sut.SaveImageAsync(Form("malware.exe", "application/octet-stream", Png())));

        Assert.Contains(".exe", ex.Message);
    }

    [Fact]
    public async Task ContentTypeNotMatchingExtension_IsRejected()
    {
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _sut.SaveImageAsync(Form("a.png", "text/html", Png())));
    }

    [Fact]
    public async Task FakeImage_WithWrongMagicBytes_IsRejected()
    {
        var notReallyAPng = new byte[64]; // all zeros

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            _sut.SaveImageAsync(Form("fake.png", "image/png", notReallyAPng)));

        Assert.Contains("not a valid image", ex.Message);
    }

    [Fact]
    public async Task EmptyFile_IsRejected()
    {
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _sut.SaveImageAsync(Form("a.png", "image/png", [])));
    }

    [Fact]
    public async Task Delete_RemovesStoredFile_ButIgnoresPathTraversal()
    {
        var url = await _sut.SaveImageAsync(Form("a.png", "image/png", Png()));
        var outside = Path.Combine(Path.GetTempPath(), "outside-" + Guid.NewGuid().ToString("N") + ".txt");
        await File.WriteAllTextAsync(outside, "keep me");

        _sut.Delete(url);
        _sut.Delete("/../" + Path.GetFileName(outside));

        Assert.False(File.Exists(Path.Combine(_root, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(File.Exists(outside));
        File.Delete(outside);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
