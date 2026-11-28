using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MSMS.Api.Common.Ai;
using MSMS.Api.Common.Email;
using MSMS.Api.Common.Storage;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Iam.Services;

namespace MSMS.Tests.Integration.Infrastructure;

/// <summary>
/// Chạy cả MSMS.Api trong bộ nhớ trên một database SQL Server LocalDB riêng (tạo bằng migration, xóa khi xong).
/// Môi trường Development nên DevSeeder nạp dữ liệu mẫu (1 phân hiệu, 6 lớp, giáo viên, học sinh, phụ huynh); phần còn thiếu do <see cref="ExtraSeed"/> bổ sung.
/// SMTP, Cloudinary và Gemini được thay bằng bản giả.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string Password = "Msms@123";
    private readonly string dbName = $"MSMS_IT_{Guid.NewGuid():N}";
    private readonly string connectionString;

    public TestClock Clock { get; } = new();
    public RecordingEmailSender Mail { get; } = new();
    public StubAiClient Ai { get; } = new();
    public RecordingOtpSender Otp { get; } = new();

    public ApiFactory()
    {
        var sql = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("MSMS_IT_SQLSERVER") ?? @"Server=(localdb)\MSSQLLocalDB;Integrated Security=true")
        { InitialCatalog = dbName, TrustServerCertificate = true };
        connectionString = sql.ConnectionString;
        using var db = NewDb();
        db.Database.Migrate();
    }

    private MsmsDbContext NewDb() => new(new DbContextOptionsBuilder<MsmsDbContext>().UseSqlServer(connectionString).Options);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = connectionString,
            ["Cloudinary:CloudName"] = "test", ["Cloudinary:ApiKey"] = "test", ["Cloudinary:ApiSecret"] = "test",
            ["Ai:ApiKey"] = "test-key",
            ["Serilog:MinimumLevel:Default"] = "Warning",
            ["Logging:LogLevel:Default"] = "Warning",
        }));
        builder.ConfigureTestServices(s =>
        {
            s.AddSingleton<IClock>(Clock);
            s.AddSingleton<IEmailSender>(Mail);
            s.AddSingleton<IImageStorage, MemoryImageStorage>();
            s.AddSingleton<IAiClient>(Ai);
            s.AddSingleton<IOtpEmailSender>(Otp);
            s.AddTransient<IStartupFilter, TestIpStartupFilter>();
        });
    }

    /// <summary>Phạm vi DI để đọc/ghi thẳng DB (dựng dữ liệu, kiểm tra kết quả).</summary>
    public async Task<T> WithDbAsync<T>(Func<MsmsDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<MsmsDbContext>());
    }

    public Task WithDbAsync(Func<MsmsDbContext, Task> action) => WithDbAsync<int>(async db => { await action(db); return 0; });

    /// <summary>Máy khách đã đăng nhập bằng tên người dùng của tài khoản có sẵn.</summary>
    public async Task<ApiClient> LoginAsync(string username, string password = Password)
    {
        var client = CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        var json = await res.Content.ReadFromJsonAsync<JsonObject>();
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"Đăng nhập {username} lỗi {(int)res.StatusCode}: {json}");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json!["token"]!.GetValue<string>());
        return new ApiClient(client);
    }

    /// <summary>Đăng nhập theo họ tên người dùng trong dữ liệu mẫu (tên người dùng do hệ thống sinh nên tra theo họ tên).</summary>
    public async Task<ApiClient> LoginAsPersonAsync(string fullName)
    {
        var username = await WithDbAsync(db => db.Account.Where(a => a.Person.FullName == fullName).Select(a => a.Username).SingleAsync());
        return await LoginAsync(username);
    }

    private World? world;
    public World World => world ??= new World(this);

    public ApiClient Anonymous() => new(CreateClient());

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        try { using var db = NewDb(); db.Database.EnsureDeleted(); } catch { /* DB tạm, bỏ qua */ }
    }
}

public sealed record ApiResult(int Status, JsonNode? Body, string Raw)
{
    public bool Ok => Status is >= 200 and < 300;
    public string Message => (Body as JsonObject)?["message"]?.GetValue<string>() ?? Raw;
    public string? Code => (Body as JsonObject)?["code"]?.GetValue<string>();
}

/// <summary>Gọi API và trả về (mã HTTP, JSON) để test so với kết quả mong đợi.</summary>
public sealed class ApiClient
{
    private static int ipCounter;
    public HttpClient Http { get; }

    public ApiClient(HttpClient http)
    {
        Http = http;
        // Mỗi máy khách có địa chỉ IP riêng để giới hạn tần suất (theo IP) không bị dồn giữa các test.
        var n = Interlocked.Increment(ref ipCounter);
        if (!http.DefaultRequestHeaders.Contains("X-Test-Ip")) http.DefaultRequestHeaders.Add("X-Test-Ip", $"10.{(n >> 16) & 255}.{(n >> 8) & 255}.{n & 255}");
    }

    /// <summary>Gửi multipart/form-data gồm các trường chữ và nhiều file (cùng tên trường), mỗi file có tên, nội dung và kiểu MIME.</summary>
    public async Task<ApiResult> PostFormAsync(string url, IDictionary<string, string?> fields, string fileField, IEnumerable<(string Name, byte[] Bytes, string ContentType)> files)
    {
        using var form = new MultipartFormDataContent();
        foreach (var (k, v) in fields) if (v != null) form.Add(new StringContent(v), k);
        foreach (var (name, bytes, type) in files)
        {
            var c = new ByteArrayContent(bytes);
            c.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(type);
            form.Add(c, fileField, name);
        }
        using var res = await Http.PostAsync(url, form);
        var raw = await res.Content.ReadAsStringAsync();
        JsonNode? json = null;
        if (!string.IsNullOrWhiteSpace(raw)) { try { json = JsonNode.Parse(raw); } catch { } }
        return new ApiResult((int)res.StatusCode, json, raw);
    }

    /// <summary>Tải tệp lên (multipart/form-data) với một trường file.</summary>
    public async Task<ApiResult> PostFileAsync(string url, string field, byte[] bytes, string fileName)
    {
        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        form.Add(content, field, fileName);
        using var res = await Http.PostAsync(url, form);
        var raw = await res.Content.ReadAsStringAsync();
        JsonNode? json = null;
        if (!string.IsNullOrWhiteSpace(raw)) { try { json = JsonNode.Parse(raw); } catch { } }
        return new ApiResult((int)res.StatusCode, json, raw);
    }

    /// <summary>Tải tệp về (byte).</summary>
    public async Task<(int Status, byte[] Bytes, string? ContentType)> GetBytesAsync(string url)
    {
        using var res = await Http.GetAsync(url);
        return ((int)res.StatusCode, await res.Content.ReadAsByteArrayAsync(), res.Content.Headers.ContentType?.MediaType);
    }

    public Task<ApiResult> GetAsync(string url) => SendAsync(HttpMethod.Get, url, null);
    public Task<ApiResult> PostAsync(string url, object? body = null) => SendAsync(HttpMethod.Post, url, body);
    public Task<ApiResult> PutAsync(string url, object? body = null) => SendAsync(HttpMethod.Put, url, body);
    public Task<ApiResult> DeleteAsync(string url) => SendAsync(HttpMethod.Delete, url, null);

    private async Task<ApiResult> SendAsync(HttpMethod method, string url, object? body)
    {
        using var req = new HttpRequestMessage(method, url);
        if (body != null || method == HttpMethod.Post || method == HttpMethod.Put) req.Content = JsonContent.Create(body ?? new { });
        using var res = await Http.SendAsync(req);
        var raw = await res.Content.ReadAsStringAsync();
        JsonNode? json = null;
        if (!string.IsNullOrWhiteSpace(raw)) { try { json = JsonNode.Parse(raw); } catch { /* không phải JSON (tệp) */ } }
        return new ApiResult((int)res.StatusCode, json, raw);
    }
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<ApiFactory> { }

/// <summary>Cho phép test đặt địa chỉ IP gọi API (header X-Test-Ip) trước mọi middleware, vì giới hạn tần suất tính theo IP.</summary>
public sealed class TestIpStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((ctx, nxt) =>
        {
            if (ctx.Request.Headers.TryGetValue("X-Test-Ip", out var ip) && IPAddress.TryParse(ip.ToString(), out var addr)) ctx.Connection.RemoteIpAddress = addr;
            return nxt(ctx);
        });
        next(app);
    };
}
