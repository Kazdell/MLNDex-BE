using System;
using System.Data.Common;
using System.Threading.Tasks;
using Infrastructure.Persistence.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.Tests.Shared
{
    public class DatabaseFixture : IAsyncLifetime
    {
        private DbConnection _dbConnection;
        private MlndexDbContext _rootDb;

        public async Task InitializeAsync()
        {
            _dbConnection = new SqliteConnection("DataSource=:memory:");
            await _dbConnection.OpenAsync();

            var options = new DbContextOptionsBuilder<MlndexDbContext>()
                .UseSqlite(_dbConnection)
                .Options;

            _rootDb = new MlndexDbContext(options);
            await _rootDb.Database.EnsureCreatedAsync(); // Push Schema to SQLite memory
        }

        public async Task ResetDatabaseAsync()
        {
            // Close old connection to drop in-memory DB and open a fresh one
            if (_rootDb != null)
                await _rootDb.DisposeAsync();
            if (_dbConnection != null)
            {
                await _dbConnection.CloseAsync();
                await _dbConnection.DisposeAsync();
            }

            _dbConnection = new SqliteConnection("DataSource=:memory:");
            await _dbConnection.OpenAsync();
            
            var options = new DbContextOptionsBuilder<MlndexDbContext>()
                .UseSqlite(_dbConnection)
                .Options;
            _rootDb = new MlndexDbContext(options);
            await _rootDb.Database.EnsureCreatedAsync();
        }

        public async Task DisposeAsync()
        {
            if (_rootDb != null)
                await _rootDb.DisposeAsync();

            if (_dbConnection != null)
            {
                await _dbConnection.CloseAsync();
                await _dbConnection.DisposeAsync();
            }
        }

        public MlndexDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<MlndexDbContext>()
                .UseSqlite(_dbConnection)
                .Options;
            
            return new MlndexDbContext(options);
        }
    }

    [CollectionDefinition("Database collection")]
    public class DatabaseCollection : ICollectionFixture<DatabaseFixture> { }
}
