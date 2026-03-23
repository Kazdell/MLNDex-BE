using System.Text.Json;
using Application.DTOs.System;
using Application.Interfaces.System;

namespace Application.Services.System
{
  public class SystemConfigService : ISystemConfigService
  {
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public SystemConfigService(string filePath)
    {
      _filePath = filePath;
    }

    public async Task<SystemConfigDto> GetAsync(CancellationToken cancellationToken = default)
    {
      await EnsureFileExistsAsync(cancellationToken);

      await _lock.WaitAsync(cancellationToken);
      try
      {
        await using var stream = File.OpenRead(_filePath);
        var dto = await JsonSerializer.DeserializeAsync<SystemConfigDto>(
            stream,
            cancellationToken: cancellationToken
        );
        return dto ?? GetDefault();
      }
      finally
      {
        _lock.Release();
      }
    }

    public async Task<SystemConfigDto> UpdateAsync(
        SystemConfigDto dto,
        CancellationToken cancellationToken = default
    )
    {
      Validate(dto);
      await EnsureFileExistsAsync(cancellationToken);

      await _lock.WaitAsync(cancellationToken);
      try
      {
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, dto, _jsonOptions, cancellationToken);
        return dto;
      }
      finally
      {
        _lock.Release();
      }
    }

    private static void Validate(SystemConfigDto dto)
    {
      if (dto.WithdrawalMaxCoins > 0 && dto.WithdrawalMinCoins > dto.WithdrawalMaxCoins)
      {
        throw new ArgumentException(
            "WithdrawalMinCoins cannot be greater than WithdrawalMaxCoins"
        );
      }
    }

    private SystemConfigDto GetDefault()
    {
      return new SystemConfigDto
      {
        ExchangeRateCoinToVnd = 100,
        WithdrawalFeePercent = 0,
        WithdrawalMinCoins = 0,
        WithdrawalMaxCoins = 0,
        BlacklistWords = new List<string>(),
      };
    }

    private async Task EnsureFileExistsAsync(CancellationToken cancellationToken)
    {
      var directory = Path.GetDirectoryName(_filePath);
      if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
      {
        Directory.CreateDirectory(directory);
      }

      if (!File.Exists(_filePath))
      {
        var defaultConfig = GetDefault();
        await _lock.WaitAsync(cancellationToken);
        try
        {
          await using var stream = File.Create(_filePath);
          await JsonSerializer.SerializeAsync(
              stream,
              defaultConfig,
              _jsonOptions,
              cancellationToken
          );
        }
        finally
        {
          _lock.Release();
        }
      }
    }
  }
}
