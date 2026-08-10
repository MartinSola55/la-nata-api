using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Template_Api.Core.DAL.DB;
using Template_Api.Core.Models;

namespace Template_Api.Core.DAL
{
    public class MigrationRunner(APIContext db, IConfiguration config)
    {
        private readonly APIContext _db = db;
        private readonly IConfiguration _config = config;

        public async Task Run()
        {
            if (!_config.GetValue("Database:RunSqlMigrations", true))
                return;

            await EnsureMigrationTableExists();

            // Every library's scripts are kept together in Core/DAL/Migrations so the execution order is a single, unambiguous sequence
            var migrationsPath = Path.Combine(AppContext.BaseDirectory, "Core", "DAL", "Migrations");
            if (!Directory.Exists(migrationsPath))
                migrationsPath = Path.Combine(Directory.GetCurrentDirectory(), "Core", "DAL", "Migrations");

            if (!Directory.Exists(migrationsPath))
                return;

            var migrationFiles = Directory
                .EnumerateFiles(migrationsPath, "*.sql")
                .OrderBy(x => x)
                .ToList();

            var executedMigrations = await _db
                .Migration
                .Select(x => x.FileName)
                .ToListAsync();

            foreach (var file in migrationFiles)
            {
                var fileName = Path.GetFileName(file);

                if (executedMigrations.Any(x => x == fileName))
                    continue;

                var sql = await File.ReadAllTextAsync(file);
                if (string.IsNullOrWhiteSpace(sql))
                    continue;

                await using var tx = await _db.Database.BeginTransactionAsync();

                try
                {
                    await _db.Database.ExecuteSqlRawAsync(sql);

                    _db.Migration.Add(new Migration
                    {
                        FileName = fileName,
                        ExecutedAt = DateTime.UtcNow
                    });

                    await _db.SaveChangesAsync();
                    await tx.CommitAsync();
                }
                catch (Exception)
                {
                    await tx.RollbackAsync();
                    throw;
                }
            }
        }

        private async Task EnsureMigrationTableExists()
        {
            var sql = @"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='_Migrations' AND xtype='U')
                BEGIN
                    CREATE TABLE [_Migrations] (
                        [Id] INT IDENTITY(1,1) PRIMARY KEY,
                        [FileName] NVARCHAR(260) NOT NULL UNIQUE,
                        [ExecutedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
                    )
                END";

            await _db.Database.ExecuteSqlRawAsync(sql);
        }
    }
}
