using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Chapter
{
  public class UpdateChapterLockDto
  {
    public ChapterLockStatus LockStatus { get; set; }  // UNLOCKED | LOCKED
    public int? UnlockPriceCoins { get; set; }          // null nếu UNLOCKED
    public DateTime? UnlockTime { get; set; }            // null nếu UNLOCKED
  }
}
