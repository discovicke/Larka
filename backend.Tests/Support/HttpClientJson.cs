using System.Net.Http.Json;

namespace backend.Tests;

/// <summary>
/// Korta anrop som skickar JSON, kräver 2xx och läser svaret. Vid fel visas status och svarstext.
/// </summary>
public static class HttpClientJson
{
    public static async Task<T> PostJsonAsync<T>(this HttpClient client, string path, object body)
    {
        using var response = await client.PostAsJsonAsync(path, body);
        return await response.ReadJsonAsync<T>();
    }

    public static async Task PostJsonAsync(this HttpClient client, string path, object body)
    {
        using var response = await client.PostAsJsonAsync(path, body);
        await response.EnsureSuccessAsync();
    }

    public static async Task<T> PutJsonAsync<T>(this HttpClient client, string path, object body)
    {
        using var response = await client.PutAsJsonAsync(path, body);
        return await response.ReadJsonAsync<T>();
    }

    public static async Task PutJsonAsync(this HttpClient client, string path, object body)
    {
        using var response = await client.PutAsJsonAsync(path, body);
        await response.EnsureSuccessAsync();
    }

    public static async Task<T> GetJsonAsync<T>(this HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        return await response.ReadJsonAsync<T>();
    }

    public static async Task<T> ReadJsonAsync<T>(this HttpResponseMessage response)
    {
        await response.EnsureSuccessAsync();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    public static async Task EnsureSuccessAsync(this HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        var request = response.RequestMessage;
        throw new HttpRequestException(
            $"{request?.Method} {request?.RequestUri} gav {(int)response.StatusCode} {response.StatusCode}: {body}");
    }
}
