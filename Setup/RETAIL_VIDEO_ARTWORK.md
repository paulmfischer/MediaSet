# Retail video titles and collection artwork

Movie lookup retains the original UPC product title in diagnostic logs and derives
a search query by removing recognized distributor prefixes and retail annotations.
Distributor cleanup uses a fixed list, so missing or inaccurate brand metadata
does not prevent known prefixes from being removed. Unknown brand values are not
used to remove arbitrary title words. Meaningful parentheses, brackets, punctuation
and numbers survive; annotations are removed only when they begin with recognized
format, disc-count or edition text.

Manufacturer-only product titles do not reach TMDB. Image lookup then tries the
stored entity title, retaining the existing full-title and available date checks.
Search results and detail titles must agree; the first search result is never
accepted simply because it was returned first.

Trilogies and film collections search TMDB's collection catalog and fetch the
matched collection's own poster. Matching requires the full collection base name;
trilogies and quadrilogies also require three or four catalog parts respectively.
Explicit TV seasons retain TV routing. An unspecified "Complete Collection" can
refer to either a film collection or a TV series, so both catalogs are checked
without falling back to an individual film.

Retail bundles, double features and other packages without a corresponding catalog
collection remain unresolved. Missing collection artwork never falls back to a
member film's poster. If UPC package metadata describes a collection or bundle
that conflicts with the stored film title, background image lookup returns an
ambiguous-match outcome and stops the stored-film fallback. Correct the package
metadata or supply artwork manually. Catalog collection artwork does not verify
physical packaging, region or edition.

Structured diagnostics include `RawTitle`, `NormalizedQuery`, `MatchRationale`,
provider identity and collection match counts. Unresolved bundles explicitly log
the need for manual artwork.

## Production investigation for issue #610

Read-only inspection on October 23, 2026 found the running `mediaset-api` container
using OCI revision `298036e8a3b656ac4320bcee2f4f754090fa2b7a`, version `4.0.0`.
Its image was created October 14 at 12:39 UTC and the container started at 14:38 UTC.
This differs from local investigation revision `62658af` cited in the issue.
Regression tests reproduce the observed product inputs with absent/inaccurate
brands against the current source. No deployment, production retry, database or
container changes are part of this fix.
