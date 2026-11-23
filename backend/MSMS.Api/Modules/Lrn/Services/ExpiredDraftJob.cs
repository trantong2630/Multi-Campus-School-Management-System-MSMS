using Microsoft.Extensions.Options;

namespace MSMS.Api.Modules.Lrn.Services;

/// <summary>Cấu hình LRN (mục "Lrn" trong appsettings).</summary>
public sealed class LrnOptions
{
    /// <summary>Bao nhiêu phút quét một lần để tự nộp bản nháp đã quá hạn thực tế.</summary>
    public int ExpiredDraftSweepMinutes { get; set; } = 2;
}

/// <summary>
/// BR-21: định kỳ tự nộp và chấm các bản nháp đã quá hạn thực tế, để sổ liên lạc, các màn Tình hình và MSMS AI có số nộp bài đúng
/// mà không phải chờ ai mở trang bài tập. Trang bài tập vẫn tự nộp ngay lúc mở; hai đường đua nhau thì chỉ một đường nộp được
/// (<see cref="AttemptGrader.FinalizeAsync"/>), nên chạy nhiều tiến trình cùng lúc vẫn an toàn.
/// </summary>
public sealed class ExpiredDraftJob(IServiceScopeFactory scopes, IOptions<LrnOptions> options, ILogger<ExpiredDraftJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, options.Value.ExpiredDraftSweepMinutes)));
        try
        {
            do await RunOnceAsync();
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Server đang tắt.
        }
    }

    /// <summary>Một lượt quét. Lỗi chỉ ghi log để lượt sau chạy tiếp, không làm dừng job.</summary>
    public async Task RunOnceAsync()
    {
        try
        {
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AttemptGrader>().FinalizeExpiredAsync(null, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lượt tự nộp bản nháp quá hạn bị lỗi; sẽ thử lại ở lượt sau");
        }
    }
}
