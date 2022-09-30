using System.Text.Json;
using MovieCastIdentifier.Models;

namespace MovieCastIdentifier;

public class ImdbApi : IImdbApi
{
    private readonly HttpClient _httpClient;
    private readonly ImdbSettings _settings;

    public ImdbApi(HttpClient httpClient, ImdbSettings settings)
    {
        _httpClient = httpClient;
        _settings = settings;
    }

    public async Task<ImdbResponse> GetCastMember(string name)
    {
        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri(_settings.BaseUrl + $"{name}"),
            Headers =
            {
                { "X-RapidAPI-Key", _settings.ApiKey },
                { "X-RapidAPI-Host", _settings.ApiHost },
            }
        };
        using (var response = await _httpClient.SendAsync(request))
        {
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync();
            var member = JsonSerializer.Deserialize<ImdbResponse>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return member;
        }
    }
}