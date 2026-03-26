using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repositories.Basic;
using Repositories.Models;

namespace Services.GlassesService
{
    /// <summary>
    /// Preorder info to attach to a Frame response when the frame is out of stock
    /// but belongs to an active campaign.
    /// </summary>
    public class PreorderInfoDto
    {
        public Guid CampaignId { get; set; }
        public string CampaignName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int? MaxSlots { get; set; }
        public int CurrentSlots { get; set; }
        public DateTime? EstimatedDeliveryDate { get; set; }
        public double? CampaignPrice { get; set; }
        public int MaxQuantityPerOrder { get; set; }
    }

    public interface IPreorderCampaignService
    {
        Task<PreorderCampaign> CreateCampaignAsync(PreorderCampaign campaign, List<CampaignFrame> frames);
        Task<List<PreorderCampaign>> GetAllCampaignsAsync();
        Task<List<PreorderCampaign>> GetActiveCampaignsAsync();
        Task<PreorderCampaign?> GetCampaignByIdAsync(Guid campaignId);
        Task<PreorderCampaign?> UpdateCampaignAsync(Guid campaignId, PreorderCampaign updates);
        Task<bool> EndCampaignAsync(Guid campaignId);
        Task<bool> ValidatePreorderAgainstCampaignAsync(Guid campaignId, List<PreorderItem> items);
        Task<bool> IncrementCampaignSlotsAsync(Guid campaignId, int quantity);
        /// <summary>
        /// Returns the set of FrameIds that belong to upcoming (not yet started) campaigns.
        /// </summary>
        Task<HashSet<Guid>> GetUpcomingCampaignFrameIdsAsync();

        /// <summary>
        /// Returns a dictionary mapping FrameId to PreorderInfoDto for frames that belong
        /// to currently active campaigns (status == "active").
        /// </summary>
        Task<Dictionary<Guid, PreorderInfoDto>> GetActiveCampaignFrameInfoAsync();
    }

    public class PreorderCampaignService : IPreorderCampaignService
    {
        private readonly GenericRepository<PreorderCampaign> _campaignRepository;
        private readonly GenericRepository<CampaignFrame> _campaignFrameRepository;
        private readonly GenericRepository<Frame> _frameRepository;

        public PreorderCampaignService(
            GenericRepository<PreorderCampaign> campaignRepository,
            GenericRepository<CampaignFrame> campaignFrameRepository,
            GenericRepository<Frame> frameRepository)
        {
            _campaignRepository = campaignRepository;
            _campaignFrameRepository = campaignFrameRepository;
            _frameRepository = frameRepository;
        }

        /// <summary>
        /// Computes the effective runtime status of a campaign based on its dates and
        /// persisted status. If the persisted status is "upcoming" but the current time
        /// is within [StartDate, EndDate], the effective status is "active".
        /// If the current time is past EndDate and the status is not already "ended",
        /// the effective status is "ended".
        /// </summary>
        private static string ResolveEffectiveStatus(PreorderCampaign campaign, DateTime now)
        {
            // If the campaign was manually ended, respect that.
            if (string.Equals(campaign.Status, "ended", StringComparison.OrdinalIgnoreCase))
                return "ended";

            // Past the end date ? ended
            if (now > campaign.EndDate)
                return "ended";

            // Within the date range ? active
            if (now >= campaign.StartDate && now <= campaign.EndDate)
                return "active";

            // Before the start date ? upcoming
            return "upcoming";
        }

        /// <summary>
        /// Auto-corrects a campaign's persisted status if it is stale (e.g. still "upcoming"
        /// when it should be "active" based on dates). Persists the change to the database
        /// so subsequent queries are consistent.
        /// </summary>
        private async Task AutoUpdateCampaignStatusAsync(PreorderCampaign campaign, DateTime now)
        {
            var effectiveStatus = ResolveEffectiveStatus(campaign, now);
            if (!string.Equals(campaign.Status, effectiveStatus, StringComparison.OrdinalIgnoreCase))
            {
                campaign.Status = effectiveStatus;
                await _campaignRepository.UpdateAsync(campaign);
            }
        }

        /// <summary>
        /// Auto-corrects persisted statuses for a batch of campaigns.
        /// </summary>
        private async Task AutoUpdateCampaignStatusesAsync(List<PreorderCampaign> campaigns, DateTime now)
        {
            foreach (var campaign in campaigns)
            {
                await AutoUpdateCampaignStatusAsync(campaign, now);
            }
        }

        public async Task<PreorderCampaign> CreateCampaignAsync(PreorderCampaign campaign, List<CampaignFrame> frames)
        {
            campaign.CampaignId = Guid.NewGuid();
            campaign.Status = "upcoming";
            campaign.CurrentSlots = 0;
            campaign.CreatedAt = TimeHelper.Now;

            // Validate that all referenced frames exist
            foreach (var frame in frames)
            {
                if (frame.FrameId.HasValue)
                {
                    var existingFrame = await _frameRepository.GetByIdAsync(frame.FrameId.Value);
                    if (existingFrame == null)
                        throw new ArgumentException($"Frame with ID {frame.FrameId} not found");
                }
            }

            var created = await _campaignRepository.CreateAsync(campaign);

            foreach (var frame in frames)
            {
                frame.CampaignFrameId = Guid.NewGuid();
                frame.CampaignId = created.CampaignId;
                if (frame.MaxQuantityPerOrder <= 0)
                    frame.MaxQuantityPerOrder = 2;
                await _campaignFrameRepository.CreateAsync(frame);
            }

            return await GetCampaignByIdAsync(created.CampaignId) ?? created;
        }

        public async Task<List<PreorderCampaign>> GetAllCampaignsAsync()
        {
            var campaigns = await _campaignRepository.GetAllAsyncInclude(c => c.CampaignFrames);
            var list = campaigns.OrderByDescending(c => c.CreatedAt).ToList();

            // Auto-correct stale statuses
            await AutoUpdateCampaignStatusesAsync(list, TimeHelper.Now);

            return list;
        }

        public async Task<List<PreorderCampaign>> GetActiveCampaignsAsync()
        {
            var now = TimeHelper.Now;
            var campaigns = await _campaignRepository.GetAllAsyncInclude(c => c.CampaignFrames);
            var list = campaigns.ToList();

            // Auto-correct stale statuses before filtering
            await AutoUpdateCampaignStatusesAsync(list, now);

            return list
                .Where(c => c.StartDate <= now && c.EndDate >= now &&
                            (c.Status == "upcoming" || c.Status == "active"))
                .ToList();
        }

        public async Task<PreorderCampaign?> GetCampaignByIdAsync(Guid campaignId)
        {
            var campaigns = await _campaignRepository.SearchAsyncInclude(
                c => c.CampaignId == campaignId,
                c => c.CampaignFrames);
            var campaign = campaigns.FirstOrDefault();

            if (campaign != null)
            {
                await AutoUpdateCampaignStatusAsync(campaign, TimeHelper.Now);
            }

            return campaign;
        }

        public async Task<PreorderCampaign?> UpdateCampaignAsync(Guid campaignId, PreorderCampaign updates)
        {
            var campaigns = await _campaignRepository.SearchAsync(c => c.CampaignId == campaignId);
            var existing = campaigns.FirstOrDefault();
            if (existing == null) return null;

            if (!string.IsNullOrEmpty(updates.CampaignName))
                existing.CampaignName = updates.CampaignName;
            if (updates.Description != null)
                existing.Description = updates.Description;
            if (updates.MaxSlots.HasValue)
                existing.MaxSlots = updates.MaxSlots;
            if (updates.EstimatedDeliveryDate.HasValue)
                existing.EstimatedDeliveryDate = updates.EstimatedDeliveryDate;

            await _campaignRepository.UpdateAsync(existing);

            return await GetCampaignByIdAsync(campaignId);
        }

        public async Task<bool> EndCampaignAsync(Guid campaignId)
        {
            var campaigns = await _campaignRepository.SearchAsync(c => c.CampaignId == campaignId);
            var existing = campaigns.FirstOrDefault();
            if (existing == null) return false;

            existing.Status = "ended";
            existing.EndDate = TimeHelper.Now;
            await _campaignRepository.UpdateAsync(existing);
            return true;
        }

        public async Task<bool> ValidatePreorderAgainstCampaignAsync(Guid campaignId, List<PreorderItem> items)
        {
            var campaign = await GetCampaignByIdAsync(campaignId);
            if (campaign == null) return false;

            var now = TimeHelper.Now;
            if (campaign.StartDate > now || campaign.EndDate < now)
                return false;

            // Calculate total quantity across all items
            int totalQuantity = items.Sum(i => i.Quantity ?? 1);

            if (campaign.MaxSlots.HasValue && (campaign.CurrentSlots + totalQuantity) > campaign.MaxSlots.Value)
                return false;

            var campaignFrameIds = campaign.CampaignFrames.Select(cf => cf.FrameId).ToHashSet();
            foreach (var item in items)
            {
                if (!item.FrameId.HasValue || !campaignFrameIds.Contains(item.FrameId))
                    return false;

                var cf = campaign.CampaignFrames.FirstOrDefault(cf => cf.FrameId == item.FrameId);
                if (cf != null && (item.Quantity ?? 1) > cf.MaxQuantityPerOrder)
                    return false;
            }

            return true;
        }

        public async Task<bool> IncrementCampaignSlotsAsync(Guid campaignId, int quantity)
        {
            var campaigns = await _campaignRepository.SearchAsync(c => c.CampaignId == campaignId);
            var campaign = campaigns.FirstOrDefault();
            if (campaign == null) return false;

            campaign.CurrentSlots += quantity;
            if (campaign.Status == "upcoming")
                campaign.Status = "active";

            await _campaignRepository.UpdateAsync(campaign);
            return true;
        }

        public async Task<HashSet<Guid>> GetUpcomingCampaignFrameIdsAsync()
        {
            var now = TimeHelper.Now;
            var campaigns = await _campaignRepository.GetAllAsyncInclude(c => c.CampaignFrames);
            var list = campaigns.ToList();

            // Auto-correct stale statuses so that campaigns whose start date
            // has passed are no longer treated as "upcoming"
            await AutoUpdateCampaignStatusesAsync(list, now);

            return list
                .Where(c => string.Equals(c.Status, "upcoming", StringComparison.OrdinalIgnoreCase)
                             && c.StartDate > now)
                .SelectMany(c => c.CampaignFrames)
                .Where(cf => cf.FrameId.HasValue)
                .Select(cf => cf.FrameId!.Value)
                .ToHashSet();
        }

        public async Task<Dictionary<Guid, PreorderInfoDto>> GetActiveCampaignFrameInfoAsync()
        {
            var now = TimeHelper.Now;
            var campaigns = await _campaignRepository.GetAllAsyncInclude(c => c.CampaignFrames);
            var list = campaigns.ToList();

            // Auto-correct stale statuses so that campaigns within their date range
            // are properly marked "active"
            await AutoUpdateCampaignStatusesAsync(list, now);

            var activeCampaigns = list
                .Where(c => c.StartDate <= now && c.EndDate >= now && c.Status == "active")
                .ToList();

            var frameInfoDict = new Dictionary<Guid, PreorderInfoDto>();
            foreach (var campaign in activeCampaigns)
            {
                foreach (var frame in campaign.CampaignFrames)
                {
                    if (frame.FrameId.HasValue)
                    {
                        frameInfoDict[frame.FrameId.Value] = new PreorderInfoDto
                        {
                            CampaignId = campaign.CampaignId,
                            CampaignName = campaign.CampaignName,
                            Description = campaign.Description,
                            StartDate = campaign.StartDate,
                            EndDate = campaign.EndDate,
                            MaxSlots = campaign.MaxSlots,
                            CurrentSlots = campaign.CurrentSlots,
                            EstimatedDeliveryDate = campaign.EstimatedDeliveryDate,
                            CampaignPrice = frame.CampaignPrice,
                            MaxQuantityPerOrder = frame.MaxQuantityPerOrder
                        };
                    }
                }
            }

            return frameInfoDict;
        }
    }
}
