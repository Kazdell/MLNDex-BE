using Application.DTOs.Revenue.Request;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Revenue.Response
{
    public class CreatorRevenueSummaryDto
    {
        public decimal TotalRevenue { get; set; }
        public int TotalUnlocks { get; set; }
        public decimal AveragePerPeriod { get; set; }
        public List<RevenueDataPointDto> DataPoints { get; set; } = new();
        public List<RevenueBySeriesDto> BySeries { get; set; } = new();
    }

    public class SeriesRevenueSummaryDto
    {
        public int SeriesId { get; set; }
        public string SeriesTitle { get; set; } = null!;
        public decimal TotalRevenue { get; set; }
        public int TotalUnlocks { get; set; }
        public decimal AveragePerPeriod { get; set; }
        public List<RevenueDataPointDto> DataPoints { get; set; } = new();
    }

    public class TeamRevenueSummaryDto
    {
        public decimal TotalRevenue { get; set; }
        public int TotalUnlocks { get; set; }
        public decimal AveragePerPeriod { get; set; }
        public List<RevenueDataPointDto> DataPoints { get; set; } = [];
        public List<RevenueBySeriesDto> BySeries { get; set; } = []; 
    }
}
