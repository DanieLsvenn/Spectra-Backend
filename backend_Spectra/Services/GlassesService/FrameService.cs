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
        Task<bool> CheckVariantStockAsync(Guid frameId, Guid colorId, int quantity);
        Task<bool> DeductStockAsync(Guid frameId, int quantity);
        Task<bool> DeductVariantStockAsync(Guid frameId, Guid colorId, int quantity);
        Task<bool> RestoreStockAsync(Guid frameId, int quantity);
        Task<bool> RestoreVariantStockAsync(Guid frameId, Guid colorId, int quantity);
        Task<List<Frame>> GetLowStockFramesAsync();
        Task<List<Frame>> GetOutOfStockFramesAsync();

        // Frame colors
        Task SetFrameColorsAsync(Guid frameId, List<FrameColorInput> colorInputs);

        // Frame sizes
        Task SetFrameSizesAsync(Guid frameId, List<string> sizes);

        // Frame lens types
        Task SetFrameLensTypesAsync(Guid frameId, List<Guid> lensTypeIds);
        Task<List<LensType>> GetSupportedLensTypesAsync(Guid frameId);

        // Validation
        FrameValidationResult ValidateFrameSizeAttributes(int? lensWidth, int? bridgeWidth, int? frameWidth, int? templeLength, string? size = null);
    }

    /// <summary>
    /// Input model for setting frame colors with per-variant stock
    /// </summary>
    public class FrameColorInput
    {
        public Guid ColorId { get; set; }
        public int StockQuantity { get; set; }
        public double ColorExtraCost { get; set; }
    }

    public class FrameService : IFrameService
    {
        private readonly GenericRepository<Frame> _frameRepository;
        private readonly GenericRepository<FrameMedium> _frameMediaRepository;
        private readonly GenericRepository<FrameColor> _frameColorRepository;
        private readonly GenericRepository<FrameSize> _frameSizeRepository;
        private readonly GenericRepository<FrameLensType> _frameLensTypeRepository;
        private readonly IPreorderCampaignService _campaignService;

        private const string AvailableStatus = "available";
        private const string InactiveStatus = "inactive";

        // Size attribute validation ranges
        //private const int MinLensWidth = 38;
        //private const int MaxLensWidth = 62;
        //private const int MinBridgeWidth = 12;
        //private const int MaxBridgeWidth = 24;
        //private const int MinFrameWidth = 115;
        //private const int MaxFrameWidth = 155;
        //private const int MinTempleLength = 115;
        //private const int MaxTempleLength = 155;

        private static readonly string[] ValidSizes = { "small", "medium", "large", "extra-large" };

        public FrameService(
            GenericRepository<Frame> frameRepository,
            GenericRepository<FrameMedium> frameMediaRepository,
            GenericRepository<FrameColor> frameColorRepository,
            GenericRepository<FrameSize> frameSizeRepository,
            GenericRepository<FrameLensType> frameLensTypeRepository,
            IPreorderCampaignService campaignService)
        {
            _frameRepository = frameRepository;
            _frameMediaRepository = frameMediaRepository;
            _frameColorRepository = frameColorRepository;
            _frameSizeRepository = frameSizeRepository;
            _frameLensTypeRepository = frameLensTypeRepository;
            _campaignService = campaignService;
        }

        #region Public Read Operations

        /// <summary>
        /// Builds an IQueryable for Frame with all navigation properties eagerly loaded,
        /// including nested Color inside FrameColors and supported LensTypes.
        /// Uses AsNoTracking to prevent EF navigation fixup from wiring inverse collections
        /// (e.g. Brand.Frames, Shape.Frames) which causes deep object graphs that fail serialization.
        /// </summary>
        private IQueryable<Frame> GetFrameQueryWithIncludes()
        {
            return _frameRepository.GetSet()
                .AsNoTracking()
                .Include(f => f.FrameMedia)
                .Include(f => f.Brand)
                .Include(f => f.Material)
                .Include(f => f.Shape)
                .Include(f => f.FrameColors)
                    .ThenInclude(fc => fc.Color)
                .Include(f => f.FrameLensTypes)
                    .ThenInclude(flt => flt.LensType);
        }

        public async Task<PaginationResult<Frame>> GetAvailableFramesAsync(int currentPage = 1, int pageSize = 10)
        {
            var allFrames = await GetFrameQueryWithIncludes().ToListAsync();

            // FrameIds in upcoming (not-yet-started) campaigns � hide these if out of stock
            var upcomingCampaignFrameIds = await _campaignService.GetUpcomingCampaignFrameIdsAsync();

            // Show all non-inactive frames, but hide out-of-stock frames
            // that are in an upcoming campaign (to avoid spoiling the preorder launch)
            var availableFrames = allFrames
                .Where(f =>
                {
                    if (IsInactiveStatus(f.Status))
                        return false;

                    if (string.Equals(f.Status, "out_of_stock", StringComparison.OrdinalIgnoreCase)
                        && upcomingCampaignFrameIds.Contains(f.FrameId))
                        return false;

                    return true;
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
            var frame = await GetFrameQueryWithIncludes()
                .FirstOrDefaultAsync(f => f.FrameId == frameId);

            if (frame == null)
                return null;

            // Return any frame that is not inactive
            if (IsInactiveStatus(frame.Status))
                return null;

            return frame;
        }

        public async Task<List<FrameMedium>> GetFrameMediaAsync(Guid frameId)
        {
            var frame = await _frameRepository.GetByIdAsync(frameId);

            if (frame == null || IsInactiveStatus(frame.Status))
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

        private static bool IsInactiveStatus(string? status)
        {
            return InactiveStatus.Equals(status, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Manager Operations

        public async Task<PaginationResult<Frame>> GetAllFramesAsync(int currentPage = 1, int pageSize = 10)
        {
            var allFrames = await GetFrameQueryWithIncludes().ToListAsync();

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
            return await GetFrameQueryWithIncludes()
                .FirstOrDefaultAsync(f => f.FrameId == frameId);
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

            if (updatedFrame.ShapeId.HasValue)
                existingFrame.ShapeId = updatedFrame.ShapeId;

            if (updatedFrame.LensWidth.HasValue)
                existingFrame.LensWidth = updatedFrame.LensWidth;

            if (updatedFrame.BridgeWidth.HasValue)
                existingFrame.BridgeWidth = updatedFrame.BridgeWidth;

            if (updatedFrame.FrameWidth.HasValue)
                existingFrame.FrameWidth = updatedFrame.FrameWidth;

            if (updatedFrame.TempleLength.HasValue)
                existingFrame.TempleLength = updatedFrame.TempleLength;

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

            if (updatedFrame.MinRx.HasValue)
                existingFrame.MinRx = updatedFrame.MinRx;

            if (updatedFrame.MaxRx.HasValue)
                existingFrame.MaxRx = updatedFrame.MaxRx;

            if (updatedFrame.MinPd.HasValue)
                existingFrame.MinPd = updatedFrame.MinPd;

            if (updatedFrame.MaxPd.HasValue)
                existingFrame.MaxPd = updatedFrame.MaxPd;

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

        public async Task<bool> CheckVariantStockAsync(Guid frameId, Guid colorId, int quantity)
        {
            var variants = await _frameColorRepository.SearchAsync(
                fc => fc.FrameId == frameId && fc.ColorId == colorId);
            var variant = variants.FirstOrDefault();

            if (variant == null)
                return false;

            return (variant.StockQuantity ?? 0) >= quantity;
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

        public async Task<bool> DeductVariantStockAsync(Guid frameId, Guid colorId, int quantity)
        {
            var variants = await _frameColorRepository.SearchAsync(
                fc => fc.FrameId == frameId && fc.ColorId == colorId);
            var variant = variants.FirstOrDefault();

            if (variant == null)
                return false;

            var currentStock = variant.StockQuantity ?? 0;
            if (currentStock < quantity)
                return false;

            variant.StockQuantity = currentStock - quantity;
            await _frameColorRepository.UpdateAsync(variant);

            // Recalculate frame-level stock as sum of all variant stocks
            await RecalculateFrameStockAsync(frameId);

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

        public async Task<bool> RestoreVariantStockAsync(Guid frameId, Guid colorId, int quantity)
        {
            var variants = await _frameColorRepository.SearchAsync(
                fc => fc.FrameId == frameId && fc.ColorId == colorId);
            var variant = variants.FirstOrDefault();

            if (variant == null)
                return false;

            var currentStock = variant.StockQuantity ?? 0;
            variant.StockQuantity = currentStock + quantity;
            await _frameColorRepository.UpdateAsync(variant);

            // Recalculate frame-level stock as sum of all variant stocks
            await RecalculateFrameStockAsync(frameId);

            return true;
        }

        /// <summary>
        /// Recalculates the frame-level StockQuantity as the sum of all variant stocks.
        /// Also updates the frame status based on total stock.
        /// </summary>
        private async Task RecalculateFrameStockAsync(Guid frameId)
        {
            var frames = await _frameRepository.SearchAsync(f => f.FrameId == frameId);
            var frame = frames.FirstOrDefault();
            if (frame == null) return;

            var allVariants = await _frameColorRepository.SearchAsync(fc => fc.FrameId == frameId);
            var totalStock = allVariants.Sum(v => v.StockQuantity ?? 0);

            frame.StockQuantity = totalStock;

            if (totalStock <= 0)
            {
                frame.Status = "out_of_stock";
            }
            else if (frame.Status?.ToLower() == "out_of_stock")
            {
                frame.Status = AvailableStatus;
            }

            await _frameRepository.UpdateAsync(frame);
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

        public async Task SetFrameColorsAsync(Guid frameId, List<FrameColorInput> colorInputs)
        {
            // Remove existing frame colors
            var existing = await _frameColorRepository.SearchAsync(fc => fc.FrameId == frameId);
            foreach (var fc in existing)
            {
                await _frameColorRepository.DeleteAsync(fc);
            }

            // Add new frame colors with per-variant stock
            int totalStock = 0;
            for (int i = 0; i < colorInputs.Count; i++)
            {
                var frameColor = new FrameColor
                {
                    FrameColorId = Guid.NewGuid(),
                    FrameId = frameId,
                    ColorId = colorInputs[i].ColorId,
                    IsDefault = i == 0,
                    StockQuantity = colorInputs[i].StockQuantity,
                    ColorExtraCost = colorInputs[i].ColorExtraCost
                };
                await _frameColorRepository.CreateAsync(frameColor);
                totalStock += colorInputs[i].StockQuantity;
            }

            // Update frame-level stock to match sum of variants
            var frames = await _frameRepository.SearchAsync(f => f.FrameId == frameId);
            var frame = frames.FirstOrDefault();
            if (frame != null)
            {
                frame.StockQuantity = totalStock;
                if (totalStock <= 0)
                {
                    frame.Status = "out_of_stock";
                }
                else if (frame.Status?.ToLower() == "out_of_stock")
                {
                    frame.Status = AvailableStatus;
                }
                await _frameRepository.UpdateAsync(frame);
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

        #region Frame Lens Types

        public async Task SetFrameLensTypesAsync(Guid frameId, List<Guid> lensTypeIds)
        {
            // Remove existing frame lens types
            var existing = await _frameLensTypeRepository.SearchAsync(flt => flt.FrameId == frameId);
            foreach (var flt in existing)
            {
                await _frameLensTypeRepository.DeleteAsync(flt);
            }

            // Add new frame lens types
            foreach (var lensTypeId in lensTypeIds)
            {
                var frameLensType = new FrameLensType
                {
                    FrameLensTypeId = Guid.NewGuid(),
                    FrameId = frameId,
                    LensTypeId = lensTypeId
                };
                await _frameLensTypeRepository.CreateAsync(frameLensType);
            }
        }

        public async Task<List<LensType>> GetSupportedLensTypesAsync(Guid frameId)
        {
            var frameLensTypes = await _frameLensTypeRepository.SearchAsyncInclude(
                flt => flt.FrameId == frameId,
                flt => flt.LensType);
            return frameLensTypes
                .Where(flt => flt.LensType != null)
                .Select(flt => flt.LensType)
                .ToList();
        }

        #endregion

        #region Validation

        public FrameValidationResult ValidateFrameSizeAttributes(
            int? lensWidth,
            int? bridgeWidth,
            int? frameWidth,
            int? templeLength,
            string? size = null)
        {
            var result = new FrameValidationResult { IsValid = true };

            //if (lensWidth.HasValue)
            //{
            //    if (lensWidth < MinLensWidth || lensWidth > MaxLensWidth)
            //    {
            //        result.IsValid = false;
            //        result.Errors.Add($"Lens width must be between {MinLensWidth}mm and {MaxLensWidth}mm");
            //    }
            //}

            //if (bridgeWidth.HasValue)
            //{
            //    if (bridgeWidth < MinBridgeWidth || bridgeWidth > MaxBridgeWidth)
            //    {
            //        result.IsValid = false;
            //        result.Errors.Add($"Bridge width must be between {MinBridgeWidth}mm and {MaxBridgeWidth}mm");
            //    }
            //}

            //if (frameWidth.HasValue)
            //{
            //    if (frameWidth < MinFrameWidth || frameWidth > MaxFrameWidth)
            //    {
            //        result.IsValid = false;
            //        result.Errors.Add($"Frame width must be between {MinFrameWidth}mm and {MaxFrameWidth}mm");
            //    }
            //}

            //if (templeLength.HasValue)
            //{
            //    if (templeLength < MinTempleLength || templeLength > MaxTempleLength)
            //    {
            //        result.IsValid = false;
            //        result.Errors.Add($"Temple length must be between {MinTempleLength}mm and {MaxTempleLength}mm");
            //    }
            //}

            // Size validation
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
