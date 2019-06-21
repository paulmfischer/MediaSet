namespace MediaSet.Shared.ViewModels
{
    public class MediaViewModel
    {
        public string Title { get; set; }
        public string SortTitle { get; set; }
        public string BarCode { get; set; }
        public string ISBN { get; set; }
        public FormatViewModel Format { get; set; }
        public int? FormatId { get; set; }
    }
}
