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

        // Validation
        FrameValidationResult ValidateFrameSizeAttributes(int? lensWidth, int? bridgeWidth, int? frameWidth, int? templeLength);
    }

    public class FrameService : IFrameService
    {
        private readonly GenericRepository<Frame> _frameRepository;
        private readonly GenericRepository<FrameMedium> _frameMediaRepository;

        private const string AvailableStatus = "available";
        private const string InactiveStatus = "inactive";

        // Size attribute validation ranges
        private const int MinLensWidth = 40;
        private const int MaxLensWidth = 62;
        private const int MinBridgeWidth = 14;
        private const int MaxBridgeWidth = 24;
        private const int MinFrameWidth = 120;
        private const int MaxFrameWidth = 150;
        private const int MinTempleLength = 120;
        private const int MaxTempleLength = 155;

        public FrameService(
            GenericRepository<Frame> frameRepository,
            GenericRepository<FrameMedium> frameMediaRepository)
        {
            _frameRepository = frameRepository;
            _frameMediaRepository = frameMediaRepository;
        }

        #region Public Read Operations

        public async Task<PaginationResult<Frame>> GetAvailableFramesAsync(int currentPage = 1, int pageSize = 10)
        {
            Expression<Func<Frame, bool>> predicate = f => 
                f.Status != null && f.Status.ToLower() == AvailableStatus;

            return await _frameRepository.SearchWithPagingAsyncIncludeOrderBy(
                predicate,
                currentPage,
                pageSize,
                orderBy: f => f.FrameName,
                ascending: true,
                f => f.FrameMedia
            );
        }

        public async Task<Frame?> GetFrameByIdAsync(Guid frameId)
        {
            var frame = await _frameRepository.GetByIdAsyncInclude(
                frameId,
                f => f.FrameMedia
            );

            if (frame == null || frame.Status?.ToLower() != AvailableStatus)
            {
                return null;
            }

            return frame;
        }

        public async Task<List<FrameMedium>> GetFrameMediaAsync(Guid frameId)
        {
            var frame = await _frameRepository.GetByIdAsync(frameId);
            
            if (frame == null || frame.Status?.ToLower() != AvailableStatus)
            {
                return new List<FrameMedium>();
            }

            var media = await _frameMediaRepository.SearchAsync(m => m.FrameId == frameId);
            return media.ToList();
        }

        #endregion

        #region Manager Operations

        public async Task<PaginationResult<Frame>> GetAllFramesAsync(int currentPage = 1, int pageSize = 10)
        {
            // Returns all frames regardless of status (for managers)
            Expression<Func<Frame, bool>> predicate = f => true;

            return await _frameRepository.SearchWithPagingAsyncIncludeOrderBy(
                predicate,
                currentPage,
                pageSize,
                orderBy: f => f.FrameName,
                ascending: true,
                f => f.FrameMedia
            );
        }

        public async Task<Frame?> GetFrameByIdForManagerAsync(Guid frameId)
        {
            // Returns frame regardless of status (for managers)
            return await _frameRepository.GetByIdAsyncInclude(
                frameId,
                f => f.FrameMedia
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
            // Use SearchAsync to find the frame by ID - more reliable than FindAsync with Guid
            var frames = await _frameRepository.SearchAsync(f => f.FrameId == frameId);
            var existingFrame = frames.FirstOrDefault();
            
            if (existingFrame == null)
            {
                return null;
            }

            // Update only non-null properties
            if (!string.IsNullOrEmpty(updatedFrame.FrameName))
                existingFrame.FrameName = updatedFrame.FrameName;
            
            if (updatedFrame.Brand != null)
                existingFrame.Brand = updatedFrame.Brand;
            
            if (updatedFrame.Color != null)
                existingFrame.Color = updatedFrame.Color;
            
            if (updatedFrame.Material != null)
                existingFrame.Material = updatedFrame.Material;
            
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

            return await _frameRepository.UpdateAsync(existingFrame);
        }

        public async Task<bool> SoftDeleteFrameAsync(Guid frameId)
        {
            // Use SearchAsync to find the frame by ID - more reliable than FindAsync with Guid
            var frames = await _frameRepository.SearchAsync(f => f.FrameId == frameId);
            var frame = frames.FirstOrDefault();
            
            if (frame == null)
            {
                return false;
            }

            // Soft delete by setting status to inactive
            frame.Status = InactiveStatus;
            await _frameRepository.UpdateAsync(frame);
            
            return true;
        }

        #endregion

        #region Validation

        public FrameValidationResult ValidateFrameSizeAttributes(
            int? lensWidth, 
            int? bridgeWidth, 
            int? frameWidth, 
            int? templeLength)
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

            return result;
        }

        #endregion
    }
}
