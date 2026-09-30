namespace FoodSupply.Services;

public sealed class ProfilePhotoStore(IWebHostEnvironment environment)
{
    public const int MaxBytes = 2 * 1024 * 1024;
    private string DirectoryPath => Path.Combine(environment.ContentRootPath, "App_Data", "profile-photos");
    private string PhotoPath(int userId) => Path.Combine(DirectoryPath, $"{userId}.photo");

    public long? Version(int userId) => File.Exists(PhotoPath(userId))
        ? File.GetLastWriteTimeUtc(PhotoPath(userId)).Ticks : null;

    public static string? ContentType(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "image/png";
        if (bytes.StartsWith(new byte[] { 255, 216, 255 })) return "image/jpeg";
        return null;
    }

    public async Task SaveAsync(int userId, IFormFile photo)
    {
        if (photo.Length <= 0 || photo.Length > MaxBytes)
            throw new ArgumentException("Choose a JPG or PNG photo no larger than 2 MB.");
        using var input = photo.OpenReadStream();
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await input.ReadAsync(chunk)) > 0)
        {
            if (buffer.Length + read > MaxBytes)
                throw new ArgumentException("Choose a JPG or PNG photo no larger than 2 MB.");
            await buffer.WriteAsync(chunk.AsMemory(0, read));
        }
        var bytes = buffer.ToArray();
        if (ContentType(bytes) == null)
            throw new ArgumentException("Choose a JPG or PNG image file.");
        Directory.CreateDirectory(DirectoryPath);
        var temporary = Path.Combine(DirectoryPath, $"{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllBytesAsync(temporary, bytes);
            File.Move(temporary, PhotoPath(userId), overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public async Task<byte[]?> ReadAsync(int userId)
    {
        try { return await File.ReadAllBytesAsync(PhotoPath(userId)); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    public void Remove(int userId)
    {
        if (File.Exists(PhotoPath(userId))) File.Delete(PhotoPath(userId));
    }
}
