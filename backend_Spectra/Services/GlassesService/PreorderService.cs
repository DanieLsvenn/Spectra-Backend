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
    public interface IPreorderService
    {
        // Create operations
        Task<Preorder> CreatePreorderAsync(Preorder preorder, List<PreorderItem> preorderItems);
        Task<OrderValidationResult> ValidatePreorderItemsAsync(List<PreorderItem> preorderItems, Guid userId);

        // Read operations
        Task<Preorder?> GetPreorderByIdAsync(Guid preorderId);
        Task<Preorder?> GetPreorderByIdWithDetailsAsync(Guid preorderId);
        Task<List<Preorder>> GetPreordersByUserAsync(Guid userId);
        Task<PaginationResult<Preorder>> GetPreordersByUserAsync(Guid userId, int currentPage = 1, int pageSize = 10);
        Task<PaginationResult<Preorder>> GetAllPreordersAsync(int currentPage = 1, int pageSize = 10);

        // Update operations
        Task<Preorder?> UpdatePreorderStatusAsync(Guid preorderId, string newStatus, string userRole);
        Task<bool> CancelPreorderAsync(Guid preorderId);

        // Conversion
        Task<Order?> ConvertPreorderToOrderAsync(Guid preorderId, string shippingAddress);
        Task<bool> CanConvertToOrderAsync(Guid preorderId);

        // Price calculation
        Task<double> CalculatePreorderTotalAsync(List<PreorderItem> preorderItems);
    }

    public class PreorderService : IPreorderService
    {
        private readonly GenericRepository<Preorder> _preorderRepository;
        private readonly GenericRepository<PreorderItem> _preorderItemRepository;
        private readonly GenericRepository<Order> _orderRepository;
        private readonly GenericRepository<OrderItem> _orderItemRepository;
        private readonly GenericRepository<Frame> _frameRepository;
        private readonly GenericRepository<LensType> _lensTypeRepository;
        private readonly GenericRepository<LensFeature> _lensFeatureRepository;
        private readonly GenericRepository<Prescription> _prescriptionRepository;
        private readonly GenericRepository<Payment> _paymentRepository;

        // Preorder statuses
        public static class PreorderStatus
        {
            public const string Pending = "pending";
            public const string Confirmed = "confirmed";
            public const string Paid = "paid";
            public const string ConvertedToOrder = "converted";
            public const string Cancelled = "cancelled";
        }

        public PreorderService(
            GenericRepository<Preorder> preorderRepository,
            GenericRepository<PreorderItem> preorderItemRepository,
            GenericRepository<Order> orderRepository,
            GenericRepository<OrderItem> orderItemRepository,
            GenericRepository<Frame> frameRepository,
            GenericRepository<LensType> lensTypeRepository,
            GenericRepository<LensFeature> lensFeatureRepository,
            GenericRepository<Prescription> prescriptionRepository,
            GenericRepository<Payment> paymentRepository)
        {
            _preorderRepository = preorderRepository;
            _preorderItemRepository = preorderItemRepository;
            _orderRepository = orderRepository;
            _orderItemRepository = orderItemRepository;
            _frameRepository = frameRepository;
            _lensTypeRepository = lensTypeRepository;
            _lensFeatureRepository = lensFeatureRepository;
            _prescriptionRepository = prescriptionRepository;
            _paymentRepository = paymentRepository;
        }

        #region Create Operations

        public async Task<Preorder> CreatePreorderAsync(Preorder preorder, List<PreorderItem> preorderItems)
        {
            preorder.PreorderId = Guid.NewGuid();
            preorder.Status = PreorderStatus.Pending;
            preorder.CreatedAt = DateTime.UtcNow;

            // Set expected date (default 14 days from now if not specified)
            if (!preorder.ExpectedDate.HasValue)
            {
                preorder.ExpectedDate = DateTime.UtcNow.AddDays(14);
            }

            var createdPreorder = await _preorderRepository.CreateAsync(preorder);

            // Create preorder items
            foreach (var item in preorderItems)
            {
                item.PreorderItemId = Guid.NewGuid();
                item.PreorderId = createdPreorder.PreorderId;
                item.PreorderPrice = await CalculateItemPriceAsync(item);
                await _preorderItemRepository.CreateAsync(item);
            }

            return await GetPreorderByIdWithDetailsAsync(createdPreorder.PreorderId) ?? createdPreorder;
        }

        public async Task<OrderValidationResult> ValidatePreorderItemsAsync(List<PreorderItem> preorderItems, Guid userId)
        {
            var result = new OrderValidationResult { IsValid = true };

            if (preorderItems == null || !preorderItems.Any())
            {
                result.IsValid = false;
                result.Errors.Add("Preorder must contain at least one item");
                return result;
            }

            foreach (var item in preorderItems)
            {
                // Validate frame exists
                if (item.FrameId.HasValue)
                {
                    var frames = await _frameRepository.SearchAsync(f => f.FrameId == item.FrameId);
                    var frame = frames.FirstOrDefault();

                    if (frame == null)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Frame with ID {item.FrameId} not found");
                        continue;
                    }

                    // For preorders, frame doesn't need to be available (it's a preorder)
                    // But validate selectedColor when frame color is NULL
                    if (string.IsNullOrEmpty(frame.Color) && string.IsNullOrEmpty(item.SelectedColor))
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Frame '{frame.FrameName}' requires a color selection");
                    }
                }
                else
                {
                    result.IsValid = false;
                    result.Errors.Add("Each preorder item must have a frame");
                }

                // Validate lens type and prescription requirement
                if (item.LensTypeId.HasValue)
                {
                    var lensTypes = await _lensTypeRepository.SearchAsync(lt => lt.LensTypeId == item.LensTypeId);
                    var lensType = lensTypes.FirstOrDefault();

                    if (lensType == null)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Lens type with ID {item.LensTypeId} not found");
                        continue;
                    }

                    if (lensType.RequiresPrescription == true && !item.PrescriptionId.HasValue)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Lens type '{lensType.LensSpecification}' requires a prescription");
                    }
                }

                // Validate prescription if provided
                if (item.PrescriptionId.HasValue)
                {
                    var prescriptions = await _prescriptionRepository.SearchAsync(p => p.PrescriptionId == item.PrescriptionId);
                    var prescription = prescriptions.FirstOrDefault();

                    if (prescription == null)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Prescription with ID {item.PrescriptionId} not found");
                    }
                    else if (prescription.UserId != userId)
                    {
                        result.IsValid = false;
                        result.Errors.Add("Prescription does not belong to the current user");
                    }
                }

                // Validate quantity
                if (!item.Quantity.HasValue || item.Quantity <= 0)
                {
                    result.IsValid = false;
                    result.Errors.Add("Each preorder item must have a quantity greater than 0");
                }
            }

            return result;
        }

        #endregion

        #region Read Operations

        public async Task<Preorder?> GetPreorderByIdAsync(Guid preorderId)
        {
            var preorders = await _preorderRepository.SearchAsync(p => p.PreorderId == preorderId);
            return preorders.FirstOrDefault();
        }

        public async Task<Preorder?> GetPreorderByIdWithDetailsAsync(Guid preorderId)
        {
            var preorders = await _preorderRepository.SearchAsyncInclude(
                p => p.PreorderId == preorderId,
                p => p.PreorderItems,
                p => p.User,
                p => p.Payments
            );

            var preorder = preorders.FirstOrDefault();

            if (preorder != null)
            {
                foreach (var item in preorder.PreorderItems)
                {
                    if (item.FrameId.HasValue)
                    {
                        var frames = await _frameRepository.SearchAsync(f => f.FrameId == item.FrameId);
                        item.Frame = frames.FirstOrDefault();
                    }
                    if (item.LensTypeId.HasValue)
                    {
                        var lensTypes = await _lensTypeRepository.SearchAsync(lt => lt.LensTypeId == item.LensTypeId);
                        item.LensType = lensTypes.FirstOrDefault();
                    }
                    if (item.FeatureId.HasValue)
                    {
                        var features = await _lensFeatureRepository.SearchAsync(f => f.FeatureId == item.FeatureId);
                        item.Feature = features.FirstOrDefault();
                    }
                    if (item.PrescriptionId.HasValue)
                    {
                        var prescriptions = await _prescriptionRepository.SearchAsync(p => p.PrescriptionId == item.PrescriptionId);
                        item.Prescription = prescriptions.FirstOrDefault();
                    }
                }
            }

            return preorder;
        }

        public async Task<List<Preorder>> GetPreordersByUserAsync(Guid userId)
        {
            var preorders = await _preorderRepository.SearchAsyncInclude(
                p => p.UserId == userId,
                p => p.PreorderItems,
                p => p.Payments
            );
            return preorders.ToList();
        }

        public async Task<PaginationResult<Preorder>> GetPreordersByUserAsync(Guid userId, int currentPage = 1, int pageSize = 10)
        {
            return await _preorderRepository.SearchWithPagingAsyncIncludeOrderBy(
                p => p.UserId == userId,
                currentPage,
                pageSize,
                orderBy: p => p.CreatedAt,
                ascending: false,
                p => p.PreorderItems,
                p => p.Payments
            );
        }

        public async Task<PaginationResult<Preorder>> GetAllPreordersAsync(int currentPage = 1, int pageSize = 10)
        {
            return await _preorderRepository.SearchWithPagingAsyncIncludeOrderBy(
                p => true,
                currentPage,
                pageSize,
                orderBy: p => p.CreatedAt,
                ascending: false,
                p => p.PreorderItems,
                p => p.User,
                p => p.Payments
            );
        }

        #endregion

        #region Update Operations

        public async Task<Preorder?> UpdatePreorderStatusAsync(Guid preorderId, string newStatus, string userRole)
        {
            var preorder = await GetPreorderByIdAsync(preorderId);

            if (preorder == null)
            {
                return null;
            }

            // Only manager/admin/staff can update status
            var allowedRoles = new[] { "manager", "admin", "staff" };
            if (!allowedRoles.Contains(userRole.ToLower()))
            {
                return null;
            }

            preorder.Status = newStatus.ToLower();
            return await _preorderRepository.UpdateAsync(preorder);
        }

        public async Task<bool> CancelPreorderAsync(Guid preorderId)
        {
            var preorder = await GetPreorderByIdAsync(preorderId);

            if (preorder == null)
            {
                return false;
            }

            // Check if preorder has been paid
            var payments = await _paymentRepository.SearchAsync(p =>
                p.PreorderId == preorderId &&
                p.PaymentStatus != null && p.PaymentStatus.ToLower() == "completed");

            if (payments.Any())
            {
                // Cannot cancel paid preorder without refund process
                return false;
            }

            preorder.Status = PreorderStatus.Cancelled;
            await _preorderRepository.UpdateAsync(preorder);

            return true;
        }

        #endregion

        #region Conversion

        public async Task<bool> CanConvertToOrderAsync(Guid preorderId)
        {
            var preorder = await GetPreorderByIdAsync(preorderId);

            if (preorder == null)
            {
                return false;
            }

            // Must be in paid or confirmed status
            var convertibleStatuses = new[] { PreorderStatus.Paid, PreorderStatus.Confirmed };
            if (!convertibleStatuses.Contains(preorder.Status?.ToLower()))
            {
                return false;
            }

            return true;
        }

        public async Task<Order?> ConvertPreorderToOrderAsync(Guid preorderId, string shippingAddress)
        {
            if (!await CanConvertToOrderAsync(preorderId))
            {
                return null;
            }

            var preorder = await GetPreorderByIdWithDetailsAsync(preorderId);

            if (preorder == null)
            {
                return null;
            }

            // Create new order from preorder
            var order = new Order
            {
                OrderId = Guid.NewGuid(),
                UserId = preorder.UserId,
                ShippingAddress = shippingAddress,
                Status = OrderService.OrderStatus.Confirmed,
                CreatedAt = DateTime.UtcNow,
                TotalAmount = 0
            };

            // Calculate total and create order
            double totalAmount = 0;
            var createdOrder = await _orderRepository.CreateAsync(order);

            // Convert preorder items to order items
            foreach (var preorderItem in preorder.PreorderItems)
            {
                var orderItem = new OrderItem
                {
                    OrderItemId = Guid.NewGuid(),
                    OrderId = createdOrder.OrderId,
                    PrescriptionId = preorderItem.PrescriptionId,
                    FrameId = preorderItem.FrameId,
                    FeatureId = preorderItem.FeatureId,
                    LensTypeId = preorderItem.LensTypeId,
                    Quantity = preorderItem.Quantity,
                    OrderPrice = preorderItem.PreorderPrice,
                    SelectedColor = preorderItem.SelectedColor
                };

                await _orderItemRepository.CreateAsync(orderItem);
                totalAmount += (orderItem.OrderPrice ?? 0) * (orderItem.Quantity ?? 1);
            }

            // Update order total
            createdOrder.TotalAmount = totalAmount;
            await _orderRepository.UpdateAsync(createdOrder);

            // Update preorder status
            preorder.Status = PreorderStatus.ConvertedToOrder;
            await _preorderRepository.UpdateAsync(preorder);

            // Transfer payments to order
            var payments = await _paymentRepository.SearchAsync(p => p.PreorderId == preorderId);
            foreach (var payment in payments)
            {
                payment.OrderId = createdOrder.OrderId;
                await _paymentRepository.UpdateAsync(payment);
            }

            return createdOrder;
        }

        #endregion

        #region Price Calculation

        public async Task<double> CalculatePreorderTotalAsync(List<PreorderItem> preorderItems)
        {
            double total = 0;

            foreach (var item in preorderItems)
            {
                var itemPrice = await CalculateItemPriceAsync(item);
                total += itemPrice * (item.Quantity ?? 1);
            }

            return total;
        }

        private async Task<double> CalculateItemPriceAsync(PreorderItem item)
        {
            double basePrice = 0;
            double lensTypePrice = 0;
            double featurePrice = 0;

            if (item.FrameId.HasValue)
            {
                var frames = await _frameRepository.SearchAsync(f => f.FrameId == item.FrameId);
                var frame = frames.FirstOrDefault();
                basePrice = frame?.BasePrice ?? 0;
            }

            if (item.LensTypeId.HasValue)
            {
                var lensTypes = await _lensTypeRepository.SearchAsync(lt => lt.LensTypeId == item.LensTypeId);
                var lensType = lensTypes.FirstOrDefault();
                lensTypePrice = lensType?.ExtraPrice ?? 0;
            }

            if (item.FeatureId.HasValue)
            {
                var features = await _lensFeatureRepository.SearchAsync(f => f.FeatureId == item.FeatureId);
                var feature = features.FirstOrDefault();
                featurePrice = feature?.ExtraPrice ?? 0;
            }

            return basePrice + lensTypePrice + featurePrice;
        }

        #endregion
    }
}
