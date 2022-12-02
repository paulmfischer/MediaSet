using Microsoft.AspNetCore.Mvc;
using API.Data;
using API.DTOs;

namespace API.Controllers;

[ApiController]
[Route("[controller]")]
public class MetadataController : ControllerBase
{
    private readonly MetadataService metadataService;

    public MetadataController(MetadataService _metadataService)
    {
        metadataService = _metadataService;
    }

    [HttpGet("Format")]
    public async Task<ActionResult<List<Format>>> GetFormats()
    {
        return await metadataService.GetAll<Format>();
    }

    [HttpPost("Format")]
    public async Task<ActionResult<Format>> CreateFormat(CreateFormat format)
    {
        var newFormat = new Format
        {
            MediaType = format.MediaType,
            Name = format.Name,
        };

        return await metadataService.Create<Format>(newFormat);
    }

    [HttpPut("Format")]
    public async Task<ActionResult<Format>> UpdateFormat(UpdateFormat format)
    {
        var dbFormat = metadataService.GetById<Format>(format.Id);

        if (dbFormat == null)
        {
            return NotFound();
        }
        
        dbFormat.MediaType = format.MediaType;
        dbFormat.Name = format.Name;

        return await metadataService.Update<Format>(dbFormat);
    }
    
    [HttpGet("Genre")]
    public async Task<ActionResult<List<Genre>>> GetGenres()
    {
        return await metadataService.GetAll<Genre>();
    }

    [HttpPost("Genre")]
    public async Task<ActionResult<Genre>> CreateGenre(CreateMetadata genre)
    {
        var newGenre = new Genre 
        {
            Name = genre.Name,
        };

        return await metadataService.Create<Genre>(newGenre);
    }

    [HttpPut("Genre")]
    public async Task<ActionResult<Genre>> UpdateGenre(UpdateMetadata genre)
    {
        var dbGenre = metadataService.GetById<Genre>(genre.Id);

        if (dbGenre == null)
        {
            return NotFound();
        }

        dbGenre.Name = genre.Name;

        return await metadataService.Update<Genre>(dbGenre);
    }

    [HttpGet("Studio")]
    public async Task<ActionResult<List<Studio>>> GetStudios()
    {
        return await metadataService.GetAll<Studio>();
    }

    [HttpPost("Studio")]
    public async Task<ActionResult<Studio>> CreateStudio(CreateMetadata studio)
    {
        var newStudio = new Studio
        {
            Name = studio.Name,
        };

        return await metadataService.Create<Studio>(newStudio);
    }

    [HttpPut("Studio")]
    public async Task<ActionResult<Studio>> UpdateStudio(UpdateMetadata studio)
    {
        var dbStudio = metadataService.GetById<Studio>(studio.Id);

        if (dbStudio == null)
        {
            return NotFound();
        }

        dbStudio.Name = studio.Name;

        return await metadataService.Update<Studio>(dbStudio);
    }
}