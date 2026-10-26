namespace RROMS.Web.Services.Restaurant;

/// <summary>Saves uploaded pictures (menu items, promotion banners, the logo) under wwwroot/images.</summary>
public static class ImageStorage
{
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

    private const long MaxBytes = 2 * 1024 * 1024;

    /// <summary>Returns what is wrong with the file, or null when it can be saved.</summary>
    public static string? Validate(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!AllowedExtensions.Contains(extension))
            return "Choose a JPG, PNG, WEBP or GIF picture.";

        if (file.Length > MaxBytes)
            return "That picture is larger than 2 MB. Choose a smaller one.";

        return null;
    }

    /// <summary>Saves the file and returns its web path, for example /images/menu/abc.jpg.</summary>
    public static async Task<string> SaveAsync(IFormFile file, string webRootPath, string folder)
    {
        var directory = Path.Combine(webRootPath, "images", folder);
        Directory.CreateDirectory(directory);

        var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName).ToLowerInvariant();

        await using var stream = new FileStream(Path.Combine(directory, fileName), FileMode.Create);
        await file.CopyToAsync(stream);

        return $"/images/{folder}/{fileName}";
    }

    public static void Delete(string? webPath, string webRootPath)
    {
        if (string.IsNullOrEmpty(webPath)) return;

        var fullPath = Path.Combine(webRootPath, webPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

        if (File.Exists(fullPath))
            File.Delete(fullPath);
    }
}
