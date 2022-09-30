using MovieCastIdentifier.Models;

namespace MovieCastIdentifier;

public class ImdbApi : IImdbApi
{
    private readonly HttpClient _httpClient;

    public ImdbApi(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<Member> GetCastMember(string name)
    {
        var response = await _httpClient.GetAsync($"/?t={title}&apikey=1a2b3c4d");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var member = JsonConvert.DeserializeObject<Member>(content);
        return member;
    }
}