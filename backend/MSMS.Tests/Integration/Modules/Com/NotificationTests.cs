using MSMS.Api.Data.Entities;
using MSMS.Tests.Integration.Infrastructure;

namespace MSMS.Tests.Integration.Modules.Com;

/// <summary>COM-01: thông báo (danh sách, đếm chưa đọc, đánh dấu đã đọc, riêng tư). Tên test bắt đầu bằng mã case (COM_xx).</summary>
[Collection("api")]
public class NotificationTests(ApiFactory f)
{
    private World w => f.World;

    private async Task<(Who User, List<long> Ids)> UserWithNotificationsAsync(int count)
    {
        var u = await w.NewAccountAsync("COM " + World.Uniq(), "Staff", "Teacher", "Active");
        var ids = await f.WithDbAsync(async db =>
        {
            var list = Enumerable.Range(0, count).Select(i => new Notification
            {
                RecipientPersonId = u.PersonId, NotificationType = "ScheduleChanged", Title = "Thông báo " + i, Body = "Nội dung " + i,
                CreatedAt = f.Clock.Now.AddMinutes(-i),
            }).ToList();
            db.Notification.AddRange(list);
            await db.SaveChangesAsync();
            return list.Select(n => n.NotificationId).ToList();
        });
        return (u, ids);
    }

    [Fact]
    public async Task COM_01_List_unread_counter_mark_read_and_read_all()
    {
        var (u, ids) = await UserWithNotificationsAsync(3);
        var c = await f.LoginAsync(u.Username);
        var list = await c.GetAsync("/api/com/notifications");
        Assert.Equal(200, list.Status);
        Assert.Equal(3, list.Body!["total"]!.GetValue<int>());
        var titles = list.Body["items"]!.AsArray().Select(x => x!["title"]!.GetValue<string>()).ToList();
        Assert.Equal(new[] { "Thông báo 0", "Thông báo 1", "Thông báo 2" }, titles);   // mới nhất trước
        Assert.Equal(3, (await c.GetAsync("/api/com/notifications/unread-count")).Body!["unreadCount"]!.GetValue<int>());
        Assert.Equal(204, (await c.PutAsync($"/api/com/notifications/{ids[0]}/read")).Status);
        Assert.Equal(2, (await c.GetAsync("/api/com/notifications/unread-count")).Body!["unreadCount"]!.GetValue<int>());
        var all = await c.PutAsync("/api/com/notifications/read-all");
        Assert.Equal(200, all.Status);
        Assert.Equal(2, all.Body!["updated"]!.GetValue<int>());
        Assert.Equal(0, (await c.GetAsync("/api/com/notifications/unread-count")).Body!["unreadCount"]!.GetValue<int>());
        var unread = await c.GetAsync("/api/com/notifications?isRead=false");
        Assert.Empty(unread.Body!["items"]!.AsArray());
    }

    [Fact]
    public async Task COM_03_Notifications_of_other_people_look_like_they_do_not_exist()
    {
        var (_, ids) = await UserWithNotificationsAsync(1);
        var (b, _) = await UserWithNotificationsAsync(1);
        var cb = await f.LoginAsync(b.Username);
        var open = await cb.PostAsync($"/api/com/notifications/{ids[0]}/open");
        Assert.Equal(404, open.Status);
        Assert.Equal("Không tìm thấy thông báo.", open.Message);
        Assert.Equal(404, (await cb.PutAsync($"/api/com/notifications/{ids[0]}/read")).Status);
        Assert.Equal(404, (await cb.PostAsync("/api/com/notifications/987654321/open")).Status);
        Assert.False(await f.WithDbAsync(db => db.Notification.Where(n => n.NotificationId == ids[0]).Select(n => n.IsRead).SingleAsync()));
    }
}
