using System;
using System.IO;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using Microsoft.Extensions.Logging;

namespace TRAVEL.Services
{
    public interface IImageStorageService
    {
        Task<string> UploadImageAsync(Stream imageStream, string fileName, string contentType);
        Task<(Stream stream, string contentType, string fileName)?> GetImageAsync(string imageId);
        Task<bool> DeleteImageAsync(string imageId);
        string GetImageUrl(string imageId);
    }

    public class ImageStorageService : IImageStorageService
    {
        private readonly IMongoDatabase _database;
        private readonly GridFSBucket _gridFS;
        private readonly ILogger<ImageStorageService> _logger;

        public ImageStorageService(IMongoDatabase database, ILogger<ImageStorageService> logger)
        {
            _database = database;
            _gridFS = new GridFSBucket(database, new GridFSBucketOptions
            {
                BucketName = "images",
                ChunkSizeBytes = 1048576 // 1MB chunks
            });
            _logger = logger;
        }

        /// <summary>
        /// Upload an image to MongoDB GridFS
        /// </summary>
        public async Task<string> UploadImageAsync(Stream imageStream, string fileName, string contentType)
        {
            try
            {
                var options = new GridFSUploadOptions
                {
                    Metadata = new BsonDocument
                    {
                        { "contentType", contentType },
                        { "uploadedAt", DateTime.UtcNow },
                        { "originalFileName", fileName }
                    }
                };

                var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
                var fileId = await _gridFS.UploadFromStreamAsync(uniqueFileName, imageStream, options);

                _logger.LogInformation($"Image uploaded to MongoDB GridFS: {fileId}");
                return fileId.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error uploading image to MongoDB: {fileName}");
                throw;
            }
        }

        /// <summary>
        /// Get an image from MongoDB GridFS
        /// </summary>
        public async Task<(Stream stream, string contentType, string fileName)?> GetImageAsync(string imageId)
        {
            try
            {
                if (!ObjectId.TryParse(imageId, out ObjectId objectId))
                {
                    _logger.LogWarning($"Invalid MongoDB image ID: {imageId}");
                    return null;
                }

                var filter = Builders<GridFSFileInfo>.Filter.Eq("_id", objectId);
                var fileInfo = await _gridFS.Find(filter).FirstOrDefaultAsync();

                if (fileInfo == null)
                {
                    _logger.LogWarning($"Image not found in MongoDB: {imageId}");
                    return null;
                }

                var stream = new MemoryStream();
                await _gridFS.DownloadToStreamAsync(objectId, stream);
                stream.Position = 0;

                var contentType = fileInfo.Metadata?.GetValue("contentType", "image/jpeg").AsString ?? "image/jpeg";
                var fileName = fileInfo.Filename;

                return (stream, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving image from MongoDB: {imageId}");
                return null;
            }
        }

        /// <summary>
        /// Delete an image from MongoDB GridFS
        /// </summary>
        public async Task<bool> DeleteImageAsync(string imageId)
        {
            try
            {
                if (!ObjectId.TryParse(imageId, out ObjectId objectId))
                    return false;

                await _gridFS.DeleteAsync(objectId);
                _logger.LogInformation($"Image deleted from MongoDB: {imageId}");
                return true;
            }
            catch (GridFSFileNotFoundException)
            {
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting image: {imageId}");
                return false;
            }
        }

        /// <summary>
        /// Get the URL to serve an image (stored in PostgreSQL package record)
        /// </summary>
        public string GetImageUrl(string imageId)
        {
            if (string.IsNullOrEmpty(imageId))
                return null;

            return $"/api/images/{imageId}";
        }
    }
}
