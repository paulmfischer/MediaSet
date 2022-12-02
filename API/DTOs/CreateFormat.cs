using API.Data;

namespace API.DTOs;

public class CreateFormat
{
    public string Name { get; set; } = default!;
    public MediaType MediaType { get; set; }
}