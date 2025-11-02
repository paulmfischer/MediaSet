# MusicBrainz Setup

This document explains how the MusicBrainz API integration works for music barcode lookup in MediaSet.

## Overview

MusicBrainz is a free, open music encyclopedia that provides comprehensive album information. MediaSet uses the MusicBrainz API to look up music albums by barcode (UPC/EAN) and retrieve:

- Album title
- Artist name
- Release date
- Genres (via tags)
- Label information
- Track count and track list with durations
- Album format (CD, Vinyl, Digital, etc.)
- Total duration

## API Details

### Endpoint Used

**Search by Barcode:**
```
GET https://musicbrainz.org/ws/2/release/?query=barcode:{barcode}&inc=artists+labels+recordings+genres&fmt=json
```

### Authentication

**No API key required!** MusicBrainz is a free service that doesn't require authentication.

### Rate Limiting

MusicBrainz enforces a **1 request per second** rate limit for polite usage. MediaSet implements automatic rate limiting to comply with this:

- Uses a `SemaphoreSlim` to ensure only one request at a time
- Enforces a minimum 1-second delay between requests
- Returns a 503 status code if the rate limit is exceeded (handled gracefully)

### User-Agent Requirement

MusicBrainz **requires** a descriptive User-Agent header that identifies your application and provides contact information. MediaSet configures this in `appsettings.json`:

```json
"MusicBrainzConfiguration": {
  "BaseUrl": "https://musicbrainz.org/",
  "Timeout": 10,
  "UserAgent": "MediaSet/1.0 (https://github.com/paulmfischer/MediaSet)"
}
```

**Important:** If you deploy your own instance of MediaSet, please update the User-Agent to include your own contact information.

## Configuration

### Production (`appsettings.json`)

```json
{
  "MusicBrainzConfiguration": {
    "BaseUrl": "https://musicbrainz.org/",
    "Timeout": 10,
    "UserAgent": "MediaSet/1.0 (https://github.com/paulmfischer/MediaSet)"
  }
}
```

### Development (`appsettings.Development.json`)

```json
{
  "MusicBrainzConfiguration": {
    "BaseUrl": "https://musicbrainz.org/",
    "Timeout": 10,
    "UserAgent": "MediaSet/1.0 (https://github.com/paulmfischer/MediaSet)"
  }
}
```

## How It Works

1. **User enters a barcode** (UPC or EAN) in the music add/edit form
2. **User clicks "Lookup"** button
3. **Frontend sends request** to MediaSet API: `POST /musics/add` or `/musics/{id}/edit` with intent="lookup"
4. **Backend calls MusicBrainz** API with the barcode
5. **MusicBrainz returns** release information (if found)
6. **Backend maps** the response to a `MusicResponse` object
7. **Frontend pre-fills** the form with the retrieved data
8. **User reviews and saves** the music entity

## Example Barcodes for Testing

Here are some real UPC barcodes you can use for testing:

- `093624993629` - Abbey Road by The Beatles (1969)
- `602547202413` - 25 by Adele (2015)
- `602547924742` - Purpose by Justin Bieber (2015)
- `602547393203` - Lemonade by Beyoncé (2016)

## API Response Example

For barcode `093624993629` (Abbey Road), MusicBrainz returns:

```json
{
  "releases": [
    {
      "id": "0c324f3a-dcbb-4e5a-8d8a-f13f2f49c663",
      "title": "Abbey Road",
      "date": "1969-09-26",
      "country": "GB",
      "barcode": "093624993629",
      "artist-credit": [
        {
          "name": "The Beatles",
          "artist": {
            "id": "b10bbbfc-cf9e-42e0-be17-e2c3e1d2600d",
            "name": "The Beatles"
          }
        }
      ],
      "label-info": [
        {
          "label": {
            "id": "3f9a90e3-0c6d-4e96-ad30-98ccf94a1d5d",
            "name": "Apple Records"
          }
        }
      ],
      "media": [
        {
          "format": "CD",
          "track-count": 17,
          "tracks": [
            {
              "number": "1",
              "title": "Come Together",
              "length": 259266
            },
            {
              "number": "2",
              "title": "Something",
              "length": 182933
            }
            // ... more tracks
          ]
        }
      ],
      "tags": [
        {
          "count": 8,
          "name": "rock"
        },
        {
          "count": 3,
          "name": "pop"
        }
      ]
    }
  ]
}
```

## Error Handling

MediaSet handles various error scenarios:

- **No results found**: Returns a 404 response with appropriate message
- **Invalid barcode**: Returns "No music found for barcode {barcode}"
- **Rate limit exceeded**: Waits and retries (503 from MusicBrainz)
- **Network errors**: Logs error and returns null
- **Malformed data**: Handles missing fields gracefully

## Resources

- [MusicBrainz API Documentation](https://musicbrainz.org/doc/MusicBrainz_API)
- [MusicBrainz Web Service](https://musicbrainz.org/doc/Development/XML_Web_Service/Version_2)
- [MusicBrainz Rate Limiting](https://musicbrainz.org/doc/MusicBrainz_API/Rate_Limiting)
- [MusicBrainz Release Search](https://musicbrainz.org/doc/MusicBrainz_API/Search)

## Contributing to MusicBrainz

MusicBrainz is a community-driven project. If you find missing or incorrect data:

1. Create a free account at https://musicbrainz.org/register
2. Search for the release
3. Click "Edit" to suggest corrections
4. Your edits will be reviewed by the community

## Troubleshooting

### "No releases found for barcode"

- Verify the barcode is correct (check the physical album)
- The album may not be in MusicBrainz yet (consider adding it!)
- Some older releases may not have barcode information

### "MusicBrainz rate limit exceeded"

- MediaSet enforces 1 request per second automatically
- If you're making many lookups quickly, you may see delays
- This is intentional to be respectful of MusicBrainz resources

### Missing track information

- Not all releases in MusicBrainz have complete track lists
- Consider contributing the missing information to MusicBrainz

## License

MusicBrainz data is licensed under Creative Commons licenses. Please respect their terms of use when using the API.
