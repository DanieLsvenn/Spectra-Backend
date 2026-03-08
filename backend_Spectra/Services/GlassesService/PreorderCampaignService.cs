using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repositories.Basic;
using Repositories.Models;

namespace Services.GlassesService
{
    public interface IPreorderCampaignService
    {
        Task<PreorderCampaign> CreateCampaignAsync(PreorderCampaign campaign, List<CampaignFrame> frames);
        Task<List<PreorderCampaign>> GetActiveCampaignsAsync();
        Task<PreorderCampaign?> GetCampaignByIdAsync(Guid campaignId);
        Task<PreorderCampaign?> UpdateCampaignAsync(Guid campaignId, PreorderCampaign updates);
        Task<bool> EndCampaignAsync(Guid campaignId);
        Task<bool> ValidatePreorderAgainstCampaignAsync(Guid campaignId, List<PreorderItem> items);
        Task<bool> IncrementCampaignSlotsAsync(Guid campaignId);
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

        public async Task<PreorderCampaign> CreateCampaignAsync(PreorderCampaign campaign, List<CampaignFrame> frames)
        {
            campaign.CampaignId = Guid.NewGuid();
            campaign.Status = "upcoming";
            campaign.CurrentSlots = 0;
            campaign.CreatedAt = DateTime.UtcNow;

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

        public async Task<List<PreorderCampaign>> GetActiveCampaignsAsync()
        {
            var now = DateTime.UtcNow;
            var campaigns = await _campaignRepository.GetAllAsyncInclude(c => c.CampaignFrames);
            return campaigns
                .Where(c => c.StartDate <= now && c.EndDate >= now &&
                            (c.Status == "upcoming" || c.Status == "active"))
                .ToList();
        }

        public async Task<PreorderCampaign?> GetCampaignByIdAsync(Guid campaignId)
        {
            var campaigns = await _campaignRepository.SearchAsyncInclude(
                c => c.CampaignId == campaignId,
                c => c.CampaignFrames);
            return campaigns.FirstOrDefault();
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
            existing.EndDate = DateTime.UtcNow;
            await _campaignRepository.UpdateAsync(existing);
            return true;
        }

        public async Task<bool> ValidatePreorderAgainstCampaignAsync(Guid campaignId, List<PreorderItem> items)
        {
            var campaign = await GetCampaignByIdAsync(campaignId);
            if (campaign == null) return false;

            var now = DateTime.UtcNow;
            if (campaign.StartDate > now || campaign.EndDate < now)
                return false;

            if (campaign.MaxSlots.HasValue && campaign.CurrentSlots >= campaign.MaxSlots.Value)
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

        public async Task<bool> IncrementCampaignSlotsAsync(Guid campaignId)
        {
            var campaigns = await _campaignRepository.SearchAsync(c => c.CampaignId == campaignId);
            var campaign = campaigns.FirstOrDefault();
            if (campaign == null) return false;

            campaign.CurrentSlots++;
            if (campaign.Status == "upcoming")
                campaign.Status = "active";

            await _campaignRepository.UpdateAsync(campaign);
            return true;
        }
    }
}
