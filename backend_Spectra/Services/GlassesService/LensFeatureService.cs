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

        // Price calculation helpers
        double CalculateTotalPrice(double frameBasePrice, LensType? lensType, LensFeature? feature, LensIndex? lensIndex);
        double GetFeatureExtraPrice(LensFeature? feature);
        double GetLensTypeBasePrice(LensType? lensType);
        double GetLensIndexAdditionalPrice(LensIndex? lensIndex);

        // Price validation
        PriceValidationResult ValidatePrice(double? price);

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

        private const double MinPrice = 0;
        private const double MaxPrice = 10000;

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
                orderBy: lf => lf.FeatureSpecification,
                ascending: true
            );
        }

        public async Task<LensFeature?> GetLensFeatureByIdAsync(Guid featureId)
        {
            var features = await _lensFeatureRepository.SearchAsync(lf => lf.FeatureId == featureId);
            return features.FirstOrDefault();
        }

        #endregion

        #region Price Calculation Helpers

        public double CalculateTotalPrice(double frameBasePrice, LensType? lensType, LensFeature? feature, LensIndex? lensIndex)
        {
            return frameBasePrice + GetLensTypeBasePrice(lensType) + GetFeatureExtraPrice(feature) + GetLensIndexAdditionalPrice(lensIndex);
        }

        public double GetFeatureExtraPrice(LensFeature? feature)
        {
            return feature?.ExtraPrice ?? 0;
        }

        public double GetLensTypeBasePrice(LensType? lensType)
        {
            return lensType?.BasePrice ?? 0;
        }

        public double GetLensIndexAdditionalPrice(LensIndex? lensIndex)
        {
            return lensIndex?.AdditionalPrice ?? 0;
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

            if (!string.IsNullOrEmpty(updatedFeature.FeatureSpecification))
                existingFeature.FeatureSpecification = updatedFeature.FeatureSpecification;

            if (updatedFeature.ExtraPrice.HasValue)
                existingFeature.ExtraPrice = updatedFeature.ExtraPrice;

            return await _lensFeatureRepository.UpdateAsync(existingFeature);
        }

        public async Task<bool> CanDeleteLensFeatureAsync(Guid featureId)
        {
            var orderItems = await _orderItemRepository.SearchAsync(oi => oi.FeatureId == featureId);
            if (orderItems.Any()) return false;

            var preorderItems = await _preorderItemRepository.SearchAsync(pi => pi.FeatureId == featureId);
            if (preorderItems.Any()) return false;

            return true;
        }

        public async Task<bool> DeleteLensFeatureAsync(Guid featureId)
        {
            if (!await CanDeleteLensFeatureAsync(featureId))
                return false;

            var feature = await GetLensFeatureByIdAsync(featureId);
            if (feature == null) return false;

            return await _lensFeatureRepository.DeleteAsync(feature);
        }

        #endregion
    }
}
