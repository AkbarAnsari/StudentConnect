namespace StudentConnect.Infrastructure.Storage;

public class FileStorageOptions
{
    // Relative to the API's content root — never a client-supplied or machine-specific absolute path.
    public string RootPath { get; set; } = "storage/learning-materials";
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;
}
