using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TRAVEL.Services;

namespace TRAVEL.Controllers
{
    /// <summary>
    /// Image Upload API Controller
    /// </summary>
    [ApiController]
    [Route("api/images")]
    public class ImageUploadController : ControllerBase
    {
        private readonly IImageUploadService _imageService;
        private readonly ILogger<ImageUploadController> _logger;

        public ImageUploadController(IImageUploadService imageService, ILogger<ImageUploadController> logger)
        {
            _imageService = imageService;
            _logger = logger;
        }

        /// <summary>
        /// Upload a single image for packages
        /// </summary>
        [HttpPost("upload")]
        [Authorize(Roles = "Admin")]
        [RequestSizeLimit(10 * 1024 * 1024)] // 10MB limit
        public async Task<IActionResult> UploadImage(IFormFile file, [FromQuery] string folder = "packages")
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { success = false, message = "No file provided" });
            }

            var result = await _imageService.UploadImageAsync(file, folder);

            if (result.Success)
            {
                return Ok(new
                {
                    success = true,
                    message = result.Message,
                    data = new
                    {
                        url = result.Url,
                        fileName = result.FileName,
                        fileSize = result.FileSize
                    }
                });
            }

            return BadRequest(new { success = false, message = result.Message });
        }

        /// <summary>
        /// Upload multiple images for packages
        /// </summary>
        [HttpPost("upload-multiple")]
        [Authorize(Roles = "Admin")]
        [RequestSizeLimit(50 * 1024 * 1024)] // 50MB total limit
        public async Task<IActionResult> UploadMultipleImages(IEnumerable<IFormFile> files, [FromQuery] string folder = "packages")
        {
            if (files == null || !files.Any())
            {
                return BadRequest(new { success = false, message = "No files provided" });
            }

            var results = await _imageService.UploadImagesAsync(files, folder);

            var successResults = results.Where(r => r.Success).ToList();
            var failedResults = results.Where(r => !r.Success).ToList();

            if (successResults.Any())
            {
                return Ok(new
                {
                    success = true,
                    message = $"Uploaded {successResults.Count} of {results.Count} images",
                    data = new
                    {
                        uploaded = successResults.Select(r => new
                        {
                            url = r.Url,
                            fileName = r.FileName,
                            fileSize = r.FileSize
                        }),
                        failed = failedResults.Select(r => r.Message)
                    }
                });
            }

            return BadRequest(new
            {
                success = false,
                message = "All uploads failed",
                errors = failedResults.Select(r => r.Message)
            });
        }

        /// <summary>
        /// Delete an image
        /// </summary>
        [HttpDelete("delete")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteImage([FromQuery] string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl))
            {
                return BadRequest(new { success = false, message = "No image URL provided" });
            }

            var result = await _imageService.DeleteImageAsync(imageUrl);

            if (result)
            {
                return Ok(new { success = true, message = "Image deleted successfully" });
            }

            return BadRequest(new { success = false, message = "Failed to delete image" });
        }

        /// <summary>
        /// Upload profile picture for users
        /// </summary>
        [HttpPost("profile")]
        [Authorize]
        [RequestSizeLimit(5 * 1024 * 1024)] // 5MB limit for profile pictures
        public async Task<IActionResult> UploadProfilePicture(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { success = false, message = "No file provided" });
            }

            var result = await _imageService.UploadImageAsync(file, "profiles");

            if (result.Success)
            {
                return Ok(new
                {
                    success = true,
                    message = "Profile picture uploaded",
                    data = new
                    {
                        url = result.Url,
                        fileName = result.FileName
                    }
                });
            }

            return BadRequest(new { success = false, message = result.Message });
        }
    }
}