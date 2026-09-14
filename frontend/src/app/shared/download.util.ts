// A blob fetched via HttpClient (so the auth interceptor attaches the Bearer token) rather
// than a plain anchor href, which would not carry the token at all.
export function triggerBlobDownload(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(url);
}
