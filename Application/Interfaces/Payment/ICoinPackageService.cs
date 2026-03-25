using Application.DTOs.Payment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces.Payment
{
	public interface ICoinPackageService
	{
		/// <summary>
		/// Lấy danh sách gói coin.
		/// activeOnly = true → chỉ lấy gói đang active (dùng cho User).
		/// activeOnly = false → lấy tất cả kể cả inactive (dùng cho Admin).
		/// </summary>
		Task<List<CoinPackageResponseDto>> GetAllAsync(bool activeOnly = false);

		Task<CoinPackageResponseDto?> GetByIdAsync(int packageId);

		Task<CoinPackageResponseDto> CreateAsync(CreateCoinPackageDto dto);

		Task<CoinPackageResponseDto> UpdateAsync(int packageId, UpdateCoinPackageDto dto);

		/// <summary>Soft delete — set IsActive = false, không xoá khỏi DB.</summary>
		Task DeactivateAsync(int packageId);
	}
}
