using System.ComponentModel;

namespace MediaSet.Api.Features.Entities.Models;

public enum IdentifierType
{
    [Description("isbn")]
    Isbn,

    [Description("lccn")]
    Lccn,

    [Description("oclc")]
    Oclc,

    [Description("olid")]
    Olid,

    [Description("upc")]
    Upc,

    [Description("ean")]
    Ean
}
