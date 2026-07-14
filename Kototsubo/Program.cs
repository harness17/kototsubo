using Dev.CommonLibrary.Entity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Site.Common;
using Site.Repository;
using Site.Services;

var builder = WebApplication.CreateBuilder(args);

var siteConnection = builder.Configuration.GetConnectionString("SiteConnection");
if (string.IsNullOrWhiteSpace(siteConnection))
{
    throw new InvalidOperationException(
        $"ConnectionStrings:SiteConnection is not configured for the {builder.Environment.EnvironmentName} environment.");
}

// Data Protection キーの永続化
var keysFolder = Path.Combine(builder.Environment.ContentRootPath, "DataProtectionKeys");
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysFolder))
    .SetApplicationName("Kototsubo");

builder.Services.AddControllersWithViews()
    .AddRazorRuntimeCompilation();

// DBContext
builder.Services.AddDbContextPool<DBContext>(options =>
    options.UseSqlServer(siteConnection));

// Response compression
builder.Services.AddResponseCompression(options =>
{
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] { "image/svg+xml" });
});

// ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.User.RequireUniqueEmail = false;
})
.AddEntityFrameworkStores<DBContext>()
.AddDefaultTokenProviders();

// Antiforgery
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = builder.Environment.IsDevelopment()
        ? ".AspNetCore.Antiforgery.Kototsubo-Dev"
        : ".AspNetCore.Antiforgery.Kototsubo";
    options.HeaderName = "X-CSRF-TOKEN";
});

// Cookie認証設定
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/LogOff";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(1440);
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(1440);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// IHttpContextAccessor (EntityBase用)
builder.Services.AddHttpContextAccessor();

// AutoMapper
builder.Services.AddAutoMapper(cfg => cfg.AddMaps(AppDomain.CurrentDomain.GetAssemblies()));

// Repository
builder.Services.AddScoped<ItemRepository>();
builder.Services.AddScoped<WordRepository>();

// External API and import services
builder.Services.AddHttpClient("OpenBD", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddScoped<OpenBDLookupService>();
builder.Services.AddSingleton<KindleImportParser>();
builder.Services.AddHttpClient("NdlSearch", client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddScoped<INdlSearchService, NdlSearchService>();
builder.Services.AddScoped<NdlPrimaryBookLookupService>();
builder.Services.AddScoped<IBookLookupService>(
    provider => provider.GetRequiredService<NdlPrimaryBookLookupService>());
builder.Services.AddScoped<IBookCandidateLookupService>(
    provider => provider.GetRequiredService<NdlPrimaryBookLookupService>());
builder.Services.AddHttpClient("RakutenBooks", client =>
{
    client.BaseAddress = new Uri("https://openapi.rakuten.co.jp/services/api/");
    client.Timeout = TimeSpan.FromSeconds(15);
    var referer = builder.Configuration["ExternalApis:RakutenReferer"];
    if (!string.IsNullOrWhiteSpace(referer))
    {
        client.DefaultRequestHeaders.Referrer = new Uri(referer);
    }
});
builder.Services.AddSingleton<RakutenBooksClient>();

builder.Services.AddHttpClient("SteamStore", client =>
{
    client.BaseAddress = new Uri("https://store.steampowered.com/");
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddScoped<ISteamAppDetailsLookupService, SteamAppDetailsLookupService>();

var rakutenApplicationId = builder.Configuration["ExternalApis:RakutenApplicationId"];
if (!string.IsNullOrWhiteSpace(rakutenApplicationId))
{
    builder.Services.AddScoped<IGameLookupService, RakutenGameLookupService>();
    builder.Services.AddScoped<ICdLookupService, RakutenCdLookupService>();
    builder.Services.AddScoped<IDvdLookupService, RakutenDvdLookupService>();
    builder.Services.AddScoped<RakutenBookLookupService>();
}
else
{
    builder.Services.AddSingleton<IGameLookupService, GameLookupServiceStub>();
    builder.Services.AddSingleton<ICdLookupService, CdLookupServiceStub>();
    builder.Services.AddSingleton<IDvdLookupService, DvdLookupServiceStub>();
}

var app = builder.Build();

// IHttpContextAccessorをEntityBaseに設定
var accessor = app.Services.GetRequiredService<IHttpContextAccessor>();
EntityBase.HttpContextAccessor = accessor;

// Logger設定
var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
Dev.CommonLibrary.Common.Logger.GetLogger().SetLogger(loggerFactory.CreateLogger("App"));

// DB マイグレーション適用・ロールおよび任意の初回ユーザー作成
await using (var scope = app.Services.CreateAsyncScope())
{
    var sp = scope.ServiceProvider;
    var context = sp.GetRequiredService<DBContext>();
    await context.Database.MigrateAsync();
    await StartupSeeder.SeedAsync(
        sp.GetRequiredService<RoleManager<ApplicationRole>>(),
        sp.GetRequiredService<UserManager<ApplicationUser>>(),
        builder.Configuration);
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseResponseCompression();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        var headers = context.Context.Response.Headers;
        if (app.Environment.IsDevelopment())
        {
            headers.CacheControl = "no-cache, no-store";
            headers.Pragma = "no-cache";
            headers.Expires = "0";
            return;
        }
        headers.CacheControl = "public,max-age=604800";
    }
});
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
