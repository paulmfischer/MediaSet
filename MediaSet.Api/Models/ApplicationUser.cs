using AspNetCore.Identity.MongoDbCore.Models;
using MongoDbGenericRepository.Attributes;

namespace MediaSet.Api.Models;

[CollectionName("User")]
public class ApplicationUser : MongoIdentityUser<Guid>
{}