namespace SJewls.Application.Interfaces;

public interface ISupabaseStorageService
{
    Task<string> UploadImageAsync(Stream fileStream, string fileName, string contentType, string folder = "jewellery-categories", CancellationToken cancellationToken = default);
    bool ValidateImageFile(string fileName, string contentType, long fileLength, out string? errorMessage);
}
