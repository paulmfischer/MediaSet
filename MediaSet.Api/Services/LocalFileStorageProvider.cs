namespace MediaSet.Api.Services;

/// <summary>
/// Implementation of IImageStorageProvider for local filesystem storage.
/// Handles directory creation, file I/O, and security against path traversal attacks.
/// </summary>
public class LocalFileStorageProvider : IImageStorageProvider
{
    private readonly string _rootPath;
    private readonly ILogger<LocalFileStorageProvider> _logger;

    /// <summary>
    /// Initialize the LocalFileStorageProvider
    /// </summary>
    /// <param name="rootPath">The root directory where images will be stored</param>
    /// <param name="logger">Logger for diagnostic information</param>
    /// <exception cref="ArgumentException">Thrown if rootPath is null, empty, or invalid</exception>
    public LocalFileStorageProvider(string rootPath, ILogger<LocalFileStorageProvider> logger)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("Root path cannot be null or empty", nameof(rootPath));
        }

        // Ensure root path is absolute
        if (!Path.IsPathRooted(rootPath))
        {
            throw new ArgumentException("Root path must be absolute", nameof(rootPath));
        }

        _rootPath = Path.GetFullPath(rootPath);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogInformation("LocalFileStorageProvider initialized with root path: {RootPath}", _rootPath);
    }

    /// <summary>
    /// Save image data to the local filesystem
    /// </summary>
    /// <param name="imageData">The binary image data to save</param>
    /// <param name="relativePath">The relative path within the storage root</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <exception cref="ArgumentException">Thrown if relativePath attempts path traversal</exception>
    /// <exception cref="IOException">Thrown if file I/O operation fails</exception>
    public async Task SaveImageAsync(byte[] imageData, string relativePath, CancellationToken cancellationToken)
    {
        if (imageData == null || imageData.Length == 0)
        {
            throw new ArgumentException("Image data cannot be null or empty", nameof(imageData));
        }

        if (!ValidatePath(relativePath))
        {
            _logger.LogWarning("Attempted to save image with invalid path: {RelativePath}", relativePath);
            throw new ArgumentException("Invalid relative path", nameof(relativePath));
        }

        try
        {
            var fullPath = Path.Combine(_rootPath, relativePath);

            // Validate that the resolved path is still within the root directory
            if (!IsPathWithinRoot(fullPath))
            {
                _logger.LogWarning("Path traversal attempt detected: {FullPath}", fullPath);
                throw new ArgumentException("Path traversal not allowed", nameof(relativePath));
            }

            // Ensure directory exists
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                _logger.LogDebug("Created directory: {Directory}", directory);
            }

            // Save file
            await File.WriteAllBytesAsync(fullPath, imageData, cancellationToken);
            _logger.LogDebug("Image saved successfully: {FullPath} ({SizeBytes} bytes)", fullPath, imageData.Length);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Image save operation was cancelled for path: {RelativePath}", relativePath);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving image to path: {RelativePath}", relativePath);
            throw;
        }
    }

    /// <summary>
    /// Retrieve image data from the local filesystem as a stream
    /// </summary>
    /// <param name="relativePath">The relative path within the storage root</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stream containing the image data, or null if file does not exist</returns>
    /// <exception cref="ArgumentException">Thrown if relativePath attempts path traversal</exception>
    public async Task<Stream?> GetImageAsync(string relativePath, CancellationToken cancellationToken)
    {
        if (!ValidatePath(relativePath))
        {
            _logger.LogWarning("Attempted to get image with invalid path: {RelativePath}", relativePath);
            throw new ArgumentException("Invalid relative path", nameof(relativePath));
        }

        try
        {
            var fullPath = Path.Combine(_rootPath, relativePath);

            // Validate that the resolved path is still within the root directory
            if (!IsPathWithinRoot(fullPath))
            {
                _logger.LogWarning("Path traversal attempt detected: {FullPath}", fullPath);
                throw new ArgumentException("Path traversal not allowed", nameof(relativePath));
            }

            if (!File.Exists(fullPath))
            {
                _logger.LogDebug("Image file not found: {FullPath}", fullPath);
                return null;
            }

            // Read file asynchronously into memory
            var imageData = await File.ReadAllBytesAsync(fullPath, cancellationToken);
            _logger.LogDebug("Image read successfully: {FullPath} ({SizeBytes} bytes)", fullPath, imageData.Length);
            
            // Return as MemoryStream for compatibility with stream APIs
            return new MemoryStream(imageData, writable: false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Image get operation was cancelled for path: {RelativePath}", relativePath);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving image from path: {RelativePath}", relativePath);
            throw;
        }
    }

    /// <summary>
    /// Delete image from the local filesystem
    /// </summary>
    /// <param name="relativePath">The relative path within the storage root</param>
    /// <exception cref="ArgumentException">Thrown if relativePath attempts path traversal</exception>
    public void DeleteImage(string relativePath)
    {
        if (!ValidatePath(relativePath))
        {
            _logger.LogWarning("Attempted to delete image with invalid path: {RelativePath}", relativePath);
            throw new ArgumentException("Invalid relative path", nameof(relativePath));
        }

        try
        {
            var fullPath = Path.Combine(_rootPath, relativePath);

            // Validate that the resolved path is still within the root directory
            if (!IsPathWithinRoot(fullPath))
            {
                _logger.LogWarning("Path traversal attempt detected: {FullPath}", fullPath);
                throw new ArgumentException("Path traversal not allowed", nameof(relativePath));
            }

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                _logger.LogDebug("Image deleted successfully: {FullPath}", fullPath);

                // Try to clean up empty directories
                CleanupEmptyDirectories(Path.GetDirectoryName(fullPath));
            }
            else
            {
                _logger.LogDebug("Image file not found for deletion: {FullPath}", fullPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting image from path: {RelativePath}", relativePath);
            throw;
        }
    }

    /// <summary>
    /// Check if image exists in the local filesystem
    /// </summary>
    /// <param name="relativePath">The relative path within the storage root</param>
    /// <returns>True if file exists, false otherwise</returns>
    /// <exception cref="ArgumentException">Thrown if relativePath attempts path traversal</exception>
    public bool Exists(string relativePath)
    {
        if (!ValidatePath(relativePath))
        {
            _logger.LogWarning("Attempted to check existence with invalid path: {RelativePath}", relativePath);
            throw new ArgumentException("Invalid relative path", nameof(relativePath));
        }

        try
        {
            var fullPath = Path.Combine(_rootPath, relativePath);

            // Validate that the resolved path is still within the root directory
            if (!IsPathWithinRoot(fullPath))
            {
                _logger.LogWarning("Path traversal attempt detected: {FullPath}", fullPath);
                throw new ArgumentException("Path traversal not allowed", nameof(relativePath));
            }

            var exists = File.Exists(fullPath);
            _logger.LogDebug("Image existence check for {FullPath}: {Exists}", fullPath, exists);
            return exists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking image existence for path: {RelativePath}", relativePath);
            throw;
        }
    }

    /// <summary>
    /// Validate that a relative path is safe (no parent directory references)
    /// </summary>
    /// <param name="relativePath">The path to validate</param>
    /// <returns>True if path is valid, false otherwise</returns>
    private static bool ValidatePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        // Reject paths with parent directory references
        if (relativePath.Contains("..") || relativePath.StartsWith("/") || relativePath.StartsWith("\\"))
        {
            return false;
        }

        // Reject absolute paths
        if (Path.IsPathRooted(relativePath))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Check if a full path is within the storage root directory
    /// </summary>
    /// <param name="fullPath">The full path to check</param>
    /// <returns>True if path is within root, false otherwise</returns>
    private bool IsPathWithinRoot(string fullPath)
    {
        // Normalize both paths for comparison
        var normalizedFullPath = Path.GetFullPath(fullPath);
        var normalizedRootPath = Path.GetFullPath(_rootPath);

        // Ensure trailing separator for consistent comparison
        if (!normalizedRootPath.EndsWith(Path.DirectorySeparatorChar))
        {
            normalizedRootPath += Path.DirectorySeparatorChar;
        }

        return normalizedFullPath.StartsWith(normalizedRootPath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Recursively clean up empty directories up to the root path
    /// </summary>
    /// <param name="directory">The directory to start cleanup from</param>
    private void CleanupEmptyDirectories(string? directory)
    {
        if (string.IsNullOrEmpty(directory) || !IsPathWithinRoot(directory))
        {
            return;
        }

        try
        {
            // Don't delete the root path itself
            if (directory.Equals(_rootPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (Directory.Exists(directory) && IsDirectoryEmpty(directory))
            {
                Directory.Delete(directory);
                _logger.LogDebug("Cleaned up empty directory: {Directory}", directory);

                // Recursively clean parent directories
                var parentDirectory = Path.GetDirectoryName(directory);
                CleanupEmptyDirectories(parentDirectory);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error cleaning up empty directory: {Directory}", directory);
            // Don't throw - cleanup is non-critical
        }
    }

    /// <summary>
    /// Check if a directory is empty
    /// </summary>
    /// <param name="directory">The directory to check</param>
    /// <returns>True if directory is empty, false otherwise</returns>
    private static bool IsDirectoryEmpty(string directory)
    {
        try
        {
            return !Directory.EnumerateFileSystemEntries(directory).Any();
        }
        catch (Exception)
        {
            // Swallow all exceptions: directory may not exist, access denied, etc.
            // This is non-critical; return false if unable to determine.
            return false;
        }
    }
}
