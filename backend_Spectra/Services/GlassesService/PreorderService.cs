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
        private readonly GenericRepository<FrameLensType> _frameLensTypeRepository;
        private readonly GenericRepository<LensType> _lensTypeRepository;
        private readonly GenericRepository<LensFeature> _lensFeatureRepository;
        private readonly GenericRepository<LensIndex> _lensIndexRepository;
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

        // Valid preorder status transitions (mirrors Order pattern)
        private static readonly Dictionary<string, string[]> ValidPreorderStatusTransitions = new()
        {
            { PreorderStatus.Pending, new[] { PreorderStatus.Confirmed, PreorderStatus.Cancelled } },
            { PreorderStatus.Confirmed, new[] { PreorderStatus.Paid, PreorderStatus.Cancelled } },
            { PreorderStatus.Paid, new[] { PreorderStatus.ConvertedToOrder, PreorderStatus.Cancelled } },
            { PreorderStatus.ConvertedToOrder, Array.Empty<string>() },
            { PreorderStatus.Cancelled, Array.Empty<string>() }
        };

        public PreorderService(
            GenericRepository<Preorder> preorderRepository,
            GenericRepository<PreorderItem> preorderItemRepository,
            GenericRepository<Order> orderRepository,
            GenericRepository<OrderItem> orderItemRepository,
            GenericRepository<Frame> frameRepository,
            GenericRepository<FrameLensType> frameLensTypeRepository,
            GenericRepository<LensType> lensTypeRepository,
            GenericRepository<LensFeature> lensFeatureRepository,
            GenericRepository<LensIndex> lensIndexRepository,
            GenericRepository<Prescription> prescriptionRepository,
            GenericRepository<Payment> paymentRepository)
        {
            _preorderRepository = preorderRepository;
            _preorderItemRepository = preorderItemRepository;
            _orderRepository = orderRepository;
            _orderItemRepository = orderItemRepository;
            _frameRepository = frameRepository;
            _frameLensTypeRepository = frameLensTypeRepository;
            _lensTypeRepository = lensTypeRepository;
            _lensFeatureRepository = lensFeatureRepository;
            _lensIndexRepository = lensIndexRepository;
            _prescriptionRepository = prescriptionRepository;
            _paymentRepository = paymentRepository;
        }

        #region Create Operations

        public async Task<Preorder> CreatePreorderAsync(Preorder preorder, List<PreorderItem> preorderItems)
        {
            preorder.PreorderId = Guid.NewGuid();
            preorder.Status = PreorderStatus.Pending;
            preorder.CreatedAt = TimeHelper.Now;

            // Set expected date (default 14 days from now if not specified)
            if (!preorder.ExpectedDate.HasValue)
            {
                preorder.ExpectedDate = TimeHelper.Now.AddDays(14);
            }

            var createdPreorder = await _preorderRepository.CreateAsync(preorder);

            // Create preorder items
            foreach (var item in preorderItems)
            {
                item.PreorderItemId = Guid.NewGuid();
                item.PreorderId = createdPreorder.PreorderId;
                item.UnitPrice = await CalculateItemPriceAsync(item);
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
                Frame frame = null;

                // Validate frame exists
                if (item.FrameId.HasValue)
                {
                    var frames = await _frameRepository.SearchAsyncInclude(
                        f => f.FrameId == item.FrameId,
                        f => f.FrameColors);
                    frame = frames.FirstOrDefault();

                    if (frame == null)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Frame with ID {item.FrameId} not found");
                        continue;
                    }

                    // For preorders, frame doesn't need to be available (it's a preorder)
                    // But validate selectedColor is valid for the frame if colors are defined
                    if (item.SelectedColorId.HasValue && frame.FrameColors.Any())
                    {
                        var validColor = frame.FrameColors.Any(fc => fc.ColorId == item.SelectedColorId);
                        if (!validColor)
                        {
                            result.IsValid = false;
                            result.Errors.Add($"Selected color is not available for frame '{frame.FrameName}'");
                        }
                    }
                    else if (frame.FrameColors.Any() && !item.SelectedColorId.HasValue)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Frame '{frame.FrameName}' requires a color selection");
                    }
                }
                else
                {
                    result.IsValid = false;
                    result.Errors.Add("Each preorder item must have a frame");
                    continue;
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

                    // Validate that the lens type is supported by this frame
                    if (frame != null)
                    {
                        var supportedLensTypes = await _frameLensTypeRepository.SearchAsync(
                            flt => flt.FrameId == frame.FrameId);
                        var supportedIds = supportedLensTypes
                            .Where(flt => flt.LensTypeId.HasValue)
                            .Select(flt => flt.LensTypeId!.Value)
                            .ToHashSet();

                        if (supportedIds.Any())
                        {
                            var isAlwaysAllowed = lensType.RequiresPrescription != true;
                            if (!isAlwaysAllowed && !supportedIds.Contains(lensType.LensTypeId))
                            {
                                result.IsValid = false;
                                result.Errors.Add($"Frame '{frame.FrameName}' does not support lens type '{lensType.LensSpecification}'. Please check the frame's supported lens types.");
                                continue;
                            }
                        }
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
                    else if (frame != null)
                    {
                        // Validate prescription values against frame Rx/PD limits
                        ValidatePrescriptionAgainstFrame(result, frame, prescription);
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

        /// <summary>
        /// Validates a prescription's sphere and PD values against the frame's supported Rx/PD limits.
        /// </summary>
        private void ValidatePrescriptionAgainstFrame(OrderValidationResult result, Frame frame, Prescription prescription)
        {
            if (frame.MinRx.HasValue || frame.MaxRx.HasValue)
            {
                var sphereValues = new List<double?> { prescription.SphereLeft, prescription.SphereRight };
                foreach (var sphere in sphereValues.Where(s => s.HasValue))
                {
                    if (frame.MinRx.HasValue && sphere < frame.MinRx)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Prescription sphere value ({sphere}) is below the frame's minimum supported Rx ({frame.MinRx}). This frame cannot support your prescription.");
                        return;
                    }
                    if (frame.MaxRx.HasValue && sphere > frame.MaxRx)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Prescription sphere value ({sphere}) exceeds the frame's maximum supported Rx ({frame.MaxRx}). This frame cannot support your prescription.");
                        return;
                    }
                }
            }

            if (frame.MinPd.HasValue || frame.MaxPd.HasValue)
            {
                if (prescription.PupillaryDistance.HasValue)
                {
                    var pd = prescription.PupillaryDistance.Value;
                    if (frame.MinPd.HasValue && pd < frame.MinPd)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Prescription PD ({pd}mm) is below the frame's minimum supported PD ({frame.MinPd}mm). This frame cannot fit your pupillary distance.");
                    }
                    if (frame.MaxPd.HasValue && pd > frame.MaxPd)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Prescription PD ({pd}mm) exceeds the frame's maximum supported PD ({frame.MaxPd}mm). This frame cannot fit your pupillary distance.");
                    }
                }
            }
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

            var currentStatus = preorder.Status?.ToLower() ?? PreorderStatus.Pending;
            var targetStatus = newStatus.ToLower();

            // Validate status transition
            if (!ValidPreorderStatusTransitions.TryGetValue(currentStatus, out var allowedTransitions)
                || !allowedTransitions.Contains(targetStatus))
            {
                throw new InvalidOperationException(
                    $"Cannot transition preorder from '{currentStatus}' to '{targetStatus}'. " +
                    $"Allowed transitions: {(allowedTransitions != null ? string.Join(", ", allowedTransitions) : "none")}");
            }

            preorder.Status = targetStatus;
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

            // Must be in paid or confirmed status (not already converted or cancelled)
            var convertibleStatuses = new[] { PreorderStatus.Paid, PreorderStatus.Confirmed };
            if (!convertibleStatuses.Contains(preorder.Status?.ToLower()))
            {
                return false;
            }

            // Guard against duplicate conversion — check if an order already exists for this preorder
            var existingOrders = await _orderRepository.SearchAsync(o => o.ConvertedFromPreorderId == preorderId);
            if (existingOrders.Any())
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
                CreatedAt = TimeHelper.Now,
                TotalAmount = 0,
                ConvertedFromPreorderId = preorder.PreorderId
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
                    LensIndexId = preorderItem.LensIndexId,
                    Quantity = preorderItem.Quantity,
                    UnitPrice = preorderItem.UnitPrice,
                    SelectedColorId = preorderItem.SelectedColorId,
                    SelectedSize = preorderItem.SelectedSize
                };

                await _orderItemRepository.CreateAsync(orderItem);
                totalAmount += (orderItem.UnitPrice ?? 0) * (orderItem.Quantity ?? 1);
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
            double lensIndexPrice = 0;

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
                lensTypePrice = lensType?.BasePrice ?? 0;
            }

            if (item.FeatureId.HasValue)
            {
                var features = await _lensFeatureRepository.SearchAsync(f => f.FeatureId == item.FeatureId);
                var feature = features.FirstOrDefault();
                featurePrice = feature?.ExtraPrice ?? 0;
            }

            if (item.LensIndexId.HasValue)
            {
                var lensIndices = await _lensIndexRepository.SearchAsync(li => li.LensIndexId == item.LensIndexId);
                var lensIndex = lensIndices.FirstOrDefault();
                lensIndexPrice = lensIndex?.AdditionalPrice ?? 0;
            }

            return basePrice + lensTypePrice + featurePrice + lensIndexPrice;
        }

        #endregion
    }
}
