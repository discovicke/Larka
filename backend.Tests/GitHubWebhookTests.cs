using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace backend.Tests;

public class GitHubWebhookTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private const string _secret = "test-webhook-secret";

    [Fact]
    public async Task Webhook_WithValidSignature_IsAccepted()
    {
        const string body = """
            {
              "repository": { "full_name": "skol-org/labb-1-alice" },
              "check_run": { "status": "completed", "conclusion": "success" }
            }
            """;

        var response = await PostAsync(body, Signature(body));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Webhook_WithInvalidSignature_IsRejected()
    {
        const string body = """{"repository":{"full_name":"skol-org/labb-1-alice"}}""";

        var response = await PostAsync(body, "sha256=deadbeef");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Webhook_WithMalformedJson_ReturnsBadRequest()
    {
        const string body = """{"repository":""";

        var response = await PostAsync(body, Signature(body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<HttpResponseMessage> PostAsync(string body, string signature)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/github")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-GitHub-Event", "check_run");
        request.Headers.Add("X-Hub-Signature-256", signature);
        return await _client.SendAsync(request);
    }

    private static string Signature(string body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secret));
        return "sha256=" + Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
    }
}
