using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repositories.Basic;
using Repositories.Models;

namespace Services.GlassesService
{
    public interface IColorService
    {
        Task<List<Color>> GetAllColorsAsync();
        Task<Color?> GetColorByIdAsync(Guid colorId);
        Task<Color> CreateColorAsync(Color color);
        Task<Color?> UpdateColorAsync(Guid colorId, Color updatedColor);
        Task<bool> SoftDeleteColorAsync(Guid colorId);
    }

    public class ColorService : IColorService
    {
        private readonly GenericRepository<Color> _colorRepository;

        public ColorService(GenericRepository<Color> colorRepository)
        {
            _colorRepository = colorRepository;
        }

        public async Task<List<Color>> GetAllColorsAsync()
        {
            var colors = await _colorRepository.SearchAsync(c => c.Status == "active");
            return colors.ToList();
        }

        public async Task<Color?> GetColorByIdAsync(Guid colorId)
        {
            var colors = await _colorRepository.SearchAsync(c => c.ColorId == colorId);
            return colors.FirstOrDefault();
        }

        public async Task<Color> CreateColorAsync(Color color)
        {
            color.ColorId = Guid.NewGuid();
            color.Status = "active";
            return await _colorRepository.CreateAsync(color);
        }

        public async Task<Color?> UpdateColorAsync(Guid colorId, Color updatedColor)
        {
            var existing = await GetColorByIdAsync(colorId);
            if (existing == null) return null;

            if (!string.IsNullOrEmpty(updatedColor.ColorName))
                existing.ColorName = updatedColor.ColorName;
            if (updatedColor.HexCode != null)
                existing.HexCode = updatedColor.HexCode;

            return await _colorRepository.UpdateAsync(existing);
        }

        public async Task<bool> SoftDeleteColorAsync(Guid colorId)
        {
            var existing = await GetColorByIdAsync(colorId);
            if (existing == null) return false;

            existing.Status = "inactive";
            await _colorRepository.UpdateAsync(existing);
            return true;
        }
    }
}
