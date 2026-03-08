using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repositories.Basic;
using Repositories.ModelExtensions;
using Repositories.Models;

namespace Services.GlassesService
{
    public interface ILensIndexService
    {
        Task<List<LensIndex>> GetAllLensIndicesAsync();
        Task<LensIndex?> GetLensIndexByIdAsync(Guid lensIndexId);
        Task<List<LensIndex>> GetCompatibleIndicesForPrescriptionAsync(double sphere);
        Task<LensIndex> CreateLensIndexAsync(LensIndex lensIndex);
        Task<LensIndex?> UpdateLensIndexAsync(Guid lensIndexId, LensIndex updatedLensIndex);
        Task<bool> SoftDeleteLensIndexAsync(Guid lensIndexId);
    }

    public class LensIndexService : ILensIndexService
    {
        private readonly GenericRepository<LensIndex> _lensIndexRepository;

        public LensIndexService(GenericRepository<LensIndex> lensIndexRepository)
        {
            _lensIndexRepository = lensIndexRepository;
        }

        public async Task<List<LensIndex>> GetAllLensIndicesAsync()
        {
            var indices = await _lensIndexRepository.SearchAsyncInclude(
                li => li.Status == "active",
                li => li.Brand,
                li => li.Color);
            return indices.OrderBy(li => li.IndexValue).ToList();
        }

        public async Task<LensIndex?> GetLensIndexByIdAsync(Guid lensIndexId)
        {
            var indices = await _lensIndexRepository.SearchAsyncInclude(
                li => li.LensIndexId == lensIndexId,
                li => li.Brand,
                li => li.Color);
            return indices.FirstOrDefault();
        }

        public async Task<List<LensIndex>> GetCompatibleIndicesForPrescriptionAsync(double sphere)
        {
            var allIndices = await _lensIndexRepository.SearchAsyncInclude(
                li => li.Status == "active",
                li => li.Brand,
                li => li.Color);
            return allIndices
                .Where(li =>
                    (!li.MinPrescription.HasValue || sphere >= li.MinPrescription.Value) &&
                    (!li.MaxPrescription.HasValue || sphere <= li.MaxPrescription.Value))
                .OrderBy(li => li.IndexValue)
                .ToList();
        }

        public async Task<LensIndex> CreateLensIndexAsync(LensIndex lensIndex)
        {
            lensIndex.LensIndexId = Guid.NewGuid();
            lensIndex.Status = "active";
            return await _lensIndexRepository.CreateAsync(lensIndex);
        }

        public async Task<LensIndex?> UpdateLensIndexAsync(Guid lensIndexId, LensIndex updatedLensIndex)
        {
            var existing = await GetLensIndexByIdAsync(lensIndexId);
            if (existing == null) return null;

            if (!string.IsNullOrEmpty(updatedLensIndex.Name))
                existing.Name = updatedLensIndex.Name;
            if (updatedLensIndex.Description != null)
                existing.Description = updatedLensIndex.Description;
            if (updatedLensIndex.IndexValue > 0)
                existing.IndexValue = updatedLensIndex.IndexValue;
            existing.AdditionalPrice = updatedLensIndex.AdditionalPrice;
            if (updatedLensIndex.MinPrescription.HasValue)
                existing.MinPrescription = updatedLensIndex.MinPrescription;
            if (updatedLensIndex.MaxPrescription.HasValue)
                existing.MaxPrescription = updatedLensIndex.MaxPrescription;
            if (updatedLensIndex.BrandId.HasValue)
                existing.BrandId = updatedLensIndex.BrandId;
            if (updatedLensIndex.ColorId.HasValue)
                existing.ColorId = updatedLensIndex.ColorId;

            return await _lensIndexRepository.UpdateAsync(existing);
        }

        public async Task<bool> SoftDeleteLensIndexAsync(Guid lensIndexId)
        {
            var existing = await GetLensIndexByIdAsync(lensIndexId);
            if (existing == null) return false;

            existing.Status = "inactive";
            await _lensIndexRepository.UpdateAsync(existing);
            return true;
        }
    }
}
