using API.Data;

namespace API.DTOs;

public class UpdateFormat
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public MediaType MediaType { get; set; }
}