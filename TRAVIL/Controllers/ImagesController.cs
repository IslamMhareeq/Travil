using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TRAVEL.Services;

namespace TRAVEL.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ImagesController : ControllerBase
    {
        private readonly IImageStorageService _imageService;
        private readonly ILogger<ImagesController> _logger;

        private readonly string[] _allowedTypes = { "image/jpeg", "image/png", "image/gif", "image/webp" };
        private readonly string[] _allowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        private const long MaxFileSize = 10 * 1024 * 1024; // 10MB

        public ImagesController(
            IImageStorageService imageService,
            ILogger<ImagesController> logger)
        {
            _imageService = imageService;
            _logger = logger;
        }

        /// <summary>
        /// Upload a single image to MongoDB GridFS
        /// POST /api/images/upload
        /// </summary>
        [HttpPost("upload")]
        [Authorize]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest(new { success = false, message = "No file provided" });

                if (file.Length > MaxFileSize)
                    return BadRequest(new { success = false, message = "File size exceeds 10MB limit" });

                if (!Array.Exists(_allowedTypes, t => t.Equals(file.ContentType, StringComparison.OrdinalIgnoreCase)))
                    return BadRequest(new { success = false, message = "Invalid file type. Allowed: JPG, PNG, GIF, WEBP" });

                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!Array.Exists(_allowedExtensions, e => e.Equals(extension)))
                    return BadRequest(new { success = false, message = "Invalid file extension" });

                using var stream = file.OpenReadStream();
                var imageId = await _imageService.UploadImageAsync(stream, file.FileName, file.ContentType);
                var imageUrl = _imageService.GetImageUrl(imageId);

                _logger.LogInformation($"Image uploaded to MongoDB: {imageId}");

                return Ok(new
                {
                    success = true,
                    message = "Image uploaded successfully",
                    data = new
                    {
                        imageId = imageId,
                        imageUrl = imageUrl,  // This URL goes into PostgreSQL
                        fileName = file.FileName,
                        contentType = file.ContentType,
                        size = file.Length
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading image");
                return StatusCode(500, new { success = false, message = "Error uploading image" });
            }
        }

        /// <summary>
        /// Upload multiple images to MongoDB GridFS
        /// POST /api/images/upload-multiple
        /// </summary>
        [HttpPost("upload-multiple")]
        [Authorize]
        [RequestSizeLimit(50 * 1024 * 1024)]
        public async Task<IActionResult> UploadMultipleImages(List<IFormFile> files)
        {
            try
            {
                if (files == null || files.Count == 0)
                    return BadRequest(new { success = false, message = "No files provided" });

                if (files.Count > 10)
                    return BadRequest(new { success = false, message = "Maximum 10 files allowed" });

                var uploadedImages = new List<object>();
                var errors = new List<string>();

                foreach (var file in files)
                {
                    if (file.Length > MaxFileSize)
                    {
                        errors.Add($"{file.FileName}: Exceeds 10MB limit");
                        continue;
                    }

                    if (!Array.Exists(_allowedTypes, t => t.Equals(file.ContentType, StringComparison.OrdinalIgnoreCase)))
                    {
                        errors.Add($"{file.FileName}: Invalid file type");
                        continue;
                    }

                    try
                    {
                        using var stream = file.OpenReadStream();
                        var imageId = await _imageService.UploadImageAsync(stream, file.FileName, file.ContentType);
                        var imageUrl = _imageService.GetImageUrl(imageId);

                        uploadedImages.Add(new
                        {
                            imageId = imageId,
                            imageUrl = imageUrl,
                            fileName = file.FileName,
                            size = file.Length
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error uploading: {file.FileName}");
                        errors.Add($"{file.FileName}: Upload failed");
                    }
                }

                return Ok(new
                {
                    success = true,
                    message = $"Uploaded {uploadedImages.Count} of {files.Count} files",
                    data = new { uploaded = uploadedImages, errors = errors }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading multiple images");
                return StatusCode(500, new { success = false, message = "Error uploading images" });
            }
        }

        /// <summary>
        /// Serve an image from MongoDB GridFS
        /// GET /api/images/{imageId}
        /// </summary>
        [HttpGet("{imageId}")]
        [AllowAnonymous]
        [ResponseCache(Duration = 86400)] // Cache 24 hours
        public async Task<IActionResult> GetImage(string imageId)
        {
            try
            {
                var result = await _imageService.GetImageAsync(imageId);

                if (result == null)
                    return NotFound(new { success = false, message = "Image not found" });

                var (stream, contentType, fileName) = result.Value;

                Response.Headers["Cache-Control"] = "public, max-age=86400";
                return File(stream, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving image: {imageId}");
                return StatusCode(500, new { success = false, message = "Error retrieving image" });
            }
        }

        /// <summary>
        /// Delete an image from MongoDB GridFS
        /// DELETE /api/images/{imageId}
        /// </summary>
        [HttpDelete("{imageId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteImage(string imageId)
        {
            try
            {
                var deleted = await _imageService.DeleteImageAsync(imageId);

                if (!deleted)
                    return NotFound(new { success = false, message = "Image not found" });

                return Ok(new { success = true, message = "Image deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting image: {imageId}");
                return StatusCode(500, new { success = false, message = "Error deleting image" });
            }
        }
    }
}