namespace StudentConnect.Application;

// Small, deliberately narrow abstraction so file storage can later be swapped for
// Azure Blob Storage without changing any controller or business logic — only this
// interface's implementation would need to change.
public interface IFileStorage
{
    Task SaveFileAsync(string storedFileName, Stream content, CancellationToken cancellationToken = default);
    Task<Stream?> GetFileAsync(string storedFileName, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(string storedFileName, CancellationToken cancellationToken = default);
    Task<bool> FileExistsAsync(string storedFileName, CancellationToken cancellationToken = default);
}
