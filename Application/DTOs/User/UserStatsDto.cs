using System;
using System.Collections.Generic;

namespace Application.DTOs.User
{
    public class UserStatsDto
    {
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int BannedUsers { get; set; }
        public int NewMembersLast7Days { get; set; }
        public List<UserChartDataDto> ChartData { get; set; } = new();
    }

    public class UserChartDataDto
    {
        public string Date { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
