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
            var config = await _context.SystemConfigs.FirstOrDefaultAsync(cancellationToken);

            if (config == null)
            {
                // Return defaults if no config exists yet
                return new SystemConfigDto
                {
                    ExchangeRateCoinToVnd = 1000,
                    WithdrawalFeePercent = 10,
                    WithdrawalMinCoins = 50,
                    WithdrawalMaxCoins = 1000,
                    BlacklistWords = new List<string>()
                };
            }

            return new SystemConfigDto
            {
                ExchangeRateCoinToVnd = config.ExchangeRateCoinToVnd,
                WithdrawalFeePercent = config.WithdrawalFeePercent,
                WithdrawalMinCoins = config.WithdrawalMinCoins,
                WithdrawalMaxCoins = config.WithdrawalMaxCoins,
                BlacklistWords = string.IsNullOrEmpty(config.BlacklistWordsJson) 
                    ? new List<string>() 
                    : JsonSerializer.Deserialize<List<string>>(config.BlacklistWordsJson) ?? new List<string>()
            };
        }

        private static string? GetValue(Dictionary<string, string> settings, string key)
        {
            return settings.TryGetValue(key, out var val) ? val : null;
        }

        public async Task<SystemConfigDto> UpdateAsync(SystemConfigDto dto, CancellationToken cancellationToken = default)
        {
            Validate(dto);

            var config = await _context.SystemConfigs.FirstOrDefaultAsync(cancellationToken);
            if (config == null)
            {
                config = new SystemConfigs();
                _context.SystemConfigs.Add(config);
            }

            config.ExchangeRateCoinToVnd = dto.ExchangeRateCoinToVnd;
            config.WithdrawalFeePercent = dto.WithdrawalFeePercent;
            config.WithdrawalMinCoins = dto.WithdrawalMinCoins;
            config.WithdrawalMaxCoins = dto.WithdrawalMaxCoins;
            config.BlacklistWordsJson = JsonSerializer.Serialize(dto.BlacklistWords);
            config.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            return dto;
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
