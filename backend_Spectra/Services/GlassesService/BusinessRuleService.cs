using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repositories.Basic;
using Repositories.Models;

namespace Services.GlassesService
{
    public interface IBusinessRuleService
    {
        Task<List<BusinessRule>> GetAllRulesAsync();
        Task<List<BusinessRule>> GetRulesByCategoryAsync(string category);
        Task<BusinessRule?> GetRuleByKeyAsync(string ruleKey);
        Task<double> GetRuleValueAsDoubleAsync(string ruleKey, double defaultValue);
        Task<int> GetRuleValueAsIntAsync(string ruleKey, int defaultValue);
        Task<BusinessRule?> UpdateRuleAsync(string ruleKey, string newValue, string updatedBy);
    }

    public class BusinessRuleService : IBusinessRuleService
    {
        private readonly GenericRepository<BusinessRule> _ruleRepository;

        public BusinessRuleService(GenericRepository<BusinessRule> ruleRepository)
        {
            _ruleRepository = ruleRepository;
        }

        public async Task<List<BusinessRule>> GetAllRulesAsync()
        {
            var rules = await _ruleRepository.GetAllAsync();
            return rules.OrderBy(r => r.Category).ThenBy(r => r.RuleKey).ToList();
        }

        public async Task<List<BusinessRule>> GetRulesByCategoryAsync(string category)
        {
            var rules = await _ruleRepository.SearchAsync(
                r => r.Category != null && r.Category.ToLower() == category.ToLower());
            return rules.OrderBy(r => r.RuleKey).ToList();
        }

        public async Task<BusinessRule?> GetRuleByKeyAsync(string ruleKey)
        {
            var rules = await _ruleRepository.SearchAsync(r => r.RuleKey == ruleKey);
            return rules.FirstOrDefault();
        }

        public async Task<double> GetRuleValueAsDoubleAsync(string ruleKey, double defaultValue)
        {
            var rule = await GetRuleByKeyAsync(ruleKey);
            if (rule != null && double.TryParse(rule.RuleValue, out var value))
                return value;
            return defaultValue;
        }

        public async Task<int> GetRuleValueAsIntAsync(string ruleKey, int defaultValue)
        {
            var rule = await GetRuleByKeyAsync(ruleKey);
            if (rule != null && int.TryParse(rule.RuleValue, out var value))
                return value;
            return defaultValue;
        }

        public async Task<BusinessRule?> UpdateRuleAsync(string ruleKey, string newValue, string updatedBy)
        {
            var rules = await _ruleRepository.SearchAsync(r => r.RuleKey == ruleKey);
            var rule = rules.FirstOrDefault();
            if (rule == null) return null;

            rule.RuleValue = newValue;
            rule.UpdatedAt = DateTime.UtcNow;
            rule.UpdatedBy = updatedBy;

            return await _ruleRepository.UpdateAsync(rule);
        }
    }
}
