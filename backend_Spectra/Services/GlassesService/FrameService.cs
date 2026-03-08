using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Repositories.Basic;
using Repositories.ModelExtensions;
using Repositories.Models;

namespace Services.GlassesService
{
    public class FrameValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public interface IFrameService
    {
        // Read operations (public)
        Task<PaginationResult<Frame>> GetAvailableFramesAsync(int currentPage = 1, int pageSize = 10);
        Task<Frame?> GetFrameByIdAsync(Guid frameId);
        Task<List<FrameMedium>> GetFrameMediaAsync(Guid frameId);

        // Manager operations
        Task<PaginationResult<Frame>> GetAllFramesAsync(int currentPage = 1, int pageSize = 10);
        Task<Frame?> GetFrameByIdForManagerAsync(Guid frameId);
        Task<Frame> CreateFrameAsync(Frame frame);
        Task<Frame?> UpdateFrameAsync(Guid frameId, Frame updatedFrame);
        Task<bool> SoftDeleteFrameAsync(Guid frameId);

        // Inventory management
        Task<bool> CheckStockAvailabilityAsync(Guid frameId, int quantity);
        Task<bool> DeductStockAsync(Guid frameId, int quantity);
        Task<bool> RestoreStockAsync(Guid frameId, int quantity);
        Task<List<Frame>> GetLowStockFramesAsync();
        Task<List<Frame>> GetOutOfStockFramesAsync();

        // Frame colors
        Task SetFrameColorsAsync(Guid frameId, List<Guid> colorIds);

        // Frame sizes
        Task SetFrameSizesAsync(Guid frameId, List<string> sizes);

        // Validation
        FrameValidationResult ValidateFrameSizeAttributes(int? lensWidth, int? bridgeWidth, int? frameWidth, int? templeLength, string? shape = null, string? size = null);
    }

    public class FrameService : IFrameService
    {
        private readonly GenericRepository<Frame> _frameRepository;
        private readonly GenericRepository<FrameMedium> _frameMediaRepository;
        private readonly GenericRepository<FrameColor> _frameColorRepository;
        private readonly GenericRepository<FrameSize> _frameSizeRepository;
        private readonly GenericRepository<CampaignFrame> _campaignFrameRepository;
        private readonly GenericRepository<PreorderCampaign> _campaignRepository;

        private const string AvailableStatus = "available";
        private const string InactiveStatus = "inactive";

        // Size attribute validation ranges
        private const int MinLensWidth = 38;
        private const int MaxLensWidth = 62;
        private const int MinBridgeWidth = 12;
        private const int MaxBridgeWidth = 24;
        private const int MinFrameWidth = 115;
        private const int MaxFrameWidth = 155;
        private const int MinTempleLength = 115;
        private const int MaxTempleLength = 155;

        private static readonly string[] ValidShapes = { "square", "rectangle", "round", "oval", "cat-eye", "aviator", "browline", "geometric", "wrap" };
        private static readonly string[] ValidSizes = { "small", "medium", "large", "extra-large" };

        public FrameService(
            GenericRepository<Frame> frameRepository,
            GenericRepository<FrameMedium> frameMediaRepository,
            GenericRepository<FrameColor> frameColorRepository,
            GenericRepository<FrameSize> frameSizeRepository,
            GenericRepository<CampaignFrame> campaignFrameRepository,
            GenericRepository<PreorderCampaign> campaignRepository)
        {
            _frameRepository = frameRepository;
            _frameMediaRepository = frameMediaRepository;
            _frameColorRepository = frameColorRepository;
            _frameSizeRepository = frameSizeRepository;
            _campaignFrameRepository = campaignFrameRepository;
            _campaignRepository = campaignRepository;
        }

        #region Public Read Operations

        public async Task<PaginationResult<Frame>> GetAvailableFramesAsync(int currentPage = 1, int pageSize = 10)
        {
            var allFrames = await _frameRepository.GetAllAsyncInclude(
                f => f.FrameMedia,
                f => f.Brand,
                f => f.Material,
                f => f.FrameColors);

            // Determine which out-of-stock frames are in active preorder campaigns
            var activeCampaignFrameIds = await GetActiveCampaignFrameIdsAsync();

            var availableFrames = allFrames
                .Where(f =>
                {
                    if (IsAvailableStatus(f.Status))
                        return true;

                    // Show out-of-stock frames only if they are in an active preorder campaign
                    if (string.Equals(f.Status, "out_of_stock", StringComparison.OrdinalIgnoreCase))
                        return activeCampaignFrameIds.Contains(f.FrameId);

                    return false;
                })
                .OrderBy(f => f.FrameName)
                .ToList();

            var totalItems = availableFrames.Count;
            var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            var items = availableFrames
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PaginationResult<Frame>
            {
                TotalItems = totalItems,
                TotalPages = totalPages,
                CurrentPage = currentPage,
                PageSize = pageSize,
                Items = items
            };
        }

        public async Task<Frame?> GetFrameByIdAsync(Guid frameId)
        {
            var frame = await _frameRepository.GetByIdAsyncInclude(
                frameId,
                f => f.FrameMedia,
                f => f.Brand,
                f => f.Material,
                f => f.FrameColors
            );

            if (frame == null)
                return null;

            if (IsAvailableStatus(frame.Status))
                return frame;

            // Allow out-of-stock frames that are in an active preorder campaign
            if (string.Equals(frame.Status, "out_of_stock", StringComparison.OrdinalIgnoreCase))
            {
                var activeCampaignFrameIds = await GetActiveCampaignFrameIdsAsync();
                if (activeCampaignFrameIds.Contains(frame.FrameId))
                    return frame;
            }

            return null;
        }

        public async Task<List<FrameMedium>> GetFrameMediaAsync(Guid frameId)
        {
            var frame = await _frameRepository.GetByIdAsync(frameId);

            if (frame == null || !IsAvailableStatus(frame.Status))
            {
                return new List<FrameMedium>();
            }

            var media = await _frameMediaRepository.SearchAsync(m => m.FrameId == frameId);
            return media.ToList();
        }

        private static bool IsAvailableStatus(string? status)
        {
            return AvailableStatus.Equals(status, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns the set of FrameIds that belong to currently active preorder campaigns.
        /// If the campaign tables don't exist yet (pre-migration), returns an empty set.
        /// </summary>
        private async Task<HashSet<Guid>> GetActiveCampaignFrameIdsAsync()
        {
            try
            {
                var now = DateTime.UtcNow;
                var allCampaigns = await _campaignRepository.GetAllAsyncInclude(c => c.CampaignFrames);
                return allCampaigns
                    .Where(c => c.StartDate <= now && c.EndDate >= now &&
                                (c.Status == "upcoming" || c.Status == "active"))
                    .SelectMany(c => c.CampaignFrames)
                    .Where(cf => cf.FrameId.HasValue)
                    .Select(cf => cf.FrameId!.Value)
                    .ToHashSet();
            }
            catch
            {
                // Campaign tables may not exist yet – treat as no active campaigns
                return new HashSet<Guid>();
            }
        }

        #endregion

        #region Manager Operations

        public async Task<PaginationResult<Frame>> GetAllFramesAsync(int currentPage = 1, int pageSize = 10)
        {
            var allFrames = await _frameRepository.GetAllAsyncInclude(
                f => f.FrameMedia,
                f => f.Brand,
                f => f.Material,
                f => f.FrameColors);

            var orderedFrames = allFrames
                .OrderBy(f => f.FrameName)
                .ToList();

            var totalItems = orderedFrames.Count;
            var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            var items = orderedFrames
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PaginationResult<Frame>
            {
                TotalItems = totalItems,
                TotalPages = totalPages,
                CurrentPage = currentPage,
                PageSize = pageSize,
                Items = items
            };
        }

        public async Task<Frame?> GetFrameByIdForManagerAsync(Guid frameId)
        {
            return await _frameRepository.GetByIdAsyncInclude(
                frameId,
                f => f.FrameMedia,
                f => f.Brand,
                f => f.Material,
                f => f.FrameColors
            );
        }

        public async Task<Frame> CreateFrameAsync(Frame frame)
        {
            frame.FrameId = Guid.NewGuid();
            frame.Status = AvailableStatus;

            return await _frameRepository.CreateAsync(frame);
        }

        public async Task<Frame?> UpdateFrameAsync(Guid frameId, Frame updatedFrame)
        {
            var frames = await _frameRepository.SearchAsync(f => f.FrameId == frameId);
            var existingFrame = frames.FirstOrDefault();

            if (existingFrame == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(updatedFrame.FrameName))
                existingFrame.FrameName = updatedFrame.FrameName;

            if (updatedFrame.BrandId.HasValue)
                existingFrame.BrandId = updatedFrame.BrandId;

            if (updatedFrame.MaterialId.HasValue)
                existingFrame.MaterialId = updatedFrame.MaterialId;

            if (updatedFrame.LensWidth.HasValue)
                existingFrame.LensWidth = updatedFrame.LensWidth;

            if (updatedFrame.BridgeWidth.HasValue)
                existingFrame.BridgeWidth = updatedFrame.BridgeWidth;

            if (updatedFrame.FrameWidth.HasValue)
                existingFrame.FrameWidth = updatedFrame.FrameWidth;

            if (updatedFrame.TempleLength.HasValue)
                existingFrame.TempleLength = updatedFrame.TempleLength;

            if (updatedFrame.Shape != null)
                existingFrame.Shape = updatedFrame.Shape;

            if (updatedFrame.Size != null)
                existingFrame.Size = updatedFrame.Size;

            if (updatedFrame.BasePrice.HasValue)
                existingFrame.BasePrice = updatedFrame.BasePrice;

            if (!string.IsNullOrEmpty(updatedFrame.Status))
                existingFrame.Status = updatedFrame.Status;

            if (updatedFrame.StockQuantity.HasValue)
                existingFrame.StockQuantity = updatedFrame.StockQuantity;

            if (updatedFrame.ReorderLevel.HasValue)
                existingFrame.ReorderLevel = updatedFrame.ReorderLevel;

            return await _frameRepository.UpdateAsync(existingFrame);
        }

        public async Task<bool> SoftDeleteFrameAsync(Guid frameId)
        {
            var frames = await _frameRepository.SearchAsync(f => f.FrameId == frameId);
            var frame = frames.FirstOrDefault();

            if (frame == null)
            {
                return false;
            }

            frame.Status = InactiveStatus;
            await _frameRepository.UpdateAsync(frame);

            return true;
        }

        #endregion

        #region Inventory Management

        public async Task<bool> CheckStockAvailabilityAsync(Guid frameId, int quantity)
        {
            var frames = await _frameRepository.SearchAsync(f => f.FrameId == frameId);
            var frame = frames.FirstOrDefault();

            if (frame == null)
                return false;

            return (frame.StockQuantity ?? 0) >= quantity;
        }

        public async Task<bool> DeductStockAsync(Guid frameId, int quantity)
        {
            var frames = await _frameRepository.SearchAsync(f => f.FrameId == frameId);
            var frame = frames.FirstOrDefault();

            if (frame == null)
                return false;

            var currentStock = frame.StockQuantity ?? 0;
            if (currentStock < quantity)
                return false;

            frame.StockQuantity = currentStock - quantity;

            if (frame.StockQuantity <= 0)
            {
                frame.Status = "out_of_stock";
            }

            await _frameRepository.UpdateAsync(frame);
            return true;
        }

        public async Task<bool> RestoreStockAsync(Guid frameId, int quantity)
        {
            var frames = await _frameRepository.SearchAsync(f => f.FrameId == frameId);
            var frame = frames.FirstOrDefault();

            if (frame == null)
                return false;

            var currentStock = frame.StockQuantity ?? 0;
            frame.StockQuantity = currentStock + quantity;

            if (frame.StockQuantity > 0 && frame.Status?.ToLower() == "out_of_stock")
            {
                frame.Status = AvailableStatus;
            }

            await _frameRepository.UpdateAsync(frame);
            return true;
        }

        public async Task<List<Frame>> GetLowStockFramesAsync()
        {
            var allFrames = await _frameRepository.GetAllAsync();
            return allFrames
                .Where(f => f.Status?.ToLower() == AvailableStatus &&
                           (f.StockQuantity ?? 0) <= (f.ReorderLevel ?? 5) &&
                           (f.StockQuantity ?? 0) > 0)
                .ToList();
        }

        public async Task<List<Frame>> GetOutOfStockFramesAsync()
        {
            var allFrames = await _frameRepository.GetAllAsync();
            return allFrames
                .Where(f => (f.StockQuantity ?? 0) <= 0 ||
                           f.Status?.ToLower() == "out_of_stock")
                .ToList();
        }

        #endregion

        #region Frame Colors

        public async Task SetFrameColorsAsync(Guid frameId, List<Guid> colorIds)
        {
            // Remove existing frame colors
            var existing = await _frameColorRepository.SearchAsync(fc => fc.FrameId == frameId);
            foreach (var fc in existing)
            {
                await _frameColorRepository.DeleteAsync(fc);
            }

            // Add new frame colors
            for (int i = 0; i < colorIds.Count; i++)
            {
                var frameColor = new FrameColor
                {
                    FrameColorId = Guid.NewGuid(),
                    FrameId = frameId,
                    ColorId = colorIds[i],
                    IsDefault = i == 0
                };
                await _frameColorRepository.CreateAsync(frameColor);
            }
        }

        #endregion

        #region Frame Sizes

        public async Task SetFrameSizesAsync(Guid frameId, List<string> sizes)
        {
            // Remove existing frame sizes
            var existing = await _frameSizeRepository.SearchAsync(fs => fs.FrameId == frameId);
            foreach (var fs in existing)
            {
                await _frameSizeRepository.DeleteAsync(fs);
            }

            // Add new frame sizes
            for (int i = 0; i < sizes.Count; i++)
            {
                var frameSize = new FrameSize
                {
                    FrameSizeId = Guid.NewGuid(),
                    FrameId = frameId,
                    Size = sizes[i],
                    IsDefault = i == 0
                };
                await _frameSizeRepository.CreateAsync(frameSize);
            }
        }

        #endregion

        #region Validation

        public FrameValidationResult ValidateFrameSizeAttributes(
            int? lensWidth,
            int? bridgeWidth,
            int? frameWidth,
            int? templeLength,
            string? shape = null,
            string? size = null)
        {
            var result = new FrameValidationResult { IsValid = true };

            if (lensWidth.HasValue)
            {
                if (lensWidth < MinLensWidth || lensWidth > MaxLensWidth)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Lens width must be between {MinLensWidth}mm and {MaxLensWidth}mm");
                }
            }

            if (bridgeWidth.HasValue)
            {
                if (bridgeWidth < MinBridgeWidth || bridgeWidth > MaxBridgeWidth)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Bridge width must be between {MinBridgeWidth}mm and {MaxBridgeWidth}mm");
                }
            }

            if (frameWidth.HasValue)
            {
                if (frameWidth < MinFrameWidth || frameWidth > MaxFrameWidth)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Frame width must be between {MinFrameWidth}mm and {MaxFrameWidth}mm");
                }
            }

            if (templeLength.HasValue)
            {
                if (templeLength < MinTempleLength || templeLength > MaxTempleLength)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Temple length must be between {MinTempleLength}mm and {MaxTempleLength}mm");
                }
            }

            // Task 5: Strict FrameWidth formula
            if (lensWidth.HasValue && bridgeWidth.HasValue && frameWidth.HasValue)
            {
                var expected = 2 * lensWidth.Value + bridgeWidth.Value;
                if (frameWidth.Value != expected)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Frame width must equal (2 × Lens Width) + Bridge Width. Expected {expected}mm but got {frameWidth.Value}mm.");
                }
            }

            // Task 5: BridgeWidth < LensWidth
            if (bridgeWidth.HasValue && lensWidth.HasValue && bridgeWidth >= lensWidth)
            {
                result.IsValid = false;
                result.Errors.Add("Bridge width must be less than lens width");
            }

            // Task 5: Shape validation
            if (!string.IsNullOrEmpty(shape))
            {
                if (!ValidShapes.Contains(shape.ToLower()))
                {
                    result.IsValid = false;
                    result.Errors.Add($"Invalid shape. Allowed values: {string.Join(", ", ValidShapes)}");
                }
            }

            // Task 5: Size validation
            if (!string.IsNullOrEmpty(size))
            {
                if (!ValidSizes.Contains(size.ToLower()))
                {
                    result.IsValid = false;
                    result.Errors.Add($"Invalid size. Allowed values: {string.Join(", ", ValidSizes)}");
                }
            }

            return result;
        }

        #endregion
    }
}
