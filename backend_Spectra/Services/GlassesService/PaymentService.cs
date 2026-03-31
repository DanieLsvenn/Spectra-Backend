using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Repositories.Basic;
using Repositories.ModelExtensions;
using Repositories.Models;

namespace Services.GlassesService
{
    public class VnPayPaymentRequest
    {
        public Guid PaymentId { get; set; }
        public double Amount { get; set; }
        public string OrderInfo { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string ReturnUrl { get; set; } = string.Empty;
    }

    public class VnPayPaymentResponse
    {
        public bool Success { get; set; }
        public string PaymentUrl { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class VnPayCallbackResult
    {
        public bool Success { get; set; }
        public Guid PaymentId { get; set; }
        public string TransactionId { get; set; } = string.Empty;
        public string ResponseCode { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public interface IPaymentService
    {
        // Create operations
        Task<Payment> CreatePaymentAsync(Payment payment);
        Task<(Payment? Payment, string Error)> CreatePaymentForOrderAsync(Guid orderId, string paymentMethod);
        Task<(Payment? Payment, string Error)> CreatePaymentForPreorderAsync(Guid preorderId, string paymentMethod);

        // Read operations
        Task<Payment?> GetPaymentByIdAsync(Guid paymentId);
        Task<List<Payment>> GetPaymentsByUserAsync(Guid userId);
        Task<PaginationResult<Payment>> GetPaymentsByUserAsync(Guid userId, int currentPage = 1, int pageSize = 10);
        Task<List<Payment>> GetPaymentsByOrderAsync(Guid orderId);
        Task<List<Payment>> GetPaymentsByPreorderAsync(Guid preorderId);

        // Update operations
        Task<Payment?> UpdatePaymentStatusAsync(Guid paymentId, string status);
        Task<bool> HasCompletedPaymentAsync(Guid? orderId, Guid? preorderId);

        // VNPay integration
        VnPayPaymentResponse CreateVnPayPaymentUrl(VnPayPaymentRequest request);
        VnPayCallbackResult ProcessVnPayCallback(Dictionary<string, string> vnpayData);
        Task<Payment?> CompleteVnPayPaymentAsync(Guid paymentId, string transactionId);
    }

    public class PaymentService : IPaymentService
    {
        private readonly GenericRepository<Payment> _paymentRepository;
        private readonly GenericRepository<Order> _orderRepository;
        private readonly IOrderService _orderService;
        private readonly GenericRepository<Preorder> _preorderRepository;
        private readonly IConfiguration _configuration;
        private readonly IExchangeRateService _exchangeRateService;

        // Payment statuses
        public static class PaymentStatus
        {
            public const string Pending = "pending";
            public const string Processing = "processing";
            public const string Completed = "completed";
            public const string Failed = "failed";
            public const string Cancelled = "cancelled";
            public const string Refunded = "refunded";
        }

        // Payment methods
        public static class PaymentMethod
        {
            public const string VnPay = "vnpay";
            public const string Cash = "cash";
            public const string BankTransfer = "bank_transfer";
        }

        public PaymentService(
            GenericRepository<Payment> paymentRepository,
            GenericRepository<Order> orderRepository,
            IOrderService orderService,
            GenericRepository<Preorder> preorderRepository,
            IConfiguration configuration,
            IExchangeRateService exchangeRateService)
        {
            _paymentRepository = paymentRepository;
            _orderRepository = orderRepository;
            _orderService = orderService;
            _preorderRepository = preorderRepository;
            _configuration = configuration;
            _exchangeRateService = exchangeRateService;
        }

        #region Create Operations

        public async Task<Payment> CreatePaymentAsync(Payment payment)
        {
            payment.PaymentId = Guid.NewGuid();
            payment.PaymentStatus = PaymentStatus.Pending;
            return await _paymentRepository.CreateAsync(payment);
        }

        public async Task<(Payment? Payment, string Error)> CreatePaymentForOrderAsync(Guid orderId, string paymentMethod)
        {
            // Load order with items/shipping details so we can guarantee amount includes shipping.
            var order = await _orderService.GetOrderByIdWithDetailsAsync(orderId);

            if (order == null)
            {
                return (null, "Order not found");
            }

            // Check for existing completed payment (prevent double payment)
            if (await HasCompletedPaymentAsync(orderId, null))
            {
                return (null, "Order has already been paid");
            }

            // Check for existing pending payment
            var existingPayments = await _paymentRepository.SearchAsync(p =>
                p.OrderId == orderId &&
                p.PaymentStatus != null && p.PaymentStatus.ToLower() == PaymentStatus.Pending);

            if (existingPayments.Any())
            {
                return (null, "A pending payment already exists for this order");
            }

            // Ensure shipping fee is included in final payment amount (order.TotalAmount is expected to include it,
            // but sometimes order may still be item subtotal when race condition or stale data occurs).
            double finalAmount = order.TotalAmount ?? 0;
            var orderItemsTotal = order.OrderItems?.Sum(i => (i.UnitPrice ?? 0) * (i.Quantity ?? 1)) ?? 0;
            var shippingFee = order.ShippingFee ?? 0;

            // If order total doesn't already include shipping, add it.
            if (Math.Abs(finalAmount - (orderItemsTotal + shippingFee)) > 0.01)
            {
                // items total + shipping (as expected). If finalAmount already includes shipping exactly, this does nothing.
                finalAmount = orderItemsTotal + shippingFee;
            }

            var payment = new Payment
            {
                PaymentId = Guid.NewGuid(),
                OrderId = orderId,
                PreorderId = null, // Ensure only one FK is set
                Amount = finalAmount,
                PaymentMethod = paymentMethod.ToLower(),
                PaymentStatus = PaymentStatus.Pending
            };

            var createdPayment = await _paymentRepository.CreateAsync(payment);
            return (createdPayment, string.Empty);
        }

        public async Task<(Payment? Payment, string Error)> CreatePaymentForPreorderAsync(Guid preorderId, string paymentMethod)
        {
            // Validate only one foreign key
            var preorders = await _preorderRepository.SearchAsyncInclude(
                p => p.PreorderId == preorderId,
                p => p.PreorderItems);
            var preorder = preorders.FirstOrDefault();

            if (preorder == null)
            {
                return (null, "Preorder not found");
            }

            // Check for existing completed payment (prevent double payment)
            if (await HasCompletedPaymentAsync(null, preorderId))
            {
                return (null, "Preorder has already been paid");
            }

            // Calculate preorder total
            double totalAmount = 0;
            foreach (var item in preorder.PreorderItems)
            {
                totalAmount += (item.UnitPrice ?? 0) * (item.Quantity ?? 1);
            }

            var payment = new Payment
            {
                PaymentId = Guid.NewGuid(),
                OrderId = null, // Ensure only one FK is set
                PreorderId = preorderId,
                Amount = totalAmount,
                PaymentMethod = paymentMethod.ToLower(),
                PaymentStatus = PaymentStatus.Pending
            };

            var createdPayment = await _paymentRepository.CreateAsync(payment);
            return (createdPayment, string.Empty);
        }

        #endregion

        #region Read Operations

        public async Task<Payment?> GetPaymentByIdAsync(Guid paymentId)
        {
            var payments = await _paymentRepository.SearchAsync(p => p.PaymentId == paymentId);
            return payments.FirstOrDefault();
        }

        public async Task<List<Payment>> GetPaymentsByUserAsync(Guid userId)
        {
            // Get payments through orders
            var orderPayments = await _paymentRepository.SearchAsyncInclude(
                p => p.Order != null && p.Order.UserId == userId,
                p => p.Order);

            // Get payments through preorders
            var preorderPayments = await _paymentRepository.SearchAsyncInclude(
                p => p.Preorder != null && p.Preorder.UserId == userId,
                p => p.Preorder);

            var allPayments = orderPayments.ToList();
            allPayments.AddRange(preorderPayments.Where(pp => !allPayments.Any(op => op.PaymentId == pp.PaymentId)));

            return allPayments.OrderByDescending(p => p.PaidAt).ToList();
        }

        public async Task<PaginationResult<Payment>> GetPaymentsByUserAsync(Guid userId, int currentPage = 1, int pageSize = 10)
        {
            var allPayments = await GetPaymentsByUserAsync(userId);
            
            var totalItems = allPayments.Count;
            var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            var items = allPayments
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PaginationResult<Payment>
            {
                TotalItems = totalItems,
                TotalPages = totalPages,
                CurrentPage = currentPage,
                PageSize = pageSize,
                Items = items
            };
        }

        public async Task<List<Payment>> GetPaymentsByOrderAsync(Guid orderId)
        {
            var payments = await _paymentRepository.SearchAsync(p => p.OrderId == orderId);
            return payments.ToList();
        }

        public async Task<List<Payment>> GetPaymentsByPreorderAsync(Guid preorderId)
        {
            var payments = await _paymentRepository.SearchAsync(p => p.PreorderId == preorderId);
            return payments.ToList();
        }

        #endregion

        #region Update Operations

        public async Task<Payment?> UpdatePaymentStatusAsync(Guid paymentId, string status)
        {
            var payment = await GetPaymentByIdAsync(paymentId);

            if (payment == null)
            {
                return null;
            }

            // Validate status
            var validStatuses = new[] { PaymentStatus.Pending, PaymentStatus.Processing, 
                PaymentStatus.Completed, PaymentStatus.Failed, PaymentStatus.Cancelled, PaymentStatus.Refunded };
            
            if (!validStatuses.Contains(status.ToLower()))
            {
                return null;
            }

            // Prevent updating completed payments (except for refund)
            if (payment.PaymentStatus?.ToLower() == PaymentStatus.Completed && 
                status.ToLower() != PaymentStatus.Refunded)
            {
                return null;
            }

            payment.PaymentStatus = status.ToLower();

            if (status.ToLower() == PaymentStatus.Completed)
            {
                payment.PaidAt = TimeHelper.Now;
            }

            return await _paymentRepository.UpdateAsync(payment);
        }

        public async Task<bool> HasCompletedPaymentAsync(Guid? orderId, Guid? preorderId)
        {
            IEnumerable<Payment> payments;

            if (orderId.HasValue)
            {
                payments = await _paymentRepository.SearchAsync(p =>
                    p.OrderId == orderId &&
                    p.PaymentStatus != null && p.PaymentStatus.ToLower() == PaymentStatus.Completed);
            }
            else if (preorderId.HasValue)
            {
                payments = await _paymentRepository.SearchAsync(p =>
                    p.PreorderId == preorderId &&
                    p.PaymentStatus != null && p.PaymentStatus.ToLower() == PaymentStatus.Completed);
            }
            else
            {
                return false;
            }

            return payments.Any();
        }

        #endregion

        #region VNPay Integration

        public VnPayPaymentResponse CreateVnPayPaymentUrl(VnPayPaymentRequest request)
        {
            try
            {
                var vnpayConfig = _configuration.GetSection("VnPay");
                var vnp_TmnCode = vnpayConfig["TmnCode"] ?? "DEMO1234";
                var vnp_HashSecret = vnpayConfig["HashSecret"] ?? "DEMOSECRET1234567890";
                var vnp_Url = vnpayConfig["PaymentUrl"] ?? "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
                var vnp_Version = vnpayConfig["Version"] ?? "2.1.0";

                // Ensure we use the actual order/preorder total (with shipping included) if possible.
                var usdAmount = request.Amount;
                var paymentRecord = GetPaymentByIdAsync(request.PaymentId).GetAwaiter().GetResult();

                if (paymentRecord != null)
                {
                    if (paymentRecord.OrderId.HasValue)
                    {
                        var order = _orderService.GetOrderByIdWithDetailsAsync(paymentRecord.OrderId.Value).GetAwaiter().GetResult();
                        if (order != null && order.TotalAmount.HasValue)
                        {
                            usdAmount = order.TotalAmount.Value;
                        }
                    }
                    else if (paymentRecord.PreorderId.HasValue)
                    {
                        var preorder = _preorderRepository.SearchAsync(p => p.PreorderId == paymentRecord.PreorderId.Value).GetAwaiter().GetResult().FirstOrDefault();
                        if (preorder != null && preorder.TotalAmount.HasValue)
                        {
                            usdAmount = preorder.TotalAmount.Value;
                        }
                    }
                }


                // Convert USD to VND using the rate service (which now reads business-rule override first)
                var usdToVndRate = _exchangeRateService.GetUsdToVndRateAsync().GetAwaiter().GetResult();
                var amountInVnd = usdAmount * usdToVndRate;

                // Align with front-end rounding policy: round to nearest 500 VND
                var amountInVndRounded = Math.Round(amountInVnd / 500.0, MidpointRounding.AwayFromZero) * 500.0;

                // VNPay requires amount in VND * 100 (no decimal)
                // Minimum amount is typically 10,000 VND
                var vnpayAmount = (long)(amountInVndRounded * 100);

                // Use Vietnam timezone (UTC+7) for VNPay
                var vietnamNow = TimeHelper.Now;

                var vnpay = new SortedDictionary<string, string>();

                vnpay.Add("vnp_Amount", vnpayAmount.ToString());
                vnpay.Add("vnp_Command", "pay");
                vnpay.Add("vnp_CreateDate", vietnamNow.ToString("yyyyMMddHHmmss"));
                vnpay.Add("vnp_CurrCode", "VND");
                vnpay.Add("vnp_ExpireDate", vietnamNow.AddMinutes(15).ToString("yyyyMMddHHmmss"));
                vnpay.Add("vnp_IpAddr", request.IpAddress);
                vnpay.Add("vnp_Locale", "vn");
                vnpay.Add("vnp_OrderInfo", request.OrderInfo);
                vnpay.Add("vnp_OrderType", "other");
                vnpay.Add("vnp_ReturnUrl", request.ReturnUrl);
                vnpay.Add("vnp_TmnCode", vnp_TmnCode);
                vnpay.Add("vnp_TxnRef", request.PaymentId.ToString());
                vnpay.Add("vnp_Version", vnp_Version);

                // Build the data string for hashing (URL encoded values)
                var signData = string.Join("&", vnpay.Select(kvp => $"{kvp.Key}={WebUtility.UrlEncode(kvp.Value)}"));
                
                // Create secure hash
                var secureHash = HmacSHA512(vnp_HashSecret, signData);

                // Build query string (URL encoded)
                var queryString = new StringBuilder();
                foreach (var kvp in vnpay)
                {
                    if (queryString.Length > 0)
                    {
                        queryString.Append("&");
                    }
                    queryString.Append(kvp.Key);
                    queryString.Append("=");
                    queryString.Append(WebUtility.UrlEncode(kvp.Value));
                }

                var paymentUrl = $"{vnp_Url}?{queryString}&vnp_SecureHash={secureHash}";

                return new VnPayPaymentResponse
                {
                    Success = true,
                    PaymentUrl = paymentUrl,
                    Message = "Payment URL generated successfully"
                };
            }
            catch (Exception ex)
            {
                return new VnPayPaymentResponse
                {
                    Success = false,
                    PaymentUrl = string.Empty,
                    Message = $"Error generating payment URL: {ex.Message}"
                };
            }
        }

        public VnPayCallbackResult ProcessVnPayCallback(Dictionary<string, string> vnpayData)
        {
            try
            {
                var vnpayConfig = _configuration.GetSection("VnPay");
                var vnp_HashSecret = vnpayConfig["HashSecret"] ?? "DEMOSECRET1234567890";

                // Get secure hash from callback
                if (!vnpayData.TryGetValue("vnp_SecureHash", out var vnp_SecureHash))
                {
                    return new VnPayCallbackResult
                    {
                        Success = false,
                        Message = "Missing secure hash"
                    };
                }

                // Remove hash fields for verification
                var dataToVerify = new SortedDictionary<string, string>(vnpayData);
                dataToVerify.Remove("vnp_SecureHash");
                dataToVerify.Remove("vnp_SecureHashType");

                // Verify signature (values should be URL encoded as received from query string)
                var signData = string.Join("&", dataToVerify.Select(kvp => $"{kvp.Key}={WebUtility.UrlEncode(kvp.Value)}"));
                var calculatedHash = HmacSHA512(vnp_HashSecret, signData);

                if (!calculatedHash.Equals(vnp_SecureHash, StringComparison.InvariantCultureIgnoreCase))
                {
                    return new VnPayCallbackResult
                    {
                        Success = false,
                        Message = "Invalid signature"
                    };
                }

                // Get response code
                vnpayData.TryGetValue("vnp_ResponseCode", out var responseCode);
                vnpayData.TryGetValue("vnp_TxnRef", out var txnRef);
                vnpayData.TryGetValue("vnp_TransactionNo", out var transactionNo);

                var success = responseCode == "00";

                return new VnPayCallbackResult
                {
                    Success = success,
                    PaymentId = Guid.TryParse(txnRef, out var paymentId) ? paymentId : Guid.Empty,
                    TransactionId = transactionNo ?? string.Empty,
                    ResponseCode = responseCode ?? string.Empty,
                    Message = success ? "Payment successful" : $"Payment failed with code: {responseCode}"
                };
            }
            catch (Exception ex)
            {
                return new VnPayCallbackResult
                {
                    Success = false,
                    Message = $"Error processing callback: {ex.Message}"
                };
            }
        }

        public async Task<Payment?> CompleteVnPayPaymentAsync(Guid paymentId, string transactionId)
        {
            var payment = await GetPaymentByIdAsync(paymentId);

            if (payment == null)
            {
                return null;
            }

            // Skip if already completed (idempotent for IPN + return URL race)
            if (payment.PaymentStatus?.ToLower() == PaymentStatus.Completed)
            {
                return payment;
            }

            payment.PaymentStatus = PaymentStatus.Completed;
            payment.PaidAt = TimeHelper.Now;

            // Use SaveAsync — only updates changed columns (PaymentStatus, PaidAt).
            // UpdateAsync uses EntityState.Modified which overwrites ALL columns and
            // was proven to set Order.userId to NULL in production.
            await _paymentRepository.SaveAsync();

            // Update order status — use SaveAsync to only change the Status column
            if (payment.OrderId.HasValue)
            {
                var orders = await _orderRepository.SearchAsync(o => o.OrderId == payment.OrderId);
                var order = orders.FirstOrDefault();
                if (order != null && order.Status?.ToLower() == "pending")
                {
                    order.Status = "confirmed";
                    await _orderRepository.SaveAsync();
                }
            }

            // Update preorder status
            if (payment.PreorderId.HasValue)
            {
                var preorders = await _preorderRepository.SearchAsync(p => p.PreorderId == payment.PreorderId);
                var preorder = preorders.FirstOrDefault();
                if (preorder != null)
                {
                    preorder.Status = "paid";
                    await _preorderRepository.SaveAsync();
                }
            }

            return payment;
        }

        private static string HmacSHA512(string key, string inputData)
        {
            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
            var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(inputData));
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }

        #endregion
    }
}
