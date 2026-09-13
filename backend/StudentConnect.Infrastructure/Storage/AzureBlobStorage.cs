using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using StudentConnect.Application;

namespace StudentConnect.Infrastructure.Storage;

// Same IFileStorage contract as LocalFileStorage — controllers never know (or care)
// which implementation is active. Authenticates via Azure AD (DefaultAzureCredential:
// developer's `az login` locally, managed identity once deployed) — this storage
// account has shared-key access disabled by organizational policy, so there is no
// account key or connection string anywhere in this class or its configuration.
public class AzureBlobStorage : IFileStorage
{
    private readonly BlobContainerClient _containerClient;

    public AzureBlobStorage(IOptions<AzureBlobStorageOptions> options)
    {
        var blobServiceClient = new BlobServiceClient(new Uri(options.Value.AccountUrl), new DefaultAzureCredential());
        _containerClient = blobServiceClient.GetBlobContainerClient(options.Value.ContainerName);
    }

    public async Task SaveFileAsync(string storedFileName, Stream content, CancellationToken cancellationToken = default)
    {
        var blobClient = _containerClient.GetBlobClient(storedFileName);
        await blobClient.UploadAsync(content, overwrite: true, cancellationToken);
    }

    public async Task<Stream?> GetFileAsync(string storedFileName, CancellationToken cancellationToken = default)
    {
        var blobClient = _containerClient.GetBlobClient(storedFileName);
        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            return null;
        }

        var download = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
        return download.Value.Content;
    }

    public async Task DeleteFileAsync(string storedFileName, CancellationToken cancellationToken = default)
    {
        var blobClient = _containerClient.GetBlobClient(storedFileName);
        await blobClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);
    }

    public async Task<bool> FileExistsAsync(string storedFileName, CancellationToken cancellationToken = default)
    {
        var blobClient = _containerClient.GetBlobClient(storedFileName);
        return await blobClient.ExistsAsync(cancellationToken);
    }
}
