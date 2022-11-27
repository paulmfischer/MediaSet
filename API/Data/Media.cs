namespace API.Data;

public class MediaItem
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Barcode { get; set; }
    
    public Format Format { get; set; }

    // public Media(int id, string title, string barcode)
    // {
    //     Id = id;
    //     Title = title;
    //     Barcode = barcode;
    // }
}