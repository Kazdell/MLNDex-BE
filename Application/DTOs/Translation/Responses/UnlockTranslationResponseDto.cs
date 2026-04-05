using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Translation.Responses
{
    public class UnlockTranslationResponseDto
    {
        public int TranslationId { get; set; }
        public int ChapterId { get; set; }
        public decimal CoinsSpent { get; set; }   // was int
        public decimal NewCoinBalance { get; set; }   // was int
        public string Message { get; set; } = string.Empty;
    }
}
