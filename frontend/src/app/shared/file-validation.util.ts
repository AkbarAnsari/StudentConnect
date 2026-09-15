// Shared client-side pre-flight check reused by every upload/import form — returns an error
// message to show the user, or null when the file passes.
export function validateFile(file: File | null, allowedExtensions: string[], maxSizeBytes: number): string | null {
  if (!file) {
    return 'Please select a file.';
  }

  const extension = file.name.slice(file.name.lastIndexOf('.')).toLowerCase();
  if (!allowedExtensions.includes(extension)) {
    return `This file type is not supported. Allowed: ${allowedExtensions.join(', ')}.`;
  }

  if (file.size > maxSizeBytes) {
    return `File size must be less than ${Math.round(maxSizeBytes / (1024 * 1024))} MB.`;
  }

  return null;
}
