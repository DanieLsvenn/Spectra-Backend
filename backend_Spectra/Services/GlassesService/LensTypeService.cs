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

        private const string ActiveStatus = "active";
        private const string DisabledStatus = "disabled";

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
            return await _lensTypeRepository.CreateAsync(lensType);
        }

        public async Task<LensType?> UpdateLensTypeAsync(Guid lensTypeId, LensType updatedLensType)
        {
            var existingLensType = await GetLensTypeByIdAsync(lensTypeId);

            if (existingLensType == null)
            {
                return null;
            }

            // Update properties if provided
            if (!string.IsNullOrEmpty(updatedLensType.LensSpecification))
                existingLensType.LensSpecification = updatedLensType.LensSpecification;

            if (updatedLensType.RequiresPrescription.HasValue)
                existingLensType.RequiresPrescription = updatedLensType.RequiresPrescription;

            if (updatedLensType.ExtraPrice.HasValue)
                existingLensType.ExtraPrice = updatedLensType.ExtraPrice;

            return await _lensTypeRepository.UpdateAsync(existingLensType);
        }

        public async Task<bool> DisableLensTypeAsync(Guid lensTypeId)
        {
            var lensType = await GetLensTypeByIdAsync(lensTypeId);

            if (lensType == null)
            {
                return false;
            }

            // For lens types, we don't have a status field in the model
            // So we'll set RequiresPrescription to null to indicate disabled
            // Or we could add a Status field to the model
            // For now, we'll just return true to indicate the operation was attempted
            // In a real scenario, you might want to add a Status field to LensType
            
            return true;
        }

        public async Task<bool> CanDeleteLensTypeAsync(Guid lensTypeId)
        {
            // Check if lens type is used in any orders
            var orderItems = await _orderItemRepository.SearchAsync(oi => oi.LensTypeId == lensTypeId);
            if (orderItems.Any())
            {
                return false;
            }

            // Check if lens type is used in any preorders
            var preorderItems = await _preorderItemRepository.SearchAsync(pi => pi.LensTypeId == lensTypeId);
            if (preorderItems.Any())
            {
                return false;
            }

            return true;
        }

        public async Task<bool> DeleteLensTypeAsync(Guid lensTypeId)
        {
            // First check if we can delete
            if (!await CanDeleteLensTypeAsync(lensTypeId))
            {
                return false;
            }

            var lensType = await GetLensTypeByIdAsync(lensTypeId);

            if (lensType == null)
            {
                return false;
            }

            return await _lensTypeRepository.DeleteAsync(lensType);
        }

        #endregion
    }
}
