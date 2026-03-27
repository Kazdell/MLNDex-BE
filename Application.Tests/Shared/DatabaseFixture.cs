using System;
using System.Data.Common;
using System.Threading.Tasks;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Xunit;

namespace Application.Tests.Shared
{
    public class DatabaseFixture : IAsyncLifetime
    {
        private MsSqlContainer _dbContainer;
        private string _connectionString;
        private MlndexDbContext _rootDb;

        public async Task InitializeAsync()
        {
            _dbContainer = new MsSqlBuilder()
                .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
                .Build();

            await _dbContainer.StartAsync();
            var csBuilder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(_dbContainer.GetConnectionString())
            {
                InitialCatalog = "MLNDexTestDb"
            };
            _connectionString = csBuilder.ConnectionString;

            var options = new DbContextOptionsBuilder<MlndexDbContext>()
                .UseSqlServer(_connectionString)
                .Options;

            _rootDb = new TestMlndexDbContext(options);
            await _rootDb.Database.EnsureCreatedAsync(); // Cấp phát Schema lên CSDL Docker không có IDENTITY restriction
            await _rootDb.Database.ExecuteSqlRawAsync("EXEC sp_msforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT all'");
        }

        public async Task ResetDatabaseAsync()
        {
            // Reset dữ liệu bằng cách xóa và tạo lại Schema
            if (_rootDb != null)
            {
                await _rootDb.Database.EnsureDeletedAsync();
                await _rootDb.Database.EnsureCreatedAsync();
                await _rootDb.Database.ExecuteSqlRawAsync("EXEC sp_msforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT all'");
            }
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

    public class TestMlndexDbContext : MlndexDbContext
    {
        public TestMlndexDbContext(DbContextOptions<MlndexDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Wipe out Identity generation on integer columns exclusively for the test database
            // This safely allows hardcoded ID insertion throughout the existing 90+ test files mapping.
            foreach (var entity in modelBuilder.Model.GetEntityTypes().ToList())
            {
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
