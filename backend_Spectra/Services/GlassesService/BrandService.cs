using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repositories.Basic;
using Repositories.ModelExtensions;
using Repositories.Models;

namespace Services.GlassesService
{
    public interface IBrandService
    {
        Task<List<Brand>> GetAllBrandsAsync();
        Task<Brand?> GetBrandByIdAsync(Guid brandId);
        Task<Brand> CreateBrandAsync(Brand brand);
        Task<Brand?> UpdateBrandAsync(Guid brandId, Brand updatedBrand);
        Task<bool> SoftDeleteBrandAsync(Guid brandId);
    }

    public class BrandService : IBrandService
    {
        private readonly GenericRepository<Brand> _brandRepository;

        public BrandService(GenericRepository<Brand> brandRepository)
        {
            _brandRepository = brandRepository;
        }

        public async Task<List<Brand>> GetAllBrandsAsync()
        {
            var brands = await _brandRepository.SearchAsync(b => b.Status == "active");
            return brands.ToList();
        }

        public async Task<Brand?> GetBrandByIdAsync(Guid brandId)
        {
            var brands = await _brandRepository.SearchAsync(b => b.BrandId == brandId);
            return brands.FirstOrDefault();
        }

        public async Task<Brand> CreateBrandAsync(Brand brand)
        {
            brand.BrandId = Guid.NewGuid();
            brand.Status = "active";
            return await _brandRepository.CreateAsync(brand);
        }

        public async Task<Brand?> UpdateBrandAsync(Guid brandId, Brand updatedBrand)
        {
            var existing = await GetBrandByIdAsync(brandId);
            if (existing == null) return null;

            if (!string.IsNullOrEmpty(updatedBrand.BrandName))
                existing.BrandName = updatedBrand.BrandName;

            return await _brandRepository.UpdateAsync(existing);
        }

        public async Task<bool> SoftDeleteBrandAsync(Guid brandId)
        {
            var existing = await GetBrandByIdAsync(brandId);
            if (existing == null) return false;

            existing.Status = "inactive";
            await _brandRepository.UpdateAsync(existing);
            return true;
        }
    }
}
