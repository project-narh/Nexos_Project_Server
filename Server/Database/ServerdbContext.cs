using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Server.Database
{
    public class SqlLoggerInterceptor : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData, InterceptionResult<int> result)
        {
            var context = eventData.Context;
            if (context != null)
            {
                var changes = context.ChangeTracker.DebugView.LongView; // 변경된 SQL 미리보기
                Console.WriteLine($"변경된 SQL:\n{changes}");
            }
            return base.SavingChanges(eventData, result);
        }
    }

    public class ServerdbContext : DbContext
    {
        private readonly string _connectionString;

        public DbSet<UserAccount> UserAccounts { get; set; }
        public DbSet<ItemList> ItemLists { get; set; }
        public DbSet<UserItem> UserItems { get; set; }

        public ServerdbContext()
        {
            _connectionString = GetAccountTable();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (string.IsNullOrEmpty(_connectionString)) GetAccountTable();

            optionsBuilder.UseMySql(_connectionString,
                new MySqlServerVersion(new Version(10, 11, 11)));
            //optionsBuilder
            //    .AddInterceptors(new SqlLoggerInterceptor()) // 인터셉터 등록
            //    .LogTo(Console.WriteLine, LogLevel.Information) // EF Core 기본 SQL 로그 활성화
            //    .EnableSensitiveDataLogging(); // WHERE 조건에 포함된 데이터도 로그에 출력
        }

        //protected override void OnModelCreating(ModelBuilder modelBuilder)
        //{
        //    modelBuilder.Entity<UserItem>()
        //        .HasKey(u => u.Id); // 🎯 `id`만 PK로 설정

        //    modelBuilder.Entity<UserItem>()
        //        .HasIndex(u => new { u.UniqueId, u.Uid }) // 🎯 특정 필드를 WHERE에 사용하도록 설정
        //        .HasDatabaseName("IX_UserItem_UniqueId_Uid");
        //}

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserItem>()
                .HasKey(u => new { u.Id, u.UniqueId }); // 복합 PK 설정
            base.OnModelCreating(modelBuilder);
        }



        private string GetAccountTable()
        {
            string path = Directory.GetCurrentDirectory() + "\\Database\\Connect_Account.json";

            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"DB 보안 파일을 찾을 수 없습니다.  DB 파일 위치 : {path}");
            }

            string json = File.ReadAllText(path);
            Connect_Account account = JsonSerializer.Deserialize<Connect_Account>(json);

            return account.DB;
        }

        public IDbConnection CreateConnection()
        {
            var connection = new MySqlConnection(_connectionString);
            connection.Open();
            return connection;
        }

    }
    class Connect_Account { public string DB { get; set; } }
}
