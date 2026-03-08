using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repositories.Basic;
using Repositories.Models;

namespace Services.GlassesService
{
    public interface IMaterialService
    {
        Task<List<Material>> GetAllMaterialsAsync();
        Task<Material?> GetMaterialByIdAsync(Guid materialId);
        Task<Material> CreateMaterialAsync(Material material);
        Task<Material?> UpdateMaterialAsync(Guid materialId, Material updatedMaterial);
        Task<bool> SoftDeleteMaterialAsync(Guid materialId);
    }

    public class MaterialService : IMaterialService
    {
        private readonly GenericRepository<Material> _materialRepository;

        public MaterialService(GenericRepository<Material> materialRepository)
        {
            _materialRepository = materialRepository;
        }

        public async Task<List<Material>> GetAllMaterialsAsync()
        {
            var materials = await _materialRepository.SearchAsync(m => m.Status == "active");
            return materials.ToList();
        }

        public async Task<Material?> GetMaterialByIdAsync(Guid materialId)
        {
            var materials = await _materialRepository.SearchAsync(m => m.MaterialId == materialId);
            return materials.FirstOrDefault();
        }

        public async Task<Material> CreateMaterialAsync(Material material)
        {
            material.MaterialId = Guid.NewGuid();
            material.Status = "active";
            return await _materialRepository.CreateAsync(material);
        }

        public async Task<Material?> UpdateMaterialAsync(Guid materialId, Material updatedMaterial)
        {
            var existing = await GetMaterialByIdAsync(materialId);
            if (existing == null) return null;

            if (!string.IsNullOrEmpty(updatedMaterial.MaterialName))
                existing.MaterialName = updatedMaterial.MaterialName;

            return await _materialRepository.UpdateAsync(existing);
        }

        public async Task<bool> SoftDeleteMaterialAsync(Guid materialId)
        {
            var existing = await GetMaterialByIdAsync(materialId);
            if (existing == null) return false;

            existing.Status = "inactive";
            await _materialRepository.UpdateAsync(existing);
            return true;
        }
    }
}
