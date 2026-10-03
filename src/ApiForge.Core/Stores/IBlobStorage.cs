namespace ApiForge.Core;

public sealed record BlobMetadata(
    string StorageKey,
    string Url,
    long SizeBytes
);

public interface IBlobStorage
{
    string ProviderName { get; }

    Task<BlobMetadata> UploadAsync(string fileName, Stream content, string? contentType, CancellationToken ct = default);

    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct = default);

    Task<bool> DeleteAsync(string storageKey, CancellationToken ct = default);

    Task<bool> ExistsAsync(string storageKey, CancellationToken ct = default);
}
