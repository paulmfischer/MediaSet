namespace API.Data;

public class MediaItem
{
    public int Id { get; set; }
    public string Title { get; set; } = default!;
    public string Barcode { get; set; } = default!;
    
    public Format Format { get; set; } = default!;
}