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
    public class PrescriptionValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public interface IPrescriptionService
    {
        // Create operations
        Task<Prescription> CreatePrescriptionAsync(Prescription prescription);
        PrescriptionValidationResult ValidatePrescription(Prescription prescription);

        // Read operations
        Task<Prescription?> GetPrescriptionByIdAsync(Guid prescriptionId);
        Task<List<Prescription>> GetPrescriptionsByUserAsync(Guid userId);
        Task<PaginationResult<Prescription>> GetPrescriptionsByUserAsync(Guid userId, int currentPage = 1, int pageSize = 10);
        Task<List<Prescription>> GetValidPrescriptionsByUserAsync(Guid userId);

        // Update operations
        Task<Prescription?> UpdatePrescriptionAsync(Guid prescriptionId, Prescription updatedPrescription, Guid userId);

        // Delete operations
        Task<bool> DeletePrescriptionAsync(Guid prescriptionId, Guid userId);
        Task<bool> CanDeletePrescriptionAsync(Guid prescriptionId);

        // Validation helpers
        bool IsPrescriptionExpired(Prescription prescription);
        bool IsPrescriptionValid(Prescription prescription);
        int GetDaysUntilExpiration(Prescription prescription);
    }

    public class PrescriptionService : IPrescriptionService
    {
        private readonly GenericRepository<Prescription> _prescriptionRepository;
        private readonly GenericRepository<OrderItem> _orderItemRepository;
        private readonly GenericRepository<PreorderItem> _preorderItemRepository;

        // Validation constants
        private const double MinSphere = -20.0;
        private const double MaxSphere = 20.0;
        private const double MinCylinder = -6.0;
        private const double MaxCylinder = 0.0;
        private const int MinAxis = 1;
        private const int MaxAxis = 180;
        private const double MinAdd = 0.75;
        private const double MaxAdd = 3.50;
        private const int MinPD = 50;
        private const int MaxPD = 80;
        private const int DefaultExpirationMonths = 24; // Prescriptions typically valid for 2 years

        public PrescriptionService(
            GenericRepository<Prescription> prescriptionRepository,
            GenericRepository<OrderItem> orderItemRepository,
            GenericRepository<PreorderItem> preorderItemRepository)
        {
            _prescriptionRepository = prescriptionRepository;
            _orderItemRepository = orderItemRepository;
            _preorderItemRepository = preorderItemRepository;
        }

        #region Create Operations

        public async Task<Prescription> CreatePrescriptionAsync(Prescription prescription)
        {
            prescription.PrescriptionId = Guid.NewGuid();
            prescription.CreatedAt = DateTime.UtcNow;

            // Set default expiration date if not provided (2 years from creation)
            if (!prescription.ExpirationDate.HasValue)
            {
                prescription.ExpirationDate = DateTime.UtcNow.AddMonths(DefaultExpirationMonths);
            }

            return await _prescriptionRepository.CreateAsync(prescription);
        }

        public PrescriptionValidationResult ValidatePrescription(Prescription prescription)
        {
            var result = new PrescriptionValidationResult { IsValid = true };

            // Validate Sphere values
            if (prescription.SphereLeft.HasValue)
            {
                if (prescription.SphereLeft < MinSphere || prescription.SphereLeft > MaxSphere)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Left sphere must be between {MinSphere} and {MaxSphere}");
                }
            }

            if (prescription.SphereRight.HasValue)
            {
                if (prescription.SphereRight < MinSphere || prescription.SphereRight > MaxSphere)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Right sphere must be between {MinSphere} and {MaxSphere}");
                }
            }

            // Validate Cylinder values
            if (prescription.CylinderLeft.HasValue)
            {
                if (prescription.CylinderLeft < MinCylinder || prescription.CylinderLeft > 0)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Left cylinder must be between {MinCylinder} and 0");
                }
            }

            if (prescription.CylinderRight.HasValue)
            {
                if (prescription.CylinderRight < MinCylinder || prescription.CylinderRight > 0)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Right cylinder must be between {MinCylinder} and 0");
                }
            }

            // Validate Axis values (required if cylinder is present)
            if (prescription.CylinderLeft.HasValue && prescription.CylinderLeft != 0)
            {
                if (!prescription.AxisLeft.HasValue)
                {
                    result.IsValid = false;
                    result.Errors.Add("Left axis is required when cylinder is specified");
                }
                else if (prescription.AxisLeft < MinAxis || prescription.AxisLeft > MaxAxis)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Left axis must be between {MinAxis} and {MaxAxis}");
                }
            }

            if (prescription.CylinderRight.HasValue && prescription.CylinderRight != 0)
            {
                if (!prescription.AxisRight.HasValue)
                {
                    result.IsValid = false;
                    result.Errors.Add("Right axis is required when cylinder is specified");
                }
                else if (prescription.AxisRight < MinAxis || prescription.AxisRight > MaxAxis)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Right axis must be between {MinAxis} and {MaxAxis}");
                }
            }

            // Validate Add values (for progressive/bifocal lenses)
            if (prescription.AddLeft.HasValue)
            {
                if (prescription.AddLeft < MinAdd || prescription.AddLeft > MaxAdd)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Left add must be between {MinAdd} and {MaxAdd}");
                }
            }

            if (prescription.AddRight.HasValue)
            {
                if (prescription.AddRight < MinAdd || prescription.AddRight > MaxAdd)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Right add must be between {MinAdd} and {MaxAdd}");
                }
            }

            // Validate Pupillary Distance
            if (prescription.PupillaryDistance.HasValue)
            {
                if (prescription.PupillaryDistance < MinPD || prescription.PupillaryDistance > MaxPD)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Pupillary distance must be between {MinPD}mm and {MaxPD}mm");
                }
            }

            // Validate expiration date
            if (prescription.ExpirationDate.HasValue && prescription.ExpirationDate < DateTime.UtcNow)
            {
                result.IsValid = false;
                result.Errors.Add("Prescription expiration date cannot be in the past");
            }

            return result;
        }

        #endregion

        #region Read Operations

        public async Task<Prescription?> GetPrescriptionByIdAsync(Guid prescriptionId)
        {
            var prescriptions = await _prescriptionRepository.SearchAsync(p => p.PrescriptionId == prescriptionId);
            return prescriptions.FirstOrDefault();
        }

        public async Task<List<Prescription>> GetPrescriptionsByUserAsync(Guid userId)
        {
            var prescriptions = await _prescriptionRepository.SearchAsyncOrderBy(
                p => p.UserId == userId,
                orderBy: p => p.CreatedAt,
                ascending: false);
            return prescriptions.ToList();
        }

        public async Task<PaginationResult<Prescription>> GetPrescriptionsByUserAsync(Guid userId, int currentPage = 1, int pageSize = 10)
        {
            return await _prescriptionRepository.SearchWithPagingAsyncIncludeOrderBy(
                p => p.UserId == userId,
                currentPage,
                pageSize,
                orderBy: p => p.CreatedAt,
                ascending: false
            );
        }

        public async Task<List<Prescription>> GetValidPrescriptionsByUserAsync(Guid userId)
        {
            var prescriptions = await _prescriptionRepository.SearchAsync(
                p => p.UserId == userId &&
                     (p.ExpirationDate == null || p.ExpirationDate > DateTime.UtcNow));
            
            return prescriptions.OrderByDescending(p => p.CreatedAt).ToList();
        }

        #endregion

        #region Update Operations

        public async Task<Prescription?> UpdatePrescriptionAsync(Guid prescriptionId, Prescription updatedPrescription, Guid userId)
        {
            var existingPrescription = await GetPrescriptionByIdAsync(prescriptionId);

            if (existingPrescription == null)
            {
                return null;
            }

            // Verify ownership
            if (existingPrescription.UserId != userId)
            {
                return null;
            }

            // Update properties if provided
            if (updatedPrescription.SphereLeft.HasValue)
                existingPrescription.SphereLeft = updatedPrescription.SphereLeft;

            if (updatedPrescription.SphereRight.HasValue)
                existingPrescription.SphereRight = updatedPrescription.SphereRight;

            if (updatedPrescription.CylinderLeft.HasValue)
                existingPrescription.CylinderLeft = updatedPrescription.CylinderLeft;

            if (updatedPrescription.CylinderRight.HasValue)
                existingPrescription.CylinderRight = updatedPrescription.CylinderRight;

            if (updatedPrescription.AxisLeft.HasValue)
                existingPrescription.AxisLeft = updatedPrescription.AxisLeft;

            if (updatedPrescription.AxisRight.HasValue)
                existingPrescription.AxisRight = updatedPrescription.AxisRight;

            if (updatedPrescription.AddLeft.HasValue)
                existingPrescription.AddLeft = updatedPrescription.AddLeft;

            if (updatedPrescription.AddRight.HasValue)
                existingPrescription.AddRight = updatedPrescription.AddRight;

            if (updatedPrescription.PupillaryDistance.HasValue)
                existingPrescription.PupillaryDistance = updatedPrescription.PupillaryDistance;

            if (!string.IsNullOrEmpty(updatedPrescription.DoctorName))
                existingPrescription.DoctorName = updatedPrescription.DoctorName;

            if (!string.IsNullOrEmpty(updatedPrescription.ClinicName))
                existingPrescription.ClinicName = updatedPrescription.ClinicName;

            if (updatedPrescription.ExpirationDate.HasValue)
                existingPrescription.ExpirationDate = updatedPrescription.ExpirationDate;

            return await _prescriptionRepository.UpdateAsync(existingPrescription);
        }

        #endregion

        #region Delete Operations

        public async Task<bool> CanDeletePrescriptionAsync(Guid prescriptionId)
        {
            // Check if prescription is used in any orders
            var orderItems = await _orderItemRepository.SearchAsync(oi => oi.PrescriptionId == prescriptionId);
            if (orderItems.Any())
            {
                return false;
            }

            // Check if prescription is used in any preorders
            var preorderItems = await _preorderItemRepository.SearchAsync(pi => pi.PrescriptionId == prescriptionId);
            if (preorderItems.Any())
            {
                return false;
            }

            return true;
        }

        public async Task<bool> DeletePrescriptionAsync(Guid prescriptionId, Guid userId)
        {
            var prescription = await GetPrescriptionByIdAsync(prescriptionId);

            if (prescription == null)
            {
                return false;
            }

            // Verify ownership
            if (prescription.UserId != userId)
            {
                return false;
            }

            // Check if can delete
            if (!await CanDeletePrescriptionAsync(prescriptionId))
            {
                return false;
            }

            return await _prescriptionRepository.DeleteAsync(prescription);
        }

        #endregion

        #region Validation Helpers

        public bool IsPrescriptionExpired(Prescription prescription)
        {
            if (!prescription.ExpirationDate.HasValue)
            {
                return false; // No expiration date means not expired
            }

            return prescription.ExpirationDate < DateTime.UtcNow;
        }

        public bool IsPrescriptionValid(Prescription prescription)
        {
            // Check if not expired
            if (IsPrescriptionExpired(prescription))
            {
                return false;
            }

            // Check if has at least sphere values
            if (!prescription.SphereLeft.HasValue && !prescription.SphereRight.HasValue)
            {
                return false;
            }

            return true;
        }

        public int GetDaysUntilExpiration(Prescription prescription)
        {
            if (!prescription.ExpirationDate.HasValue)
            {
                return int.MaxValue; // No expiration
            }

            var daysUntilExpiration = (prescription.ExpirationDate.Value - DateTime.UtcNow).Days;
            return daysUntilExpiration < 0 ? 0 : daysUntilExpiration;
        }

        #endregion
    }
}
