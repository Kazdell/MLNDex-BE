using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Application.Services.Auth;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Xunit;
using Xunit.Abstractions;

namespace Application.Tests.Services.Auth;

public class OtpService_CsvAlignedTests
{
  private readonly ITestOutputHelper _output;

  public OtpService_CsvAlignedTests(ITestOutputHelper output)
  {
    _output = output;
  }

  private static IMemoryCache CreateMemoryCache()
  {
    return new MemoryCache(new MemoryCacheOptions());
  }

  [Fact]
  public async Task GenerateOtpAsync_TC01_Success()
  {
    using var cache = CreateMemoryCache();
    var service = new OtpService(cache);

    var output = await service.GenerateOtpAsync("otp@test.com");

    _output.WriteLine("Input: email=otp@test.com");
    _output.WriteLine($"Output: otp={output}");

    output.Should().NotBeNullOrWhiteSpace();
    Regex.IsMatch(output, "^[0-9]{6}$").Should().BeTrue();

    cache.TryGetValue("otp:otp@test.com", out string? cached).Should().BeTrue();
    cached.Should().Be(output);
  }

  [Fact]
  public async Task GenerateOtpAsync_TC02_InvalidInput()
  {
    using var cache = CreateMemoryCache();
    var service = new OtpService(cache);

    var ex = await Assert.ThrowsAsync<NullReferenceException>(() => service.GenerateOtpAsync(null!));

    _output.WriteLine("Input: email=null");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task GenerateOtpAsync_TC03_Exception()
  {
    var cache = CreateMemoryCache();
    cache.Dispose();
    var service = new OtpService(cache);

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.GenerateOtpAsync("otp@test.com"));

    _output.WriteLine("Input: disposed memory cache");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }

  [Fact]
  public void ValidateOtp_TC03_NotFound()
  {
    using var cache = CreateMemoryCache();
    var service = new OtpService(cache);

    var isValid = service.ValidateOtp("missing@test.com", "123456");

    _output.WriteLine("Input: email=missing@test.com, code=123456");
    _output.WriteLine($"Output: isValid={isValid}");

    isValid.Should().BeFalse();
  }
}
