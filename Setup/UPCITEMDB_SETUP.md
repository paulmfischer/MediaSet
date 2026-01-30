# UPCItemDb integration — end-user how-to

This document explains how to enable UPCItemDb configuration to allow for UPC lookups. This is needed if you want to lookup Books/Games/Movies by barcode. These entities will first call out to UPCItemDb and get the Title from UPCItemDb and then make a call to their respective metadata service for more information.  This will only explain the configuration to enable for UPCItemDb. To enable Book/Game/Movie integrations, see their respective setup documents.

Book Metadata

1) Configure MediaSet (recommended: edit `docker-compose.prod.yml`)

    - Open `docker-compose.prod.yml` and locate the `mediaset-api` service environment section.
    - Find the commented OpenLibrary entries and uncomment them
    - Replace the placeholder value for `OpenLibraryConfiguration_ContactEmail` with your email. This is mostly used for tracking purposes 
      - Optionally you can set the `MusicBrainz_API_KEY` environment variable in your deployment tooling or `.env` file. Example lines in `docker-compose.prod.yml`:

    ```bash
    # UpcItemDb configuration (for barcode lookup)
    # UpcItemDbConfiguration__BaseUrl: "https://api.upcitemdb.com/"
    # UpcItemDbConfiguration__Timeout: "10"
    ```
    - After updating `docker-compose.prod.yml` (or your `.env`), restart the stack:

    ```bash
    docker compose -f docker-compose.prod.yml up -d --build
    ```

2) Verification for barcode lookup

    - After starting MediaSet, perform a music lookup by barcode from the Add/Edit screen.
      - The API logs should show MusicBrainz calls when the music lookup strategy is used.
      - The Add/Edit form will also be populated with metadata if it finds an album by that barcode.
