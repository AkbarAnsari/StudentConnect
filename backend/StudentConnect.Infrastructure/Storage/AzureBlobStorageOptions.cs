namespace StudentConnect.Infrastructure.Storage;

// AccountUrl/ContainerName are not secrets — no key or connection string here.
// Authentication uses Azure AD (DefaultAzureCredential), never a shared account key.
public class AzureBlobStorageOptions
{
    public string AccountUrl { get; set; } = string.Empty;
    public string ContainerName { get; set; } = "learning-materials";
}
