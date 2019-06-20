using MediaSet.Data.Models;
using MediaSet.Data.Repositories;
using MediaSet.Shared.ViewModels;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace MediaSet.Server.Controllers
{
    [Route("api/[controller]")]
    public class MetadataController : Controller
    {
        private IMetadataRepository MetadataRepository { get; set; }

        public MetadataController(IMetadataRepository metadataRepository)
        {
            MetadataRepository = metadataRepository;
        }

        [HttpGet("[action]")]
        public IEnumerable<PublisherViewModel> Publishers()
        {
            var publishers = MetadataRepository.GetAll<Publisher>();
            var viewPublishers = new List<PublisherViewModel>();

            foreach (var pub in publishers)
            {
                viewPublishers.Add(new PublisherViewModel
                {
                    Id = pub.Id,
                    Name = pub.Name
                });
            }

            return viewPublishers;
        }

        [HttpGet("[action]")]
        public IEnumerable<FormatViewModel> Formats()
        {
            var formats = MetadataRepository.GetAll<Format>();
            var viewFormats = new List<FormatViewModel>();

            foreach (var pub in formats)
            {
                viewFormats.Add(new FormatViewModel
                {
                    Id = pub.Id,
                    Name = pub.Name
                });
            }

            return viewFormats;
        }
    }
}
