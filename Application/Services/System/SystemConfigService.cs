using Application.DTOs.System;
using Application.Interfaces.Data;
using Application.Interfaces.System;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Application.Services.System
{
    public class SystemConfigService : ISystemConfigService
    {
        private readonly IMlndexDbContext _context;

        public SystemConfigService(IMlndexDbContext context)
        {
            _context = context;
        }

        public async Task<SystemConfigDto> GetAsync(CancellationToken cancellationToken = default)
        {
            var settings = await _context.SystemSettings.ToDictionaryAsync(x => x.Key, x => x.Value, cancellationToken);

            return new SystemConfigDto
            {
                ExchangeRateCoinToVnd = decimal.TryParse(GetValue(settings, "ExchangeRateCoinToVnd"), out var rate) ? rate : 1000,
                WithdrawalFeePercent = decimal.TryParse(GetValue(settings, "WithdrawalFeePercent"), out var fee) ? fee : 10,
                WithdrawalMinCoins = decimal.TryParse(GetValue(settings, "WithdrawalMinCoins"), out var min) ? min : 50,
                WithdrawalMaxCoins = decimal.TryParse(GetValue(settings, "WithdrawalMaxCoins"), out var max) ? max : 1000,
                BlacklistWords = string.IsNullOrEmpty(GetValue(settings, "BlacklistWords")) 
                    ? new List<string>() 
                    : JsonSerializer.Deserialize<List<string>>(GetValue(settings, "BlacklistWords")!) ?? new List<string>()
            };
        }

        private static string? GetValue(Dictionary<string, string> settings, string key)
        {
            return settings.TryGetValue(key, out var val) ? val : null;
        }

        public async Task<SystemConfigDto> UpdateAsync(SystemConfigDto dto, CancellationToken cancellationToken = default)
        {
            Validate(dto);

            await UpdateSettingAsync("ExchangeRateCoinToVnd", dto.ExchangeRateCoinToVnd.ToString(), cancellationToken);
            await UpdateSettingAsync("WithdrawalFeePercent", dto.WithdrawalFeePercent.ToString(), cancellationToken);
            await UpdateSettingAsync("WithdrawalMinCoins", dto.WithdrawalMinCoins.ToString(), cancellationToken);
            await UpdateSettingAsync("WithdrawalMaxCoins", dto.WithdrawalMaxCoins.ToString(), cancellationToken);
            await UpdateSettingAsync("BlacklistWords", JsonSerializer.Serialize(dto.BlacklistWords), cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            return dto;
        }

        private async Task UpdateSettingAsync(string key, string value, CancellationToken cancellationToken)
        {
            var setting = await _context.SystemSettings.FirstOrDefaultAsync(x => x.Key == key, cancellationToken);
            if (setting == null)
            {
                _context.SystemSettings.Add(new SystemSetting 
                { 
                    Key = key, 
                    Value = value, 
                    UpdatedAt = DateTime.UtcNow,
                    Description = $"Auto-generated setting for {key}"
                });
            }
            else
            {
                setting.Value = value;
                setting.UpdatedAt = DateTime.UtcNow;
            }
        }

        private static void Validate(SystemConfigDto dto)
        {
            if (dto.WithdrawalMaxCoins > 0 && dto.WithdrawalMinCoins > dto.WithdrawalMaxCoins)
            {
                throw new ArgumentException("WithdrawalMinCoins cannot be greater than WithdrawalMaxCoins");
            }
        }
    }
}
