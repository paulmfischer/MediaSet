# IGDB integration — end-user how-to

This document explains how to obtain IGDB credentials and enable IGDB-based game lookups in MediaSet. It also explains the additional UPCitemdb requirement for barcode-based game lookup.

Game data provided by [IGDB](https://www.igdb.com).

1) Obtain IGDB credentials

    - IGDB access requires a Twitch Developer account. Create an application at https://dev.twitch.tv/console. Note: Twitch requires two-factor authentication (2FA) to be enabled on your account before you can register an application.
    - Click **Register Your Application**, choose a name, set the OAuth Redirect URL to `http://localhost`, and select **Application Integration** as the category.
    - After creating the app, click **Manage** to retrieve your **Client ID** and generate a **Client Secret**. Keep both values secret.

2) Configure MediaSet (recommended: edit `docker-compose.prod.yml`)

    - Open `docker-compose.prod.yml` and locate the `mediaset-api` service environment section.
    - Find the commented IGDB entries and uncomment them, replacing the placeholder values:

    ```bash
    # IGDB configuration (for game lookup)
    # IgdbConfiguration__ClientId: "[ReplaceThis]"        <-- replace with your Twitch Client ID
    # IgdbConfiguration__ClientSecret: "[ReplaceThis]"    <-- replace with your Twitch Client Secret
    # IgdbConfiguration__BaseUrl: "https://api.igdb.com/v4/"
    # IgdbConfiguration__TokenUrl: "https://id.twitch.tv/oauth2/token"
    # IgdbConfiguration__Timeout: "30"
    ```

    - After updating `docker-compose.prod.yml` (or your `.env`), restart the stack:

    ```bash
    docker compose -f docker-compose.prod.yml up -d --build
    ```

3) Enable barcode-based game lookup (UPCitemdb — required)

    - IGDB provides game metadata but MediaSet relies on a barcode database to map UPCs to titles. To enable lookup by barcode you must also enable UPCitemdb in the compose file:

    ```bash
    # UpcItemDb configuration (for barcode lookup)
    # UpcItemDbConfiguration__BaseUrl: "https://api.upcitemdb.com/" <-- uncomment
    # UpcItemDbConfiguration__Timeout: "10" <-- uncomment
    ```

    - You can also provide a UPCItemDb API key if you get one by adding:

    ```bash
    # UPCITEMDB_API_KEY: "[ReplaceThis]"    <-- replace with your UpcItemDb API key (optional but recommended)
    ```

4) Verification for barcode lookup

    - After starting MediaSet, perform a game lookup by barcode from the Add/Edit screen.
      - The API logs should show results from IGDB when the game lookup strategy is used.
      - The Add/Edit form will also be populated with metadata if it finds a game by that barcode.
    - If lookups fail with authentication errors, double-check that both `IgdbConfiguration__ClientId` and `IgdbConfiguration__ClientSecret` are set correctly in the environment accessible to the `mediaset-api` container.
