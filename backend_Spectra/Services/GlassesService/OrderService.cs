using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repositories.Basic;
using Repositories.ModelExtensions;
using Repositories.Models;

namespace Services.GlassesService
{
    public class OrderValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public interface IOrderService
    {
        // Create
        Task<Order> CreateOrderAsync(Order order, List<OrderItem> orderItems);
        Task<OrderValidationResult> ValidateOrderItemsAsync(List<OrderItem> orderItems, Guid userId);

        // Read
        Task<Order?> GetOrderByIdAsync(Guid orderId);
        Task<Order?> GetOrderByIdWithDetailsAsync(Guid orderId);
        Task<List<Order>> GetOrdersByUserAsync(Guid userId);
        Task<PaginationResult<Order>> GetOrdersByUserAsync(Guid userId, int currentPage = 1, int pageSize = 10);
        Task<PaginationResult<Order>> GetAllOrdersAsync(int currentPage = 1, int pageSize = 10);

        // Update
        Task<Order?> UpdateOrderStatusAsync(Guid orderId, string newStatus, string userRole, Guid userId);
        Task<Order?> UpdateOrderShippingAsync(Guid orderId, string shippingMethod, double shippingFee, double totalAmount);
        Task<bool> CanModifyOrderAsync(Guid orderId);
        Task<Order?> ConfirmDeliveryAsync(Guid orderId, Guid userId);
        Task<Order?> CancelOrderByCustomerAsync(Guid orderId, Guid userId);

        // Price
        Task<double> CalculateOrderTotalAsync(List<OrderItem> orderItems);
        Task<double> CalculateItemPriceAsync(OrderItem item);
    }

    public class OrderService : IOrderService
    {
        private readonly GenericRepository<Order> _orderRepository;
        private readonly GenericRepository<OrderItem> _orderItemRepository;
        private readonly GenericRepository<Frame> _frameRepository;
        private readonly GenericRepository<FrameColor> _frameColorRepository;
        private readonly GenericRepository<FrameLensType> _frameLensTypeRepository;
        private readonly GenericRepository<LensType> _lensTypeRepository;
        private readonly GenericRepository<LensFeature> _lensFeatureRepository;
        private readonly GenericRepository<LensIndex> _lensIndexRepository;
        private readonly GenericRepository<Prescription> _prescriptionRepository;
        private readonly GenericRepository<Payment> _paymentRepository;

        public static class OrderStatus
        {
            public const string Pending    = "pending";
            public const string Confirmed  = "confirmed";
            public const string Processing = "processing";
            public const string Shipped    = "shipped";
            public const string Delivered  = "delivered";
            public const string Cancelled  = "cancelled";
        }

        private static readonly Dictionary<string, string[]> ValidStatusTransitions = new()
        {
            { OrderStatus.Pending,    new[] { OrderStatus.Confirmed,  OrderStatus.Cancelled } },
            { OrderStatus.Confirmed,  new[] { OrderStatus.Processing, OrderStatus.Cancelled } },
            { OrderStatus.Processing, new[] { OrderStatus.Shipped,    OrderStatus.Cancelled } },
            { OrderStatus.Shipped,    new[] { OrderStatus.Delivered } },
            { OrderStatus.Delivered,  Array.Empty<string>() },
            { OrderStatus.Cancelled,  Array.Empty<string>() }
        };

        public OrderService(
            GenericRepository<Order>         orderRepository,
            GenericRepository<OrderItem>     orderItemRepository,
            GenericRepository<Frame>         frameRepository,
            GenericRepository<FrameColor>    frameColorRepository,
            GenericRepository<FrameLensType> frameLensTypeRepository,
            GenericRepository<LensType>      lensTypeRepository,
            GenericRepository<LensFeature>   lensFeatureRepository,
            GenericRepository<LensIndex>     lensIndexRepository,
            GenericRepository<Prescription>  prescriptionRepository,
            GenericRepository<Payment>       paymentRepository)
        {
            _orderRepository         = orderRepository;
            _orderItemRepository     = orderItemRepository;
            _frameRepository         = frameRepository;
            _frameColorRepository    = frameColorRepository;
            _frameLensTypeRepository = frameLensTypeRepository;
            _lensTypeRepository      = lensTypeRepository;
            _lensFeatureRepository   = lensFeatureRepository;
            _lensIndexRepository     = lensIndexRepository;
            _prescriptionRepository  = prescriptionRepository;
            _paymentRepository       = paymentRepository;
        }

        // ================================================================
        //  CREATE
        // ================================================================

        public async Task<Order> CreateOrderAsync(Order order, List<OrderItem> orderItems)
        {
            // Step 1: Calculate all prices upfront.
            // SearchAsync loads entities as "Unchanged" — EF will not re-INSERT them,
            // so having them in the tracker does not pollute later Add() calls.
            double total = 0;
            foreach (var item in orderItems)
            {
                item.UnitPrice = await CalculateItemPriceAsync(item);
                total += (item.UnitPrice ?? 0) * (item.Quantity ?? 1);
            }

            // Step 2: Persist the order.
            // Clear the tracker first so EF does not reconcile the Order's nullable FK
            // navigation properties (e.g. User = null) against the explicitly-set FK
            // values (e.g. UserId = validGuid).  Without this, EF Core 8 relationship
            // fixup can silently null-out FK columns on the new entity.
            order.OrderId     = Guid.NewGuid();
            order.Status      = OrderStatus.Pending;
            order.CreatedAt   = TimeHelper.Now;
            order.TotalAmount = total;

            _orderRepository.ClearTracker();
            var createdOrder = await _orderRepository.CreateAsync(order);

            // Step 3: Persist each order item.
            // Clear the tracker before each insert so EF uses the explicit FK values
            // (OrderId, FrameId, SelectedColorId, …) without navigation-property fixup
            // overriding them.  Same reason as the clear above.
            foreach (var item in orderItems)
            {
                item.OrderItemId = Guid.NewGuid();
                item.OrderId     = createdOrder.OrderId;
                _orderItemRepository.ClearTracker();
                await _orderItemRepository.CreateAsync(item);
            }

            // Step 4: Deduct stock — done in a SEPARATE pass after all items are saved.
            // UpdateAsync clears the tracker internally; doing this inside the item loop
            // was the root cause of missing items in the old code.
            foreach (var item in orderItems)
            {
                if (item.FrameId.HasValue && item.SelectedColorId.HasValue)
                    await DeductVariantStockAsync(item.FrameId.Value, item.SelectedColorId.Value, item.Quantity ?? 1);
                else if (item.FrameId.HasValue)
                    await DeductFrameStockAsync(item.FrameId.Value, item.Quantity ?? 1);
            }

            return await GetOrderByIdWithDetailsAsync(createdOrder.OrderId) ?? createdOrder;
        }

        // ================================================================
        //  VALIDATION
        // ================================================================

        public async Task<OrderValidationResult> ValidateOrderItemsAsync(List<OrderItem> orderItems, Guid userId)
        {
            var result = new OrderValidationResult { IsValid = true };

            if (orderItems == null || !orderItems.Any())
            {
                result.IsValid = false;
                result.Errors.Add("Order must contain at least one item");
                return result;
            }

            foreach (var item in orderItems)
            {
                Frame? frame = null;

                if (!item.FrameId.HasValue)
                {
                    result.IsValid = false;
                    result.Errors.Add("Each order item must have a frame");
                    continue;
                }

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

                if (frame.Status?.ToLower() != "available")
                {
                    result.IsValid = false;
                    result.Errors.Add($"Frame '{frame.FrameName}' is not available");
                    continue;
                }

                var requestedQty = item.Quantity ?? 1;

                if (item.SelectedColorId.HasValue)
                {
                    var variant = frame.FrameColors.FirstOrDefault(fc => fc.ColorId == item.SelectedColorId);
                    if (variant == null)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Selected color is not available for frame '{frame.FrameName}'");
                        continue;
                    }
                    if ((variant.StockQuantity ?? 0) < requestedQty)
                    {
                        result.IsValid = false;
                        result.Errors.Add((variant.StockQuantity ?? 0) <= 0
                            ? $"Frame '{frame.FrameName}' in the selected color is out of stock"
                            : $"Frame '{frame.FrameName}' in the selected color only has {variant.StockQuantity} in stock");
                        continue;
                    }
                }
                else
                {
                    if (frame.FrameColors.Any())
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Frame '{frame.FrameName}' requires a color selection");
                        continue;
                    }
                    if ((frame.StockQuantity ?? 0) < requestedQty)
                    {
                        result.IsValid = false;
                        result.Errors.Add((frame.StockQuantity ?? 0) <= 0
                            ? $"Frame '{frame.FrameName}' is out of stock. Please use Preorder instead."
                            : $"Frame '{frame.FrameName}' only has {frame.StockQuantity} in stock");
                        continue;
                    }
                }

                if (item.LensTypeId.HasValue)
                {
                    var lensTypes = await _lensTypeRepository.SearchAsync(lt => lt.LensTypeId == item.LensTypeId);
                    var lensType  = lensTypes.FirstOrDefault();

                    if (lensType == null)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Lens type with ID {item.LensTypeId} not found");
                        continue;
                    }

                    var supported   = await _frameLensTypeRepository.SearchAsync(flt => flt.FrameId == frame.FrameId);
                    var supportedIds = supported.Where(s => s.LensTypeId.HasValue).Select(s => s.LensTypeId!.Value).ToHashSet();
                    if (supportedIds.Any() && lensType.RequiresPrescription == true && !supportedIds.Contains(lensType.LensTypeId))
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Frame '{frame.FrameName}' does not support lens type '{lensType.LensSpecification}'");
                        continue;
                    }

                    if (lensType.RequiresPrescription == true)
                    {
                        if (!item.PrescriptionId.HasValue)
                        {
                            result.IsValid = false;
                            result.Errors.Add($"Lens type '{lensType.LensSpecification}' requires a prescription");
                            continue;
                        }

                        var prescriptions = await _prescriptionRepository.SearchAsync(p => p.PrescriptionId == item.PrescriptionId);
                        var prescription  = prescriptions.FirstOrDefault();

                        if (prescription == null)
                        {
                            result.IsValid = false;
                            result.Errors.Add("Prescription not found");
                        }
                        else if (prescription.UserId != userId)
                        {
                            result.IsValid = false;
                            result.Errors.Add("Prescription does not belong to the current user");
                        }
                        else if (prescription.ExpirationDate.HasValue && prescription.ExpirationDate < TimeHelper.Now)
                        {
                            result.IsValid = false;
                            result.Errors.Add("Prescription has expired");
                        }
                        else
                        {
                            ValidatePrescriptionAgainstFrame(result, frame, prescription);
                        }
                    }
                }

                if (item.FeatureId.HasValue)
                {
                    var features = await _lensFeatureRepository.SearchAsync(f => f.FeatureId == item.FeatureId);
                    if (!features.Any())
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Lens feature with ID {item.FeatureId} not found");
                    }
                }

                if (!item.Quantity.HasValue || item.Quantity <= 0)
                {
                    result.IsValid = false;
                    result.Errors.Add("Each order item must have a quantity greater than 0");
                }
            }

            return result;
        }

        private void ValidatePrescriptionAgainstFrame(OrderValidationResult result, Frame frame, Prescription prescription)
        {
            if (frame.MinRx.HasValue || frame.MaxRx.HasValue)
            {
                foreach (var sphere in new[] { prescription.SphereLeft, prescription.SphereRight }.Where(s => s.HasValue))
                {
                    if (frame.MinRx.HasValue && sphere < frame.MinRx)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Prescription sphere ({sphere}) is below the frame minimum Rx ({frame.MinRx})");
                        return;
                    }
                    if (frame.MaxRx.HasValue && sphere > frame.MaxRx)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Prescription sphere ({sphere}) exceeds the frame maximum Rx ({frame.MaxRx})");
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
                        result.Errors.Add($"PD ({pd}mm) is below the frame minimum PD ({frame.MinPd}mm)");
                    }
                    if (frame.MaxPd.HasValue && pd > frame.MaxPd)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"PD ({pd}mm) exceeds the frame maximum PD ({frame.MaxPd}mm)");
                    }
                }
            }
        }

        // ================================================================
        //  READ
        // ================================================================

        public async Task<Order?> GetOrderByIdAsync(Guid orderId)
        {
            var orders = await _orderRepository.SearchAsync(o => o.OrderId == orderId);
            return orders.FirstOrDefault();
        }

        public async Task<Order?> GetOrderByIdWithDetailsAsync(Guid orderId)
        {
            var orders = await _orderRepository.SearchAsyncInclude(
                o => o.OrderId == orderId,
                o => o.OrderItems,
                o => o.User,
                o => o.Payments);

            var order = orders.FirstOrDefault();
            if (order != null)
                await EnrichOrderItemsAsync(new[] { order });
            return order;
        }

        public async Task<List<Order>> GetOrdersByUserAsync(Guid userId)
        {
            var orders = await _orderRepository.SearchAsyncInclude(
                o => o.UserId == userId,
                o => o.OrderItems,
                o => o.Payments);
            var list = orders.ToList();
            await EnrichOrderItemsAsync(list);
            return list;
        }

        public async Task<PaginationResult<Order>> GetOrdersByUserAsync(Guid userId, int currentPage = 1, int pageSize = 10)
        {
            var result = await _orderRepository.SearchWithPagingAsyncIncludeOrderBy(
                o => o.UserId == userId,
                currentPage,
                pageSize,
                orderBy: o => o.CreatedAt,
                ascending: false,
                o => o.OrderItems,
                o => o.User,
                o => o.Payments);
            await EnrichOrderItemsAsync(result.Items);
            return result;
        }

        public async Task<PaginationResult<Order>> GetAllOrdersAsync(int currentPage = 1, int pageSize = 10)
        {
            var result = await _orderRepository.SearchWithPagingAsyncIncludeOrderBy(
                o => true,
                currentPage,
                pageSize,
                orderBy: o => o.CreatedAt,
                ascending: false,
                o => o.OrderItems,
                o => o.User,
                o => o.Payments);
            await EnrichOrderItemsAsync(result.Items);
            return result;
        }

        private async Task EnrichOrderItemsAsync(IEnumerable<Order> orders)
        {
            foreach (var order in orders)
            {
                if (order.OrderItems == null) continue;
                foreach (var item in order.OrderItems)
                {
                    if (item.FrameId.HasValue && item.Frame == null)
                    {
                        var frames = await _frameRepository.SearchAsync(f => f.FrameId == item.FrameId);
                        item.Frame = frames.FirstOrDefault();
                    }
                    if (item.LensTypeId.HasValue && item.LensType == null)
                    {
                        var lensTypes = await _lensTypeRepository.SearchAsync(lt => lt.LensTypeId == item.LensTypeId);
                        item.LensType = lensTypes.FirstOrDefault();
                    }
                    if (item.FeatureId.HasValue && item.Feature == null)
                    {
                        var features = await _lensFeatureRepository.SearchAsync(f => f.FeatureId == item.FeatureId);
                        item.Feature = features.FirstOrDefault();
                    }
                    if (item.PrescriptionId.HasValue && item.Prescription == null)
                    {
                        var prescriptions = await _prescriptionRepository.SearchAsync(p => p.PrescriptionId == item.PrescriptionId);
                        item.Prescription = prescriptions.FirstOrDefault();
                    }
                    if (item.Frame        != null) item.Frame.OrderItems        = null;
                    if (item.LensType     != null) item.LensType.OrderItems     = null;
                    if (item.Feature      != null) item.Feature.OrderItems      = null;
                    if (item.Prescription != null)
                    {
                        item.Prescription.OrderItems = null;
                        if (item.Prescription.User != null) item.Prescription.User.Orders = null;
                    }
                    item.Order = null;
                }
                if (order.User != null) order.User.Orders = null;
            }

            // Detach everything from the change tracker after enrichment.
            // The null assignments above (item.Order = null, user.Orders = null, etc.)
            // are for JSON serialization only. If these entities stay tracked, the next
            // SaveChangesAsync() anywhere in the request will flush those nulls to the DB,
            // wiping out OrderItem.OrderId and Order.UserId. Clearing the tracker ensures
            // these read-only DTOs cannot corrupt the database.
            _orderRepository.ClearTracker();
        }

        // ================================================================
        //  UPDATE
        // ================================================================

        public async Task<Order?> UpdateOrderStatusAsync(Guid orderId, string newStatus, string userRole, Guid userId)
        {
            var order = await GetOrderByIdAsync(orderId);
            if (order == null) return null;

            var current = order.Status?.ToLower() ?? OrderStatus.Pending;
            newStatus = newStatus.ToLower();

            if (!ValidStatusTransitions.TryGetValue(current, out var allowed) || !allowed.Contains(newStatus))
                return null;

            switch (userRole.ToLower())
            {
                case "customer":
                    return null;
                case "staff":
                    var staffAllowed = new[] { OrderStatus.Processing, OrderStatus.Shipped, OrderStatus.Delivered };
                    if (!staffAllowed.Contains(newStatus)) return null;
                    break;
                case "manager":
                case "admin":
                    break;
                default:
                    return null;
            }

            order.Status = newStatus;

            if (newStatus == OrderStatus.Delivered)
            {
                order.ArrivalDate = TimeHelper.Now;
                order.DeliveredAt = TimeHelper.Now;
            }

            if (newStatus == OrderStatus.Cancelled)
            {
                var full = await GetOrderByIdWithDetailsAsync(orderId);
                if (full?.OrderItems != null)
                {
                    foreach (var item in full.OrderItems)
                    {
                        if (item.FrameId.HasValue && item.SelectedColorId.HasValue)
                            await RestoreVariantStockAsync(item.FrameId.Value, item.SelectedColorId.Value, item.Quantity ?? 1);
                        else if (item.FrameId.HasValue)
                            await RestoreFrameStockAsync(item.FrameId.Value, item.Quantity ?? 1);
                    }
                }
            }

            return await _orderRepository.UpdateAsync(order);
        }

        public async Task<Order?> UpdateOrderShippingAsync(Guid orderId, string shippingMethod, double shippingFee, double totalAmount)
        {
            // Clear tracker to discard poisoned navigation state left by
            // EnrichOrderItemsAsync (item.Order = null, user.Orders = null)
            // which would otherwise cause SaveAsync to null-out FK columns.
            _orderRepository.ClearTracker();

            var order = await GetOrderByIdAsync(orderId);
            if (order == null) return null;

            order.ShippingMethod = shippingMethod;
            order.ShippingFee    = shippingFee;
            order.TotalAmount    = totalAmount;

            await _orderRepository.SaveAsync();
            return order;
        }

        public async Task<bool> CanModifyOrderAsync(Guid orderId)
        {
            var payments = await _paymentRepository.SearchAsync(p =>
                p.OrderId == orderId &&
                p.PaymentStatus != null && p.PaymentStatus.ToLower() == "completed");
            return !payments.Any();
        }

        public async Task<Order?> ConfirmDeliveryAsync(Guid orderId, Guid userId)
        {
            var order = await GetOrderByIdAsync(orderId);
            if (order == null)                                    return null;
            if (order.UserId != userId)                           return null;
            if (order.Status?.ToLower() != OrderStatus.Delivered) return null;
            if (order.DeliveryConfirmedAt.HasValue)               return order;

            order.DeliveryConfirmedAt = TimeHelper.Now;
            return await _orderRepository.UpdateAsync(order);
        }

        public async Task<Order?> CancelOrderByCustomerAsync(Guid orderId, Guid userId)
        {
            var order = await GetOrderByIdAsync(orderId);
            if (order == null)                                  return null;
            if (order.UserId != userId)                         return null;
            if (order.Status?.ToLower() != OrderStatus.Pending) return null;

            var full = await GetOrderByIdWithDetailsAsync(orderId);
            if (full?.OrderItems != null)
            {
                foreach (var item in full.OrderItems)
                {
                    if (item.FrameId.HasValue && item.SelectedColorId.HasValue)
                        await RestoreVariantStockAsync(item.FrameId.Value, item.SelectedColorId.Value, item.Quantity ?? 1);
                    else if (item.FrameId.HasValue)
                        await RestoreFrameStockAsync(item.FrameId.Value, item.Quantity ?? 1);
                }
            }

            order.Status              = OrderStatus.Cancelled;
            order.CancelledByCustomer = true;
            return await _orderRepository.UpdateAsync(order);
        }

        // ================================================================
        //  PRICE CALCULATION
        // ================================================================

        public async Task<double> CalculateOrderTotalAsync(List<OrderItem> orderItems)
        {
            double total = 0;
            foreach (var item in orderItems)
                total += await CalculateItemPriceAsync(item) * (item.Quantity ?? 1);
            return total;
        }

        public async Task<double> CalculateItemPriceAsync(OrderItem item)
        {
            double basePrice      = 0;
            double lensTypePrice  = 0;
            double featurePrice   = 0;
            double lensIndexPrice = 0;
            double colorExtraCost = 0;

            if (item.FrameId.HasValue)
            {
                var frames  = await _frameRepository.SearchAsync(f => f.FrameId == item.FrameId);
                basePrice   = frames.FirstOrDefault()?.BasePrice ?? 0;
            }
            if (item.LensTypeId.HasValue)
            {
                var lt       = await _lensTypeRepository.SearchAsync(l => l.LensTypeId == item.LensTypeId);
                lensTypePrice = lt.FirstOrDefault()?.BasePrice ?? 0;
            }
            if (item.FeatureId.HasValue)
            {
                var feat     = await _lensFeatureRepository.SearchAsync(f => f.FeatureId == item.FeatureId);
                featurePrice = feat.FirstOrDefault()?.ExtraPrice ?? 0;
            }
            if (item.LensIndexId.HasValue)
            {
                var idx       = await _lensIndexRepository.SearchAsync(l => l.LensIndexId == item.LensIndexId);
                lensIndexPrice = idx.FirstOrDefault()?.AdditionalPrice ?? 0;
            }
            if (item.FrameId.HasValue && item.SelectedColorId.HasValue)
            {
                var fc       = await _frameColorRepository.SearchAsync(
                    c => c.FrameId == item.FrameId && c.ColorId == item.SelectedColorId);
                colorExtraCost = fc.FirstOrDefault()?.ColorExtraCost ?? 0;
            }

            return basePrice + lensTypePrice + featurePrice + lensIndexPrice + colorExtraCost;
        }

        // ================================================================
        //  STOCK HELPERS
        // ================================================================

        private async Task DeductFrameStockAsync(Guid frameId, int quantity)
        {
            var frames = await _frameRepository.SearchAsync(f => f.FrameId == frameId);
            var frame  = frames.FirstOrDefault();
            if (frame == null) return;
            frame.StockQuantity = Math.Max(0, (frame.StockQuantity ?? 0) - quantity);
            if (frame.StockQuantity <= 0) frame.Status = "out_of_stock";
            await _frameRepository.UpdateAsync(frame);
        }

        private async Task DeductVariantStockAsync(Guid frameId, Guid colorId, int quantity)
        {
            var variants = await _frameColorRepository.SearchAsync(fc => fc.FrameId == frameId && fc.ColorId == colorId);
            var variant  = variants.FirstOrDefault();
            if (variant != null)
            {
                variant.StockQuantity = Math.Max(0, (variant.StockQuantity ?? 0) - quantity);
                await _frameColorRepository.UpdateAsync(variant);
            }
            await RecalculateFrameStockAsync(frameId);
        }

        private async Task RestoreFrameStockAsync(Guid frameId, int quantity)
        {
            var frames = await _frameRepository.SearchAsync(f => f.FrameId == frameId);
            var frame  = frames.FirstOrDefault();
            if (frame == null) return;
            frame.StockQuantity = (frame.StockQuantity ?? 0) + quantity;
            if (frame.StockQuantity > 0 && frame.Status?.ToLower() == "out_of_stock")
                frame.Status = "available";
            await _frameRepository.UpdateAsync(frame);
        }

        private async Task RestoreVariantStockAsync(Guid frameId, Guid colorId, int quantity)
        {
            var variants = await _frameColorRepository.SearchAsync(fc => fc.FrameId == frameId && fc.ColorId == colorId);
            var variant  = variants.FirstOrDefault();
            if (variant != null)
            {
                variant.StockQuantity = (variant.StockQuantity ?? 0) + quantity;
                await _frameColorRepository.UpdateAsync(variant);
            }
            await RecalculateFrameStockAsync(frameId);
        }

        private async Task RecalculateFrameStockAsync(Guid frameId)
        {
            var frames = await _frameRepository.SearchAsync(f => f.FrameId == frameId);
            var frame  = frames.FirstOrDefault();
            if (frame == null) return;
            var allVariants = await _frameColorRepository.SearchAsync(fc => fc.FrameId == frameId);
            var totalStock  = allVariants.Sum(v => v.StockQuantity ?? 0);
            frame.StockQuantity = totalStock;
            if (totalStock <= 0)      frame.Status = "out_of_stock";
            else if (frame.Status?.ToLower() == "out_of_stock") frame.Status = "available";
            await _frameRepository.UpdateAsync(frame);
        }
    }
}
