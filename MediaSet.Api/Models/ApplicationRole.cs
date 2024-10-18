using AspNetCore.Identity.MongoDbCore.Models;
using MongoDbGenericRepository.Attributes;

namespace MediaSet.Api.Models;

[CollectionName("Roles")]
public class ApplicationRole : MongoIdentityRole<Guid>
{}