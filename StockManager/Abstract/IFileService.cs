namespace StockManager.Abstract;

public interface IFileService
{
    /// <summary>Validates (size, extension, content type, magic bytes) and stores an image. Returns its relative URL.</summary>
    Task<string> SaveImageAsync(IFormFile file, CancellationToken ct = default);

    /// <summary>Deletes a previously stored file. Silently ignores unknown / unsafe paths.</summary>
    void Delete(string? relativeUrl);
}
