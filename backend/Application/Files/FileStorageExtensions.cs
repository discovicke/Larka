using Backend.Application.Abstractions;

namespace Backend.Application.Files;

public static class FileStorageExtensions
{
    // Allt är redan sparat när filerna tas bort, så en fil som blir kvar i lagringen ska inte fälla anropet.
    public static async Task DeleteQuietlyAsync(this IFileStorage storage, string key, ILogger logger)
    {
        try
        {
            await storage.DeleteAsync(key, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not delete {StorageKey} from file storage", key);
        }
    }

    public static async Task DeleteQuietlyAsync(this IFileStorage storage, IEnumerable<string> keys, ILogger logger)
    {
        foreach (var key in keys)
        {
            await storage.DeleteQuietlyAsync(key, logger);
        }
    }
}
