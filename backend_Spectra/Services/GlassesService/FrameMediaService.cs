using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repositories.Basic;
using Repositories.Models;

namespace Services.GlassesService
{
    public class MediaValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public interface IFrameMediaService
    {
        // Create operations
        Task<FrameMedium> AddMediaAsync(FrameMedium media);
        Task<List<FrameMedium>> AddMultipleMediaAsync(Guid frameId, List<FrameMedium> mediaList);

        // Read operations
        Task<FrameMedium?> GetMediaByIdAsync(Guid mediaId);
        Task<List<FrameMedium>> GetMediaByFrameIdAsync(Guid frameId);

        // Update operations
        Task<FrameMedium?> UpdateMediaAsync(Guid mediaId, FrameMedium updatedMedia);

        // Delete operations
        Task<bool> DeleteMediaAsync(Guid mediaId);
        Task<bool> DeleteAllMediaByFrameIdAsync(Guid frameId);

        // Validation
        MediaValidationResult ValidateMedia(FrameMedium media);
        bool IsValidMediaType(string mediaType);
        bool IsValidMediaUrl(string url);
    }

    public class FrameMediaService : IFrameMediaService
    {
        private readonly GenericRepository<FrameMedium> _mediaRepository;
        private readonly GenericRepository<Frame> _frameRepository;

        // Valid media types
        public static class MediaType
        {
            public const string Image = "image";
            public const string Video = "video";
            public const string Thumbnail = "thumbnail";
            public const string Gallery = "gallery";
        }

        private static readonly string[] ValidMediaTypes =
        {
            MediaType.Image,
            MediaType.Video,
            MediaType.Thumbnail,
            MediaType.Gallery
        };

        // Valid image extensions
        private static readonly string[] ValidImageExtensions =
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg", ".bmp"
        };

        // Valid video extensions
        private static readonly string[] ValidVideoExtensions =
        {
            ".mp4", ".webm", ".mov", ".avi", ".mkv"
        };

        public FrameMediaService(
            GenericRepository<FrameMedium> mediaRepository,
            GenericRepository<Frame> frameRepository)
        {
            _mediaRepository = mediaRepository;
            _frameRepository = frameRepository;
        }

        #region Create Operations

        public async Task<FrameMedium> AddMediaAsync(FrameMedium media)
        {
            media.MediaId = Guid.NewGuid();

            // Default media type to image if not specified
            if (string.IsNullOrEmpty(media.MediaType))
            {
                media.MediaType = MediaType.Image;
            }

            return await _mediaRepository.CreateAsync(media);
        }

        public async Task<List<FrameMedium>> AddMultipleMediaAsync(Guid frameId, List<FrameMedium> mediaList)
        {
            var createdMedia = new List<FrameMedium>();

            foreach (var media in mediaList)
            {
                media.MediaId = Guid.NewGuid();
                media.FrameId = frameId;

                if (string.IsNullOrEmpty(media.MediaType))
                {
                    media.MediaType = MediaType.Image;
                }

                var created = await _mediaRepository.CreateAsync(media);
                createdMedia.Add(created);
            }

            return createdMedia;
        }

        #endregion

        #region Read Operations

        public async Task<FrameMedium?> GetMediaByIdAsync(Guid mediaId)
        {
            var mediaList = await _mediaRepository.SearchAsync(m => m.MediaId == mediaId);
            return mediaList.FirstOrDefault();
        }

        public async Task<List<FrameMedium>> GetMediaByFrameIdAsync(Guid frameId)
        {
            var mediaList = await _mediaRepository.SearchAsync(m => m.FrameId == frameId);
            return mediaList.ToList();
        }

        #endregion

        #region Update Operations

        public async Task<FrameMedium?> UpdateMediaAsync(Guid mediaId, FrameMedium updatedMedia)
        {
            var existingMedia = await GetMediaByIdAsync(mediaId);

            if (existingMedia == null)
            {
                return null;
            }

            // Update allowed fields
            if (!string.IsNullOrEmpty(updatedMedia.MediaUrl))
                existingMedia.MediaUrl = updatedMedia.MediaUrl;

            if (!string.IsNullOrEmpty(updatedMedia.MediaType))
                existingMedia.MediaType = updatedMedia.MediaType;

            return await _mediaRepository.UpdateAsync(existingMedia);
        }

        #endregion

        #region Delete Operations

        public async Task<bool> DeleteMediaAsync(Guid mediaId)
        {
            var media = await GetMediaByIdAsync(mediaId);

            if (media == null)
            {
                return false;
            }

            return await _mediaRepository.DeleteAsync(media);
        }

        public async Task<bool> DeleteAllMediaByFrameIdAsync(Guid frameId)
        {
            var mediaList = await GetMediaByFrameIdAsync(frameId);

            foreach (var media in mediaList)
            {
                await _mediaRepository.DeleteAsync(media);
            }

            return true;
        }

        #endregion

        #region Validation

        public MediaValidationResult ValidateMedia(FrameMedium media)
        {
            var result = new MediaValidationResult { IsValid = true };

            // Validate frame ID
            if (!media.FrameId.HasValue)
            {
                result.IsValid = false;
                result.Errors.Add("Frame ID is required");
            }

            // Validate media URL
            if (string.IsNullOrWhiteSpace(media.MediaUrl))
            {
                result.IsValid = false;
                result.Errors.Add("Media URL is required");
            }
            else if (!IsValidMediaUrl(media.MediaUrl))
            {
                result.IsValid = false;
                result.Errors.Add("Invalid media URL format");
            }

            // Validate media type
            if (!string.IsNullOrEmpty(media.MediaType) && !IsValidMediaType(media.MediaType))
            {
                result.IsValid = false;
                result.Errors.Add($"Invalid media type. Allowed: {string.Join(", ", ValidMediaTypes)}");
            }

            return result;
        }

        public bool IsValidMediaType(string mediaType)
        {
            return ValidMediaTypes.Contains(mediaType.ToLower());
        }

        public bool IsValidMediaUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            // Check if it's a valid URL format
            if (Uri.TryCreate(url, UriKind.Absolute, out var uriResult))
            {
                return uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps;
            }

            // Also allow relative paths starting with /
            if (url.StartsWith("/"))
            {
                return true;
            }

            // Check for valid file extensions
            var extension = Path.GetExtension(url)?.ToLower();
            if (!string.IsNullOrEmpty(extension))
            {
                return ValidImageExtensions.Contains(extension) || ValidVideoExtensions.Contains(extension);
            }

            return false;
        }

        #endregion
    }
}
