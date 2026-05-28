
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using LittleOSS.Options;
using LittleOSS.Data;

namespace LittleOSS
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllers();

            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            // Configure OSS options from appsettings.json
            builder.Services.Configure<OssOptions>(
                builder.Configuration.GetSection(OssOptions.Position));

            // Load access keys from config into singleton options
            builder.Services.AddSingleton<AccessKeyOptions>(_ =>
            {
                var accessKeys = builder.Configuration
                    .GetSection("AccessKeys")
                    .Get<List<AccessKeyConfig>>() ?? [];
                return new AccessKeyOptions { AccessKeys = accessKeys };
            });

            // Diagnostic: Log configuration loading
            var accessKeysSection = builder.Configuration.GetSection("AccessKeys");
            Console.WriteLine($"[Config] AccessKeys section exists: {accessKeysSection.Exists()}");
            Console.WriteLine($"[Config] AccessKeys raw value: {accessKeysSection.Value}");
            Console.WriteLine($"[Config] AccessKeys children count: {accessKeysSection.GetChildren().Count()}");

            var accessKeyList = accessKeysSection.Get<List<AccessKeyConfig>>();
            Console.WriteLine($"[Config] AccessKeys list count: {accessKeyList?.Count ?? -1}");
            if (accessKeyList != null)
            {
                foreach (var ak in accessKeyList)
                {
                    Console.WriteLine($"[Config] AccessKey: KeyId={ak.KeyId}, SecretKey={ak.SecretKey}");
                }
            }

            var ossSection = builder.Configuration.GetSection("Oss");
            Console.WriteLine($"[Config] Oss section exists: {ossSection.Exists()}");
            Console.WriteLine($"[Config] Oss Regions: {string.Join(", ", ossSection.Get<OssOptions>()?.Regions ?? [])}");

            // Register DbContext with dynamic database selection
            var dbConfig = builder.Configuration.GetSection("Oss:Database").Get<DatabaseOptions>()
                ?? new DatabaseOptions();

            builder.Services.AddDbContextFactory<Data.OssDbContext>(options =>
            {
                if (dbConfig.Provider.Equals("MySql", StringComparison.OrdinalIgnoreCase))
                {
                    var connectionString = dbConfig.ConnectionString
                        ?? "Server=localhost;Port=3306;Database=littleoss;User=root;Password=;";
                    var serverVersion = new MySqlServerVersion(new Version(8, 0, 0));
                    options.UseMySql(connectionString, serverVersion);
                }
                else
                {
                    var sqliteFileName = string.IsNullOrEmpty(dbConfig.SqlitePath)
                        ? "oss.db"
                        : dbConfig.SqlitePath;
                    var dbPath = Path.Combine(builder.Environment.ContentRootPath, sqliteFileName);
                    options.UseSqlite($"Data Source={dbPath}");
                }
            });

            // Register OSS services
            builder.Services.AddSingleton<Services.IOssConfigService, Services.OssConfigService>();
            builder.Services.AddSingleton<Services.IRegionLockService, Services.RegionLockService>();
            builder.Services.AddScoped<Services.IFileStorageService, Services.FileStorageService>();
            builder.Services.AddScoped<Services.IMetadataService, Services.MetadataService>();
            builder.Services.AddScoped<Services.IQuotaService, Services.QuotaService>();
            builder.Services.AddScoped<Services.IConcurrencyCoordinator, Services.ConcurrencyCoordinator>();

            // Register authentication
            builder.Services.AddAuthentication("OssAuth")
                .AddScheme<AuthenticationSchemeOptions, Authentication.OssAuthenticationHandler>("OssAuth", null);

            builder.Services.AddAuthorization();

            var app = builder.Build();

            // Ensure storage directory exists
            var storageRoot = app.Services.GetRequiredService<Services.IOssConfigService>().StorageRoot;
            if (!Directory.Exists(storageRoot))
            {
                Directory.CreateDirectory(storageRoot);
            }

            // Ensure database is created
            using (var scope = app.Services.CreateScope())
            {
                var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<Data.OssDbContext>>();
                await using var context = await contextFactory.CreateDbContextAsync();
                await context.Database.EnsureCreatedAsync();
            }

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
