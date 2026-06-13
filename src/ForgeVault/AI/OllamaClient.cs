using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace ForgeVault.AI;

/// <summary>
/// Lightweight client for the local Ollama API.
/// </summary>
public sealed class OllamaClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public OllamaClient(string baseUrl = "http://localhost:11434")
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient = new HttpClient();
    }

    public async Task<IAsyncEnumerable<string>?> GenerateStreamAsync(string model, string prompt, CancellationToken cancellationToken = default)
    {
        var request = new
        {
            model,
            prompt,
            stream = true
        };

        var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/api/generate", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return ReadStreamAsync(response, cancellationToken);
    }

    private static async IAsyncEnumerable<string> ReadStreamAsync(HttpResponseMessage response, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line))
                continue;

            using var doc = JsonDocument.Parse(line);
            if (doc.RootElement.TryGetProperty("response", out var responseElement))
            {
                yield return responseElement.GetString() ?? string.Empty;
            }
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
