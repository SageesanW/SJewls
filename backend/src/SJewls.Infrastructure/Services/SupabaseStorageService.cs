using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SJewls.Application.Interfaces;

namespace SJewls.Infrastructure.Services;

public class SupabaseStorageService : ISupabaseStorageService
{
    private readonly HttpClient _httpClient;
    private readonly string _supabaseUrl;
    private readonly string _serviceKey;
    private readonly string _bucketName;
    private readonly ILogger<SupabaseStorageService> _logger;

    private static readonly string[] AllowedExtensions = { ".png", ".jpg", ".jpeg", ".webp" };
    private static readonly string[] AllowedContentTypes = { "image/png", "image/jpeg", "image/webp" };
    public const long MaxFileSizeBytes = 2 * 1024 * 1024; // 2MB

    public SupabaseStorageService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<SupabaseStorageService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _supabaseUrl = (configuration["Supabase:Url"] ?? "https://thxeqfxmtcntqovfkiax.supabase.co").TrimEnd('/');
        _serviceKey = configuration["Supabase:ServiceKey"] ?? string.Empty;
        _bucketName = configuration["Supabase:BucketName"] ?? "sjewls-media";
    }

    public bool ValidateImageFile(string fileName, string contentType, long fileLength, out string? errorMessage)
    {
        if (fileLength <= 0)
        {
            errorMessage = "Uploaded file is empty.";
            return false;
        }

        if (fileLength > MaxFileSizeBytes)
        {
            errorMessage = $"Image size exceeds the maximum allowed limit of 2MB (actual: {Math.Round((double)fileLength / 1024 / 1024, 2)} MB).";
            return false;
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            errorMessage = $"Unsupported file format '{extension}'. Allowed formats are PNG, JPG, and WEBP.";
            return false;
        }

        var normalizedContentType = contentType.ToLowerInvariant();
        if (!AllowedContentTypes.Contains(normalizedContentType))
        {
            errorMessage = $"Unsupported image MIME type '{contentType}'. Allowed types are image/png, image/jpeg, and image/webp.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    public async Task<string> UploadImageAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        string folder = "jewellery-categories",
        CancellationToken cancellationToken = default)
    {
        if (!ValidateImageFile(fileName, contentType, fileStream.Length, out var validationError))
        {
            throw new ArgumentException(validationError, nameof(fileName));
        }

        // Sanitize file name
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var rawName = Path.GetFileNameWithoutExtension(fileName);
        var cleanName = Regex.Replace(rawName, @"[^a-zA-Z0-9_\-]", "_");
        if (cleanName.Length > 30) cleanName = cleanName.Substring(0, 30);

        var uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8);
        var objectPath = $"{folder}/{uniqueId}_{cleanName}{ext}";

        var uploadUrl = $"{_supabaseUrl}/storage/v1/object/{_bucketName}/{objectPath}";

        using var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
        request.Headers.Add("Authorization", $"Bearer {_serviceKey}");
        request.Headers.Add("apikey", _serviceKey);

        fileStream.Position = 0;
        using var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        request.Content = streamContent;

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Supabase Storage upload failed with status {StatusCode}: {ErrorBody}", response.StatusCode, errorBody);
            throw new InvalidOperationException($"Failed to upload image to Supabase Storage: {response.StatusCode} - {errorBody}");
        }

        var publicUrl = $"{_supabaseUrl}/storage/v1/object/public/{_bucketName}/{objectPath}";
        _logger.LogInformation("Successfully uploaded image to Supabase Storage. Public URL: {PublicUrl}", publicUrl);

        return publicUrl;
    }
}
