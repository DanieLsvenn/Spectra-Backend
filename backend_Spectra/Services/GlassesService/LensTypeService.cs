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
    public interface ILensTypeService
    {
        // Read operations
        Task<List<LensType>> GetAllLensTypesAsync();
        Task<PaginationResult<LensType>> GetLensTypesAsync(int currentPage = 1, int pageSize = 10);
        Task<LensType?> GetLensTypeByIdAsync(Guid lensTypeId);
        Task<List<LensType>> GetLensTypesRequiringPrescriptionAsync();
        Task<List<LensType>> GetLensTypesNotRequiringPrescriptionAsync();
        bool RequiresPrescription(LensType lensType);

        // Write operations (Manager only)
        Task<LensType> CreateLensTypeAsync(LensType lensType);
        Task<LensType?> UpdateLensTypeAsync(Guid lensTypeId, LensType updatedLensType);
        Task<bool> DisableLensTypeAsync(Guid lensTypeId);
        Task<bool> CanDeleteLensTypeAsync(Guid lensTypeId);
        Task<bool> DeleteLensTypeAsync(Guid lensTypeId);
    }

    public class LensTypeService : ILensTypeService
    {
        private readonly GenericRepository<LensType> _lensTypeRepository;
        private readonly GenericRepository<OrderItem> _orderItemRepository;
        private readonly GenericRepository<PreorderItem> _preorderItemRepository;

        public LensTypeService(
            GenericRepository<LensType> lensTypeRepository,
            GenericRepository<OrderItem> orderItemRepository,
            GenericRepository<PreorderItem> preorderItemRepository)
        {
            _lensTypeRepository = lensTypeRepository;
            _orderItemRepository = orderItemRepository;
            _preorderItemRepository = preorderItemRepository;
        }

        #region Read Operations

        public async Task<List<LensType>> GetAllLensTypesAsync()
        {
            return await _lensTypeRepository.GetAllAsync();
        }

        public async Task<PaginationResult<LensType>> GetLensTypesAsync(int currentPage = 1, int pageSize = 10)
        {
            Expression<Func<LensType, bool>> predicate = lt => true;

            return await _lensTypeRepository.SearchWithPagingAsyncIncludeOrderBy(
                predicate,
                currentPage,
                pageSize,
                orderBy: lt => lt.LensSpecification,
                ascending: true
            );
        }

        public async Task<LensType?> GetLensTypeByIdAsync(Guid lensTypeId)
        {
            var lensTypes = await _lensTypeRepository.SearchAsync(lt => lt.LensTypeId == lensTypeId);
            return lensTypes.FirstOrDefault();
        }

        public async Task<List<LensType>> GetLensTypesRequiringPrescriptionAsync()
        {
            var lensTypes = await _lensTypeRepository.SearchAsync(lt => lt.RequiresPrescription == true);
            return lensTypes.ToList();
        }

        public async Task<List<LensType>> GetLensTypesNotRequiringPrescriptionAsync()
        {
            var lensTypes = await _lensTypeRepository.SearchAsync(lt => lt.RequiresPrescription == false || lt.RequiresPrescription == null);
            return lensTypes.ToList();
        }

        public bool RequiresPrescription(LensType lensType)
        {
            return lensType.RequiresPrescription == true;
        }

        #endregion

        #region Write Operations

        public async Task<LensType> CreateLensTypeAsync(LensType lensType)
        {
            lensType.LensTypeId = Guid.NewGuid();
            if (string.IsNullOrEmpty(lensType.Status))
                lensType.Status = "active";
            return await _lensTypeRepository.CreateAsync(lensType);
        }

        public async Task<LensType?> UpdateLensTypeAsync(Guid lensTypeId, LensType updatedLensType)
        {
            var existingLensType = await GetLensTypeByIdAsync(lensTypeId);

            if (existingLensType == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(updatedLensType.LensSpecification))
                existingLensType.LensSpecification = updatedLensType.LensSpecification;

            if (updatedLensType.RequiresPrescription.HasValue)
                existingLensType.RequiresPrescription = updatedLensType.RequiresPrescription;

            if (updatedLensType.BasePrice.HasValue)
                existingLensType.BasePrice = updatedLensType.BasePrice;

            if (updatedLensType.Description != null)
                existingLensType.Description = updatedLensType.Description;

            if (updatedLensType.Category != null)
                existingLensType.Category = updatedLensType.Category;

            if (updatedLensType.BrandId.HasValue)
                existingLensType.BrandId = updatedLensType.BrandId;

            if (updatedLensType.MaterialId.HasValue)
                existingLensType.MaterialId = updatedLensType.MaterialId;

            if (updatedLensType.ColorId.HasValue)
                existingLensType.ColorId = updatedLensType.ColorId;

            return await _lensTypeRepository.UpdateAsync(existingLensType);
        }

        public async Task<bool> DisableLensTypeAsync(Guid lensTypeId)
        {
            var lensType = await GetLensTypeByIdAsync(lensTypeId);
            if (lensType == null) return false;

            lensType.Status = "disabled";
            await _lensTypeRepository.UpdateAsync(lensType);
            return true;
        }

        public async Task<bool> CanDeleteLensTypeAsync(Guid lensTypeId)
        {
            var orderItems = await _orderItemRepository.SearchAsync(oi => oi.LensTypeId == lensTypeId);
            if (orderItems.Any()) return false;

            var preorderItems = await _preorderItemRepository.SearchAsync(pi => pi.LensTypeId == lensTypeId);
            if (preorderItems.Any()) return false;

            return true;
        }

        public async Task<bool> DeleteLensTypeAsync(Guid lensTypeId)
        {
            if (!await CanDeleteLensTypeAsync(lensTypeId))
                return false;

            var lensType = await GetLensTypeByIdAsync(lensTypeId);
            if (lensType == null) return false;

            return await _lensTypeRepository.DeleteAsync(lensType);
        }

        #endregion
    }
}
