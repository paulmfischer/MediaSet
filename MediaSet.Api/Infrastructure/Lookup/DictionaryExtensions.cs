using System.Text.Json;

namespace MediaSet.Api.Infrastructure.Lookup;

public static class DictionaryExtensions
{
    public static string ExtractStringFromData(this Dictionary<string, object> data, string key)
    {
        if (!data.TryGetValue(key, out var value))
            return string.Empty;

        return value switch
        {
            string str => str ?? string.Empty,
            JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString() ?? string.Empty,
            _ => value?.ToString() ?? string.Empty
        };
    }

    public static List<Author> ExtractAuthorsFromData(this Dictionary<string, object> data)
    {
        var authors = new List<Author>();

        if (data.TryGetValue("authors", out var authorsObj) && authorsObj is JsonElement authorsElement)
        {
            if (authorsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var authorElement in authorsElement.EnumerateArray())
                {
                    if (authorElement.ValueKind == JsonValueKind.Object)
                    {
                        var name = authorElement.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
                        var url = authorElement.TryGetProperty("url", out var urlElement) ? urlElement.GetString() : null;

                        if (!string.IsNullOrEmpty(name))
                        {
                            authors.Add(new Author(name, url ?? ""));
                        }
                    }
                }
            }
        }

        return authors;
    }

    public static int ExtractNumberOfPagesFromData(this Dictionary<string, object> data)
    {
        // Favor 'number_of_pages' over 'pagination' if both are present
        if (data.TryGetValue("number_of_pages", out var numPagesObj))
        {
            return numPagesObj switch
            {
                int pages => pages,
                JsonElement element when element.ValueKind == JsonValueKind.Number => element.GetInt32(),
                JsonElement element when element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), out var parsedFromElement) => parsedFromElement,
                string str when int.TryParse(str, out var parsedFromString) => parsedFromString,
                _ => 0
            };
        }

        if (data.TryGetValue("pagination", out var pagesObj))
        {
            return pagesObj switch
            {
                int pages => pages,
                JsonElement element when element.ValueKind == JsonValueKind.Number => element.GetInt32(),
                JsonElement element when element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), out var parsedFromElement) => parsedFromElement,
                string str when int.TryParse(str, out var parsedFromString) => parsedFromString,
                _ => 0
            };
        }

        return 0;
    }

    public static List<Publisher> ExtractPublishersFromData(this Dictionary<string, object> data)
    {
        var publishers = new List<Publisher>();

        if (data.TryGetValue("publishers", out var publishersObj) && publishersObj is JsonElement publishersElement)
        {
            if (publishersElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var publisherElement in publishersElement.EnumerateArray())
                {
                    if (publisherElement.ValueKind == JsonValueKind.String)
                    {
                        var name = publisherElement.GetString();
                        if (!string.IsNullOrEmpty(name))
                        {
                            publishers.Add(new Publisher(name));
                        }
                    }
                    else if (publisherElement.ValueKind == JsonValueKind.Object)
                    {
                        var name = publisherElement.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
                        if (!string.IsNullOrEmpty(name))
                        {
                            publishers.Add(new Publisher(name));
                        }
                    }
                }
            }
        }

        return publishers;
    }

    public static List<Subject> ExtractSubjectsFromData(this Dictionary<string, object> data)
    {
        var subjects = new List<Subject>();

        if (data.TryGetValue("subjects", out var subjectsObj) && subjectsObj is JsonElement subjectsElement)
        {
            if (subjectsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var subjectElement in subjectsElement.EnumerateArray())
                {
                    if (subjectElement.ValueKind == JsonValueKind.String)
                    {
                        var name = subjectElement.GetString();
                        if (!string.IsNullOrEmpty(name))
                        {
                            subjects.Add(new Subject(name, ""));
                        }
                    }
                    else if (subjectElement.ValueKind == JsonValueKind.Object)
                    {
                        var name = subjectElement.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
                        var url = subjectElement.TryGetProperty("url", out var urlElement) ? urlElement.GetString() : null;

                        if (!string.IsNullOrEmpty(name))
                        {
                            subjects.Add(new Subject(name, url ?? ""));
                        }
                    }
                }
            }
        }

        return subjects;
    }
}
