using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Revenue.Request
{
    public class RevenueQueryDto
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public string Granularity { get; set; } = "day"; // "day" | "month" | "year"
    }

    public class RevenueDataPointDto
    {
        public string Label { get; set; } = null!;
        public decimal Amount { get; set; }
        public int UnlockCount { get; set; }
    }

    public class RevenueBySeriesDto
    {
        public int SeriesId { get; set; }
        public string SeriesTitle { get; set; } = null!;
        public decimal Revenue { get; set; }
        public int UnlockCount { get; set; }
    }
}
