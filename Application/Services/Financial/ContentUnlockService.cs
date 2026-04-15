using Application.DTOs.Chapter;
using Application.DTOs.Common;
using Application.DTOs.Translation.Responses;
using Application.Exceptions;
using Application.Interfaces.Data;
using Application.Interfaces.Financial;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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
        private readonly IMemoryCache _cache;  

        public ContentUnlockService(
            IMlndexDbContext db,
            ILogger<ContentUnlockService> logger,
            IMemoryCache cache)  
        {
            _db = db;
            _logger = logger;
            _cache = cache;  
        }

        public async Task<UnlockChapterResponseDto> UnlockChapterAsync(
            int userId, int chapterId, CancellationToken ct = default)
        {
            // ── 1. Load chapter ────────────────────────────────────────────────
            var chapter = await _db.Chapters
                .Include(c => c.Series)
                    .ThenInclude(s => s.Creator)
                        .ThenInclude(cr => cr.User)
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

            // ── 4. Idempotency ─────────────────────────────────────────────────
            var alreadyUnlocked = await _db.ChapterUnlocks
                .AnyAsync(u => u.ChapterId == chapterId
                    && u.UserId == userId
                    && u.TranslationId == null, ct);
            if (alreadyUnlocked)
                throw new AppException(ErrorCodes.CHAPTER_ALREADY_UNLOCKED);

            // ── 5. Price must be configured ────────────────────────────────────
            if (chapter.UnlockPriceCoins is null or <= 0)
                throw new AppException(ErrorCodes.CHAPTER_PRICE_NOT_CONFIGURED);

            var price = (decimal)chapter.UnlockPriceCoins.Value;

            // ── 6. Load buyer wallet ───────────────────────────────────────────
            var buyerWallet = await _db.Wallets
                .FirstOrDefaultAsync(w => w.UserId == userId, ct)
                ?? throw new AppException(ErrorCodes.WALLET_NOT_FOUND);

            if (buyerWallet.CoinBalance < price)
                throw new AppException(ErrorCodes.OPERATION_NOT_ALLOWED);

            // ── 7. Load tác giả wallet ─────────────────────────────────────────
            var authorUserId = chapter.Series.Creator.UserId;
            var authorWallet = await _db.Wallets
                .FirstOrDefaultAsync(w => w.UserId == authorUserId, ct)
                ?? throw new AppException(ErrorCodes.WALLET_NOT_FOUND);

            // ── 8. Trừ coin buyer ──────────────────────────────────────────────
            buyerWallet.CoinBalance -= price;
            buyerWallet.TotalSpent += price;

            // ── 9. Cộng 100% cho tác giả ──────────────────────────────────────
            authorWallet.CoinBalance += price;
            authorWallet.TotalEarned += price;
            chapter.Series.Creator.TotalRevenue += price;

            // ── 10. Transaction: buyer trừ coin ───────────────────────────────
            var buyerTx = new Transaction
            {
                UserId = userId,
                WalletId = buyerWallet.WalletId,
                Type = TransactionType.CHAPTER_UNLOCK,
                AmountCoins = price,
                Status = TransactionStatus.COMPLETED,
                Note = $"Mở khóa chapter {chapterId} — series {chapter.SeriesId}",
                CreatedAt = now,
            };
            _db.Transactions.Add(buyerTx);

            // ── 11. Transaction: tác giả nhận coin ────────────────────────────
            var authorTx = new Transaction
            {
                UserId = authorUserId,
                WalletId = authorWallet.WalletId,
                Type = TransactionType.AUTHOR_ROYALTY,
                RelatedSeriesId = chapter.SeriesId,
                AmountCoins = price,
                Status = TransactionStatus.COMPLETED,
                Note = $"Hoa hồng 100% từ mở khóa chapter {chapterId} — series {chapter.SeriesId}",
                CreatedAt = now,
            };
            _db.Transactions.Add(authorTx);

            // ── 12. ChapterUnlock record ───────────────────────────────────────
            _db.ChapterUnlocks.Add(new ChapterUnlock
            {
                ChapterId = chapterId,
                UserId = userId,
                Transaction = buyerTx,
                CoinsPaid = price,
                UnlockSource = UnlockSource.COIN
            });

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "[ChapterUnlock] UserId={UserId} unlocked ChapterId={ChapterId} for {Price} coins. " +
                "AuthorUserId={AuthorUserId} received {Price} coins.",
                userId, chapterId, price, authorUserId, price);

            _cache.Remove($"SeriesDetails_{chapter.SeriesId}_User_{userId}");
            _cache.Remove($"SeriesDetails_{chapter.SeriesId}_User_0");

            return new UnlockChapterResponseDto
            {
                ChapterId = chapterId,
                CoinsSpent = price,
                NewCoinBalance = buyerWallet.CoinBalance,
                Message = $"Mở khóa thành công! Đã trừ {price} coin.",
            };
        }

        public async Task<UnlockTranslationResponseDto> UnlockTranslationAsync(
            int userId, int translationId, CancellationToken ct = default)
        {
            // ── 1. Load translation + chapter + team + tác giả ────────────────
            var translation = await _db.Translations
                .Include(t => t.Chapter)
                    .ThenInclude(c => c.Series)
                        .ThenInclude(s => s.Creator)
                            .ThenInclude(cr => cr.User)
                .Include(t => t.Permission)
                    .ThenInclude(p => p!.Team)
                        .ThenInclude(team => team.Leader)
                .FirstOrDefaultAsync(t => t.TranslationId == translationId, ct)
                ?? throw new AppException(ErrorCodes.TRANSLATION_NOT_FOUND);

            var chapter = translation.Chapter
                ?? throw new AppException(ErrorCodes.OPERATION_NOT_ALLOWED);

            // ── 2. Chapter phải published ──────────────────────────────────────
            if (chapter.Status != ChapterStatus.PUBLISHED)
                throw new AppException(ErrorCodes.OPERATION_NOT_ALLOWED);

            // ── 3. Chapter gốc phải đang locked ───────────────────────────────
            var now = DateTime.UtcNow;
            var effectiveChapterLock = chapter.LockStatus;
            if (effectiveChapterLock == ChapterLockStatus.LOCKED
                && chapter.UnlockTime.HasValue
                && now >= chapter.UnlockTime.Value)
            {
                effectiveChapterLock = ChapterLockStatus.UNLOCKED;
            }

            if (effectiveChapterLock == ChapterLockStatus.UNLOCKED)
                throw new AppException(ErrorCodes.OPERATION_NOT_ALLOWED);

            // ── 4. Đã mua chapter gốc rồi thì không cần mua dịch ──────────────
            var hasChapterUnlock = await _db.ChapterUnlocks
                .AnyAsync(u => u.ChapterId == chapter.ChapterId
                    && u.UserId == userId
                    && u.TranslationId == null, ct);
            if (hasChapterUnlock)
                throw new AppException(ErrorCodes.OPERATION_NOT_ALLOWED);

            // ── 5. Idempotency ─────────────────────────────────────────────────
            var alreadyUnlocked = await _db.ChapterUnlocks
                .AnyAsync(u => u.ChapterId == chapter.ChapterId
                    && u.UserId == userId
                    && u.TranslationId == translationId, ct);
            if (alreadyUnlocked)
                throw new AppException(ErrorCodes.OPERATION_NOT_ALLOWED);

            // ── 6. Team phải bật monetization & có giá ────────────────────────
            var team = translation.Permission?.Team;
            if (team == null || !team.IsMonetizationEnabled)
                throw new AppException(ErrorCodes.OPERATION_NOT_ALLOWED);

            var teamId = team.TeamId;

            var rawPrice = team.DefaultUnlockPriceCoins
                ?? chapter.UnlockPriceCoins
                ?? throw new AppException(ErrorCodes.OPERATION_NOT_ALLOWED);

            if (rawPrice <= 0)
                throw new AppException(ErrorCodes.OPERATION_NOT_ALLOWED);

            var price = (decimal)rawPrice;

            // ── 7. Load SystemConfig để lấy tỉ lệ hoa hồng ───────────────────
            var config = await _db.SystemConfigs.FirstOrDefaultAsync(ct);
            var authorCommissionPct = config?.TranslationAuthorCommissionPercent ?? 70m;
            var teamCommissionPct = 100m - authorCommissionPct;

            var authorShare = Math.Round(price * authorCommissionPct / 100m, 2);
            var teamShare = price - authorShare; // tránh rounding drift

            // ── 8. Load các wallet liên quan ──────────────────────────────────
            var buyerWallet = await _db.Wallets
                .FirstOrDefaultAsync(w => w.UserId == userId, ct)
                ?? throw new AppException(ErrorCodes.WALLET_NOT_FOUND);

            if (buyerWallet.CoinBalance < price)
                throw new AppException(ErrorCodes.OPERATION_NOT_ALLOWED);

            var authorUserId = chapter.Series.Creator.UserId;
            var authorWallet = await _db.Wallets
                .FirstOrDefaultAsync(w => w.UserId == authorUserId, ct)
                ?? throw new AppException(ErrorCodes.WALLET_NOT_FOUND);

            var teamLeaderUserId = team.LeaderId;
            var teamLeaderWallet = await _db.Wallets
                .FirstOrDefaultAsync(w => w.UserId == teamLeaderUserId, ct)
                ?? throw new AppException(ErrorCodes.WALLET_NOT_FOUND);

            // ── 9. Trừ coin buyer ──────────────────────────────────────────────
            buyerWallet.CoinBalance -= price;
            buyerWallet.TotalSpent += price;

            // ── 10. Cộng hoa hồng cho tác giả ────────────────────────────────
            authorWallet.CoinBalance += authorShare;
            authorWallet.TotalEarned += authorShare;
            chapter.Series.Creator.TotalRevenue += authorShare;

            // ── 11. Cộng phần còn lại cho team leader ─────────────────────────
            teamLeaderWallet.CoinBalance += teamShare;
            teamLeaderWallet.TotalEarned += teamShare;

            // ── 12. Transactions ───────────────────────────────────────────────
            var buyerTx = new Transaction
            {
                UserId = userId,
                WalletId = buyerWallet.WalletId,
                Type = TransactionType.CHAPTER_UNLOCK,
                AmountCoins = price,
                Status = TransactionStatus.COMPLETED,
                Note = $"Mở khóa bản dịch {translationId} — Ch.{chapter.ChapterNumber} — nhóm {team.TeamName}",
                CreatedAt = now,
            };
            _db.Transactions.Add(buyerTx);

            _db.Transactions.Add(new Transaction
            {
                UserId = authorUserId,
                WalletId = authorWallet.WalletId,
                Type = TransactionType.AUTHOR_ROYALTY,
                RelatedSeriesId = chapter.SeriesId,
                AmountCoins = authorShare,
                Status = TransactionStatus.COMPLETED,
                Note = $"Hoa hồng {authorCommissionPct}% từ bản dịch {translationId} — Ch.{chapter.ChapterNumber}",
                CreatedAt = now,
            });

            _db.Transactions.Add(new Transaction
            {
                UserId = teamLeaderUserId,
                WalletId = teamLeaderWallet.WalletId,
                Type = TransactionType.TEAM_ROYALTY,
                AmountCoins = teamShare,
                Status = TransactionStatus.COMPLETED,
                RelatedEntityId = teamId,
                RelatedEntityType = "TEAM",
                RelatedSeriesId = chapter.SeriesId,
                Note = $"Hoa hồng {teamCommissionPct}% từ bản dịch {translationId}...",
                CreatedAt = now,
            });

            // ── 13. ChapterUnlock record ───────────────────────────────────────
            _db.ChapterUnlocks.Add(new ChapterUnlock
            {
                ChapterId = chapter.ChapterId,
                UserId = userId,
                TranslationId = translationId,
                Transaction = buyerTx,
                CoinsPaid = price,
                UnlockSource = UnlockSource.COIN,
            });

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "[TranslationUnlock] UserId={UserId} unlocked TranslationId={TranslationId} ChapterId={ChapterId} " +
                "for {Price} coins. Author={AuthorUserId} +{AuthorShare}, TeamLeader={TeamLeaderId} +{TeamShare}.",
                userId, translationId, chapter.ChapterId, price,
                authorUserId, authorShare, teamLeaderUserId, teamShare);

            _cache.Remove($"SeriesDetails_{chapter.SeriesId}_User_{userId}");
            _cache.Remove($"SeriesDetails_{chapter.SeriesId}_User_0");

            return new UnlockTranslationResponseDto
            {
                TranslationId = translationId,
                ChapterId = chapter.ChapterId,
                CoinsSpent = price,
                NewCoinBalance = buyerWallet.CoinBalance,
                Message = $"Mở khóa bản dịch thành công! Đã trừ {price} coin.",
            };
        }
    }
}
