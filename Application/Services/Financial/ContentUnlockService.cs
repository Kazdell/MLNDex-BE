using Application.DTOs.Common;
using Application.DTOs.Translation.Responses;
using Application.DTOs.Chapter;
using Application.Exceptions;
using Application.Interfaces.Data;
using Application.Interfaces.Financial;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Services.Financial
{
    public class ContentUnlockService : IContentUnlockService
    {
        private readonly IMlndexDbContext _db;
        private readonly ILogger<ContentUnlockService> _logger;

        public ContentUnlockService(IMlndexDbContext db, ILogger<ContentUnlockService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<UnlockChapterResponseDto> UnlockChapterAsync(
            int userId, int chapterId, CancellationToken ct = default)
        {
            // ── 1. Load chapter ────────────────────────────────────────────────
            var chapter = await _db.Chapters
                .Include(c => c.Series)
                .FirstOrDefaultAsync(c => c.ChapterId == chapterId, ct)
                ?? throw new AppException(ErrorCodes.CHAPTER_NOT_FOUND);

            // ── 2. Must be published ───────────────────────────────────────────
            if (chapter.Status != ChapterStatus.PUBLISHED)
                throw new AppException(ErrorCodes.CHAPTER_NOT_PUBLISHED);

            // ── 3. Effective lock status ───────────────────────────────────────
            var now = DateTime.UtcNow;
            var effectiveLock = chapter.LockStatus;
            if (effectiveLock == ChapterLockStatus.LOCKED
                && chapter.UnlockTime.HasValue
                && now >= chapter.UnlockTime.Value)
            {
                effectiveLock = ChapterLockStatus.UNLOCKED;
            }

            if (effectiveLock == ChapterLockStatus.UNLOCKED)
                throw new AppException(ErrorCodes.CHAPTER_ALREADY_FREE);

            // ── 4. Idempotency (Mua gốc mở gốc+dịch) ─────────────────────────────────────────────────
            var alreadyUnlocked = await _db.ChapterUnlocks
                .AnyAsync(u => u.ChapterId == chapterId
                    && u.UserId == userId
                    && u.TranslationId == null, ct);
            if (alreadyUnlocked)
                throw new AppException(ErrorCodes.CHAPTER_ALREADY_UNLOCKED);

            // ── 5. Price must be configured ────────────────────────────────────
            if (chapter.UnlockPriceCoins is null or <= 0)
                throw new AppException(ErrorCodes.CHAPTER_PRICE_NOT_CONFIGURED);

            var price = chapter.UnlockPriceCoins.Value;

            // ── 6. Load wallet ─────────────────────────────────────────────────
            var wallet = await _db.Wallets
                .FirstOrDefaultAsync(w => w.UserId == userId, ct)
                ?? throw new AppException(ErrorCodes.WALLET_NOT_FOUND);

            if (wallet.CoinBalance < price)
                throw new AppException(ErrorCodes.OPERATION_NOT_ALLOWED,
                    $"Số dư không đủ. Cần {price} coin, bạn đang có {wallet.CoinBalance} coin.");

            // ── 7. Deduct coins ────────────────────────────────────────────────
            wallet.CoinBalance -= price;
            wallet.TotalSpent += price;

            // ── 8. Create transaction record ───────────────────────────────────
            var coinTransaction = new Transaction
            {
                UserId = userId,
                WalletId = wallet.WalletId,
                Type = TransactionType.CHAPTER_UNLOCK,
                AmountCoins = price,
                Status = TransactionStatus.COMPLETED,
                Note = $"Mở khóa chapter {chapterId} — series {chapter.SeriesId}",
                CreatedAt = now,
            };
            _db.Transactions.Add(coinTransaction);

            // ── 9. Add ChapterUnlock ────────
            var unlockRecord = new ChapterUnlock
            {
                ChapterId = chapterId,
                UserId = userId,
                Transaction = coinTransaction,
                CoinsPaid = (decimal)price,
                UnlockSource = UnlockSource.COIN
            };
            _db.ChapterUnlocks.Add(unlockRecord);

            // ── 10. Save changes ────────────────────────────
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "[ChapterUnlock] UserId={UserId} unlocked ChapterId={ChapterId} for {Price} coins. New balance: {Balance}",
                userId, chapterId, price, wallet.CoinBalance);

            return new UnlockChapterResponseDto
            {
                ChapterId = chapterId,
                CoinsSpent = price,
                NewCoinBalance = wallet.CoinBalance,
                Message = $"Mở khóa thành công! Đã trừ {price} coin.",
            };
        }

    public async Task<UnlockTranslationResponseDto> UnlockTranslationAsync(
      int userId,
      int translationId,
      CancellationToken ct = default
    )
    {
      // ── 1. Load translation + chapter + team ─────────────────────────────
      var translation =
        await _db
          .Translations.Include(t => t.Chapter)
            .ThenInclude(c => c.Series)
          .Include(t => t.Permission)
            .ThenInclude(p => p!.Team)
          .FirstOrDefaultAsync(t => t.TranslationId == translationId, ct)
        ?? throw new AppException(ErrorCodes.TRANSLATION_NOT_FOUND);

      var chapter =
        translation.Chapter
        ?? throw new AppException(
          ErrorCodes.OPERATION_NOT_ALLOWED,
          "Bản dịch không liên kết với chương hợp lệ."
        );

      // ── 2. Chapter phải được published ───────────────────────────────────
      if (chapter.Status != ChapterStatus.PUBLISHED)
        throw new AppException(
          ErrorCodes.OPERATION_NOT_ALLOWED,
          "Chương này chưa được phát hành."
        );

      // ── 3. Translation chỉ có giá khi chapter gốc đang bị lock ──────────
      var now = DateTime.UtcNow;
      var effectiveChapterLock = chapter.LockStatus;
      if (
        effectiveChapterLock == ChapterLockStatus.LOCKED
        && chapter.UnlockTime.HasValue
        && now >= chapter.UnlockTime.Value
      )
      {
        effectiveChapterLock = ChapterLockStatus.UNLOCKED;
      }

      if (effectiveChapterLock == ChapterLockStatus.UNLOCKED)
        throw new AppException(
          ErrorCodes.OPERATION_NOT_ALLOWED,
          "Chương gốc đang miễn phí, bản dịch này không cần mua."
        );

      // ── 4. User đã unlock chapter gốc rồi → đọc được tất cả translation ─
      var hasChapterUnlock = await _db.ChapterUnlocks.AnyAsync(
        u => u.ChapterId == chapter.ChapterId && u.UserId == userId && u.TranslationId == null, // null = đã mua chapter gốc
        ct
      );

      if (hasChapterUnlock)
        throw new AppException(
          ErrorCodes.OPERATION_NOT_ALLOWED,
          "Bạn đã mở khóa chương gốc nên có thể đọc tất cả bản dịch miễn phí."
        );

      // ── 5. Idempotency: đã mua translation này chưa? ─────────────────────
      var alreadyUnlocked = await _db.ChapterUnlocks.AnyAsync(
        u =>
          u.ChapterId == chapter.ChapterId
          && u.UserId == userId
          && u.TranslationId == translationId,
        ct
      );

      if (alreadyUnlocked)
        throw new AppException(
          ErrorCodes.OPERATION_NOT_ALLOWED,
          "Bạn đã mua bản dịch này rồi."
        );

      // ── 6. Team phải bật monetization & có giá ───────────────────────────
      var team = translation.Permission?.Team;

      if (team == null || !team.IsMonetizationEnabled)
        throw new AppException(
          ErrorCodes.OPERATION_NOT_ALLOWED,
          "Nhóm dịch này chưa bật tính năng kinh doanh."
        );

      var rawPrice =
        team.DefaultUnlockPriceCoins
        ?? chapter.UnlockPriceCoins
        ?? throw new AppException(
          ErrorCodes.OPERATION_NOT_ALLOWED,
          "Bản dịch này chưa được cấu hình giá mở khóa."
        );

      if (rawPrice <= 0)
        throw new AppException(
          ErrorCodes.OPERATION_NOT_ALLOWED,
          "Giá mở khóa không hợp lệ."
        );

      var price = (decimal)rawPrice;

      // ── 7. Kiểm tra ví ───────────────────────────────────────────────────
      var wallet =
        await _db.Wallets.FirstOrDefaultAsync(w => w.UserId == userId, ct)
        ?? throw new AppException(ErrorCodes.WALLET_NOT_FOUND);

      if (wallet.CoinBalance < price)
        throw new AppException(
          ErrorCodes.OPERATION_NOT_ALLOWED,
          $"Số dư không đủ. Cần {price} coin, bạn đang có {wallet.CoinBalance} coin."
        );

      // ── 8. Trừ coin ──────────────────────────────────────────────────────
      wallet.CoinBalance -= price;
      wallet.TotalSpent += price;

      // ── 9. Transaction record ─────────────────────────────────────────────
      var coinTransaction = new Transaction
      {
        UserId = userId,
        WalletId = wallet.WalletId,
        Type = TransactionType.CHAPTER_UNLOCK,
        AmountCoins = price,
        Status = TransactionStatus.COMPLETED,
        Note =
          $"Mở khóa bản dịch {translationId} — Ch.{chapter.ChapterNumber} — nhóm {team.TeamName}",
        CreatedAt = now,
      };
      _db.Transactions.Add(coinTransaction);

      // ── 10. ChapterUnlock với TranslationId để phân biệt loại unlock ─────
      _db.ChapterUnlocks.Add(
        new ChapterUnlock
        {
          ChapterId = chapter.ChapterId,
          UserId = userId,
          TranslationId = translationId, // khác null → unlock bản dịch cụ thể
          Transaction = coinTransaction, // EF tự map TransactionId
          CoinsPaid = (decimal)price,
          UnlockSource = UnlockSource.COIN,
        }
      );

      // ── 11. Một lần SaveChanges duy nhất ──────────────────────────────────
      await _db.SaveChangesAsync(ct);

      _logger.LogInformation(
        "[TranslationUnlock] UserId={UserId} unlocked TranslationId={TranslationId} "
          + "ChapterId={ChapterId} Team={TeamName} for {Price} coins. Balance={Balance}",
        userId,
        translationId,
        chapter.ChapterId,
        team.TeamName,
        price,
        wallet.CoinBalance
      );

      return new UnlockTranslationResponseDto
      {
        TranslationId = translationId,
        ChapterId = chapter.ChapterId,
        CoinsSpent = price,
        NewCoinBalance = wallet.CoinBalance,
        Message = $"Mở khóa bản dịch thành công! Đã trừ {price} coin.",
      };
    }
        }
    }
