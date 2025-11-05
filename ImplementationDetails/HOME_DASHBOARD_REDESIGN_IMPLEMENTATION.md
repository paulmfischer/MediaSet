# Home Dashboard Redesign Implementation Plan

## Overview

The current home page of MediaSet displays statistics in a basic text format with simple labels and numbers. This implementation will redesign the home dashboard to present statistics in a more visually appealing and informative way using modern UI components, charts, and graphics.

The redesigned dashboard will:
- Display statistics for all entity types (Books, Movies, Games, Music)
- Use visual components like cards, charts, and lists to organize information
- Provide a more engaging and professional user experience
- Leverage existing Tailwind CSS styling and Lucide icons
- Be fully responsive for mobile and desktop views

## Related Existing Functionality

### Backend Components

**Stats API** (`MediaSet.Api/Stats/StatsApi.cs`)
- Provides GET `/stats` endpoint
- Returns aggregated statistics for all media types
- Currently returns stats for Books, Movies, and Games only

**Stats Service** (`MediaSet.Api/Services/StatsService.cs`)
- `GetMediaStatsAsync()` method fetches and calculates statistics
- Includes caching for performance
- Currently calculates stats for Books, Movies, and Games
- **Missing**: Music statistics are not currently calculated

**Stats Models** (`MediaSet.Api/Models/Stats.cs`)
- `Stats` record contains `BookStats`, `MovieStats`, and `GameStats`
- Each entity stats record includes:
  - Total count
  - Format counts and lists
  - Entity-specific metrics (e.g., unique authors for books, platforms for games)
- **Missing**: `MusicStats` record

**Entity Services**
- All entity types (Book, Movie, Game, Music) have registered services in `Program.cs`
- Music entity service exists: `IEntityService<Music>`

### Frontend Components

**Current Home Page** (`MediaSet.Remix/app/routes/_index.tsx`)
- Simple two-column layout
- Displays book and movie stats in plain text format
- Uses `getStats()` from `stats-data.ts`

**Stats Data Function** (`MediaSet.Remix/app/stats-data.ts`)
- Fetches stats from `/stats` endpoint
- Type definitions for `Stats`, `BookStats`, `MovieStats`
- **Missing**: Type definitions for `GameStats` and `MusicStats`

**Type Definitions** (`MediaSet.Remix/app/models.ts`)
- Defines entity types: `Book`, `Movie`, `Game`, `Music`
- Includes entity enum

**UI Components**
- `badge.tsx`: Reusable badge component
- `spinner.tsx`: Loading indicator
- Various form components with consistent styling
- Uses `lucide-react` for icons (LibraryBig, Clapperboard, Gamepad2, Music, etc.)

**Layout** (`MediaSet.Remix/app/root.tsx`)
- Navigation with entity-specific icons
- Dark theme with Tailwind CSS
- Responsive design with mobile menu

### Infrastructure/Configuration

**Styling**: Tailwind CSS with dark theme (zinc-700, zinc-800, zinc-900 palette)
**Icons**: lucide-react library already available
**Charting**: Will use CSS-based visualizations for SSR compatibility (no JavaScript charting library needed)

## Requirements

### Functional Requirements

1. Display comprehensive statistics for all four entity types: Books, Movies, Games, and Music
2. Present statistics using visual components:
   - Stat cards with icons for quick overview metrics
   - CSS-based horizontal bars for format/platform distribution
   - Badge lists for detailed breakdowns
3. Maintain responsive design that works on mobile and desktop
4. Use consistent dark theme styling matching the rest of the application
5. Show loading states while fetching data
6. Handle empty states gracefully (when no media items exist)
7. Ensure full SSR compatibility (no client-side only components)

### Non-Functional Requirements

- **Performance**: Leverage existing stats caching to ensure fast page loads
- **Accessibility**: Use semantic HTML, proper ARIA labels, and keyboard navigation
- **Visual Consistency**: Match existing UI patterns and color scheme
- **Maintainability**: Use reusable components for stats cards and charts
- **Responsiveness**: Mobile-first design that adapts to all screen sizes

## Proposed Changes

### Backend Changes (MediaSet.Api)

#### Modified Models

**Stats.cs** (`MediaSet.Api/Models/Stats.cs`)
- Add `MusicStats` property to main `Stats` record
- Create new `MusicStats` record with:
  - `int Total` - total number of music albums
  - `int TotalFormats` - number of distinct formats
  - `IEnumerable<string> Formats` - list of format names
  - `int UniqueArtists` - number of distinct artists
  - `int TotalTracks` - total number of tracks across all albums

```csharp
public record Stats(
  BookStats BookStats, 
  MovieStats MovieStats, 
  GameStats GameStats,
  MusicStats MusicStats  // NEW
);

// NEW
public record MusicStats(
  int Total,
  int TotalFormats,
  IEnumerable<string> Formats,
  int UniqueArtists,
  int TotalTracks
);
```

#### Modified Services

**StatsService.cs** (`MediaSet.Api/Services/StatsService.cs`)
- Inject `IEntityService<Music>` in constructor
- Add music statistics calculation in `GetMediaStatsAsync()` method
- Calculate:
  - Total music count
  - Distinct formats
  - Unique artists (from `Artist` field)
  - Total tracks (from `Tracks` field or `DiscList` count)

Changes:
```csharp
private readonly IEntityService<Music> musicService;

public StatsService(
    IEntityService<Book> _bookService,
    IEntityService<Movie> _movieService,
    IEntityService<Game> _gameService,
    IEntityService<Music> _musicService,  // NEW
    ICacheService _cacheService,
    IOptions<CacheSettings> _cacheSettings,
    ILogger<StatsService> _logger)
{
    // ... existing assignments
    musicService = _musicService;  // NEW
}

public async Task<Stats> GetMediaStatsAsync(CancellationToken cancellationToken = default)
{
    // ... existing cache check
    
    var bookTask = bookService.GetListAsync(cancellationToken);
    var movieTask = movieService.GetListAsync(cancellationToken);
    var gameTask = gameService.GetListAsync(cancellationToken);
    var musicTask = musicService.GetListAsync(cancellationToken);  // NEW
    await Task.WhenAll(bookTask, movieTask, gameTask, musicTask);  // MODIFIED
    
    var books = bookTask.Result;
    var movies = movieTask.Result;
    var games = gameTask.Result;
    var musics = musicTask.Result;  // NEW
    
    // ... existing calculations for books, movies, games
    
    // NEW: Music stats calculation
    var musicFormats = musics
        .Where(music => !string.IsNullOrWhiteSpace(music.Format))
        .Select(music => music.Format.Trim())
        .Distinct();
    
    var musicStats = new MusicStats(
        musics.Count(),
        musicFormats.Count(),
        musicFormats,
        musics.Where(music => !string.IsNullOrWhiteSpace(music.Artist))
            .Select(music => music.Artist.Trim())
            .Distinct()
            .Count(),
        musics.Where(music => music.Tracks.HasValue)
            .Select(music => music.Tracks ?? 0)
            .Sum()
    );
    
    var stats = new Stats(bookStats, movieStats, gameStats, musicStats);  // MODIFIED
    
    // ... existing caching
}
```

#### Modified API Endpoints

**StatsApi.cs** (`MediaSet.Api/Stats/StatsApi.cs`)
- Update logging to include music stats
- Add music metrics to log output

```csharp
logger.LogInformation(
    "Stats: books total={bookTotal}, formats={bookFormats}, pages={bookPages}; " +
    "movies total={movieTotal}, formats={movieFormats}, tvSeries={tvSeries}; " +
    "games total={gameTotal}, formats={gameFormats}, platforms={gamePlatforms}; " +
    "music total={musicTotal}, formats={musicFormats}, tracks={musicTracks}",  // NEW
    stats.BookStats.Total,
    stats.BookStats.TotalFormats,
    stats.BookStats.TotalPages,
    stats.MovieStats.Total,
    stats.MovieStats.TotalFormats,
    stats.MovieStats.TotalTvSeries,
    stats.GameStats.Total,
    stats.GameStats.TotalFormats,
    stats.GameStats.TotalPlatforms,
    stats.MusicStats.Total,      // NEW
    stats.MusicStats.TotalFormats,  // NEW
    stats.MusicStats.TotalTracks    // NEW
);
```

### Frontend Changes (MediaSet.Remix)

#### Dependencies

**No new dependencies needed!** All visualizations will use CSS/Tailwind.

#### Modified Data Functions

**stats-data.ts** (`MediaSet.Remix/app/stats-data.ts`)
- Add `GameStats` and `MusicStats` type definitions
- Update `Stats` type to include all four entity types

```typescript
type Stats = {
  bookStats: BookStats;
  movieStats: MovieStats;
  gameStats: GameStats;   // NEW
  musicStats: MusicStats; // NEW
};

// NEW
type GameStats = {
  total: number;
  totalFormats: number;
  formats: string[];
  totalPlatforms: number;
  platforms: string[];
};

// NEW
type MusicStats = {
  total: number;
  totalFormats: number;
  formats: string[];
  uniqueArtists: number;
  totalTracks: number;
};
```

#### New Components

**StatCard.tsx** (`MediaSet.Remix/app/components/StatCard.tsx`)
- Reusable card component for displaying individual statistics
- Uses CSS for all styling and effects
- Props:
  - `title: string` - Card title
  - `value: string | number` - Main stat value
  - `icon: React.ReactNode` - Lucide icon component
  - `subtitle?: string` - Optional subtitle/description
  - `colorClass?: string` - Optional Tailwind color class for icon background

```tsx
interface StatCardProps {
  title: string;
  value: string | number;
  icon: React.ReactNode;
  subtitle?: string;
  colorClass?: string;
}

export default function StatCard({ 
  title, 
  value, 
  icon, 
  subtitle, 
  colorClass = "bg-blue-500/10" 
}: StatCardProps) {
  return (
    <div className="bg-zinc-800 rounded-lg p-6 border border-zinc-700 hover:border-zinc-600 transition-colors">
      <div className="flex items-start justify-between">
        <div className="flex-1">
          <p className="text-zinc-400 text-sm font-medium mb-1">{title}</p>
          <p className="text-3xl font-bold text-zinc-100 mb-1">{value}</p>
          {subtitle && (
            <p className="text-zinc-500 text-xs">{subtitle}</p>
          )}
        </div>
        <div className={`p-3 rounded-lg ${colorClass}`}>
          {icon}
        </div>
      </div>
    </div>
  );
}
```

**FormatChart.tsx** (`MediaSet.Remix/app/components/FormatChart.tsx`)
- CSS and SVG-based horizontal bar chart component for displaying format distribution
- Fully SSR-compatible (renders as inline SVG)
- Uses pure CSS for styling and SVG for graphics
- Props:
  - `data: Array<{name: string, count: number}>` - Format data with counts
  - `title: string` - Chart title
  - `color?: string` - Bar color (hex value for inline styles)
  - `maxValue?: number` - Optional max value for scaling bars

```tsx
interface FormatChartProps {
  data: Array<{ name: string; count: number }>;
  title: string;
  color?: string;
  maxValue?: number;
}

export default function FormatChart({ 
  data, 
  title, 
  color = "#3b82f6",
  maxValue 
}: FormatChartProps) {
  // Calculate max value if not provided
  const max = maxValue || Math.max(...data.map(d => d.count), 1);
  const barHeight = 24;
  const barSpacing = 8;
  const chartHeight = data.length * (barHeight + barSpacing);
  
  return (
    <div className="bg-zinc-800 rounded-lg p-6 border border-zinc-700">
      <h3 className="text-lg font-semibold text-zinc-100 mb-4">{title}</h3>
      <div className="space-y-3">
        {data.map((item, index) => {
          const percentage = (item.count / max) * 100;
          return (
            <div key={item.name}>
              <div className="flex justify-between items-center mb-1">
                <span className="text-sm text-zinc-300">{item.name}</span>
                <span className="text-sm text-zinc-400">{item.count}</span>
              </div>
              <svg width="100%" height={barHeight} className="rounded-full overflow-hidden">
                {/* Background bar */}
                <rect 
                  width="100%" 
                  height={barHeight} 
                  fill="#3f3f46" 
                  rx="12"
                />
                {/* Foreground bar */}
                <rect 
                  width={`${percentage}%`} 
                  height={barHeight} 
                  fill={color}
                  rx="12"
                  className="transition-all duration-300"
                />
              </svg>
            </div>
          );
        })}
      </div>
    </div>
  );
}
```

**Note**: This component uses inline SVG elements for full SSR compatibility. If we need actual counts per format (not just unique format names), we would need to enhance the backend Stats API to return format counts. For the initial implementation, we'll display formats as badges instead of bars.

**EntitySection.tsx** (`MediaSet.Remix/app/components/EntitySection.tsx`)
- Reusable section component for each entity type
- Displays overview stats and format breakdown
- Fully SSR-compatible
- Props:
  - `title: string` - Section title
  - `icon: React.ReactNode` - Entity icon
  - `stats: object` - Entity-specific stats
  - `colorClass?: string` - Tailwind color class for theming

```tsx
import StatCard from './StatCard';

interface EntitySectionProps {
  title: string;
  icon: React.ReactNode;
  totalLabel: string;
  total: number;
  stats: Array<{ label: string; value: string | number }>;
  formats: string[];
  colorClass?: string;
  badgeColorClass?: string;
}

export default function EntitySection({
  title,
  icon,
  totalLabel,
  total,
  stats,
  formats,
  colorClass = "bg-blue-500/10",
  badgeColorClass = "bg-blue-500/10 text-blue-400 border-blue-500/20"
}: EntitySectionProps) {
  return (
    <section className="space-y-4">
      <div className="flex items-center gap-3 mb-4">
        {icon}
        <h2 className="text-2xl font-bold text-zinc-100">{title}</h2>
      </div>
      
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <StatCard
          title={totalLabel}
          value={total}
          icon={icon}
          colorClass={colorClass}
        />
        {stats.map((stat, idx) => (
          <StatCard
            key={idx}
            title={stat.label}
            value={stat.value}
            icon={icon}
            colorClass={colorClass}
          />
        ))}
      </div>
      
      {formats.length > 0 && (
        <div className="bg-zinc-800 rounded-lg p-6 border border-zinc-700">
          <h3 className="text-lg font-semibold text-zinc-100 mb-3">{title} Formats</h3>
          <div className="flex flex-wrap gap-2">
            {formats.map((format) => (
              <span
                key={format}
                className={`px-3 py-1 rounded-full text-sm border ${badgeColorClass}`}
              >
                {format}
              </span>
            ))}
          </div>
        </div>
      )}
    </section>
  );
}
```

#### Modified Routes

**_index.tsx** (`MediaSet.Remix/app/routes/_index.tsx`)
- Complete redesign of home page layout
- Use new components for visual presentation
- Display all four entity types
- Responsive grid layout
- Add loading and empty states

```tsx
import { json, type MetaFunction } from "@remix-run/node";
import { useLoaderData } from "@remix-run/react";
import { getStats } from "~/stats-data";
import { LibraryBig, Clapperboard, Gamepad2, Music, TrendingUp } from "lucide-react";
import StatCard from "~/components/StatCard";

export const meta: MetaFunction = () => {
  return [
    { title: "MediaSet - Dashboard" },
    { name: "description", content: "Your media collection dashboard" },
  ];
};

export const loader = async () => {
  const stats = await getStats();
  return json({ stats });
};

export default function Index() {
  const { stats } = useLoaderData<typeof loader>();
  
  // Calculate total items across all media types
  const totalItems = 
    stats.bookStats.total + 
    stats.movieStats.total + 
    stats.gameStats.total + 
    stats.musicStats.total;

  return (
    <div className="space-y-8">
      {/* Hero Section */}
      <section className="text-center py-8">
        <h1 className="text-4xl font-bold text-zinc-100 mb-3">
          Welcome to MediaSet
        </h1>
        <p className="text-zinc-400 text-lg">
          Your personal media collection manager
        </p>
      </section>

      {/* Overview Cards */}
      <section>
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-4">
          <StatCard
            title="Total Items"
            value={totalItems}
            icon={<TrendingUp className="w-6 h-6 text-purple-400" />}
            colorClass="bg-purple-500/10"
          />
          <StatCard
            title="Books"
            value={stats.bookStats.total}
            icon={<LibraryBig className="w-6 h-6 text-blue-400" />}
            colorClass="bg-blue-500/10"
          />
          <StatCard
            title="Movies"
            value={stats.movieStats.total}
            icon={<Clapperboard className="w-6 h-6 text-red-400" />}
            colorClass="bg-red-500/10"
          />
          <StatCard
            title="Games"
            value={stats.gameStats.total}
            icon={<Gamepad2 className="w-6 h-6 text-green-400" />}
            colorClass="bg-green-500/10"
          />
          <StatCard
            title="Music"
            value={stats.musicStats.total}
            icon={<Music className="w-6 h-6 text-yellow-400" />}
            colorClass="bg-yellow-500/10"
          />
        </div>
      </section>

      {/* Books Section */}
      {stats.bookStats.total > 0 && (
        <section className="space-y-4">
          <div className="flex items-center gap-3">
            <LibraryBig className="w-8 h-8 text-blue-400" />
            <h2 className="text-2xl font-bold text-zinc-100">Books</h2>
          </div>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
            <StatCard
              title="Total Books"
              value={stats.bookStats.total}
              icon={<LibraryBig className="w-5 h-5 text-blue-400" />}
              colorClass="bg-blue-500/10"
            />
            <StatCard
              title="Total Pages"
              value={stats.bookStats.totalPages.toLocaleString()}
              icon={<LibraryBig className="w-5 h-5 text-blue-400" />}
              colorClass="bg-blue-500/10"
            />
            <StatCard
              title="Unique Authors"
              value={stats.bookStats.uniqueAuthors}
              icon={<LibraryBig className="w-5 h-5 text-blue-400" />}
              colorClass="bg-blue-500/10"
            />
            <StatCard
              title="Formats"
              value={stats.bookStats.totalFormats}
              icon={<LibraryBig className="w-5 h-5 text-blue-400" />}
              colorClass="bg-blue-500/10"
            />
          </div>
          {stats.bookStats.formats.length > 0 && (
            <div className="bg-zinc-800 rounded-lg p-6 border border-zinc-700">
              <h3 className="text-lg font-semibold text-zinc-100 mb-3">Book Formats</h3>
              <div className="flex flex-wrap gap-2">
                {stats.bookStats.formats.map((format) => (
                  <span
                    key={format}
                    className="px-3 py-1 bg-blue-500/10 text-blue-400 rounded-full text-sm border border-blue-500/20"
                  >
                    {format}
                  </span>
                ))}
              </div>
            </div>
          )}
        </section>
      )}

      {/* Movies Section */}
      {stats.movieStats.total > 0 && (
        <section className="space-y-4">
          <div className="flex items-center gap-3">
            <Clapperboard className="w-8 h-8 text-red-400" />
            <h2 className="text-2xl font-bold text-zinc-100">Movies</h2>
          </div>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
            <StatCard
              title="Total Movies"
              value={stats.movieStats.total}
              icon={<Clapperboard className="w-5 h-5 text-red-400" />}
              colorClass="bg-red-500/10"
            />
            <StatCard
              title="TV Series"
              value={stats.movieStats.totalTvSeries}
              icon={<Clapperboard className="w-5 h-5 text-red-400" />}
              colorClass="bg-red-500/10"
            />
            <StatCard
              title="Formats"
              value={stats.movieStats.totalFormats}
              icon={<Clapperboard className="w-5 h-5 text-red-400" />}
              colorClass="bg-red-500/10"
            />
          </div>
          {stats.movieStats.formats.length > 0 && (
            <div className="bg-zinc-800 rounded-lg p-6 border border-zinc-700">
              <h3 className="text-lg font-semibold text-zinc-100 mb-3">Movie Formats</h3>
              <div className="flex flex-wrap gap-2">
                {stats.movieStats.formats.map((format) => (
                  <span
                    key={format}
                    className="px-3 py-1 bg-red-500/10 text-red-400 rounded-full text-sm border border-red-500/20"
                  >
                    {format}
                  </span>
                ))}
              </div>
            </div>
          )}
        </section>
      )}

      {/* Games Section */}
      {stats.gameStats.total > 0 && (
        <section className="space-y-4">
          <div className="flex items-center gap-3">
            <Gamepad2 className="w-8 h-8 text-green-400" />
            <h2 className="text-2xl font-bold text-zinc-100">Games</h2>
          </div>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
            <StatCard
              title="Total Games"
              value={stats.gameStats.total}
              icon={<Gamepad2 className="w-5 h-5 text-green-400" />}
              colorClass="bg-green-500/10"
            />
            <StatCard
              title="Platforms"
              value={stats.gameStats.totalPlatforms}
              icon={<Gamepad2 className="w-5 h-5 text-green-400" />}
              colorClass="bg-green-500/10"
            />
            <StatCard
              title="Formats"
              value={stats.gameStats.totalFormats}
              icon={<Gamepad2 className="w-5 h-5 text-green-400" />}
              colorClass="bg-green-500/10"
            />
          </div>
          {stats.gameStats.platforms.length > 0 && (
            <div className="bg-zinc-800 rounded-lg p-6 border border-zinc-700">
              <h3 className="text-lg font-semibold text-zinc-100 mb-3">Game Platforms</h3>
              <div className="flex flex-wrap gap-2">
                {stats.gameStats.platforms.map((platform) => (
                  <span
                    key={platform}
                    className="px-3 py-1 bg-green-500/10 text-green-400 rounded-full text-sm border border-green-500/20"
                  >
                    {platform}
                  </span>
                ))}
              </div>
            </div>
          )}
        </section>
      )}

      {/* Music Section */}
      {stats.musicStats.total > 0 && (
        <section className="space-y-4">
          <div className="flex items-center gap-3">
            <Music className="w-8 h-8 text-yellow-400" />
            <h2 className="text-2xl font-bold text-zinc-100">Music</h2>
          </div>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
            <StatCard
              title="Total Albums"
              value={stats.musicStats.total}
              icon={<Music className="w-5 h-5 text-yellow-400" />}
              colorClass="bg-yellow-500/10"
            />
            <StatCard
              title="Total Tracks"
              value={stats.musicStats.totalTracks}
              icon={<Music className="w-5 h-5 text-yellow-400" />}
              colorClass="bg-yellow-500/10"
            />
            <StatCard
              title="Unique Artists"
              value={stats.musicStats.uniqueArtists}
              icon={<Music className="w-5 h-5 text-yellow-400" />}
              colorClass="bg-yellow-500/10"
            />
            <StatCard
              title="Formats"
              value={stats.musicStats.totalFormats}
              icon={<Music className="w-5 h-5 text-yellow-400" />}
              colorClass="bg-yellow-500/10"
            />
          </div>
          {stats.musicStats.formats.length > 0 && (
            <div className="bg-zinc-800 rounded-lg p-6 border border-zinc-700">
              <h3 className="text-lg font-semibold text-zinc-100 mb-3">Music Formats</h3>
              <div className="flex flex-wrap gap-2">
                {stats.musicStats.formats.map((format) => (
                  <span
                    key={format}
                    className="px-3 py-1 bg-yellow-500/10 text-yellow-400 rounded-full text-sm border border-yellow-500/20"
                  >
                    {format}
                  </span>
                ))}
              </div>
            </div>
          )}
        </section>
      )}

      {/* Empty State */}
      {totalItems === 0 && (
        <section className="text-center py-16">
          <div className="inline-flex p-6 bg-zinc-800 rounded-full mb-4">
            <TrendingUp className="w-12 h-12 text-zinc-600" />
          </div>
          <h2 className="text-2xl font-bold text-zinc-100 mb-2">
            No Media Items Yet
          </h2>
          <p className="text-zinc-400 mb-6">
            Start building your collection by adding books, movies, games, or music.
          </p>
          <div className="flex gap-4 justify-center">
            <a href="/books" className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors">
              Add Books
            </a>
            <a href="/movies" className="px-4 py-2 bg-red-600 text-white rounded-lg hover:bg-red-700 transition-colors">
              Add Movies
            </a>
            <a href="/games" className="px-4 py-2 bg-green-600 text-white rounded-lg hover:bg-green-700 transition-colors">
              Add Games
            </a>
            <a href="/musics" className="px-4 py-2 bg-yellow-600 text-white rounded-lg hover:bg-yellow-700 transition-colors">
              Add Music
            </a>
          </div>
        </section>
      )}
    </div>
  );
}
```

### Testing Changes

#### Backend Tests (MediaSet.Api.Tests)

**StatsServiceTests.cs** (`MediaSet.Api.Tests/Services/StatsServiceTests.cs`)
- Add tests for music statistics calculation
- Test scenarios:
  - `GetMediaStatsAsync_WithMusicItems_ReturnsCorrectMusicStats`
  - `GetMediaStatsAsync_WithMusicFormats_CountsDistinctFormats`
  - `GetMediaStatsAsync_WithMusicArtists_CountsUniqueArtists`
  - `GetMediaStatsAsync_WithMusicTracks_SumsTotalTracks`
  - `GetMediaStatsAsync_WithNoMusic_ReturnsZeroMusicStats`

**StatsApiTests.cs** (`MediaSet.Api.Tests/Stats/StatsApiTests.cs`)
- Add test to verify music stats are included in API response
- Test scenario:
  - `GetStats_ReturnsStatsWithAllEntityTypes_IncludingMusic`

## Implementation Steps

1. **Backend: Add Music Statistics Support**
   - Modify `Stats.cs` to add `MusicStats` record
   - Update `StatsService.cs` to inject music service and calculate music statistics
   - Update `StatsApi.cs` logging to include music stats
   - Run and verify API returns music stats

2. **Backend: Add Tests for Music Statistics**
   - Add test cases in `StatsServiceTests.cs`
   - Add test case in `StatsApiTests.cs`
   - Verify all tests pass

3. **Frontend: Update Stats Data Types**
   - Modify `stats-data.ts` to add `GameStats` and `MusicStats` types
   - Update `Stats` type to include all entity types
   - Verify TypeScript compilation succeeds

4. **Frontend: Create Reusable Components**
   - Create `StatCard.tsx` component
   - Create `FormatChart.tsx` component (optional - for future use if format counts are added)
   - Create `EntitySection.tsx` component (if using consolidated approach)
   - Verify components render correctly in isolation

5. **Frontend: Redesign Home Page**
   - Completely rewrite `_index.tsx` with new layout
   - Implement hero section
   - Implement overview cards
   - Implement entity-specific sections for Books, Movies, Games, Music
   - Implement empty state
   - Test responsive behavior on mobile and desktop

6. **Integration Testing**
   - Test full stack with real data
   - Verify caching works correctly
   - Test performance with large datasets
   - Test responsive design on various screen sizes
   - Test accessibility with keyboard navigation and screen readers
   - Verify SSR works correctly (view page source to confirm markup is present)

7. **Documentation Updates**
   - Update README if needed
   - Add comments to complex UI logic
   - Document new component props

8. **Final Review and Polish**
    - Review visual consistency across all sections
    - Adjust spacing, colors, and typography as needed
    - Optimize performance if necessary
    - Get user feedback and iterate

## Acceptance Criteria

- [ ] Backend API returns statistics for all four entity types (Books, Movies, Games, Music)
- [ ] `MusicStats` includes total count, formats, unique artists, and total tracks
- [ ] Frontend displays a modern, visually appealing dashboard
- [ ] Dashboard includes overview cards showing totals for each entity type
- [ ] Each entity type has its own detailed section with relevant statistics
- [ ] Format/platform lists are displayed as visual badges or pills
- [ ] Empty state is shown when no media items exist
- [ ] Dashboard is fully responsive on mobile, tablet, and desktop
- [ ] All statistics leverage existing backend caching for performance
- [ ] Page maintains dark theme consistency with rest of application
- [ ] All backend tests pass, including new music statistics tests
- [ ] Accessibility standards are met (ARIA labels, keyboard navigation)
- [ ] Code adheres to project style guides for both backend and frontend
- [ ] Page fully renders on server (SSR) with no hydration errors
- [ ] CSS and SVG used for all visualizations (no client-side charting libraries)

## Risks and Mitigations

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| Music statistics calculation impacts performance | Medium | Low | Leverage existing caching strategy; music service already exists and is optimized |
| Complex responsive layout breaks on certain screen sizes | Medium | Medium | Follow mobile-first approach; test thoroughly on various devices; use Tailwind responsive utilities |
| Empty state not engaging enough | Low | Low | Design clear call-to-action buttons; use icons and friendly messaging |
| Colors may not work well with dark theme | Low | Medium | Use theme-consistent zinc palette; test color contrast; use opacity overlays for brand colors |
| Format lists may be too long | Low | Medium | Implement scrolling or "show more" functionality if needed; initially display as wrapping badges |
| SVG rendering issues in older browsers | Low | Low | Use simple SVG features with broad support; test in target browsers |

## Open Questions

1. Should we add CSS/SVG-based visualizations for format distribution, or are simple badge lists sufficient?
   - **Decision**: Start with badge lists for simplicity; FormatChart component with SVG bars is provided as optional enhancement if format counts are added to backend
   
2. Should we show additional metrics like average pages per book, average runtime per movie, etc.?
   - **Recommendation**: Keep it simple initially; can add derived metrics in future iteration
   
3. Do we want interactive filtering/drilling down from the dashboard?
   - **Recommendation**: Not in this iteration; focus on visual presentation of stats first

4. Should we persist any user preferences for dashboard layout?
   - **Recommendation**: Not needed initially; all users see same view

5. Should we add date-based statistics (items added this week/month)?
   - **Recommendation**: Not in this iteration; would require additional backend changes to track creation dates

## Dependencies

**Backend:**
- No new external dependencies

**Frontend:**
- No new dependencies required! All visualizations use CSS/Tailwind

**Infrastructure:**
- No infrastructure changes needed
- No environment variable changes needed
- No database schema changes needed

## References

- Issue: https://github.com/paulmfischer/MediaSet/issues/205
- Tailwind CSS Documentation: https://tailwindcss.com/
- Lucide Icons: https://lucide.dev/
- Remix SSR Documentation: https://remix.run/docs/en/main/guides/migrating-react-router-app#server-rendering
- Current code style guides: `.github/code-style-api.md`, `.github/code-style-ui.md`
