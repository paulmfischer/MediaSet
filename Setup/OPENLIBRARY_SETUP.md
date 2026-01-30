# OpenLibrary integration — end-user how-to

This document explains how to enable OpenLibrary configuration to allow for UPC lookups. This is needed if you want to lookup Books by barcode.

1. Configure MediaSet (recommended: edit `docker-compose.prod.yml`)

    - Open `docker-compose.prod.yml` and locate the `mediaset-api` service environment section.
    - Find the commented OpenLibrary entries and uncomment them
    - Replace the placeholder value for `OpenLibraryConfiguration_ContactEmail` with your email. This is as part of the UserAgent when making calls to OpenLibrary so they have a contact for API usage.

      ```bash
      # OpenLibrary configuration (for book lookup)
      # OpenLibraryConfiguration__BaseUrl: "https://openlibrary.org/"
      # OpenLibraryConfiguration__Timeout: "30"
      # OpenLibraryConfiguration__ContactEmail: "[ReplaceThis]"
      ```
    - After updating `docker-compose.prod.yml` (or your `.env`), restart the stack:

      ```bash
      docker compose -f docker-compose.prod.yml up -d --build
      ```

2. Verification for barcode lookup

    - After starting MediaSet, perform a book lookup by barcode from the Add/Edit screen.
      - The API logs should show OpenLibrary calls when the book lookup strategy is used.
      - The Add/Edit form will also be populated with metadata if it finds an book by that barcode.