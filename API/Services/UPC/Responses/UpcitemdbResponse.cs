namespace Services.UPC.Upcitemdb.Responses;

public class UpcitemdbResponse
{
    public string Code { get; set; }
    public int Total { get; set; }
    public int Offset { get; set; }
    public IList<UpcitemdbItem> Items { get; set; }
}

public class UpcitemdbItem
{
    public string Ean { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public string Upc { get; set; }
}