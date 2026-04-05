using System;
using System.Data.Common;
using System.Threading.Tasks;
using Infrastructure.Persistence.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Xunit;

namespace Application.Tests.Shared
{
  public class DatabaseFixture : IAsyncLifetime
  {
    private MsSqlContainer? _dbContainer;
    private string _connectionString = default!;
    private MlndexDbContext? _rootDb;

    public async Task InitializeAsync()
    {
      _dbContainer = new MsSqlBuilder()
          .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
          .Build();

      await _dbContainer.StartAsync();

      var masterCs = new SqlConnectionStringBuilder(_dbContainer.GetConnectionString())
      {
        TrustServerCertificate = true
      };
      using (var conn = new SqlConnection(masterCs.ConnectionString))
      {
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "CREATE DATABASE [MLNDexTestDb]";
        await cmd.ExecuteNonQueryAsync();
      }

      var csBuilder = new SqlConnectionStringBuilder(_dbContainer.GetConnectionString())
      {
        InitialCatalog = "MLNDexTestDb",
        TrustServerCertificate = true
      };
      _connectionString = csBuilder.ConnectionString;

      var options = new DbContextOptionsBuilder<MlndexDbContext>()
          .UseSqlServer(_connectionString)
          .Options;

      _rootDb = new TestMlndexDbContext(options);
      await _rootDb.Database.EnsureCreatedAsync();
      await _rootDb.Database.ExecuteSqlRawAsync("EXEC sp_msforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT all'");
    }

    public async Task ResetDatabaseAsync()
    {
      if (_rootDb == null) return;

      await _rootDb.Database.ExecuteSqlRawAsync(@"
                EXEC sp_msforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL';
                EXEC sp_msforeachtable 'DELETE FROM ?';
                EXEC sp_msforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL';
            ");
    }

    public async Task DisposeAsync()
    {
      if (_rootDb != null)
        await _rootDb.DisposeAsync();

      if (_dbContainer != null)
      {
        await _dbContainer.StopAsync();
        await _dbContainer.DisposeAsync();
      }
    }

    public MlndexDbContext CreateDbContext()
    {
      var options = new DbContextOptionsBuilder<MlndexDbContext>()
          .UseSqlServer(_connectionString)
          .Options;

      return new TestMlndexDbContext(options);
    }
  }

  /// <summary>
  /// Overrides OnModelCreating to disable IDENTITY generation only on User and Role.
  /// UserRole keeps IDENTITY so the service can create roles normally.
  /// IMPORTANT: This does NOT connect to your real database.
  /// </summary>
  public class TestMlndexDbContext : MlndexDbContext
  {
    // Tables where tests seed with explicit IDs. Uses entity ClrType name.
    // IMPORTANT: Do NOT include entities that services CREATE (e.g. TranslationTeam,
    // TeamMember, TeamInvitation) - those need IDENTITY auto-generation.
    private static readonly HashSet<string> _entitiesWithManualIds = new() {
            "User", "Role", "CreatorProfile", "Series", "Chapter", "Language",
            "TranslationPermission", "Translation",
            "Category", "Report", "TrustScoreHistory", "Appeal",
            "Genre", "TranslationText", "TranslationPage"
        };

    public TestMlndexDbContext(DbContextOptions<MlndexDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
      base.OnModelCreating(modelBuilder);

      foreach (var entity in modelBuilder.Model.GetEntityTypes().ToList())
      {
        // Match by CLR type name (e.g., "User", "TranslationTeam")
        if (!_entitiesWithManualIds.Contains(entity.ClrType.Name))
          continue;

        foreach (var property in entity.GetProperties())
        {
          if (property.IsPrimaryKey() && (property.ClrType == typeof(int) || property.ClrType == typeof(long)))
          {
            property.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
            property.RemoveAnnotation("SqlServer:ValueGenerationStrategy");
          }
        }
      }
    }
  }

  [CollectionDefinition("Database collection")]
  public class DatabaseCollection : ICollectionFixture<DatabaseFixture> { }
}
