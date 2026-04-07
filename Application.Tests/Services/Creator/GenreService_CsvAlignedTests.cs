using System;
using System.Linq;
using System.Threading.Tasks;
using Application.Services.Creator;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace Application.Tests.Services.CreatorServices;

public class GenreService_CsvAlignedTests
{
  private readonly ITestOutputHelper _output;

  public GenreService_CsvAlignedTests(ITestOutputHelper output)
  {
    _output = output;
  }

  private static MlndexDbContext CreateInMemoryDbContext()
  {
    var options = new DbContextOptionsBuilder<MlndexDbContext>()
      .UseInMemoryDatabase(Guid.NewGuid().ToString())
      .Options;
    return new MlndexDbContext(options);
  }

  [Fact]
  public async Task GetAllGenresAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    db.Genres.AddRange(
      new Genre { GenreId = 2, Name = "Drama" },
      new Genre { GenreId = 1, Name = "Action" },
      new Genre { GenreId = 3, Name = "Comedy" });
    await db.SaveChangesAsync();

    var service = new GenreService(db);
    var output = (await service.GetAllGenresAsync()).ToList();

    _output.WriteLine("Input: genres seeded with unsorted names");
    _output.WriteLine($"Output: count={output.Count}, order={string.Join(',', output.Select(g => g.Name))}");

    output.Should().HaveCount(3);
    output.Select(g => g.Name).Should().ContainInOrder("Action", "Comedy", "Drama");
  }

  [Fact]
  public async Task GetAllGenresAsync_TC02_Empty_WhenNoData()
  {
    await using var db = CreateInMemoryDbContext();
    var service = new GenreService(db);

    var output = (await service.GetAllGenresAsync()).ToList();

    output.Should().BeEmpty();
  }

  [Fact]
  public async Task GetAllGenresAsync_TC03_BusinessRule_TracksOrderAfterInsert()
  {
    await using var db = CreateInMemoryDbContext();
    db.Genres.AddRange(
      new Genre { GenreId = 11, Name = "Zeta" },
      new Genre { GenreId = 12, Name = "Alpha" });
    await db.SaveChangesAsync();

    var service = new GenreService(db);
    var output = (await service.GetAllGenresAsync()).ToList();

    output.Select(x => x.Name).Should().ContainInOrder("Alpha", "Zeta");
  }

  [Fact]
  public async Task GetGenreByIdAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    db.Genres.Add(new Genre { GenreId = 10, Name = "Fantasy" });
    await db.SaveChangesAsync();

    var service = new GenreService(db);
    var output = await service.GetGenreByIdAsync(10);

    _output.WriteLine("Input: id=10");
    _output.WriteLine($"Output: genreName={output?.Name}");

    output.Should().NotBeNull();
    output!.Name.Should().Be("Fantasy");
  }

  [Fact]
  public async Task GetGenreByIdAsync_TC02_NotFound()
  {
    await using var db = CreateInMemoryDbContext();
    db.Genres.Add(new Genre { GenreId = 10, Name = "Fantasy" });
    await db.SaveChangesAsync();

    var service = new GenreService(db);
    var output = await service.GetGenreByIdAsync(999);

    _output.WriteLine("Input: id=999");
    _output.WriteLine($"Output: {(output is null ? "null" : "non-null")}");

    output.Should().BeNull();
  }

  [Fact]
  public async Task GetGenreByIdAsync_TC03_InvalidInput_IdLessThanOrEqualZeroReturnsNull()
  {
    await using var db = CreateInMemoryDbContext();
    db.Genres.Add(new Genre { GenreId = 1, Name = "Action" });
    await db.SaveChangesAsync();

    var service = new GenreService(db);
    var output = await service.GetGenreByIdAsync(0);

    output.Should().BeNull();
  }
}
