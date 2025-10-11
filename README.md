## MediaSet Library Management

### Screenshots of application
Home screen:

![Home screen with library information](assets/screenshots/Home-page.png)

Books home screen:

![Books index list page](assets/screenshots/Books-index.png)

Books home screen filtered:

![Books index list page filtered](assets/screenshots/Books-index-filter.png)

Books home screen mobile friendly:

![Books index list mobile view](assets/screenshots/Books-index-mobile.png)

Books detail page:

![Books detail page](assets/screenshots/Books-detail.png)

## Development

### 🐳 Containerized Development (Recommended)

**New developers don't need to install .NET, Node.js, or MongoDB!** Everything runs in Docker containers with full hot-reload support.

#### Quick Start
```bash
# Clone the repository
git clone https://github.com/paulmfischer/MediaSet.git
cd MediaSet

# Start the development environment
./dev.sh start

# Access the applications:
# Frontend: http://localhost:3000
# API: http://localhost:5000 
# MongoDB: mongodb://localhost:27017
```

For complete setup instructions, debugging, and troubleshooting, see **[DEVELOPMENT.md](DEVELOPMENT.md)**.

### 📖 Traditional Development

If you prefer to install dependencies locally:

**Prerequisites:**
- .NET 8.0 SDK
- Node.js 20+
- MongoDB

**Setup:**
1. Start MongoDB locally
2. Start the API: Press `F5` in VS Code or `dotnet run` in `MediaSet.Api/`
3. Start the frontend: `npm run dev` in `MediaSet.Remix/`
4. Access: Frontend at http://localhost:3000, API at http://localhost:5000