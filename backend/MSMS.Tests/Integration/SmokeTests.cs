using MSMS.Tests.Integration.Infrastructure;

namespace MSMS.Tests.Integration;

[Collection("api")]
public class SmokeTests(ApiFactory f)
{
    [Fact]
    public async Task The_api_runs_on_the_temporary_database_only()
    {
        var (db, server) = await f.WithDbAsync(c => Task.FromResult((c.Database.GetDbConnection().Database, c.Database.GetDbConnection().DataSource)));
        Assert.StartsWith("MSMS_IT_", db);
        Assert.Contains("localdb", server, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_and_read_a_protected_endpoint()
    {
        Assert.Equal(401, (await f.Anonymous().GetAsync("/api/sch/campuses")).Status);
        var admin = await f.LoginAsPersonAsync("Trần Đức Kiên");
        Assert.Equal(200, (await admin.GetAsync("/api/sch/campuses")).Status);
    }
}
