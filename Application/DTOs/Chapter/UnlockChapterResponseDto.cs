using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Chapter
{
    public class UnlockChapterResponseDto
    {
        public int ChapterId { get; set; }
        public decimal CoinsSpent { get; set; }
        public decimal NewCoinBalance { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
