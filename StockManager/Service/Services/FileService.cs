using Microsoft.Extensions.Options;
using StockManager.Abstract;
using StockManager.Models.Exceptions;
using StockManager.Models.Options;

namespace StockManager.Service.Services;

public class FileService(
    IWebHostEnvironment env,
    IOptions<FileStorageOptions> options,
    ILogger<FileService> logger) : IFileService
{
    private static readonly string[] DefaultExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    private static readonly Dictionary<string, string[]> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = ["image/jpeg"],
        [".jpeg"] = ["image/jpeg"],
        [".png"] = ["image/png"],
        [".webp"] = ["image/webp"]
    };

    private string WebRoot => env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");

    public async Task<string> SaveImageAsync(IFormFile file, CancellationToken ct = default)
    {
        var opt = options.Value;

        if (file is null || file.Length == 0)
            throw new BadRequestException("No file was uploaded or the file is empty.");

        if (file.Length > opt.MaxFileSizeBytes)
            throw new BadRequestException(
                $"The file is too large ({file.Length / 1024.0 / 1024.0:0.##} MB). The maximum allowed size is {opt.MaxFileSizeBytes / 1024.0 / 1024.0:0.##} MB.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowed = opt.AllowedExtensions.Length > 0 ? opt.AllowedExtensions : DefaultExtensions;

        if (string.IsNullOrEmpty(extension) || !allowed.Contains(extension, StringComparer.OrdinalIgnoreCase)
                                            || !ContentTypes.TryGetValue(extension, out var contentTypes))
            throw new BadRequestException(
                $"The file type '{extension}' is not allowed. Allowed types: {string.Join(", ", allowed)}.");

        if (!contentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            throw new BadRequestException(
                $"The content type '{file.ContentType}' does not match the file extension '{extension}'.");

        await using var input = file.OpenReadStream();

        if (!await HasValidSignatureAsync(input, extension, ct))
            throw new BadRequestException("The file content is not a valid image of the declared type.");

        var folderRelative = opt.UploadsFolder.Trim('/', '\\').Replace('\\', '/');
        var folderAbsolute = Path.Combine(WebRoot, folderRelative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(folderAbsolute);
        
        var fileName = $"{Guid.NewGuid():N}{extension}";
        await using (var target = File.Create(Path.Combine(folderAbsolute, fileName)))
        {
            await input.CopyToAsync(target, ct);
        }

        return $"/{folderRelative}/{fileName}";
    }

    public void Delete(string? relativeUrl)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl))
            return;

        try
        {
            var root = Path.GetFullPath(WebRoot);
            var full = Path.GetFullPath(Path.Combine(root, relativeUrl.TrimStart('/', '\\')));
            
            if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                return;

            if (File.Exists(full))
                File.Delete(full);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not delete file {Path}", relativeUrl);
        }
    }

    private static async Task<bool> HasValidSignatureAsync(Stream stream, string extension, CancellationToken ct)
    {
        var header = new byte[12];
        var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken: ct);
        stream.Seek(0, SeekOrigin.Begin);

        if (read < header.Length)
            return false;

        return extension switch
        {
            ".jpg" or ".jpeg" => header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".png" => header.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            ".webp" => header.AsSpan(0, 4).SequenceEqual("RIFF"u8) && header.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };
    }
}
