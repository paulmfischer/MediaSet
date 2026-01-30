# TMDB integration — end-user how-to

This document explains how to obtain a TMDB bearer token and enable TMDB-based movie lookups in MediaSet. It also explains the additional UPCitemdb requirement for barcode-based movie lookup.

1) Obtain a TMDB bearer token

- Create an account at https://www.themoviedb.org and sign in.
- Go to Settings → API (https://www.themoviedb.org/settings/api).
- Request access and copy your "API Read Access Token" (v4, Bearer). Keep this value secret.

2) Configure MediaSet (recommended: edit `docker-compose.prod.yml`)

- Open `docker-compose.prod.yml` and locate the `mediaset-api` service environment section.
- Find the commented TMDB entries and uncomment them
- Replace the placeholder value for `TmdbConfiguration__BearerToken` with your token
  - Optionally you can set the `TMDB_BEARER_TOKEN` environment variable in your deployment tooling or `.env` file. Example lines in `docker-compose.prod.yml`:

  ```bash
  # TMDB configuration (for movie lookup)
  # TmdbConfiguration__BaseUrl: "https://api.themoviedb.org/3/"
  # TmdbConfiguration__BearerToken: "[ReplaceThis]"    <-- replace with your bearer token
  # TmdbConfiguration__Timeout: "10"
  ```

  - After updating `docker-compose.prod.yml` (or your `.env`), restart the stack:

    ```bash
    docker compose -f docker-compose.prod.yml up -d --build
    ```

3) (Optional) Enable barcode-based movie lookup (UPCitemdb)

  - TMDB provides movie metadata but MediaSet relies on a barcode database to map UPCs to titles. To able lookup by barcode you must also enable UPCitemdb in the compose file:

- In `docker-compose.prod.yml` uncomment or add the UpcItemDb configuration:

  ```bash
  # UpcItemDb configuration (for barcode lookup)
  # UpcItemDbConfiguration__BaseUrl: "https://api.upcitemdb.com/" <-- uncomment
  # UpcItemDbConfiguration__Timeout: "10" <-- uncomment
  ```
- You can also provide a UPCItemDb API key if you get one by adding the following to the same section:

  ```bash
  # UPCITEMDB_API_KEY: "[ReplaceThis]"    <-- replace with your UpcItemDb API key (optional but recommended)
  ```

- Adding the API key is optional, it will just help with rate limiting but is not required for barcode lookup to work.

- If you use the project `.env` approach, set the following in the file (UPCITEMDB_API_KEY again is optional):

  ```bash
  UPCITEMDB_API_KEY=your_upcitemdb_api_key_here
  ```

4) Verification for barcode lookup

- After starting MediaSet, trigger a movie lookup (for example by scanning/adding a movie barcode). The API logs should show TMDB calls and, if applicable, UpcItemDb lookups.
- Another way is when you do a barcode lookup in the UI from add/edit, it should populate the form with data if we found the barcode.

If lookups fail with authentication errors, double-check that the bearer token are present in the environment accessible to the `mediaset-api` container.
