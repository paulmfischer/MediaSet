using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Infrastructure.Plex;
using MediaSet.Api.Tests.Features.Plex;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Infrastructure.Plex;

[TestFixture]
public class PlexClientTests
{
    private const string Token = "super-secret-token-value";

    private static PlexClient Client(FakePlexHandler handler, PlexConfiguration? config = null)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://plex.local:32400/") };
        return new PlexClient(http, Options.Create(config ?? new PlexConfiguration { Token = Token }), NullLogger<PlexClient>.Instance);
    }

    private static PlexLibrary Movies => new("1", "Movies", "movie");

    // Real servers list items without streams; the per-item metadata responses carry them
    private static FakePlexHandler Serving(string fixture) =>
        FakePlexHandler.WithServer()
            .Route("/library/sections/1/all", fixture)
            .Route("/library/metadata/1001", "metadata_1001.json")
            .Route("/library/metadata/1002", "metadata_1002.json")
            .Route("/library/metadata/1003", "metadata_1003.json");

    [Test]
    public async Task GetItemsAsync_WatchedMovie_ParsesViewCountAndLastViewedAt()
    {
        var client = Client(Serving("movie.json"));

        var item = (await client.GetItemsAsync(Movies, null, true, CancellationToken.None)).Single();

        Assert.That(item.ViewCount, Is.EqualTo(3));
        Assert.That(item.LastViewedAt, Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(1710000000)));
    }

    [Test]
    public async Task GetItemsAsync_UnwatchedMovie_HasNoViewsOrLastViewed()
    {
        var client = Client(Serving("multiple_versions.json"));

        var item = (await client.GetItemsAsync(Movies, null, true, CancellationToken.None)).Single();

        Assert.That(item.ViewCount, Is.EqualTo(0));
        Assert.That(item.LastViewedAt, Is.Null);
    }

    [Test]
    public async Task GetItemAsync_ExistingItem_ReturnsItemWithStreamsAndDeepLink()
    {
        var handler = Serving("movie.json");
        var client = Client(handler);

        var item = await client.GetItemAsync("1001", true, CancellationToken.None);

        Assert.That(item, Is.Not.Null);
        Assert.That(item!.RatingKey, Is.EqualTo("1001"));
        Assert.That(item.Type, Is.EqualTo("movie"));
        Assert.That(item.Guids.Tmdb, Is.EqualTo("603"));
        Assert.That(item.Versions, Is.Not.Empty);
        Assert.That(item.DeepLink, Does.Contain("/details?key="));
    }

    [Test]
    public async Task GetItemAsync_PlexAnswersNotFound_ReturnsNull()
    {
        var client = Client(Serving("movie.json"));

        var item = await client.GetItemAsync("424242", true, CancellationToken.None);

        Assert.That(item, Is.Null);
    }

    [Test]
    public async Task GetItemAsync_PlexServerError_ThrowsClientExceptionWithoutTokenOrUrl()
    {
        var handler = FakePlexHandler.WithServer()
            .Route("/library/metadata/1001", _ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var client = Client(handler);

        var ex = await Assert.ThrowsAsync<PlexClientException>(() => client.GetItemAsync("1001", true, CancellationToken.None));

        Assert.That(ex!.Message, Does.Not.Contain(Token).And.Not.Contain("plex.local"));
    }

    [Test]
    public async Task GetItemAsync_Request_IsGetOnlyAndKeepsTokenOutOfUrl()
    {
        var handler = Serving("movie.json");
        var client = Client(handler);

        await client.GetItemAsync("1001", true, CancellationToken.None);

        Assert.That(handler.Requests.Select(r => r.Method), Is.All.EqualTo(HttpMethod.Get));
        Assert.That(handler.Requests.Select(r => r.Uri.ToString()), Has.None.Contain(Token));
    }

    [Test]
    public async Task GetLibrariesAsync_LibrariesFixture_ReturnsAllSections()
    {
        var client = Client(FakePlexHandler.WithServer());

        var libraries = await client.GetLibrariesAsync(CancellationToken.None);

        Assert.That(libraries.Select(l => (l.Id, l.Title, l.Type)), Is.EqualTo([
            ("1", "Movies", "movie"), ("2", "TV Shows", "show"), ("3", "Kids Movies", "movie"), ("4", "Music", "artist")
        ]));
    }

    [Test]
    public async Task GetItemsAsync_MovieFixture_ParsesIdentityLibraryAddedDateAndGuids()
    {
        var client = Client(Serving("movie.json"));

        var item = (await client.GetItemsAsync(Movies, null, true, CancellationToken.None)).Single();

        Assert.That(item.RatingKey, Is.EqualTo("1001"));
        Assert.That(item.Type, Is.EqualTo("movie"));
        Assert.That(item.Title, Is.EqualTo("The Matrix"));
        Assert.That(item.Year, Is.EqualTo(1999));
        Assert.That(item.LibraryId, Is.EqualTo("1"));
        Assert.That(item.LibraryTitle, Is.EqualTo("Movies"));
        Assert.That(item.AddedAt, Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(1700000000)));
        Assert.That(item.Guids.Tmdb, Is.EqualTo("603"));
        Assert.That(item.Guids.Imdb, Is.EqualTo("tt0133093"));
        Assert.That(item.Guids.Tvdb, Is.EqualTo("169"));
    }

    [Test]
    public async Task GetItemsAsync_MovieFixture_BuildsDeepLinkWithoutToken()
    {
        var client = Client(Serving("movie.json"));

        var item = (await client.GetItemsAsync(Movies, null, true, CancellationToken.None)).Single();

        Assert.That(item.DeepLink, Is.EqualTo(
            "http://plex.local:32400/web/index.html#!/server/abc123machine/details?key=%2Flibrary%2Fmetadata%2F1001"));
        Assert.That(item.DeepLink, Does.Not.Contain(Token));
    }

    [Test]
    public async Task GetItemsAsync_MovieFixture_ParsesFileDetails()
    {
        var client = Client(Serving("movie.json"));

        var version = (await client.GetItemsAsync(Movies, null, true, CancellationToken.None)).Single().Versions.Single();

        Assert.That(version.Resolution, Is.EqualTo("1080"));
        Assert.That(version.VideoCodec, Is.EqualTo("h264"));
        Assert.That(version.Hdr, Is.False);
        Assert.That(version.Duration, Is.EqualTo(TimeSpan.FromMilliseconds(8160000)));
        Assert.That(version.Container, Is.EqualTo("mkv"));
        Assert.That(version.FilePath, Is.EqualTo("/data/movies/The Matrix (1999)/The Matrix.mkv"));
        Assert.That(version.SizeBytes, Is.EqualTo(4294967296));
    }

    [Test]
    public async Task GetItemsAsync_ShowFixture_ParsesShowWithGuidsAndNoVersions()
    {
        var client = Client(FakePlexHandler.WithServer().Route("/library/sections/2/all", "show.json"));

        var item = (await client.GetItemsAsync(new PlexLibrary("2", "TV Shows", "show"), null, true, CancellationToken.None)).Single();

        Assert.That(item.Type, Is.EqualTo("show"));
        Assert.That(item.Guids, Is.EqualTo(new PlexGuids("1399", "tt0944947", "121361")));
        Assert.That(item.Versions, Is.Empty);
    }

    [Test]
    public async Task GetSeasonsAsync_ShowWithSeasons_ReturnsIndexEpisodeCountAndDeepLinkInIndexOrder()
    {
        var client = Client(FakePlexHandler.WithServer().Route("/library/metadata/2001/children", "seasons.json"));

        var seasons = await client.GetSeasonsAsync("2001", CancellationToken.None);

        Assert.That(seasons.Select(s => s.Index), Is.EqualTo([0, 1, 2, 3]));
        Assert.That(seasons.Select(s => s.EpisodeCount), Is.EqualTo([2, 7, 13, 13]));
        Assert.That(seasons[2].RatingKey, Is.EqualTo("2102"));
        Assert.That(seasons[2].AddedAt, Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(1710000300)));
        Assert.That(seasons[2].DeepLink, Does.Contain("details?key=").And.Contain("2102"));
    }

    private static FakePlexHandler ServingEpisodes() =>
        FakePlexHandler.WithServer()
            .Route("/library/metadata/2102/children", "episodes_2102.json")
            .Route("/library/metadata/4001", "metadata_4001.json")
            .Route("/library/metadata/4002", "metadata_4002.json");

    [Test]
    public async Task GetEpisodesAsync_SeasonWithEpisodes_ParsesNumbersTitleWatchedAndFile()
    {
        var client = Client(ServingEpisodes());

        var episodes = await client.GetEpisodesAsync("2102", CancellationToken.None);

        Assert.That(episodes.Select(e => e.EpisodeIndex), Is.EqualTo([1, 2, 3]));
        Assert.That(episodes.Select(e => e.SeasonIndex), Is.All.EqualTo(2));
        Assert.That(episodes[0].RatingKey, Is.EqualTo("4001"));
        Assert.That(episodes[0].Title, Is.EqualTo("Pilot"));
        Assert.That(episodes[0].ViewCount, Is.EqualTo(2));
        Assert.That(episodes[0].LastViewedAt, Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(1710000500)));
        Assert.That(episodes[1].ViewCount, Is.EqualTo(0));
        Assert.That(episodes[1].LastViewedAt, Is.Null);
    }

    [Test]
    public async Task GetEpisodesAsync_EpisodeWithTwoVersions_ParsesVersionsWithAudioSubtitlesAndHdr()
    {
        var client = Client(ServingEpisodes());

        var pilot = (await client.GetEpisodesAsync("2102", CancellationToken.None)).First();

        var hevc = pilot.Versions.Single(v => v.Resolution == "1080");
        Assert.That(pilot.Versions, Has.Count.EqualTo(2));
        Assert.That(hevc.Hdr, Is.True);
        Assert.That(hevc.VideoCodec, Is.EqualTo("hevc"));
        Assert.That(hevc.Container, Is.EqualTo("mkv"));
        Assert.That(hevc.FilePath, Is.EqualTo("/data/tv/Show/S02E01.mkv"));
        Assert.That(hevc.SizeBytes, Is.EqualTo(1000));
        Assert.That(hevc.Duration, Is.EqualTo(TimeSpan.FromMilliseconds(2760000)));
        Assert.That(hevc.AudioTracks.Select(a => (a.Codec, a.Channels, a.Language)), Is.EqualTo([("ac3", (int?)6, "en"), ("aac", (int?)2, "fr")]));
        Assert.That(hevc.Subtitles.Select(x => (x.Language, x.External)), Is.EqualTo([("en", false), ("es", true)]));
        Assert.That(pilot.Versions.Single(v => v.Resolution == "720").Hdr, Is.False);
    }

    [Test]
    public async Task GetEpisodesAsync_EpisodeWithoutFiles_HasNoVersionsAndNoStreamLookup()
    {
        var handler = ServingEpisodes();
        var client = Client(handler);

        var episodes = await client.GetEpisodesAsync("2102", CancellationToken.None);

        Assert.That(episodes[2].Versions, Is.Empty);
        Assert.That(handler.Requests.Select(r => r.Uri.AbsolutePath), Does.Not.Contain("/library/metadata/4003"));
    }

    [Test]
    public async Task GetEpisodesAsync_MoreThanOnePage_ReadsEveryPageWithContainerHeaders()
    {
        var handler = FakePlexHandler.WithServer().Route("/library/metadata/2101/children", request =>
            FakePlexHandler.Json(FakePlexHandler.Fixture(
                request.Headers.GetValues("X-Plex-Container-Start").Single() == "0" ? "episodes_page1.json" : "episodes_page2.json")));
        var client = Client(handler, new PlexConfiguration { Token = Token, PageSize = 2 });

        var episodes = await client.GetEpisodesAsync("2101", CancellationToken.None);

        var pages = handler.Requests.Where(r => r.Uri.AbsolutePath.EndsWith("/children")).ToList();
        Assert.That(episodes.Select(e => e.Title), Is.EqualTo(["One", "Two", "Three"]));
        Assert.That(pages.Select(p => p.Headers["X-Plex-Container-Start"]), Is.EqualTo(["0", "2"]));
        Assert.That(pages.Select(p => p.Headers["X-Plex-Container-Size"]), Is.All.EqualTo("2"));
        Assert.That(handler.Requests.Select(r => r.Method), Is.All.EqualTo(HttpMethod.Get));
    }

    [Test]
    public void GetEpisodesAsync_PlexUnreachable_ThrowsPlexClientException()
    {
        var client = Client(FakePlexHandler.WithServer().Route("/library/metadata/2102/children", _ => FakePlexHandler.Json("{}", HttpStatusCode.InternalServerError)));

        Assert.That(async () => await client.GetEpisodesAsync("2102", CancellationToken.None), Throws.TypeOf<PlexClientException>());
    }

    [Test]
    public async Task GetEpisodesAsync_SeasonUnknownToPlex_ReturnsNoEpisodes()
    {
        var client = Client(FakePlexHandler.WithServer());

        Assert.That(await client.GetEpisodesAsync("2102", CancellationToken.None), Is.Empty);
    }

    [Test]
    public async Task GetEpisodesAsync_StreamLookupFails_KeepsListingDataAndFlagsStreamsUnavailable()
    {
        var handler = ServingEpisodes().Route("/library/metadata/4002", _ => FakePlexHandler.Json("{}", HttpStatusCode.InternalServerError));
        var client = Client(handler);

        var episodes = await client.GetEpisodesAsync("2102", CancellationToken.None);

        Assert.That(episodes.Select(e => e.StreamsAvailable), Is.EqualTo([true, false, true]));
        Assert.That(episodes[1].Versions.Single().Resolution, Is.EqualTo("720"));
        Assert.That(episodes[1].Versions.Single().AudioTracks, Is.Empty);
    }

    private static readonly DateTimeOffset _pilotUpdatedAt = DateTimeOffset.FromUnixTimeSeconds(1700000001);
    private static readonly DateTimeOffset _secondUpdatedAt = DateTimeOffset.FromUnixTimeSeconds(1700000002);

    private static int DetailRequests(FakePlexHandler handler, string ratingKey) =>
        handler.Requests.Count(r => r.Uri.AbsolutePath == $"/library/metadata/{ratingKey}");

    [Test]
    public async Task GetEpisodesAsync_UnchangedKnownEpisode_SkipsStreamLookupAndFlagsReuse()
    {
        var handler = ServingEpisodes();
        var known = new Dictionary<string, PlexKnownMedia> { ["4001"] = new(_pilotUpdatedAt, 1000) };

        var episodes = await Client(handler).GetEpisodesAsync("2102", CancellationToken.None, known);

        Assert.That(DetailRequests(handler, "4001"), Is.EqualTo(0));
        Assert.That(episodes[0].StreamsReused, Is.True);
        Assert.That(episodes[0].StreamsAvailable, Is.True);
        Assert.That(episodes[0].UpdatedAt, Is.EqualTo(_pilotUpdatedAt));
        Assert.That(episodes[0].Versions.Single().SizeBytes, Is.EqualTo(1000));
    }

    [Test]
    public async Task GetEpisodesAsync_KnownEpisodeWithNewUpdatedAt_FetchesStreams()
    {
        var handler = ServingEpisodes();
        var known = new Dictionary<string, PlexKnownMedia> { ["4001"] = new(_pilotUpdatedAt.AddDays(-1), 1000) };

        var episodes = await Client(handler).GetEpisodesAsync("2102", CancellationToken.None, known);

        Assert.That(DetailRequests(handler, "4001"), Is.EqualTo(1));
        Assert.That(episodes[0].StreamsReused, Is.False);
        Assert.That(episodes[0].Versions.Any(v => v.AudioTracks.Count > 0), Is.True);
    }

    [Test]
    public async Task GetEpisodesAsync_KnownEpisodeWithChangedSize_FetchesStreams()
    {
        var handler = ServingEpisodes();
        var known = new Dictionary<string, PlexKnownMedia> { ["4001"] = new(_pilotUpdatedAt, 999) };

        var episodes = await Client(handler).GetEpisodesAsync("2102", CancellationToken.None, known);

        Assert.That(DetailRequests(handler, "4001"), Is.EqualTo(1));
        Assert.That(episodes[0].StreamsReused, Is.False);
    }

    [Test]
    public async Task GetEpisodesAsync_EpisodeNotKnown_FetchesStreamsWhileKnownOnesAreSkipped()
    {
        var handler = ServingEpisodes();
        var known = new Dictionary<string, PlexKnownMedia> { ["4001"] = new(_pilotUpdatedAt, 1000) };

        var episodes = await Client(handler).GetEpisodesAsync("2102", CancellationToken.None, known);

        Assert.That(DetailRequests(handler, "4001"), Is.EqualTo(0));
        Assert.That(DetailRequests(handler, "4002"), Is.EqualTo(1));
        Assert.That(episodes[1].StreamsReused, Is.False);
    }

    [Test]
    public async Task GetEpisodesAsync_ListingWithoutUpdatedAt_FetchesStreams()
    {
        var handler = FakePlexHandler.WithServer()
            .Route("/library/metadata/2102/children", _ => FakePlexHandler.Json(
                """{"MediaContainer":{"Metadata":[{"ratingKey":"4001","type":"episode","title":"Pilot","index":1,"parentIndex":2,"Media":[{"videoResolution":"1080","Part":[{"size":1000}]}]}]}}"""))
            .Route("/library/metadata/4001", "metadata_4001.json");
        var known = new Dictionary<string, PlexKnownMedia> { ["4001"] = new(_pilotUpdatedAt, 1000) };

        var episodes = await Client(handler).GetEpisodesAsync("2102", CancellationToken.None, known);

        Assert.That(DetailRequests(handler, "4001"), Is.EqualTo(1));
        Assert.That(episodes.Single().StreamsReused, Is.False);
    }

    [Test]
    public async Task GetEpisodesAsync_BlankSeasonKey_ReturnsEmptyWithoutRequest()
    {
        var handler = ServingEpisodes();

        var episodes = await Client(handler).GetEpisodesAsync(" ", CancellationToken.None);

        Assert.That(episodes, Is.Empty);
        Assert.That(handler.Requests, Is.Empty);
    }

    [Test]
    public async Task GetSeasonsAsync_ShowWithoutSeasons_ReturnsEmpty()
    {
        var client = Client(FakePlexHandler.WithServer().Route("/library/metadata/2001/children", "seasons_none.json"));

        Assert.That(await client.GetSeasonsAsync("2001", CancellationToken.None), Is.Empty);
    }

    [Test]
    public void GetSeasonsAsync_PlexUnreachable_ThrowsPlexClientException()
    {
        var client = Client(FakePlexHandler.WithServer());

        Assert.That(async () => await client.GetSeasonsAsync("2001", CancellationToken.None), Throws.TypeOf<PlexClientException>());
    }

    [Test]
    public async Task GetItemsAsync_MultipleVersionsFixture_ExposesEveryVersion()
    {
        var client = Client(Serving("multiple_versions.json"));

        var versions = (await client.GetItemsAsync(Movies, null, true, CancellationToken.None)).Single().Versions;

        Assert.That(versions, Has.Count.EqualTo(2));
        Assert.That(versions.Select(v => v.Resolution), Is.EqualTo(["1080", "4k"]));
        Assert.That(versions.Select(v => v.FilePath), Is.EqualTo([
            "/data/movies/Dune (2021)/Dune - 1080p.mp4", "/data/movies/Dune (2021)/Dune - 2160p.mkv"
        ]));
        Assert.That(versions.Select(v => v.SizeBytes), Is.EqualTo([3000000000, 42000000000]));
    }

    [Test]
    public async Task GetItemsAsync_MultipleVersionsFixture_DetectsHdrPerVersion()
    {
        var client = Client(Serving("multiple_versions.json"));

        var versions = (await client.GetItemsAsync(Movies, null, true, CancellationToken.None)).Single().Versions;

        Assert.That(versions.Select(v => v.Hdr), Is.EqualTo([false, true]));
        Assert.That(versions[1].VideoCodec, Is.EqualTo("hevc"));
    }

    [Test]
    public async Task GetItemsAsync_SubtitlesFixture_ReportsEmbeddedAndExternalLanguages()
    {
        var client = Client(Serving("subtitles_audio.json"));

        var subtitles = (await client.GetItemsAsync(Movies, null, true, CancellationToken.None)).Single().Versions.Single().Subtitles;

        Assert.That(subtitles, Is.EqualTo([new PlexSubtitle("en", false), new PlexSubtitle("de", true)]));
    }

    [Test]
    public async Task GetItemsAsync_AudioFixture_ReportsCodecAndChannelsPerTrack()
    {
        var client = Client(Serving("subtitles_audio.json"));

        var audio = (await client.GetItemsAsync(Movies, null, true, CancellationToken.None)).Single().Versions.Single().AudioTracks;

        Assert.That(audio, Is.EqualTo([new PlexAudioTrack("dca", 6, "fr"), new PlexAudioTrack("aac", 2, "en")]));
    }

    [Test]
    public async Task GetItemsAsync_MultiplePages_ReadsEveryPageUsingStartAndSizeHeaders()
    {
        var handler = FakePlexHandler.WithServer().Route("/library/sections/1/all", request =>
            FakePlexHandler.Json(FakePlexHandler.Fixture(
                request.Headers.GetValues("X-Plex-Container-Start").Single() == "0" ? "paging_page1.json" : "paging_page2.json")));
        var client = Client(handler, new PlexConfiguration { Token = Token, PageSize = 2 });

        var items = await client.GetItemsAsync(Movies, null, true, CancellationToken.None);

        var pages = handler.Requests.Where(r => r.Uri.AbsolutePath.EndsWith("/all")).ToList();
        Assert.That(items.Select(i => i.Title), Is.EqualTo(["Alpha", "Beta", "Gamma"]));
        Assert.That(pages.Select(p => p.Headers["X-Plex-Container-Start"]), Is.EqualTo(["0", "2"]));
        Assert.That(pages.Select(p => p.Headers["X-Plex-Container-Size"]), Is.All.EqualTo("2"));
    }

    [Test]
    public async Task GetItemsAsync_TitleFilter_SendsEncodedTitleQuery()
    {
        var handler = Serving("movie.json");
        var client = Client(handler);

        await client.GetItemsAsync(Movies, "Mat & rix", true, CancellationToken.None);

        var request = handler.Requests.Single(r => r.Uri.AbsolutePath.EndsWith("/all"));
        Assert.That(request.Uri.Query, Is.EqualTo("?includeGuids=1&title=Mat%20%26%20rix"));
    }

    [Test]
    public async Task GetLibrariesForEntityTypeAsync_NoSectionIds_ReturnsAllMovieAndShowLibraries()
    {
        var client = Client(FakePlexHandler.WithServer());

        var libraries = await client.GetLibrariesForEntityTypeAsync("movies", CancellationToken.None);

        Assert.That(libraries.Select(l => l.Id), Is.EqualTo(["1", "2", "3"]));
    }

    [Test]
    public async Task GetLibrariesForEntityTypeAsync_SectionIdsConfigured_ReturnsOnlyThoseLibraries()
    {
        var config = new PlexConfiguration { Token = Token };
        config.Entities["movies"] = new PlexEntityConfiguration { Enabled = true, LibrarySectionIds = ["3"] };
        var client = Client(FakePlexHandler.WithServer(), config);

        var libraries = await client.GetLibrariesForEntityTypeAsync("movies", CancellationToken.None);

        Assert.That(libraries.Select(l => l.Id), Is.EqualTo(["3"]));
    }

    [Test]
    public async Task GetLibrariesForEntityTypeAsync_SectionIdOfShowLibrary_ReturnsTheShowLibrary()
    {
        var config = new PlexConfiguration { Token = Token };
        config.Entities["movies"] = new PlexEntityConfiguration { Enabled = true, LibrarySectionIds = ["2"] };
        var client = Client(FakePlexHandler.WithServer(), config);

        var libraries = await client.GetLibrariesForEntityTypeAsync("movies", CancellationToken.None);

        Assert.That(libraries.Select(l => l.Id), Is.EqualTo(["2"]));
    }

    [Test]
    public async Task Client_AnyOperation_OnlySendsGetRequests()
    {
        var handler = Serving("movie.json");
        var client = Client(handler);

        await client.GetLibrariesForEntityTypeAsync("movies", CancellationToken.None);
        await client.GetItemsAsync(Movies, "x", true, CancellationToken.None);

        Assert.That(handler.Requests, Is.Not.Empty);
        Assert.That(handler.Requests.Select(r => r.Method), Is.All.EqualTo(HttpMethod.Get));
    }

    [Test]
    public void IPlexClient_PublicSurface_HasNoWriteMethodNames()
    {
        var names = typeof(IPlexClient).GetMethods().Select(m => m.Name);

        Assert.That(names, Is.All.Matches(@"^Get\w+Async$"));
    }

    [Test]
    public async Task GetLibrariesAsync_ServerReturnsUnauthorized_ThrowsExceptionWithStatusAndNoToken()
    {
        var handler = new FakePlexHandler().Route("/library/sections", _ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var client = Client(handler);

        var ex = (await Assert.ThrowsAsync<PlexClientException>(() => client.GetLibrariesAsync(CancellationToken.None)))!;

        Assert.That(ex.StatusCode, Is.EqualTo(401));
        Assert.That(ex.Message, Is.EqualTo("Plex request failed (status code 401)."));
    }

    [Test]
    public async Task GetLibrariesAsync_ConnectionFails_ThrowsFixedMessageWithoutInnerOrUrl()
    {
        var handler = new FakePlexHandler().Route("/library/sections",
            _ => throw new HttpRequestException($"Connection refused http://plex.local:32400/?X-Plex-Token={Token}"));
        var client = Client(handler);

        var ex = (await Assert.ThrowsAsync<PlexClientException>(() => client.GetLibrariesAsync(CancellationToken.None)))!;

        Assert.That(ex.Message, Is.EqualTo("Plex server could not be reached."));
        Assert.That(ex.InnerException, Is.Null);
        Assert.That(ex.ToString(), Does.Not.Contain(Token).And.Not.Contain("plex.local"));
    }

    [Test]
    public async Task GetLibrariesAsync_RequestTimesOut_ThrowsTimeoutException()
    {
        var handler = new FakePlexHandler().Route("/library/sections", _ => throw new TaskCanceledException("timeout"));
        var client = Client(handler);

        var ex = (await Assert.ThrowsAsync<PlexClientException>(() => client.GetLibrariesAsync(CancellationToken.None)))!;

        Assert.That(ex.Message, Is.EqualTo("Plex request timed out."));
    }

    [Test]
    public async Task GetLibrariesAsync_CallerCancels_ThrowsOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var handler = new FakePlexHandler().Route("/library/sections", _ => throw new TaskCanceledException());
        var client = Client(handler);

        await Assert.ThrowsAsync<TaskCanceledException>(() => client.GetLibrariesAsync(cts.Token));
    }

    [Test]
    public async Task GetLibrariesAsync_InvalidJson_ThrowsUnreadableResponse()
    {
        var handler = new FakePlexHandler().Route("/library/sections", _ => FakePlexHandler.Json("<html>nope</html>"));
        var client = Client(handler);

        var ex = (await Assert.ThrowsAsync<PlexClientException>(() => client.GetLibrariesAsync(CancellationToken.None)))!;

        Assert.That(ex.Message, Is.EqualTo("Plex returned an unreadable response."));
    }

    [Test]
    public async Task GetItemsAsync_NoDeepLinksRequested_DoesNotCallIdentity()
    {
        var handler = Serving("movie.json");
        var client = Client(handler);

        var item = (await client.GetItemsAsync(Movies, null, false, CancellationToken.None)).Single();

        Assert.That(handler.Requests.Select(r => r.Uri.AbsolutePath), Does.Not.Contain("/identity"));
        Assert.That(item.DeepLink, Is.Null);
    }

    [Test]
    public async Task GetItemsAsync_ServerNeverEndsPaging_ThrowsAfterMaxPages()
    {
        var handler = FakePlexHandler.WithServer().Route("/library/sections/1/all", _ => FakePlexHandler.Json(
            """{"MediaContainer":{"size":1,"Metadata":[{"ratingKey":"1","type":"movie","title":"Loop"}]}}"""));
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://plex.local:32400/") };
        var client = new PlexClient(http, Options.Create(new PlexConfiguration { Token = Token, PageSize = 1 }), NullLogger<PlexClient>.Instance) { MaxPagesPerRead = 3 };

        var ex = await Assert.ThrowsAsync<PlexClientException>(() => client.GetItemsAsync(Movies, null, false, CancellationToken.None));

        Assert.That(ex!.Message, Is.EqualTo("Plex returned too many pages."));
        Assert.That(handler.Requests, Has.Count.EqualTo(3));
        Assert.That(PlexClient.MaxPages, Is.EqualTo(10_000));
    }

    [Test]
    public async Task GetItemsAsync_ShortPageWithoutTotalSize_StopsAfterThatPage()
    {
        var handler = FakePlexHandler.WithServer().Route("/library/sections/1/all", _ => FakePlexHandler.Json(
            """{"MediaContainer":{"size":1,"Metadata":[{"ratingKey":"1","type":"movie","title":"One"}]}}"""));
        var client = Client(handler, new PlexConfiguration { Token = Token, PageSize = 5 });

        var items = await client.GetItemsAsync(Movies, null, false, CancellationToken.None);

        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(handler.Requests.Count(r => r.Uri.AbsolutePath.EndsWith("/all")), Is.EqualTo(1));
    }

    [Test]
    public async Task GetLibrariesAsync_CancelledMidRequest_CancellationReachesHandlerAndIsNotWrapped()
    {
        using var cts = new CancellationTokenSource();
        var started = new TaskCompletionSource();
        var handler = new FakePlexHandler().Route("/library/sections", async (_, token) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage();
        });
        var client = Client(handler);

        var pending = client.GetLibrariesAsync(cts.Token);
        await started.Task;
        await cts.CancelAsync();

        Assert.That(async () => await pending, Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public async Task GetItemsAsync_Listing_RequestsGuidsWithIncludeGuids()
    {
        var handler = Serving("movie.json");
        var client = Client(handler);

        await client.GetItemsAsync(Movies, null, false, CancellationToken.None);

        var request = handler.Requests.Single(r => r.Uri.AbsolutePath.EndsWith("/all"));
        Assert.That(request.Uri.Query, Is.EqualTo("?includeGuids=1"));
    }

    [Test]
    public async Task GetItemsAsync_WithoutDetails_DoesNotFetchPerItemMetadataAndHasNoStreams()
    {
        var handler = Serving("subtitles_audio.json");
        var client = Client(handler);

        var version = (await client.GetItemsAsync(Movies, null, false, CancellationToken.None)).Single().Versions.Single();

        Assert.That(handler.Requests.Select(r => r.Uri.AbsolutePath), Has.None.StartsWith("/library/metadata"));
        Assert.That(version.AudioTracks, Is.Empty);
        Assert.That(version.Subtitles, Is.Empty);
        Assert.That(version.FilePath, Is.EqualTo("/data/movies/Amelie (2001)/Amelie.mkv"));
    }

    [Test]
    public async Task GetItemsAsync_WithDetails_FetchesStreamsFromPerItemMetadataWithGetOnly()
    {
        var handler = Serving("subtitles_audio.json");
        var client = Client(handler);

        var version = (await client.GetItemsAsync(Movies, null, true, CancellationToken.None)).Single().Versions.Single();

        var detail = handler.Requests.Single(r => r.Uri.AbsolutePath == "/library/metadata/1003");
        Assert.That(detail.Method, Is.EqualTo(HttpMethod.Get));
        Assert.That(detail.Uri.Query, Is.EqualTo("?includeGuids=1"));
        Assert.That(version.AudioTracks, Has.Count.EqualTo(2));
        Assert.That(version.Subtitles, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetItemsAsync_ItemWithoutFiles_SkipsMetadataRequest()
    {
        var handler = FakePlexHandler.WithServer().Route("/library/sections/2/all", "show.json");
        var client = Client(handler);

        await client.GetItemsAsync(new PlexLibrary("2", "TV Shows", "show"), null, true, CancellationToken.None);

        Assert.That(handler.Requests.Select(r => r.Uri.AbsolutePath), Has.None.StartsWith("/library/metadata"));
    }

    private const string ListedTwoMovies = """
        {"MediaContainer":{"size":2,"totalSize":2,"Metadata":[
         {"ratingKey":"10","type":"movie","title":"Ten","Media":[{"videoResolution":"1080","Part":[{"file":"/a.mkv","size":1}]}]},
         {"ratingKey":"11","type":"movie","title":"Eleven","Media":[{"videoResolution":"720","Part":[{"file":"/b.mkv","size":2}]}]}]}}
        """;

    private const string DetailWithStream = """
        {"MediaContainer":{"Metadata":[{"ratingKey":"11","Media":[{"videoResolution":"720","Part":[{"file":"/b.mkv","size":2,
         "Stream":[{"streamType":2,"codec":"aac","channels":2,"languageTag":"en"}]}]}]}]}}
        """;

    [TestCase(HttpStatusCode.InternalServerError)]
    [TestCase(HttpStatusCode.NotFound)]
    public async Task GetItemsAsync_OneItemDetailFails_ReturnsAllItemsWithListingDataForTheFailedOne(HttpStatusCode status)
    {
        var handler = FakePlexHandler.WithServer()
            .Route("/library/sections/1/all", _ => FakePlexHandler.Json(ListedTwoMovies))
            .Route("/library/metadata/10", _ => new HttpResponseMessage(status))
            .Route("/library/metadata/11", _ => FakePlexHandler.Json(DetailWithStream));
        var client = Client(handler);

        var items = await client.GetItemsAsync(Movies, null, true, CancellationToken.None);

        Assert.That(items.Select(i => i.Title), Is.EqualTo(["Ten", "Eleven"]));
        Assert.That(items[0].Versions.Single().FilePath, Is.EqualTo("/a.mkv"));
        Assert.That(items[0].Versions.Single().AudioTracks, Is.Empty);
        Assert.That(items[1].Versions.Single().AudioTracks, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task GetItemsAsync_DetailTimesOut_ReturnsListingDataForThatItem()
    {
        var handler = FakePlexHandler.WithServer()
            .Route("/library/sections/1/all", _ => FakePlexHandler.Json(ListedTwoMovies))
            .Route("/library/metadata/10", _ => throw new TaskCanceledException("timeout"))
            .Route("/library/metadata/11", _ => FakePlexHandler.Json(DetailWithStream));
        var client = Client(handler);

        var items = await client.GetItemsAsync(Movies, null, true, CancellationToken.None);

        Assert.That(items, Has.Count.EqualTo(2));
        Assert.That(items[0].Versions.Single().AudioTracks, Is.Empty);
    }

    [Test]
    public async Task GetItemsAsync_CallerCancelsDuringDetail_PropagatesCancellationUnwrapped()
    {
        using var cts = new CancellationTokenSource();
        var handler = FakePlexHandler.WithServer()
            .Route("/library/sections/1/all", _ => FakePlexHandler.Json(ListedTwoMovies))
            .Route("/library/metadata/10", async (_, token) =>
            {
                await cts.CancelAsync();
                await Task.Delay(Timeout.Infinite, token);
                return new HttpResponseMessage();
            });
        var client = Client(handler);

        Assert.That(async () => await client.GetItemsAsync(Movies, null, true, cts.Token), Throws.InstanceOf<OperationCanceledException>());
    }

    private static string ManyMovies(int count, long updatedAt = 1700000000) =>
        "{\"MediaContainer\":{\"size\":" + count + ",\"totalSize\":" + count + ",\"Metadata\":["
        + string.Join(",", Enumerable.Range(1, count).Select(i =>
            $"{{\"ratingKey\":\"{i}\",\"type\":\"movie\",\"title\":\"M{i}\",\"updatedAt\":{updatedAt},\"Media\":[{{\"videoResolution\":\"1080\",\"Part\":[{{\"file\":\"/{i}.mkv\",\"size\":{i}}}]}}]}}"))
        + "]}}";

    private static FakePlexHandler ServingMany(int count, Func<string, Task>? onDetail = null)
    {
        var handler = FakePlexHandler.WithServer().Route("/library/sections/1/all", _ => FakePlexHandler.Json(ManyMovies(count)));
        for (var i = 1; i <= count; i++)
        {
            var key = i.ToString();
            handler.Route($"/library/metadata/{key}", async (_, _) =>
            {
                if (onDetail != null)
                {
                    await onDetail(key);
                }
                return FakePlexHandler.Json($"{{\"MediaContainer\":{{\"Metadata\":[{{\"ratingKey\":\"{key}\",\"Media\":[{{\"videoResolution\":\"1080\",\"Part\":[{{\"file\":\"/{key}.mkv\",\"size\":{key},\"Stream\":[{{\"streamType\":2,\"codec\":\"aac\",\"channels\":2,\"languageTag\":\"en\"}}]}}]}}]}}]}}}}");
            });
        }
        return handler;
    }

    [TestCase(1)]
    [TestCase(3)]
    [TestCase(0)]
    public async Task GetItemsAsync_ManyItems_NeverExceedsDetailConcurrency(int limit)
    {
        var inFlight = 0;
        var peak = 0;
        var handler = ServingMany(12, async _ =>
        {
            var now = Interlocked.Increment(ref inFlight);
            InterlockedMax(ref peak, now);
            await Task.Delay(25);
            Interlocked.Decrement(ref inFlight);
        });
        var client = Client(handler, new PlexConfiguration { Token = Token, DetailConcurrency = limit });

        var items = await client.GetItemsAsync(Movies, null, true, CancellationToken.None);

        var effective = Math.Max(1, limit);
        Assert.That(peak, Is.LessThanOrEqualTo(effective));
        if (effective > 1)
        {
            Assert.That(peak, Is.GreaterThan(1));
        }
        Assert.That(items.Select(i => i.RatingKey), Is.EqualTo(Enumerable.Range(1, 12).Select(i => i.ToString())));
        Assert.That(items.All(i => i.Versions.Single().AudioTracks.Count == 1), Is.True);
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while (value > (current = Volatile.Read(ref target)) && Interlocked.CompareExchange(ref target, value, current) != current)
        {
        }
    }

    [Test]
    public async Task GetItemsAsync_DefaultConfiguration_RunsDetailRequestsConcurrently()
    {
        Assert.That(new PlexConfiguration().DetailConcurrency, Is.EqualTo(4));
        var inFlight = 0;
        var peak = 0;
        var twoInFlight = new TaskCompletionSource();
        // Requests wait until two are in flight at once (no timing), so peak > 1 is deterministic
        var handler = ServingMany(12, async _ =>
        {
            var now = Interlocked.Increment(ref inFlight);
            InterlockedMax(ref peak, now);
            if (now >= 2)
            {
                twoInFlight.TrySetResult();
            }
            await twoInFlight.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Interlocked.Decrement(ref inFlight);
        });

        await Client(handler).GetItemsAsync(Movies, null, true, CancellationToken.None);

        Assert.That(peak, Is.InRange(2, 4));
    }

    [Test]
    public async Task GetItemsAsync_KnownItemsUnchanged_SkipDetailRequestAndFlagReused()
    {
        var handler = ServingMany(4);
        var updated = DateTimeOffset.FromUnixTimeSeconds(1700000000);
        var known = new Dictionary<string, PlexKnownMedia>
        {
            ["1"] = new(updated, 1),                                   // unchanged
            ["2"] = new(updated.AddSeconds(-5), 2),                    // updatedAt changed
            ["3"] = new(updated, 999),                                 // file size changed
        };                                                             // 4 is new (legacy items are also absent)

        var items = await Client(handler).GetItemsAsync(Movies, null, true, CancellationToken.None, known);

        var detailed = handler.Requests.Select(r => r.Uri.AbsolutePath).Where(p => p.StartsWith("/library/metadata/")).Order().ToList();
        Assert.That(detailed, Is.EqualTo(["/library/metadata/2", "/library/metadata/3", "/library/metadata/4"]));
        Assert.That(items.Select(i => i.StreamsReused), Is.EqualTo([true, false, false, false]));
        Assert.That(items[0].Versions.Single().AudioTracks, Is.Empty);
        Assert.That(items[0].Versions.Single().FilePath, Is.EqualTo("/1.mkv"));
        Assert.That(items[1].Versions.Single().AudioTracks, Has.Count.EqualTo(1));
        Assert.That(items[0].UpdatedAt, Is.EqualTo(updated));
    }

    [Test]
    public async Task GetItemsAsync_ShowWithKnownEntry_IsNotFlaggedReusedAndNeedsNoDetail()
    {
        var handler = FakePlexHandler.WithServer().Route("/library/sections/2/all", "show.json");
        var known = new Dictionary<string, PlexKnownMedia> { ["2001"] = new(DateTimeOffset.FromUnixTimeSeconds(1), 1) };

        var item = (await Client(handler).GetItemsAsync(new PlexLibrary("2", "TV Shows", "show"), null, true, CancellationToken.None, known)).Single();

        Assert.That(item.StreamsReused, Is.False);
        Assert.That(handler.Requests.Select(r => r.Uri.AbsolutePath), Has.None.StartsWith("/library/metadata/"));
    }

    [Test]
    public async Task GetItemsAsync_DetailFails_FlagsStreamsUnavailableSoItIsReadAgain()
    {
        var handler = ServingMany(2).Route("/library/metadata/1", _ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var items = await Client(handler).GetItemsAsync(Movies, null, true, CancellationToken.None);

        Assert.That(items.Select(i => i.StreamsAvailable), Is.EqualTo([false, true]));
        Assert.That(items[0].Versions.Single().FilePath, Is.EqualTo("/1.mkv"));
    }

    [Test]
    public async Task GetItemsAsync_ConcurrentDetails_CancellationPropagatesUnwrapped()
    {
        using var cts = new CancellationTokenSource();
        var handler = ServingMany(8, async key =>
        {
            if (key == "3")
            {
                await cts.CancelAsync();
            }
            await Task.Delay(Timeout.Infinite, cts.Token);
        });
        var client = Client(handler, new PlexConfiguration { Token = Token, DetailConcurrency = 4 });

        Assert.That(async () => await client.GetItemsAsync(Movies, null, true, cts.Token), Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public async Task GetItemsAsync_ConcurrentDetailsFailAndSucceed_NeverLogTokenOrUrl()
    {
        var logger = new CapturingLogger<PlexClient>();
        var handler = ServingMany(6).Route("/library/metadata/2", _ => throw new HttpRequestException($"boom http://plex.local:32400/?X-Plex-Token={Token}"));
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://plex.local:32400/") };
        var client = new PlexClient(http, Options.Create(new PlexConfiguration { Token = Token }), logger);

        await client.GetItemsAsync(Movies, null, true, CancellationToken.None);

        Assert.That(logger.Messages, Is.Not.Empty);
        Assert.That(logger.Messages, Has.None.Contain(Token).And.None.Contain("plex.local"));
        Assert.That(handler.Requests.SelectMany(r => new[] { r.Uri.ToString() }), Has.None.Contain(Token));
    }

    private sealed class CapturingLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        private readonly object _sync = new();
        private readonly List<string> _messages = [];
        public List<string> Messages { get { lock (_sync) { return [.. _messages]; } } }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (_sync)
            { _messages.Add(formatter(state, exception) + exception); }
        }
    }

    [Test]
    public async Task GetItemsAsync_StreamHasLanguageTagAndCode_PrefersLanguageTag()
    {
        var handler = FakePlexHandler.WithServer()
            .Route("/library/sections/1/all", _ => FakePlexHandler.Json(ListedTwoMovies))
            .Route("/library/metadata/10", _ => FakePlexHandler.Json("""
                {"MediaContainer":{"Metadata":[{"ratingKey":"10","Media":[{"Part":[{"Stream":[
                 {"streamType":2,"codec":"aac","channels":2,"languageTag":"en","languageCode":"eng","language":"English"},
                 {"streamType":3,"languageCode":"fra","language":"French"},
                 {"streamType":3,"language":"German"}]}]}]}]}}
                """))
            .Route("/library/metadata/11", _ => FakePlexHandler.Json(DetailWithStream));
        var client = Client(handler);

        var version = (await client.GetItemsAsync(Movies, null, true, CancellationToken.None))[0].Versions.Single();

        Assert.That(version.AudioTracks.Single().Language, Is.EqualTo("en"));
        Assert.That(version.Subtitles.Select(s => s.Language), Is.EqualTo(["fra", "German"]));
    }
}
