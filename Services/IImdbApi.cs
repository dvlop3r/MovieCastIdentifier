using MovieCastIdentifier.Models;

namespace MovieCastIdentifier;

public interface IImdbApi
{
    Task<Member> GetCastMember(string name);
}