using Microsoft.Extensions.Options;
using StudentConnect.Application;

namespace StudentConnect.Infrastructure.Storage;

// Today's implementation of IFileStorage — stores files on the local disk, outside
// both the database and the application's source/publish directory. A future
// AzureBlobStorage class could implement the same interface with zero changes
// required anywhere else in the application.
public class LocalFileStorage : IFileStorage
{
    private readonly string _rootPath;

    public LocalFileStorage(IOptions<FileStorageOptions> options, IHostEnvironment env)
    {
        _rootPath = Path.Combine(env.ContentRootPath, options.Value.RootPath);
        Directory.CreateDirectory(_rootPath);
    }

    public async Task SaveFileAsync(string storedFileName, Stream content, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(storedFileName);
        await using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);
        await content.CopyToAsync(fileStream, cancellationToken);
    }

    public Task<Stream?> GetFileAsync(string storedFileName, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(storedFileName);
        if (!File.Exists(path))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteFileAsync(string storedFileName, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(storedFileName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public Task<bool> FileExistsAsync(string storedFileName, CancellationToken cancellationToken = default) =>
        Task.FromResult(File.Exists(ResolvePath(storedFileName)));

    // storedFileName is always a server-generated GUID-based name (see LearningMaterialsController),
    // never a client-supplied value, so path traversal via "../" is not possible here.
    private string ResolvePath(string storedFileName) => Path.Combine(_rootPath, storedFileName);
}
