using System;

namespace Application.DTOs.User
{
    public class VipPlanDto
    {
        public int PlanId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal PriceVnd { get; set; }
        public int DurationDays { get; set; }
        public bool AutoUnlockChapter { get; set; }
        public bool IsActive { get; set; }
    }
}
