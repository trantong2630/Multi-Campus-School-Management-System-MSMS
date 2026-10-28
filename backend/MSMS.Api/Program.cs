using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MSMS.Api.Common.Audit;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Notifications;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Modules.Adm;
using MSMS.Api.Modules.Com;
using MSMS.Api.Modules.Iam;
using MSMS.Api.Modules.Ppl;
using MSMS.Api.Modules.Sch;
using MSMS.Api.Modules.Scd;
using MSMS.Api.Modules.Par;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console()
    .WriteTo.File("logs/msms-.log", rollingInterval: RollingInterval.Day));

builder.Services.AddDbContext<MsmsDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<ScopeResolver>();
builder.Services.AddScoped<AuditWriter>();
builder.Services.AddScoped<NotificationSender>();

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Issuer,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ClockSkew = TimeSpan.FromMinutes(1),
        };
        
        // COM-4.8.6: Cấu hình lấy token từ query string cho SignalR (cả negotiate và websocket)
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                
                // Nếu request đi vào endpoint /signalr và có access_token trên query string
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/signalr"))
                {
                    context.Token = accessToken;
                }
                
                return Task.CompletedTask;
            },
            OnChallenge = ctx =>
            {
                ctx.HandleResponse();
                return ExceptionMiddleware.Write(ctx.HttpContext, 401, ErrorCodes.Unauthorized,
                    "Phiên đăng nhập đã hết hạn hoặc không hợp lệ. Đăng nhập lại để tiếp tục.");
            },
            OnForbidden = ctx => ExceptionMiddleware.Write(ctx.HttpContext, 403, ErrorCodes.Forbidden,
                "Không có quyền thực hiện thao tác này."),
        };
    });
builder.Services.AddAuthorization(Policies.Register);

builder.Services.AddIamModule();
builder.Services.AddSchModule();
builder.Services.AddPplModule();
builder.Services.AddScdModule();
builder.Services.AddComModule();
builder.Services.AddParModule();

builder.Services.AddAdmModule();

// Đăng ký SignalR Hub cho thông báo và tin nhắn realtime
// COM-4.8.6: Hub xác thực bằng access_token qua query string
builder.Services.AddSignalR();

builder.Services.AddControllers();

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();
app.UseAuthentication();
app.UseMiddleware<AccountStatusMiddleware>();
app.UseAuthorization();
app.MapControllers();

// Đăng ký SignalR Hub route
// COM-4.8.6: Token được truyền qua query string: /signalr?access_token=...
app.MapHub<CommunicationHub>("/signalr");

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await DevSeeder.RunAsync(
        scope.ServiceProvider.GetRequiredService<MsmsDbContext>(),
        scope.ServiceProvider.GetRequiredService<IClock>(),
        scope.ServiceProvider.GetRequiredService<ILogger<Program>>());
}

app.Run();
