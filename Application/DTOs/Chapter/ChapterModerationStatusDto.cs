using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Chapter
{
    public class ChapterModerationStatusDto
    {
        public int ChapterId { get; set; }
        public string Status { get; set; } = "pending"; // pending | processing | completed | failed
        public int? QueuePos { get; set; }              // ← thêm: vị trí trong queue
        public string? FlaggedReason { get; set; }
        public Dictionary<string, double>? CategoryScores { get; set; }
        public bool? Flagged { get; set; }              // ← thêm: để frontend biết isFlagged
    }
}
