using MovieCastIdentifier.Models;

namespace MovieCastIdentifier;

public interface IImdbApi
{
    Task<ImdbResponse> GetCastMember(string name);
}