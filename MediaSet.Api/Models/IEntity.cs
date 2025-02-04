namespace MediaSet.Api.Models;

public interface IEntity
{
  public string? Id { get; set; }
  public string Title { get; set; }
  public string Format { get; set; }
}