using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repositories.Basic;
using Repositories.ModelExtensions;
using Repositories.Models;

namespace Services.GlassesService
{
    public class ReviewSummary
    {
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public Dictionary<int, int> RatingDistribution { get; set; } = new();
    }

    public interface IProductReviewService
    {
        Task<ProductReview> CreateReviewAsync(ProductReview review);
        Task<PaginationResult<ProductReview>> GetReviewsByFrameAsync(Guid frameId, int page = 1, int pageSize = 10);
        Task<PaginationResult<ProductReview>> GetReviewsByUserAsync(Guid userId, int page = 1, int pageSize = 10);
        Task<ProductReview?> UpdateReviewAsync(Guid reviewId, ProductReview update, Guid userId);
        Task<bool> DeleteReviewAsync(Guid reviewId, Guid userId);
        Task<ReviewSummary> GetFrameReviewSummaryAsync(Guid frameId);
        Task<bool> HideReviewAsync(Guid reviewId);
        Task<bool> IsVerifiedPurchaseAsync(Guid userId, Guid frameId);
    }

    public class ProductReviewService : IProductReviewService
    {
        private readonly GenericRepository<ProductReview> _reviewRepository;
        private readonly GenericRepository<OrderItem> _orderItemRepository;
        private readonly GenericRepository<Order> _orderRepository;
        private readonly GenericRepository<Frame> _frameRepository;
        private readonly GenericRepository<User> _userRepository;

        public ProductReviewService(
            GenericRepository<ProductReview> reviewRepository,
            GenericRepository<OrderItem> orderItemRepository,
            GenericRepository<Order> orderRepository,
            GenericRepository<Frame> frameRepository,
            GenericRepository<User> userRepository)
        {
            _reviewRepository = reviewRepository;
            _orderItemRepository = orderItemRepository;
            _orderRepository = orderRepository;
            _frameRepository = frameRepository;
            _userRepository = userRepository;
        }

        public async Task<ProductReview> CreateReviewAsync(ProductReview review)
        {
            // Validate user exists
            var users = await _userRepository.SearchAsync(u => u.UserId == review.UserId);
            if (!users.Any())
                throw new InvalidOperationException("User not found");

            // Validate frame exists
            var frames = await _frameRepository.SearchAsync(f => f.FrameId == review.FrameId);
            if (!frames.Any())
                throw new InvalidOperationException("Frame not found");

            // Validate rating
            if (review.Rating < 0 || review.Rating > 5)
                throw new InvalidOperationException("Rating must be between 0 and 5");

            // Check one review per user per frame
            var existingReviews = await _reviewRepository.SearchAsync(
                r => r.UserId == review.UserId && r.FrameId == review.FrameId);
            if (existingReviews.Any())
                throw new InvalidOperationException("You have already reviewed this frame");

            review.ReviewId = Guid.NewGuid();
            review.Status = "visible";
            review.CreatedAt = TimeHelper.Now;

            return await _reviewRepository.CreateAsync(review);
        }

        public async Task<PaginationResult<ProductReview>> GetReviewsByFrameAsync(Guid frameId, int page = 1, int pageSize = 10)
        {
            return await _reviewRepository.SearchWithPagingAsyncIncludeOrderBy(
                r => r.FrameId == frameId && r.Status == "visible",
                page,
                pageSize,
                orderBy: r => r.CreatedAt,
                ascending: false,
                r => r.User
            );
        }

        public async Task<PaginationResult<ProductReview>> GetReviewsByUserAsync(Guid userId, int page = 1, int pageSize = 10)
        {
            return await _reviewRepository.SearchWithPagingAsyncIncludeOrderBy(
                r => r.UserId == userId,
                page,
                pageSize,
                orderBy: r => r.CreatedAt,
                ascending: false,
                r => r.Frame
            );
        }

        public async Task<ProductReview?> UpdateReviewAsync(Guid reviewId, ProductReview update, Guid userId)
        {
            var reviews = await _reviewRepository.SearchAsync(r => r.ReviewId == reviewId);
            var existing = reviews.FirstOrDefault();
            if (existing == null || existing.UserId != userId) return null;

            if (update.Rating >= 0 && update.Rating <= 5)
                existing.Rating = update.Rating;
            if (update.Title != null)
                existing.Title = update.Title;
            if (update.Comment != null)
                existing.Comment = update.Comment;
            existing.UpdatedAt = TimeHelper.Now;

            return await _reviewRepository.UpdateAsync(existing);
        }

        public async Task<bool> DeleteReviewAsync(Guid reviewId, Guid userId)
        {
            var reviews = await _reviewRepository.SearchAsync(r => r.ReviewId == reviewId);
            var existing = reviews.FirstOrDefault();
            if (existing == null || existing.UserId != userId) return false;

            return await _reviewRepository.DeleteAsync(existing);
        }

        public async Task<ReviewSummary> GetFrameReviewSummaryAsync(Guid frameId)
        {
            var reviews = await _reviewRepository.SearchAsync(
                r => r.FrameId == frameId && r.Status == "visible");
            var reviewList = reviews.ToList();

            var distribution = new Dictionary<int, int>();
            for (int i = 0; i <= 5; i++)
            {
                distribution[i] = reviewList.Count(r => r.Rating == i);
            }

            return new ReviewSummary
            {
                AverageRating = reviewList.Any() ? reviewList.Average(r => r.Rating) : 0,
                TotalReviews = reviewList.Count,
                RatingDistribution = distribution
            };
        }

        public async Task<bool> HideReviewAsync(Guid reviewId)
        {
            var reviews = await _reviewRepository.SearchAsync(r => r.ReviewId == reviewId);
            var existing = reviews.FirstOrDefault();
            if (existing == null) return false;

            existing.Status = "hidden";
            await _reviewRepository.UpdateAsync(existing);
            return true;
        }

        public async Task<bool> IsVerifiedPurchaseAsync(Guid userId, Guid frameId)
        {
            var orderItems = await _orderItemRepository.GetAllAsyncInclude(oi => oi.Order);
            return orderItems.Any(oi =>
                oi.FrameId == frameId &&
                oi.Order != null &&
                oi.Order.UserId == userId &&
                oi.Order.Status != null &&
                oi.Order.Status.Equals("delivered", StringComparison.OrdinalIgnoreCase));
        }
    }
}
