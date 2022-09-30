namespace MovieCastIdentifier;

public class ResponseItem
{
    public SubItem I { get; set; }
    public string Id { get; set; }
    public string L { get; set; }
    public int Rank { get; set; }
    public string S { get; set; }
}

public class SubItem
{
    public double Height { get; set; }
    public string ImageUrl { get; set; }
    public double Width { get; set; }
}