using System;

namespace Domain.Entities
{
    public class SystemConfigs
    {
        public int Id { get; set; }
        public decimal ExchangeRateCoinToVnd { get; set; }
        public decimal WithdrawalFeePercent { get; set; }
        public decimal WithdrawalMinCoins { get; set; }
        public decimal WithdrawalMaxCoins { get; set; }
        public string BlacklistWordsJson { get; set; } = "[]";
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
