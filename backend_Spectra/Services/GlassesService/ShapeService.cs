using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repositories.Basic;
using Repositories.Models;

namespace Services.GlassesService
{
    public interface IShapeService
    {
        Task<List<Shape>> GetAllShapesAsync();
        Task<Shape?> GetShapeByIdAsync(Guid shapeId);
        Task<Shape> CreateShapeAsync(Shape shape);
        Task<Shape?> UpdateShapeAsync(Guid shapeId, Shape updatedShape);
        Task<bool> SoftDeleteShapeAsync(Guid shapeId);
    }

    public class ShapeService : IShapeService
    {
        private readonly GenericRepository<Shape> _shapeRepository;

        public ShapeService(GenericRepository<Shape> shapeRepository)
        {
            _shapeRepository = shapeRepository;
        }

        public async Task<List<Shape>> GetAllShapesAsync()
        {
            var shapes = await _shapeRepository.SearchAsync(s => s.Status == "active");
            return shapes.ToList();
        }

        public async Task<Shape?> GetShapeByIdAsync(Guid shapeId)
        {
            var shapes = await _shapeRepository.SearchAsync(s => s.ShapeId == shapeId);
            return shapes.FirstOrDefault();
        }

        public async Task<Shape> CreateShapeAsync(Shape shape)
        {
            shape.ShapeId = Guid.NewGuid();
            shape.Status = "active";
            return await _shapeRepository.CreateAsync(shape);
        }

        public async Task<Shape?> UpdateShapeAsync(Guid shapeId, Shape updatedShape)
        {
            var existing = await GetShapeByIdAsync(shapeId);
            if (existing == null) return null;

            if (!string.IsNullOrEmpty(updatedShape.ShapeName))
                existing.ShapeName = updatedShape.ShapeName;

            return await _shapeRepository.UpdateAsync(existing);
        }

        public async Task<bool> SoftDeleteShapeAsync(Guid shapeId)
        {
            var existing = await GetShapeByIdAsync(shapeId);
            if (existing == null) return false;

            existing.Status = "inactive";
            await _shapeRepository.UpdateAsync(existing);
            return true;
        }
    }
}
