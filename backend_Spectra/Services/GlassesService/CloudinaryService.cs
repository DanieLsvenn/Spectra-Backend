using System;
using System.IO;
using System.Threading.Tasks;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;

namespace Services.GlassesService
{
    public class CloudinaryUploadResult
    {
        public bool Success { get; set; }
        public string Url { get; set; } = string.Empty;
        public string PublicId { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
    }

    public interface ICloudinaryService
    {
        Task<CloudinaryUploadResult> UploadImageAsync(Stream fileStream, string fileName, string folder = "products");
        Task<CloudinaryUploadResult> UploadImageAsync(byte[] fileBytes, string fileName, string folder = "products");
        Task<bool> DeleteImageAsync(string publicId);
        string GetPublicIdFromUrl(string url);
        bool IsConfigured { get; }
    }

    public class CloudinaryService : ICloudinaryService
    {
        private readonly Cloudinary? _cloudinary;
        private readonly bool _isConfigured;

        public bool IsConfigured => _isConfigured;

        public CloudinaryService(IConfiguration configuration)
        {
            var cloudinaryConfig = configuration.GetSection("Cloudinary");
            var cloudName = cloudinaryConfig["CloudName"];
            var apiKey = cloudinaryConfig["ApiKey"];
            var apiSecret = cloudinaryConfig["ApiSecret"];

            // Check if configuration is properly set (not placeholder values)
            if (string.IsNullOrEmpty(cloudName) || 
                string.IsNullOrEmpty(apiKey) || 
                string.IsNullOrEmpty(apiSecret) ||
                cloudName == "YOUR_CLOUD_NAME" ||
                apiKey == "YOUR_API_KEY" ||
                apiSecret == "YOUR_API_SECRET")
            {
                _isConfigured = false;
                _cloudinary = null;
                return;
            }

            var account = new Account(cloudName, apiKey, apiSecret);
            _cloudinary = new Cloudinary(account);
            _cloudinary.Api.Secure = true;
            _isConfigured = true;
        }

        public async Task<CloudinaryUploadResult> UploadImageAsync(Stream fileStream, string fileName, string folder = "products")
        {
            if (!_isConfigured || _cloudinary == null)
            {
                return new CloudinaryUploadResult
                {
                    Success = false,
                    Error = "Cloudinary is not configured. Please set up Cloudinary credentials in appsettings.json"
                };
            }

            try
            {
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(fileName, fileStream),
                    Folder = folder,
                    Transformation = new Transformation()
                        .Quality("auto")
                        .FetchFormat("auto"),
                    UseFilename = true,
                    UniqueFilename = true,
                    Overwrite = false
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams);

                if (uploadResult.Error != null)
                {
                    return new CloudinaryUploadResult
                    {
                        Success = false,
                        Error = uploadResult.Error.Message
                    };
                }

                return new CloudinaryUploadResult
                {
                    Success = true,
                    Url = uploadResult.SecureUrl.ToString(),
                    PublicId = uploadResult.PublicId
                };
            }
            catch (Exception ex)
            {
                return new CloudinaryUploadResult
                {
                    Success = false,
                    Error = $"Upload failed: {ex.Message}"
                };
            }
        }

        public async Task<CloudinaryUploadResult> UploadImageAsync(byte[] fileBytes, string fileName, string folder = "products")
        {
            using var stream = new MemoryStream(fileBytes);
            return await UploadImageAsync(stream, fileName, folder);
        }

        public async Task<bool> DeleteImageAsync(string publicId)
        {
            if (!_isConfigured || _cloudinary == null)
            {
                return false;
            }

            try
            {
                var deleteParams = new DeletionParams(publicId);
                var result = await _cloudinary.DestroyAsync(deleteParams);
                return result.Result == "ok";
            }
            catch
            {
                return false;
            }
        }

        public string GetPublicIdFromUrl(string url)
        {
            try
            {
                // Cloudinary URLs typically look like:
                // https://res.cloudinary.com/{cloud_name}/image/upload/v{version}/{folder}/{public_id}.{format}
                var uri = new Uri(url);
                var path = uri.AbsolutePath;
                
                // Find the upload segment and get everything after it
                var uploadIndex = path.IndexOf("/upload/", StringComparison.OrdinalIgnoreCase);
                if (uploadIndex == -1) return string.Empty;

                var afterUpload = path.Substring(uploadIndex + 8); // Skip "/upload/"
                
                // Skip version number if present (starts with 'v' followed by digits)
                var parts = afterUpload.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var startIndex = 0;
                if (parts.Length > 0 && parts[0].StartsWith("v") && parts[0].Length > 1 && char.IsDigit(parts[0][1]))
                {
                    startIndex = 1;
                }

                // Combine remaining parts (folder/public_id)
                var publicIdWithExtension = string.Join("/", parts, startIndex, parts.Length - startIndex);
                
                // Remove file extension
                var lastDotIndex = publicIdWithExtension.LastIndexOf('.');
                if (lastDotIndex > 0)
                {
                    return publicIdWithExtension.Substring(0, lastDotIndex);
                }

                return publicIdWithExtension;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
