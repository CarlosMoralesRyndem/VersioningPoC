using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Versioning.Tests;

public class VersionEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public VersionEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetVersion_ReturnsOk()
    {
        var response = await _client.GetAsync("/version");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetVersion_ReturnsExpectedFields()
    {
        var response = await _client.GetAsync("/version");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json).RootElement;

        Assert.True(doc.TryGetProperty("version", out _), "Response must contain 'version'");
        Assert.True(doc.TryGetProperty("environment", out _), "Response must contain 'environment'");
        Assert.True(doc.TryGetProperty("branch", out _), "Response must contain 'branch'");
        Assert.True(doc.TryGetProperty("commit", out _), "Response must contain 'commit'");
        Assert.True(doc.TryGetProperty("buildDate", out _), "Response must contain 'buildDate'");
    }

    [Fact]
    public async Task GetVersion_VersionIsNotEmpty()
    {
        var response = await _client.GetAsync("/version");
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json).RootElement;

        var version = doc.GetProperty("version").GetString();
        Assert.False(string.IsNullOrWhiteSpace(version), "Version must not be empty");
    }

    [Fact]
    public async Task GetVersion_VersionFollowsSemVer()
    {
        var response = await _client.GetAsync("/version");
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json).RootElement;

        var version = doc.GetProperty("version").GetString()!;
        // SemVer: starts with MAJOR.MINOR.PATCH (optionally followed by prerelease/metadata)
        Assert.Matches(@"^\d+\.\d+\.\d+", version);
    }

    [Fact]
    public async Task GetRoot_ReturnsRunningMessage()
    {
        var response = await _client.GetAsync("/");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("running", body, StringComparison.OrdinalIgnoreCase);
    }
}
