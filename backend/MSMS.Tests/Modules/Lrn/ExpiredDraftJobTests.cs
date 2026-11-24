using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Lrn.Services;
using MSMS.Tests.Modules.Sch;

namespace MSMS.Tests.Modules.Lrn;

/// <summary>
/// BR-21: job định kỳ gọi tự nộp bản nháp quá hạn cho mọi bài. Phần chấm và nộp dùng ExecuteUpdate nên không chạy được trên DB trong bộ nhớ;
/// ở đây kiểm vòng quét: chạy được khi chưa có gì quá hạn và một lượt lỗi không làm dừng job.
/// </summary>
public class ExpiredDraftJobTests
{
    private static readonly DateOnly Today = new(2026, 10, 27);

    /// <summary>Ghi lại số lần job báo lỗi.</summary>
    private sealed class ErrorCounter : ILogger<ExpiredDraftJob>
    {
        public int Errors { get; private set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Error) Errors++;
        }
    }

    private static (ExpiredDraftJob Job, ServiceProvider Services, ErrorCounter Log) Build(bool withDb, string dbName)
    {
        var s = new ServiceCollection();
        s.AddLogging();
        s.AddSingleton(SchTestSupport.ClockAt(Today));
        s.AddSingleton(Mock.Of<IHostApplicationLifetime>());
        s.AddSingleton<AnalysisJob>();
        s.AddScoped<AttemptGrader>();
        if (withDb)
            s.AddDbContext<MsmsDbContext>(o => o.UseInMemoryDatabase(dbName).ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        var services = s.BuildServiceProvider();
        var log = new ErrorCounter();
        var job = new ExpiredDraftJob(services.GetRequiredService<IServiceScopeFactory>(), Options.Create(new LrnOptions()), log);
        return (job, services, log);
    }

    [Fact(DisplayName = "LRN | Job tự nộp nháp quá hạn | Nháp còn hạn | không bị nộp")]
    public async Task DraftStillOpen_IsLeftAlone()
    {
        var dbName = Guid.NewGuid().ToString();
        var (job, services, log) = Build(withDb: true, dbName);
        using (services)
        {
            using (var scope = services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MsmsDbContext>();
                var now = scope.ServiceProvider.GetRequiredService<IClock>().Now;
                db.Assignment.Add(new Assignment
                {
                    AssignmentId = 1, SubjectId = 1, ClassId = 1, Title = "Bài", OpenAt = now.AddHours(-1), DueAt = now.AddDays(1),
                    AssignedByPersonId = 1, CreatedAt = now,
                });
                db.Submission.Add(new Submission { SubmissionId = 1, AssignmentId = 1, StudentId = 10, StartedAt = now, LastSavedAt = now });
                db.SubmissionAnswer.Add(new SubmissionAnswer
                {
                    AnswerId = 1, SubmissionId = 1, AssignmentQuestionId = 1,
                    SubmissionAnswerOption = { new SubmissionAnswerOption { OptionId = 1 } },
                });
                await db.SaveChangesAsync();
            }

            await job.RunOnceAsync();

            using var check = services.CreateScope();
            var after = await check.ServiceProvider.GetRequiredService<MsmsDbContext>().Submission.AsNoTracking().SingleAsync();
            Assert.Null(after.SubmittedAt);
            Assert.Equal(0, log.Errors);   // không cố nộp bài còn hạn
        }
    }

    [Fact(DisplayName = "LRN | Job tự nộp nháp quá hạn | Một lượt quét lỗi | chỉ ghi log, không làm dừng job")]
    public async Task FailingSweep_DoesNotThrow()
    {
        var (job, services, log) = Build(withDb: false, Guid.NewGuid().ToString());   // thiếu DbContext: lượt quét chắc chắn lỗi
        using (services)
        {
            await job.RunOnceAsync();
            await job.RunOnceAsync();
        }
        Assert.Equal(2, log.Errors);
    }
}
