# MusicBrainz integration — end-user how-to

This document explains how to obtain a MusicBrainz API key and enable MusicBrainz-based music lookups in MediaSet.


1) Configure MediaSet (recommended: edit `docker-compose.prod.yml`)

    - Open `docker-compose.prod.yml` and locate the `mediaset-api` service environment section.
    - Find the commented MusicBrainz entries and uncomment them
    - Replace the placeholder value for `MusicBrainzConfiguration__ApiKey` with your key
      - Optionally you can set the `MusicBrainz_API_KEY` environment variable in your deployment tooling or `.env` file. Example lines in `docker-compose.prod.yml`:

    ```bash
    # MusicBrainz configuration (for music lookup)
    # MusicBrainzConfiguration__BaseUrl: "https://musicbrainz.org/"
    # MusicBrainzConfiguration__ApiKey: "[ReplaceThis]"    <-- replace with your API key
    # MusicBrainzConfiguration__Timeout: "10"
    # MusicBrainzConfiguration__UserAgent: "${MUSICBRAINZ_USER_AGENT:-MediaSet/1.0 (https://github.com/paulmfischer/MediaSet)}"
    ```
    - After updating `docker-compose.prod.yml` (or your `.env`), restart the stack:

    ```bash
    docker compose -f docker-compose.prod.yml up -d --build
    ```

2) Verification for barcode lookup

    - After starting MediaSet, perform a music lookup by barcode from the Add/Edit screen.
      - The API logs should show MusicBrainz calls when the music lookup strategy is used.
      - The Add/Edit form will also be populated with metadata if it finds an album by that barcode.
