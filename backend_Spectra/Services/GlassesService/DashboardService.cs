using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repositories.Basic;
using Repositories.Models;

namespace Services.GlassesService
{
    public class DashboardStatistics
    {
        public int TotalOrders { get; set; }
        public double TotalRevenue { get; set; }
        public int TotalCustomers { get; set; }
        public int PendingOrders { get; set; }
        public int ConfirmedOrders { get; set; }
        public int ProcessingOrders { get; set; }
        public int ShippedOrders { get; set; }
        public int DeliveredOrders { get; set; }
        public int CancelledOrders { get; set; }
        public double AverageOrderValue { get; set; }
        public int TotalPreorders { get; set; }
        public int TotalComplaints { get; set; }
        public int PendingComplaints { get; set; }
    }

    public class RevenueReport
    {
        public DateTime Date { get; set; }
        public double Revenue { get; set; }
        public int OrderCount { get; set; }
    }

    public class PopularFrame
    {
        public Guid FrameId { get; set; }
        public string? FrameName { get; set; }
        public string? Brand { get; set; }
        public double? BasePrice { get; set; }
        public int TotalSold { get; set; }
        public double TotalRevenue { get; set; }
    }

    public class OrderSummary
    {
        public int TodayOrders { get; set; }
        public int WeekOrders { get; set; }
        public int MonthOrders { get; set; }
        public double TodayRevenue { get; set; }
        public double WeekRevenue { get; set; }
        public double MonthRevenue { get; set; }
    }

    public interface IDashboardService
    {
        // Statistics
        Task<DashboardStatistics> GetStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null);
        
        // Revenue
        Task<List<RevenueReport>> GetDailyRevenueAsync(DateTime startDate, DateTime endDate);
        Task<List<RevenueReport>> GetMonthlyRevenueAsync(int year);
        
        // Popular items
        Task<List<PopularFrame>> GetPopularFramesAsync(int limit = 10, DateTime? startDate = null, DateTime? endDate = null);
        
        // Order summary
        Task<OrderSummary> GetOrderSummaryAsync();
    }

    public class DashboardService : IDashboardService
    {
        private readonly GenericRepository<Order> _orderRepository;
        private readonly GenericRepository<OrderItem> _orderItemRepository;
        private readonly GenericRepository<User> _userRepository;
        private readonly GenericRepository<Preorder> _preorderRepository;
        private readonly GenericRepository<ComplaintRequest> _complaintRepository;
        private readonly GenericRepository<Payment> _paymentRepository;
        private readonly GenericRepository<Frame> _frameRepository;

        public DashboardService(
            GenericRepository<Order> orderRepository,
            GenericRepository<OrderItem> orderItemRepository,
            GenericRepository<User> userRepository,
            GenericRepository<Preorder> preorderRepository,
            GenericRepository<ComplaintRequest> complaintRepository,
            GenericRepository<Payment> paymentRepository,
            GenericRepository<Frame> frameRepository)
        {
            _orderRepository = orderRepository;
            _orderItemRepository = orderItemRepository;
            _userRepository = userRepository;
            _preorderRepository = preorderRepository;
            _complaintRepository = complaintRepository;
            _paymentRepository = paymentRepository;
            _frameRepository = frameRepository;
        }

        // SQL Server datetime minimum value (1753-01-01)
        private static readonly DateTime SqlMinDate = new DateTime(1753, 1, 1);
        // Use a reasonable max date instead of DateTime.MaxValue
        private static readonly DateTime SqlMaxDate = new DateTime(9999, 12, 31);

        #region Statistics

        public async Task<DashboardStatistics> GetStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            // If no dates specified, get all data
            var hasDateFilter = startDate.HasValue || endDate.HasValue;
            var start = startDate ?? SqlMinDate;
            var end = endDate ?? SqlMaxDate;

            // Get all orders first, then filter
            var allOrders = await _orderRepository.GetAllAsync();
            var orderList = hasDateFilter
                ? allOrders.Where(o => o.CreatedAt >= start && o.CreatedAt <= end).ToList()
                : allOrders.ToList();

            // Get all payments, then filter
            var allPayments = await _paymentRepository.GetAllAsync();
            var filteredPayments = hasDateFilter
                ? allPayments.Where(p => p.PaidAt >= start && p.PaidAt <= end).ToList()
                : allPayments.ToList();
            var completedPayments = filteredPayments
                .Where(p => p.PaymentStatus != null && 
                            p.PaymentStatus.Equals("completed", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Get all users, then filter
            var allUsers = await _userRepository.GetAllAsync();
            var filteredUsers = hasDateFilter
                ? allUsers.Where(u => u.CreatedAt >= start && u.CreatedAt <= end).ToList()
                : allUsers.ToList();
            var customers = filteredUsers
                .Where(u => u.Role != null && u.Role.Equals("customer", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Get all preorders, then filter
            var allPreorders = await _preorderRepository.GetAllAsync();
            var preorderList = hasDateFilter
                ? allPreorders.Where(p => p.CreatedAt >= start && p.CreatedAt <= end).ToList()
                : allPreorders.ToList();

            // Get all complaints, then filter
            var allComplaints = await _complaintRepository.GetAllAsync();
            var complaintList = hasDateFilter
                ? allComplaints.Where(c => c.CreatedAt >= start && c.CreatedAt <= end).ToList()
                : allComplaints.ToList();

            var totalRevenue = completedPayments.Sum(p => p.Amount ?? 0);
            var totalOrders = orderList.Count;

            return new DashboardStatistics
            {
                TotalOrders = totalOrders,
                TotalRevenue = totalRevenue,
                TotalCustomers = customers.Count,
                PendingOrders = orderList.Count(o => "pending".Equals(o.Status, StringComparison.OrdinalIgnoreCase)),
                ConfirmedOrders = orderList.Count(o => "confirmed".Equals(o.Status, StringComparison.OrdinalIgnoreCase)),
                ProcessingOrders = orderList.Count(o => "processing".Equals(o.Status, StringComparison.OrdinalIgnoreCase)),
                ShippedOrders = orderList.Count(o => "shipped".Equals(o.Status, StringComparison.OrdinalIgnoreCase)),
                DeliveredOrders = orderList.Count(o => "delivered".Equals(o.Status, StringComparison.OrdinalIgnoreCase)),
                CancelledOrders = orderList.Count(o => "cancelled".Equals(o.Status, StringComparison.OrdinalIgnoreCase)),
                AverageOrderValue = totalOrders > 0 ? totalRevenue / totalOrders : 0,
                TotalPreorders = preorderList.Count,
                TotalComplaints = complaintList.Count,
                PendingComplaints = complaintList.Count(c => "pending".Equals(c.Status, StringComparison.OrdinalIgnoreCase))
            };
        }

        #endregion

        #region Revenue

        public async Task<List<RevenueReport>> GetDailyRevenueAsync(DateTime startDate, DateTime endDate)
        {
            // Get all payments and filter in memory to avoid EF Core translation issues
            var allPayments = await _paymentRepository.GetAllAsync();
            
            var completedPayments = allPayments
                .Where(p => p.PaymentStatus != null && 
                            p.PaymentStatus.Equals("completed", StringComparison.OrdinalIgnoreCase) &&
                            p.PaidAt >= startDate && p.PaidAt <= endDate)
                .ToList();

            var dailyRevenue = completedPayments
                .Where(p => p.PaidAt.HasValue)
                .GroupBy(p => p.PaidAt!.Value.Date)
                .Select(g => new RevenueReport
                {
                    Date = g.Key,
                    Revenue = g.Sum(p => p.Amount ?? 0),
                    OrderCount = g.Count()
                })
                .OrderBy(r => r.Date)
                .ToList();

            // Fill in missing dates with zero revenue
            var result = new List<RevenueReport>();
            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                var existing = dailyRevenue.FirstOrDefault(r => r.Date == date);
                result.Add(existing ?? new RevenueReport { Date = date, Revenue = 0, OrderCount = 0 });
            }

            return result;
        }

        public async Task<List<RevenueReport>> GetMonthlyRevenueAsync(int year)
        {
            var startDate = new DateTime(year, 1, 1);
            var endDate = new DateTime(year, 12, 31, 23, 59, 59);

            // Get all payments and filter in memory
            var allPayments = await _paymentRepository.GetAllAsync();
            
            var completedPayments = allPayments
                .Where(p => p.PaymentStatus != null && 
                            p.PaymentStatus.Equals("completed", StringComparison.OrdinalIgnoreCase) &&
                            p.PaidAt >= startDate && p.PaidAt <= endDate)
                .ToList();

            var monthlyRevenue = completedPayments
                .Where(p => p.PaidAt.HasValue)
                .GroupBy(p => new { p.PaidAt!.Value.Year, p.PaidAt!.Value.Month })
                .Select(g => new RevenueReport
                {
                    Date = new DateTime(g.Key.Year, g.Key.Month, 1),
                    Revenue = g.Sum(p => p.Amount ?? 0),
                    OrderCount = g.Count()
                })
                .OrderBy(r => r.Date)
                .ToList();

            // Fill in missing months with zero revenue
            var result = new List<RevenueReport>();
            for (int month = 1; month <= 12; month++)
            {
                var date = new DateTime(year, month, 1);
                var existing = monthlyRevenue.FirstOrDefault(r => r.Date.Month == month);
                result.Add(existing ?? new RevenueReport { Date = date, Revenue = 0, OrderCount = 0 });
            }

            return result;
        }

        #endregion

        #region Popular Items

        public async Task<List<PopularFrame>> GetPopularFramesAsync(int limit = 10, DateTime? startDate = null, DateTime? endDate = null)
        {
            var hasDateFilter = startDate.HasValue || endDate.HasValue;
            var start = startDate ?? SqlMinDate;
            var end = endDate ?? SqlMaxDate;

            // Get order items with frame and order info
            var orderItems = await _orderItemRepository.GetAllAsyncInclude(
                oi => oi.Frame,
                oi => oi.Order
            );

            // Filter in memory
            var filteredOrderItems = orderItems
                .Where(oi => oi.Order != null && 
                             oi.Order.Status != null && 
                             !"cancelled".Equals(oi.Order.Status, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Apply date filter if specified
            if (hasDateFilter)
            {
                filteredOrderItems = filteredOrderItems
                    .Where(oi => oi.Order!.CreatedAt >= start && oi.Order.CreatedAt <= end)
                    .ToList();
            }

            var popularFrames = filteredOrderItems
                .Where(oi => oi.FrameId.HasValue && oi.Frame != null)
                .GroupBy(oi => oi.FrameId!.Value)
                .Select(g => new PopularFrame
                {
                    FrameId = g.Key,
                    FrameName = g.First().Frame?.FrameName,
                    Brand = g.First().Frame?.Brand,
                    BasePrice = g.First().Frame?.BasePrice,
                    TotalSold = g.Sum(oi => oi.Quantity ?? 0),
                    TotalRevenue = g.Sum(oi => (oi.OrderPrice ?? 0) * (oi.Quantity ?? 1))
                })
                .OrderByDescending(pf => pf.TotalSold)
                .Take(limit)
                .ToList();

            return popularFrames;
        }

        #endregion

        #region Order Summary

        public async Task<OrderSummary> GetOrderSummaryAsync()
        {
            var today = DateTime.UtcNow.Date;
            var weekStart = today.AddDays(-(int)today.DayOfWeek);
            var monthStart = new DateTime(today.Year, today.Month, 1);

            // Get all orders and filter in memory
            var allOrders = await _orderRepository.GetAllAsync();
            var orderList = allOrders
                .Where(o => o.Status != null && !"cancelled".Equals(o.Status, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Get all payments and filter in memory
            var allPayments = await _paymentRepository.GetAllAsync();
            var paymentList = allPayments
                .Where(p => p.PaymentStatus != null && 
                            p.PaymentStatus.Equals("completed", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Calculate summaries
            var todayOrders = orderList.Where(o => o.CreatedAt?.Date == today).ToList();
            var weekOrders = orderList.Where(o => o.CreatedAt >= weekStart).ToList();
            var monthOrders = orderList.Where(o => o.CreatedAt >= monthStart).ToList();

            var todayPayments = paymentList.Where(p => p.PaidAt?.Date == today);
            var weekPayments = paymentList.Where(p => p.PaidAt >= weekStart);
            var monthPayments = paymentList.Where(p => p.PaidAt >= monthStart);

            return new OrderSummary
            {
                TodayOrders = todayOrders.Count,
                WeekOrders = weekOrders.Count,
                MonthOrders = monthOrders.Count,
                TodayRevenue = todayPayments.Sum(p => p.Amount ?? 0),
                WeekRevenue = weekPayments.Sum(p => p.Amount ?? 0),
                MonthRevenue = monthPayments.Sum(p => p.Amount ?? 0)
            };
        }

        #endregion
    }
}
