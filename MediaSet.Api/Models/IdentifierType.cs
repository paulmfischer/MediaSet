using System.ComponentModel;

namespace MediaSet.Api.Models;

public enum IdentifierType
{
    [Description("isbn")]
    Isbn,

    [Description("lccn")]
    Lccn,

    [Description("oclc")]
    Oclc,

    [Description("olid")]
    Olid
}
