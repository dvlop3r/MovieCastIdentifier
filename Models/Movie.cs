namespace MovieCastIdentifier.Models;

public class Movie
{
    public Guid Id { get; set; }
    public IFormFile File? { get; set; }
}