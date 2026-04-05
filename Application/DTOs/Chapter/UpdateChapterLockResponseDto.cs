using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Chapter
{
    public class UpdateChapterLockResponseDto
    {
        public int ChapterId { get; set; }
        public string LockStatus { get; set; } = string.Empty;
        public int? UnlockPriceCoins { get; set; }
        public DateTime? UnlockTime { get; set; }
    }
}
