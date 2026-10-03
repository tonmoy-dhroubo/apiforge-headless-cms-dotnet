using ApiForge.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace ApiForge.Infrastructure;

public sealed class LocalDiskBlobStorage : IBlobStorage
{
    private readonly string _uploadDirectory;

    public string ProviderName => "local";

    public LocalDiskBlobStorage(IWebHostEnvironment environment, IConfiguration configuration)
    {
        var configuredPath = configuration["Storage:MediaUploadPath"];
        _uploadDirectory = !string.IsNullOrWhiteSpace(configuredPath)
            ? configuredPath
            : Path.Combine(environment.ContentRootPath, "uploads");

        Directory.CreateDirectory(_uploadDirectory);
    }

    public async Task<BlobMetadata> UploadAsync(string fileName, Stream content, string? contentType, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_uploadDirectory);

        var extension = Path.GetExtension(fileName);
        var hash = Guid.NewGuid().ToString();
        var storageKey = hash + extension;
        var physicalPath = Path.Combine(_uploadDirectory, storageKey);

        await using (var outputStream = File.Create(physicalPath))
        {
            await content.CopyToAsync(outputStream, ct);
        }

        var fileInfo = new FileInfo(physicalPath);
        var url = "/api/upload/files/" + storageKey;

        return new BlobMetadata(storageKey, url, fileInfo.Length);
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        var physicalPath = Path.Combine(_uploadDirectory, Path.GetFileName(storageKey));
        if (!File.Exists(physicalPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(physicalPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        return Task.FromResult<Stream?>(stream);
    }

    public Task<bool> DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var physicalPath = Path.Combine(_uploadDirectory, Path.GetFileName(storageKey));
        if (!File.Exists(physicalPath))
        {
            return Task.FromResult(false);
        }

        try
        {
            File.Delete(physicalPath);
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task<bool> ExistsAsync(string storageKey, CancellationToken ct = default)
    {
        var physicalPath = Path.Combine(_uploadDirectory, Path.GetFileName(storageKey));
        return Task.FromResult(File.Exists(physicalPath));
    }
}
