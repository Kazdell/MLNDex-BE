using Application.DTOs.System;
using Application.Interfaces.Data;
using Application.Interfaces.System;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Application.Services.System
{
    public class SystemConfigService : ISystemConfigService
    {
        private readonly IMlndexDbContext _context;
		private readonly ILogger<SystemConfigService> _logger;

		public SystemConfigService(IMlndexDbContext context, ILogger<SystemConfigService> logger)
        {
            _context = context;
            _logger = logger;
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
                    TranslationAuthorCommissionPercent = 70,
                    BlacklistWords = new List<string>()
                };
            }

            return new SystemConfigDto
            {
                ExchangeRateCoinToVnd = config.ExchangeRateCoinToVnd,
                WithdrawalFeePercent = config.WithdrawalFeePercent,
                WithdrawalMinCoins = config.WithdrawalMinCoins,
                WithdrawalMaxCoins = config.WithdrawalMaxCoins,
                TranslationAuthorCommissionPercent = config.TranslationAuthorCommissionPercent,
                BlacklistWords = string.IsNullOrEmpty(config.BlacklistWordsJson) 
                    ? new List<string>() 
                    : JsonSerializer.Deserialize<List<string>>(config.BlacklistWordsJson) ?? new List<string>()
            };
		}

        private static string? GetValue(Dictionary<string, string> settings, string key)
        {
            return settings.TryGetValue(key, out var val) ? val : null;
        }

        public async Task<SystemConfigDto> UpdateAsync(SystemConfigDto dto, int updatedByUserId, CancellationToken cancellationToken = default)
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
            config.TranslationAuthorCommissionPercent = dto.TranslationAuthorCommissionPercent;
            config.BlacklistWordsJson = JsonSerializer.Serialize(dto.BlacklistWords);
            config.UpdatedAt = DateTime.UtcNow;
            config.UpdatedByUserId = updatedByUserId;

			await _context.SaveChangesAsync(cancellationToken);
            return dto;
        }

		public async Task<long> CalculateCoinsAsync(long amountVnd)
		{
			var config = await _context.SystemConfigs.FirstOrDefaultAsync();
			if (config == null)
				throw new InvalidOperationException("System config not found");

			if (amountVnd <= 0)
				throw new ArgumentException("Amount must be greater than 0");

			return (long)Math.Floor(amountVnd / config.ExchangeRateCoinToVnd);
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
