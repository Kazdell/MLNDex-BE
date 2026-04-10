using Application.Interfaces.Auth;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Services.Auth
{
  public class OtpService : IOtpService
  {
    private readonly IMemoryCache _cache;

    public OtpService(IMemoryCache cache)
    {
      _cache = cache;
    }

    public Task<string> GenerateOtpAsync(string email)
    {
      var code = global::System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 999999).ToString();

      // key theo email, tự expire sau 10 phút
      _cache.Set($"otp:{email.ToLower()}", code, TimeSpan.FromMinutes(10));

      return Task.FromResult(code);
    }

    public bool ValidateOtp(string email, string code)
    {
      var key = $"otp:{email.ToLower()}";
      if (!_cache.TryGetValue(key, out string? saved)) return false;
      if (saved != code) return false;

      _cache.Remove(key); // dùng 1 lần rồi xóa
      return true;
    }
  }
}
