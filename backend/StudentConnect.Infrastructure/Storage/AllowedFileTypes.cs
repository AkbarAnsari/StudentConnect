namespace StudentConnect.Infrastructure.Storage;

// A small, fixed allow-list of file types for this learning project — not a general-purpose
// file-type detection library. Checks both the extension/content-type AND a "magic number"
// signature at the start of the file, since a client can freely lie about content-type/extension.
public static class AllowedFileTypes
{
    private static readonly Dictionary<string, (string ContentType, byte[] Signature)> Types = new()
    {
        [".pdf"] = ("application/pdf", [0x25, 0x50, 0x44, 0x46]), // %PDF
        [".png"] = ("image/png", [0x89, 0x50, 0x4E, 0x47]), // .PNG
        [".jpg"] = ("image/jpeg", [0xFF, 0xD8, 0xFF]),
        [".jpeg"] = ("image/jpeg", [0xFF, 0xD8, 0xFF]),
        [".docx"] = ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", [0x50, 0x4B, 0x03, 0x04]), // PK.. (zip)
    };

    public static bool IsExtensionAllowed(string extension) => Types.ContainsKey(extension.ToLowerInvariant());

    // Reads only the first few bytes needed to verify the signature, then rewinds the stream.
    public static async Task<bool> MatchesSignatureAsync(string extension, Stream content)
    {
        if (!Types.TryGetValue(extension.ToLowerInvariant(), out var type))
        {
            return false;
        }

        var buffer = new byte[type.Signature.Length];
        var read = await content.ReadAsync(buffer.AsMemory(0, buffer.Length));
        content.Position = 0;

        return read == type.Signature.Length && buffer.SequenceEqual(type.Signature);
    }
}
