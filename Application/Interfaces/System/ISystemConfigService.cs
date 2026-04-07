using Application.DTOs.System;

namespace Application.Interfaces.System
{
	public interface ISystemConfigService
	{
		Task<SystemConfigDto> GetAsync(CancellationToken cancellationToken = default);
		Task<SystemConfigDto> UpdateAsync(
			SystemConfigDto dto,
			int updatedByUserId,
			CancellationToken cancellationToken = default
		);
		Task<long> CalculateCoinsAsync(long amountVnd);
	}
}
