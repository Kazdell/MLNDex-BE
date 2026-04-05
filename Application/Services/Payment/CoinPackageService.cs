using Application.DTOs.Payment;
using Application.Interfaces.Data;
using Application.Interfaces.Payment;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Services.Payment;

public class CoinPackageService : ICoinPackageService
{
  private readonly IMlndexDbContext _context;
  private readonly ILogger<CoinPackageService> _logger;

  public CoinPackageService(IMlndexDbContext context, ILogger<CoinPackageService> logger)
  {
    _context = context;
    _logger = logger;
  }

  public async Task<List<CoinPackageResponseDto>> GetAllAsync(bool activeOnly = false)
  {
    var query = _context.CoinPackages.AsQueryable();

    if (activeOnly)
      query = query.Where(p => p.IsActive);

    return await query
        .OrderBy(p => p.PriceVnd)
        .Select(p => ToDto(p))
        .ToListAsync();
  }

  public async Task<CoinPackageResponseDto?> GetByIdAsync(int packageId)
  {
    var package = await _context.CoinPackages.FindAsync(packageId);
    return package is null ? null : ToDto(package);
  }

  public async Task<CoinPackageResponseDto> CreateAsync(CreateCoinPackageDto dto)
  {
    var package = new CoinPackage
    {
      Name = dto.Name,
      CoinAmount = dto.CoinAmount,
      PriceVnd = dto.PriceVnd,
      BonusCoins = dto.BonusCoins,
      IsActive = true,
      CreatedAt = DateTime.UtcNow
    };

    _context.CoinPackages.Add(package);
    await _context.SaveChangesAsync();

    _logger.LogInformation("[CoinPackage] Tạo gói mới. PackageId={PackageId} Name={Name}",
        package.PackageId, package.Name);

    return ToDto(package);
  }

  public async Task<CoinPackageResponseDto> UpdateAsync(int packageId, UpdateCoinPackageDto dto)
  {
    var package = await _context.CoinPackages.FindAsync(packageId)
        ?? throw new KeyNotFoundException($"Không tìm thấy gói coin ID={packageId}.");

    if (dto.Name is not null) package.Name = dto.Name;
    if (dto.CoinAmount.HasValue) package.CoinAmount = dto.CoinAmount.Value;
    if (dto.PriceVnd.HasValue) package.PriceVnd = dto.PriceVnd.Value;
    if (dto.BonusCoins.HasValue) package.BonusCoins = dto.BonusCoins.Value;
    if (dto.IsActive.HasValue) package.IsActive = dto.IsActive.Value;

    await _context.SaveChangesAsync();

    _logger.LogInformation("[CoinPackage] Cập nhật gói. PackageId={PackageId}", packageId);

    return ToDto(package);
  }

  public async Task DeactivateAsync(int packageId)
  {
    var package = await _context.CoinPackages.FindAsync(packageId)
        ?? throw new KeyNotFoundException($"Không tìm thấy gói coin ID={packageId}.");

    package.IsActive = false;
    await _context.SaveChangesAsync();

    _logger.LogInformation("[CoinPackage] Deactivate gói. PackageId={PackageId}", packageId);
  }

  private static CoinPackageResponseDto ToDto(CoinPackage p) => new()
  {
    PackageId = p.PackageId,
    Name = p.Name,
    PriceVnd = p.PriceVnd,
    CoinAmount = p.CoinAmount,
    BonusCoins = p.BonusCoins,
    IsActive = p.IsActive
  };
}
