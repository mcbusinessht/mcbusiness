using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;

namespace MCBusiness.Services
{
    public interface IImageOptimizationService
    {
        Task<string> OptimizeImageAsync(IFormFile file, string folder = "optimized", int maxWidth = 1200, int quality = 80);
        Task<string> CreateThumbnailAsync(string imagePath, int size = 300);
        Task OptimizeExistingImagesAsync(string folder);
    }

    public class ImageOptimizationService : IImageOptimizationService
    {
        private readonly ILogger<ImageOptimizationService> _logger;
        private readonly string _uploadsPath;

        public ImageOptimizationService(ILogger<ImageOptimizationService> logger, IWebHostEnvironment environment)
        {
            _logger = logger;
            _uploadsPath = Path.Combine(environment.WebRootPath, "uploads");
            Directory.CreateDirectory(_uploadsPath);
        }

        public async Task<string> OptimizeImageAsync(IFormFile file, string folder = "optimized", int maxWidth = 1200, int quality = 80)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return null;

                // Créer le dossier de destination
                var folderPath = Path.Combine(_uploadsPath, folder);
                Directory.CreateDirectory(folderPath);

                using var image = await Image.LoadAsync(file.OpenReadStream());
                
                // Redimensionner si l'image est trop grande
                if (image.Width > maxWidth)
                {
                    var ratio = (double)maxWidth / image.Width;
                    var newHeight = (int)(image.Height * ratio);
                    image.Mutate(x => x.Resize(maxWidth, newHeight));
                }

                // Générer un nom de fichier unique
                var fileName = $"{Guid.NewGuid()}.webp";
                var filePath = Path.Combine(folderPath, fileName);

                // Sauvegarder en WebP avec compression optimisée
                await image.SaveAsWebpAsync(filePath, new WebpEncoder 
                { 
                    Quality = quality,
                    Method = WebpEncodingMethod.BestQuality
                });

                _logger.LogInformation($"Image optimisée: {fileName} - Taille originale: {file.Length} bytes");

                return $"/uploads/{folder}/{fileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'optimisation de l'image");
                return null;
            }
        }

        public async Task<string> CreateThumbnailAsync(string imagePath, int size = 300)
        {
            try
            {
                var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", imagePath.TrimStart('/'));
                if (!File.Exists(fullPath))
                    return null;

                using var image = await Image.LoadAsync(fullPath);
                
                // Créer une miniature carrée
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(size, size),
                    Mode = ResizeMode.Crop,
                    Position = AnchorPositionMode.Center
                }));

                var fileName = $"thumb_{size}_{Path.GetFileNameWithoutExtension(imagePath)}.webp";
                var thumbFolder = Path.Combine(_uploadsPath, "thumbnails");
                Directory.CreateDirectory(thumbFolder);
                
                var thumbPath = Path.Combine(thumbFolder, fileName);
                await image.SaveAsWebpAsync(thumbPath, new WebpEncoder { Quality = 75 });

                return $"/uploads/thumbnails/{fileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur lors de la création de la miniature pour {imagePath}");
                return imagePath; // Retourner l'image originale en cas d'erreur
            }
        }

        public async Task OptimizeExistingImagesAsync(string folder)
        {
            try
            {
                var folderPath = Path.Combine(_uploadsPath, folder);
                if (!Directory.Exists(folderPath))
                    return;

                var imageFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
                    .Where(f => IsImageFile(f))
                    .ToList();

                var optimizedFolder = Path.Combine(_uploadsPath, "optimized");
                Directory.CreateDirectory(optimizedFolder);

                foreach (var imageFile in imageFiles)
                {
                    try
                    {
                        using var image = await Image.LoadAsync(imageFile);
                        
                        var fileName = $"{Path.GetFileNameWithoutExtension(imageFile)}_opt.webp";
                        var optimizedPath = Path.Combine(optimizedFolder, fileName);

                        // Redimensionner si nécessaire
                        if (image.Width > 1200)
                        {
                            var ratio = 1200.0 / image.Width;
                            var newHeight = (int)(image.Height * ratio);
                            image.Mutate(x => x.Resize(1200, newHeight));
                        }

                        await image.SaveAsWebpAsync(optimizedPath, new WebpEncoder { Quality = 80 });
                        
                        _logger.LogInformation($"Image optimisée: {fileName}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Impossible d'optimiser l'image: {imageFile}");
                    }
                }

                _logger.LogInformation($"Optimisation terminée. {imageFiles.Count} images traitées.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur lors de l'optimisation du dossier {folder}");
            }
        }

        private static bool IsImageFile(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower();
            return extension == ".jpg" || extension == ".jpeg" || 
                   extension == ".png" || extension == ".gif" || 
                   extension == ".bmp" || extension == ".tiff";
        }
    }
}
