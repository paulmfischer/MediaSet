GiantBomb integration — end-user how-to

This document explains how to obtain a GiantBomb API key and enable GiantBomb-based game lookups in MediaSet.

NOTE: The [Giantbomb API](https://www.giantbomb.com/api/) is no longer up and running at the moment so this will fail if setup. Keeping until a replacement is found or GiantBomb API comes back.

1) Obtain a GiantBomb API key

- Create an account at https://www.giantbomb.com/ and sign in.
- Visit the API page (https://www.giantbomb.com/api/) and request an API key. Copy the API key securely.

2) Configure MediaSet (recommended: edit `docker-compose.prod.yml`)

- Open `docker-compose.prod.yml` and locate the `mediaset-api` service environment section.
- Find the commented GiantBomb entries and uncomment them
- Replace the placeholder value for `GiantBombConfiguration__ApiKey` with your key
  - Optionally you can set the `GIANTBOMB_API_KEY` environment variable in your deployment tooling or `.env` file. Example lines in `docker-compose.prod.yml`:

  ```bash
  # GiantBomb configuration (for game lookup)
  # GiantBombConfiguration__BaseUrl: "https://www.giantbomb.com/api/"
  # GiantBombConfiguration__ApiKey: "[ReplaceThis]"    <-- replace with your API key
  # GiantBombConfiguration__Timeout: "10"
  ```
- After updating `docker-compose.prod.yml` (or your `.env`), restart the stack:

  ```bash
  docker compose -f docker-compose.prod.yml up -d --build
  ```

3) (Optional) Enable barcode-based movie lookup (UPCitemdb)

- GiantBomb provides movie metadata but MediaSet relies on a barcode database to map UPCs to titles. To enable lookup by barcode you must also enable UPCitemdb in the compose file:

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

- After starting MediaSet, perform a game lookup (for example by searching or adding a game record). The API logs should show GiantBomb calls when the game lookup strategy is used.
- Another way is when you do a barcode lookup in the UI from add/edit, it should populate the form with data if we found the barcode.

If lookups fail with authentication errors, double-check that the GiantBomb API key is present in the environment accessible to the `mediaset-api` container.
