using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Repositories.Basic;
using Repositories.ModelExtensions;
using Repositories.Models;

namespace Services.GlassesService
{
    public class PriceValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public interface ILensFeatureService
    {
        // Read operations
        Task<List<LensFeature>> GetAllLensFeaturesAsync();
        Task<PaginationResult<LensFeature>> GetLensFeaturesAsync(int currentPage = 1, int pageSize = 10);
        Task<LensFeature?> GetLensFeatureByIdAsync(Guid featureId);
        Task<List<LensFeature>> GetLensFeaturesByIndexAsync(double lensIndex);

        // Price calculation helpers
        double CalculateTotalPrice(double basePrice, LensFeature? feature, LensType? lensType);
        double GetFeatureExtraPrice(LensFeature? feature);
        double GetLensTypeExtraPrice(LensType? lensType);

        // Price validation
        PriceValidationResult ValidatePrice(double? price);
        PriceValidationResult ValidateLensIndex(double? lensIndex);

        // Write operations (Manager only)
        Task<LensFeature> CreateLensFeatureAsync(LensFeature lensFeature);
        Task<LensFeature?> UpdateLensFeatureAsync(Guid featureId, LensFeature updatedFeature);
        Task<bool> CanDeleteLensFeatureAsync(Guid featureId);
        Task<bool> DeleteLensFeatureAsync(Guid featureId);
    }

    public class LensFeatureService : ILensFeatureService
    {
        private readonly GenericRepository<LensFeature> _lensFeatureRepository;
        private readonly GenericRepository<OrderItem> _orderItemRepository;
        private readonly GenericRepository<PreorderItem> _preorderItemRepository;

        // Price validation constants
        private const double MinPrice = 0;
        private const double MaxPrice = 10000;

        // Lens index validation constants (common lens indices)
        private static readonly double[] ValidLensIndices = { 1.50, 1.53, 1.56, 1.57, 1.59, 1.60, 1.67, 1.70, 1.74 };
        private const double MinLensIndex = 1.50;
        private const double MaxLensIndex = 1.74;

        public LensFeatureService(
            GenericRepository<LensFeature> lensFeatureRepository,
            GenericRepository<OrderItem> orderItemRepository,
            GenericRepository<PreorderItem> preorderItemRepository)
        {
            _lensFeatureRepository = lensFeatureRepository;
            _orderItemRepository = orderItemRepository;
            _preorderItemRepository = preorderItemRepository;
        }

        #region Read Operations

        public async Task<List<LensFeature>> GetAllLensFeaturesAsync()
        {
            return await _lensFeatureRepository.GetAllAsync();
        }

        public async Task<PaginationResult<LensFeature>> GetLensFeaturesAsync(int currentPage = 1, int pageSize = 10)
        {
            Expression<Func<LensFeature, bool>> predicate = lf => true;

            return await _lensFeatureRepository.SearchWithPagingAsyncIncludeOrderBy(
                predicate,
                currentPage,
                pageSize,
                orderBy: lf => lf.LensIndex,
                ascending: true
            );
        }

        public async Task<LensFeature?> GetLensFeatureByIdAsync(Guid featureId)
        {
            var features = await _lensFeatureRepository.SearchAsync(lf => lf.FeatureId == featureId);
            return features.FirstOrDefault();
        }

        public async Task<List<LensFeature>> GetLensFeaturesByIndexAsync(double lensIndex)
        {
            var features = await _lensFeatureRepository.SearchAsync(lf => lf.LensIndex == lensIndex);
            return features.ToList();
        }

        #endregion

        #region Price Calculation Helpers

        public double CalculateTotalPrice(double basePrice, LensFeature? feature, LensType? lensType)
        {
            var featurePrice = GetFeatureExtraPrice(feature);
            var lensTypePrice = GetLensTypeExtraPrice(lensType);

            return basePrice + featurePrice + lensTypePrice;
        }

        public double GetFeatureExtraPrice(LensFeature? feature)
        {
            return feature?.ExtraPrice ?? 0;
        }

        public double GetLensTypeExtraPrice(LensType? lensType)
        {
            return lensType?.ExtraPrice ?? 0;
        }

        #endregion

        #region Validation

        public PriceValidationResult ValidatePrice(double? price)
        {
            var result = new PriceValidationResult { IsValid = true };

            if (price.HasValue)
            {
                if (price < MinPrice)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Price cannot be negative");
                }

                if (price > MaxPrice)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Price cannot exceed {MaxPrice}");
                }
            }

            return result;
        }

        public PriceValidationResult ValidateLensIndex(double? lensIndex)
        {
            var result = new PriceValidationResult { IsValid = true };

            if (lensIndex.HasValue)
            {
                if (lensIndex < MinLensIndex || lensIndex > MaxLensIndex)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Lens index must be between {MinLensIndex} and {MaxLensIndex}");
                }
            }

            return result;
        }

        #endregion

        #region Write Operations

        public async Task<LensFeature> CreateLensFeatureAsync(LensFeature lensFeature)
        {
            lensFeature.FeatureId = Guid.NewGuid();
            return await _lensFeatureRepository.CreateAsync(lensFeature);
        }

        public async Task<LensFeature?> UpdateLensFeatureAsync(Guid featureId, LensFeature updatedFeature)
        {
            var existingFeature = await GetLensFeatureByIdAsync(featureId);

            if (existingFeature == null)
            {
                return null;
            }

            // Update properties if provided
            if (updatedFeature.LensIndex.HasValue)
                existingFeature.LensIndex = updatedFeature.LensIndex;

            if (!string.IsNullOrEmpty(updatedFeature.FeatureSpecification))
                existingFeature.FeatureSpecification = updatedFeature.FeatureSpecification;

            if (updatedFeature.ExtraPrice.HasValue)
                existingFeature.ExtraPrice = updatedFeature.ExtraPrice;

            return await _lensFeatureRepository.UpdateAsync(existingFeature);
        }

        public async Task<bool> CanDeleteLensFeatureAsync(Guid featureId)
        {
            // Check if feature is used in any orders
            var orderItems = await _orderItemRepository.SearchAsync(oi => oi.FeatureId == featureId);
            if (orderItems.Any())
            {
                return false;
            }

            // Check if feature is used in any preorders
            var preorderItems = await _preorderItemRepository.SearchAsync(pi => pi.FeatureId == featureId);
            if (preorderItems.Any())
            {
                return false;
            }

            return true;
        }

        public async Task<bool> DeleteLensFeatureAsync(Guid featureId)
        {
            // First check if we can delete
            if (!await CanDeleteLensFeatureAsync(featureId))
            {
                return false;
            }

            var feature = await GetLensFeatureByIdAsync(featureId);

            if (feature == null)
            {
                return false;
            }

            return await _lensFeatureRepository.DeleteAsync(feature);
        }

        #endregion
    }
}
