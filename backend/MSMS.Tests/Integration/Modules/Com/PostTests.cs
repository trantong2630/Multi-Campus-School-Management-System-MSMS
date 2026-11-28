using MSMS.Api.Data.Entities;
using MSMS.Tests.Integration.Infrastructure;

namespace MSMS.Tests.Integration.Modules.Com;

/// <summary>COM-02: bảng tin (BR-27). Tên test bắt đầu bằng mã case (COM_xx).</summary>
[Collection("api")]
public class PostTests(ApiFactory f)
{
    private World w => f.World;

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
    private static readonly byte[] Jpg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0];

    private static Dictionary<string, string?> Fields(string? title = "Tiêu đề", string? content = "Nội dung", string? visibility = "School") =>
        new() { ["title"] = title, ["content"] = content, ["visibility"] = visibility };

    private async Task<ApiClient> TeacherAsync() { await w.EnsureAsync(); var t = await w.NewStaffAsync("GV bài đăng " + World.Uniq(), "Teacher", w.CampusAId, null); return await f.LoginAsync(t.Username); }

    private async Task<int> NewPostAsync(ApiClient c, string visibility = "School", string? title = null)
    {
        var r = await c.PostFormAsync("/api/com/posts", Fields(title ?? "Bài " + World.Uniq(), "Nội dung bài", visibility), "files", []);
        Assert.Equal(200, r.Status);
        return r.Body!["postId"]!.GetValue<int>();
    }

    [Fact]
    public async Task COM_06_Create_a_post_with_images_and_read_them_back()
    {
        var t = await TeacherAsync();
        var r = await t.PostFormAsync("/api/com/posts", Fields("Bài có ảnh " + World.Uniq(), "Dòng 1\nDòng 2", "Internal"), "files", [("a.png", Png, "image/png"), ("b.jpg", Jpg, "image/jpeg")]);
        Assert.Equal(200, r.Status);
        Assert.Equal("Posted", r.Body!["status"]!.GetValue<string>());
        var images = r.Body["images"]!.AsArray();
        Assert.Equal(2, images.Count);
        var id = r.Body["postId"]!.GetValue<int>();
        var img = await t.GetBytesAsync($"/api/com/posts/{id}/images/{images[0]!["postImageId"]!.GetValue<int>()}");
        Assert.Equal(200, img.Status);
        Assert.Equal(Png, img.Bytes);
        Assert.Equal("Dòng 1\nDòng 2", (await t.GetAsync($"/api/com/posts/{id}")).Body!["content"]!.GetValue<string>());   // xuống dòng được giữ
    }

    [Fact]
    public async Task COM_07_Post_validations()
    {
        var t = await TeacherAsync();
        Task<ApiResult> Send(Dictionary<string, string?> fl, params (string, byte[], string)[] files) => t.PostFormAsync("/api/com/posts", fl, "files", files);
        var noTitle = await Send(Fields(title: ""));
        Assert.Equal(400, noTitle.Status); Assert.Contains("Tiêu đề", noTitle.Message);
        var noContent = await Send(Fields(content: ""));
        Assert.Equal(400, noContent.Status); Assert.Contains("Nội dung", noContent.Message);
        Assert.Equal("Chọn phạm vi bài đăng: Nội bộ hoặc Toàn trường.", (await Send(Fields(visibility: "Khác"))).Message);
        var big = new byte[3 * 1024 * 1024]; Png.CopyTo(big, 0);
        Assert.Contains("lớn hơn 2 MB", (await Send(Fields(), ("big.png", big, "image/png"))).Message);
        Assert.Contains("không phải JPG hoặc PNG", (await Send(Fields(), ("a.gif", "GIF89a...."u8.ToArray(), "image/gif"))).Message);
        Assert.Equal("Có ảnh bị rỗng. Chọn lại ảnh.", (await Send(Fields(), ("e.png", [], "image/png"))).Message);
        var six = Enumerable.Range(0, 6).Select(i => ($"i{i}.png", Png, "image/png")).ToArray();
        Assert.Equal("Mỗi bài tối đa 5 ảnh. Bỏ bớt ảnh rồi thử lại.", (await Send(Fields(), six)).Message);
        var blank = await Send(Fields(content: "   \n  \n "));
        Assert.Equal(400, blank.Status); Assert.Contains("Nội dung không được để trống", blank.Message);
        var longText = await Send(Fields(content: new string('a', 5001)));
        Assert.Equal(400, longText.Status);
        Assert.Equal("Nội dung tối đa 5000 ký tự. Rút gọn rồi lưu lại.", longText.Message);
    }

    [Fact]
    public async Task COM_08_Scope_decides_who_can_see_the_post()
    {
        var t = await TeacherAsync();
        var internalId = await NewPostAsync(t, "Internal");
        var schoolId = await NewPostAsync(t, "School");
        var staff = await f.LoginAsync(w.SchoolAdmin.Username);
        Assert.Equal(200, (await staff.GetAsync($"/api/com/posts/{internalId}")).Status);
        Assert.Equal(200, (await staff.GetAsync($"/api/com/posts/{schoolId}")).Status);
        foreach (var user in new[] { await f.LoginAsPersonAsync("Hoàng Gia Bảo"), await f.LoginAsPersonAsync("Vũ Thị Lan") })
        {
            Assert.Equal(404, (await user.GetAsync($"/api/com/posts/{internalId}")).Status);
            Assert.Equal(200, (await user.GetAsync($"/api/com/posts/{schoolId}")).Status);
            var list = await user.GetAsync("/api/com/posts?pageSize=50");
            Assert.DoesNotContain($"\"postId\":{internalId},", list.Raw);
        }
    }

    [Fact]
    public async Task COM_09_Only_Principal_Vice_Principal_and_Teacher_write_posts()
    {
        await w.EnsureAsync();
        var author = await TeacherAsync();
        var id = await NewPostAsync(author);
        var blocked = new[] { await f.LoginAsync(w.SchoolAdmin.Username), await f.LoginAsPersonAsync("Hoàng Gia Bảo"), await f.LoginAsPersonAsync("Vũ Thị Lan") };
        foreach (var c in blocked)
        {
            Assert.Equal(403, (await c.PostFormAsync("/api/com/posts", Fields(), "files", [])).Status);
            Assert.Equal(403, (await c.PutAsync($"/api/com/posts/{id}", new { title = "x", content = "y", visibility = "School" })).Status);
            Assert.Equal(403, (await c.DeleteAsync($"/api/com/posts/{id}")).Status);
            Assert.Equal(403, (await c.PostAsync($"/api/com/posts/{id}/hide", new { reason = "x" })).Status);
            Assert.Equal(403, (await c.PostAsync($"/api/com/posts/{id}/unhide")).Status);
            Assert.Equal(403, (await c.PostFormAsync($"/api/com/posts/{id}/images", new Dictionary<string, string?>(), "files", [("a.png", Png, "image/png")])).Status);
        }
        Assert.Equal(200, (await blocked[0].GetAsync("/api/com/posts")).Status);   // Quản trị trường được đọc
        foreach (var user in new[] { w.Principal.Username, w.ViceA.Username })
            Assert.Equal(200, (await (await f.LoginAsync(user)).PostFormAsync("/api/com/posts", Fields("Bài " + World.Uniq()), "files", [])).Status);
    }

    [Fact]
    public async Task COM_10_Edit_and_delete_are_limited_to_the_author()
    {
        var t1 = await TeacherAsync(); var t2 = await TeacherAsync();
        var id = await NewPostAsync(t1);
        var put = await t2.PutAsync($"/api/com/posts/{id}", new { title = "Sửa", content = "Sửa", visibility = "School" });
        Assert.Equal(404, put.Status);
        Assert.Equal("Bài đăng không tồn tại hoặc không phải bài của bạn.", put.Message);
        Assert.Equal(404, (await t2.DeleteAsync($"/api/com/posts/{id}")).Status);
        Assert.Equal(200, (await t1.PutAsync($"/api/com/posts/{id}", new { title = "Đã sửa", content = "Nội dung mới", visibility = "School" })).Status);
        Assert.Equal(204, (await t1.DeleteAsync($"/api/com/posts/{id}")).Status);
        Assert.Equal(404, (await t1.GetAsync($"/api/com/posts/{id}")).Status);
    }

    [Fact]
    public async Task COM_11_Hide_and_unhide_rules()
    {
        await w.EnsureAsync();
        var t1 = await TeacherAsync(); var t2 = await TeacherAsync();
        var principal = await f.LoginAsync(w.Principal.Username);
        var vp = await f.LoginAsync(w.ViceA.Username);
        var student = await f.LoginAsPersonAsync("Hoàng Gia Bảo");
        // bước 1: tác giả ẩn bài mình không cần lý do
        var own = await NewPostAsync(t1);
        Assert.Equal(200, (await t1.PostAsync($"/api/com/posts/{own}/hide", new { })).Status);
        Assert.Equal(409, (await t1.PostAsync($"/api/com/posts/{own}/hide", new { })).Status);   // bước 6
        Assert.Equal("Bài đăng này đã được ẩn.", (await t1.PostAsync($"/api/com/posts/{own}/hide", new { })).Message);
        Assert.Equal(200, (await t1.PostAsync($"/api/com/posts/{own}/unhide")).Status);
        var visible = await t1.PostAsync($"/api/com/posts/{own}/unhide");
        Assert.Equal(409, visible.Status); Assert.Equal("Bài đăng này đang hiển thị.", visible.Message);
        // bước 2: Quản trị trường không có quyền
        var other = await NewPostAsync(t1);
        Assert.Equal(403, (await (await f.LoginAsync(w.SchoolAdmin.Username)).PostAsync($"/api/com/posts/{other}/hide", new { reason = "x" })).Status);
        // bước 4: Hiệu phó và giáo viên khác không ẩn được bài của người khác
        foreach (var c in new[] { vp, t2 })
        {
            var r = await c.PostAsync($"/api/com/posts/{other}/hide", new { reason = "x" });
            Assert.Equal(403, r.Status); Assert.Equal("Bạn không có quyền ẩn bài của người này.", r.Message);
        }
        // bước 3: Hiệu trưởng ẩn bài người khác: thiếu lý do thì 400
        var noReason = await principal.PostAsync($"/api/com/posts/{other}/hide", new { });
        Assert.Equal(400, noReason.Status); Assert.Contains("Lý do ẩn bài", noReason.Message);
        Assert.Equal(200, (await principal.PostAsync($"/api/com/posts/{other}/hide", new { reason = "Nội dung không phù hợp" })).Status);
        // bước 7: đọc danh sách
        Assert.Equal(200, (await t1.GetAsync($"/api/com/posts/{other}")).Status);       // tác giả vẫn thấy
        Assert.Equal(200, (await principal.GetAsync($"/api/com/posts/{other}")).Status);   // người ẩn và Hiệu trưởng vẫn thấy
        Assert.Equal(404, (await t2.GetAsync($"/api/com/posts/{other}")).Status);       // giáo viên khác không thấy
        Assert.Equal(404, (await student.GetAsync($"/api/com/posts/{other}")).Status);  // học sinh không thấy
        // bước 5: tác giả không hiện lại được bài do Hiệu trưởng ẩn; Hiệu trưởng hiện lại được
        var un = await t1.PostAsync($"/api/com/posts/{other}/unhide");
        Assert.Equal(403, un.Status); Assert.Equal("Chỉ người đã ẩn bài mới hiện lại được bài này.", un.Message);
        Assert.Equal(200, (await principal.PostAsync($"/api/com/posts/{other}/unhide")).Status);
        // bài của Hiệu phó cũng do Hiệu trưởng ẩn được
        var vpPost = await NewPostAsync(vp);
        Assert.Equal(200, (await principal.PostAsync($"/api/com/posts/{vpPost}/hide", new { reason = "Kiểm duyệt" })).Status);
    }
}
