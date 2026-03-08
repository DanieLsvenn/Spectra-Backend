using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Repositories.Basic;
using Repositories.Models;

namespace Services.GlassesService
{
    public class ShippingMethodInfo
    {
        public string Method { get; set; } = string.Empty;
        public double Fee { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public interface IShippingService
    {
        double CalculateShippingFee(string shippingMethod, double orderSubtotal);
        List<ShippingMethodInfo> GetAvailableShippingMethods();
        Task<Order?> AssignTrackingNumberAsync(Guid orderId, string trackingNumber, string carrier);
    }

    public class ShippingService : IShippingService
    {
        private readonly GenericRepository<Order> _orderRepository;
        private readonly double _freeShippingThreshold;

        private static readonly Dictionary<string, (double fee, string desc)> ShippingMethods = new()
        {
            { "standard", (5.0, "Standard Shipping (5-7 business days)") },
            { "express", (15.0, "Express Shipping (2-3 business days)") },
            { "free", (0.0, "Free Shipping") }
        };

        public ShippingService(
            GenericRepository<Order> orderRepository,
            IConfiguration configuration)
        {
            _orderRepository = orderRepository;
            _freeShippingThreshold = double.TryParse(configuration["Shipping:FreeShippingThreshold"], out var threshold)
                ? threshold
                : 89.0;
        }

        public double CalculateShippingFee(string shippingMethod, double orderSubtotal)
        {
            if (orderSubtotal >= _freeShippingThreshold)
                return 0;

            var method = shippingMethod?.ToLower() ?? "standard";
            return ShippingMethods.TryGetValue(method, out var info) ? info.fee : ShippingMethods["standard"].fee;
        }

        public List<ShippingMethodInfo> GetAvailableShippingMethods()
        {
            return ShippingMethods.Select(sm => new ShippingMethodInfo
            {
                Method = sm.Key,
                Fee = sm.Value.fee,
                Description = sm.Value.desc
            }).ToList();
        }

        public async Task<Order?> AssignTrackingNumberAsync(Guid orderId, string trackingNumber, string carrier)
        {
            var orders = await _orderRepository.SearchAsync(o => o.OrderId == orderId);
            var order = orders.FirstOrDefault();
            if (order == null) return null;

            order.TrackingNumber = trackingNumber;
            order.ShippingCarrier = carrier;
            order.ShippedAt = DateTime.UtcNow;

            if (order.Status?.ToLower() == "processing")
            {
                order.Status = "shipped";
            }

            return await _orderRepository.UpdateAsync(order);
        }
    }
}
