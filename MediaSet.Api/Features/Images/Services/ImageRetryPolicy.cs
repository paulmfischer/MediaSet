using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using MediaSet.Api.Features.Images.Models;
using MediaSet.Api.Shared.Attributes;
using MediaSet.Api.Shared.Models;
using MongoDB.Driver;

namespace MediaSet.Api.Features.Images.Services;

public static class ImageRetryPolicy
{
    public static string Fingerprint(IEntity entity)
    {
        var values = entity.GetType().GetProperties()
            .Where(p => p.GetCustomAttribute<LookupIdentifierAttribute>() != null || p.Name == nameof(IEntity.ImageUrl))
            .OrderBy(p => p.Name)
            .Select(p => $"{p.Name}:{p.GetValue(entity)}")
            .Concat(ImageCandidateMatcher.SearchContext(entity).OrderBy(p => p.Key)
                .Select(p => $"{p.Key}:{p.Value}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", values))));
    }

    public static ImageLookup Complete(IEntity entity, ImageLookupResult result,
        BackgroundImageLookupConfiguration config, DateTime now)
    {
        var previous = entity.ImageLookup;
        var outcome = result.Success ? ImageLookupOutcome.Success : result.Outcome
            ?? (result.PermanentFailure ? ImageLookupOutcome.MissingMetadata : ImageLookupOutcome.TransientFailure);
        var attempts = Math.Max(previous?.AttemptCount ?? 0, previous == null ? 0 : 1) + 1;
        var delay = Math.Min(config.RetryMaxDelayMinutes,
            config.RetryInitialDelayMinutes * Math.Pow(2, Math.Min(attempts - 1, 30)));
        return new ImageLookup
        {
            AttemptCount = attempts,
            ConfirmedMatch = result.ConfirmedMatch,
            LastOutcome = outcome,
            NextEligibleAt = IsRetryable(outcome) ? result.RetryAt ?? now.AddMinutes(delay) : null,
            Provider = result.Provider,
            AttemptedStrategy = result.AttemptedStrategy,
            LookupAttemptedAt = now,
            FailureReason = result.Success ? null : result.ErrorMessage,
            PermanentFailure = !IsRetryable(outcome) && outcome != ImageLookupOutcome.Success,
            MetadataFingerprint = Fingerprint(entity),
            StrategyVersion = config.StrategyVersion,
            IsBacklogRecovery = previous != null,
            RequeueOperationId = previous?.RequeueOperationId
        };
    }

    public static FilterDefinition<T> DueFilter<T>(DateTime now) where T : IEntity
    {
        var f = Builders<T>.Filter;
        return f.Eq(e => e.CoverImage, null) & f.Ne(e => e.ImageLookup, null)
            & f.In("ImageLookup.LastOutcome", new[] { ImageLookupOutcome.TransientFailure, ImageLookupOutcome.Queued,
                ImageLookupOutcome.QuotaLimited, ImageLookupOutcome.Unauthorized, ImageLookupOutcome.ProviderUnavailable,
                ImageLookupOutcome.DownloadFailure })
            & f.Ne("ImageLookup.NextEligibleAt", (DateTime?)null)
            & f.Lte("ImageLookup.NextEligibleAt", now);
    }

    public static bool IsRetryable(ImageLookupOutcome? outcome) => outcome is ImageLookupOutcome.TransientFailure
        or ImageLookupOutcome.QuotaLimited or ImageLookupOutcome.Unauthorized or ImageLookupOutcome.ProviderUnavailable
        or ImageLookupOutcome.DownloadFailure;

    public static bool CanRequeue(IEntity entity, BackgroundImageLookupConfiguration config, bool includeTerminal)
    {
        var state = entity.ImageLookup;
        if (entity.CoverImage != null || state == null || state.LastOutcome == ImageLookupOutcome.Success
            || state.LastOutcome == ImageLookupOutcome.Queued
            || (state.LastOutcome == null && string.IsNullOrWhiteSpace(state.FailureReason)))
        {
            return false;
        }
        var terminal = state.PermanentFailure || state.LastOutcome is ImageLookupOutcome.NoMatch
            or ImageLookupOutcome.MissingArtwork or ImageLookupOutcome.MissingMetadata
            or ImageLookupOutcome.UnsupportedStrategy or ImageLookupOutcome.InvalidArtwork;
        return !terminal || includeTerminal && (state.StrategyVersion != config.StrategyVersion
            || state.MetadataFingerprint != Fingerprint(entity));
    }
}
