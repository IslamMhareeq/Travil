using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace TRAVEL.Services
{
    public interface IImageUploadService
    {
        Task<ImageUploadResult> UploadImageAsync(IFormFile file, string folder = "packages");
        Task<List<ImageUploadResult>> UploadImagesAsync(IEnumerable<IFormFile> files, string folder = "packages");
        Task<bool> DeleteImageAsync(string imagePath);
        string GetImageUrl(string fileName, string folder = "packages");
        bool IsValidImage(IFormFile file);
    }

    public class ImageUploadService : IImageUploadService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ImageUploadService> _logger;

        private readonly string[] _allowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        private readonly string[] _allowedMimeTypes = { "image/jpeg", "image/png", "image/gif", "image/webp" };
        private const long MaxFileSize = 10 * 1024 * 1024; // 10MB

        public ImageUploadService(IWebHostEnvironment environment, ILogger<ImageUploadService> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        public async Task<ImageUploadResult> UploadImageAsync(IFormFile file, string folder = "packages")
        {
            var result = new ImageUploadResult();

            try
            {
                // Validate file
                if (file == null || file.Length == 0)
                {
                    result.Success = false;
                    result.Message = "No file provided";
                    return result;
                }

                if (!IsValidImage(file))
                {
                    result.Success = false;
                    result.Message = "Invalid file type. Only JPG, PNG, GIF, and WebP images are allowed.";
                    return result;
                }

                if (file.Length > MaxFileSize)
                {
                    result.Success = false;
                    result.Message = "File size exceeds 10MB limit";
                    return result;
                }

                // Create upload directory
                var uploadPath = Path.Combine(_environment.WebRootPath, "uploads", folder);
                if (!Directory.Exists(uploadPath))
                {
                    Directory.CreateDirectory(uploadPath);
                }

                // Generate unique filename
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                var fileName = $"{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadPath, fileName);

                // Save file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                result.Success = true;
                result.FileName = fileName;
                result.FilePath = filePath;
                result.Url = $"/uploads/{folder}/{fileName}";
                result.Message = "Image uploaded successfully";
                result.FileSize = file.Length;

                _logger.LogInformation($"Image uploaded successfully: {fileName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading image");
                result.Success = false;
                result.Message = "Error uploading image";
            }

            return result;
        }

        public async Task<List<ImageUploadResult>> UploadImagesAsync(IEnumerable<IFormFile> files, string folder = "packages")
        {
            var results = new List<ImageUploadResult>();

            if (files == null)
                return results;

            foreach (var file in files)
            {
                var result = await UploadImageAsync(file, folder);
                results.Add(result);
            }

            return results;
        }

        public async Task<bool> DeleteImageAsync(string imagePath)
        {
            try
            {
                if (string.IsNullOrEmpty(imagePath))
                    return false;

                // Convert URL to file path
                var relativePath = imagePath.TrimStart('/');
                var fullPath = Path.Combine(_environment.WebRootPath, relativePath);

                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    _logger.LogInformation($"Image deleted: {imagePath}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting image: {imagePath}");
                return false;
            }
        }

        public string GetImageUrl(string fileName, string folder = "packages")
        {
            if (string.IsNullOrEmpty(fileName))
                return null;

            return $"/uploads/{folder}/{fileName}";
        }

        public bool IsValidImage(IFormFile file)
        {
            if (file == null)
                return false;

            // Check extension
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!_allowedExtensions.Contains(extension))
                return false;

            // Check MIME type
            if (!_allowedMimeTypes.Contains(file.ContentType.ToLowerInvariant()))
                return false;

            // Verify file signature (magic bytes)
            using (var reader = new BinaryReader(file.OpenReadStream()))
            {
                var headerBytes = reader.ReadBytes(8);
                file.OpenReadStream().Position = 0; // Reset stream position

                // JPEG: FF D8 FF
                if (headerBytes.Length >= 3 && headerBytes[0] == 0xFF && headerBytes[1] == 0xD8 && headerBytes[2] == 0xFF)
                    return true;

                // PNG: 89 50 4E 47 0D 0A 1A 0A
                if (headerBytes.Length >= 8 && headerBytes[0] == 0x89 && headerBytes[1] == 0x50 && headerBytes[2] == 0x4E && headerBytes[3] == 0x47)
                    return true;

                // GIF: 47 49 46 38
                if (headerBytes.Length >= 4 && headerBytes[0] == 0x47 && headerBytes[1] == 0x49 && headerBytes[2] == 0x46 && headerBytes[3] == 0x38)
                    return true;

                // WebP: 52 49 46 46 ... 57 45 42 50
                if (headerBytes.Length >= 4 && headerBytes[0] == 0x52 && headerBytes[1] == 0x49 && headerBytes[2] == 0x46 && headerBytes[3] == 0x46)
                    return true;
            }

            return false;
        }
    }

    public class ImageUploadResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string Url { get; set; }
        public long FileSize { get; set; }
    }
}