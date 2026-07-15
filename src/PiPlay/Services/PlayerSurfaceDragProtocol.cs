using System.Text.Json;

namespace PiPlay.Services;

/// <summary>
/// Closed wire contract for a passive YouTube-player drag gesture. The injected page script can
/// request exactly one capability: begin moving the owning Popout Player. A per-window nonce,
/// per-document token, and exact current YouTube source prevent delayed navigation messages from
/// widening that capability (Q-3/Q-7/Q-8).
/// </summary>
internal static class PlayerSurfaceDragProtocol
{
    internal const int Version = 1;
    internal const string Channel = "piplay.window";
    internal const string TypeDragStart = "dragStart";

    internal static bool TryParse(
        string? json,
        string? messageSource,
        string? currentSource,
        string expectedNonce,
        string? expectedDocumentToken)
    {
        if (string.IsNullOrWhiteSpace(json) || string.IsNullOrEmpty(expectedNonce)
            || string.IsNullOrEmpty(expectedDocumentToken)) return false;
        if (!IsCurrentYouTubeDocument(messageSource, currentSource)) return false;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !HasExactSchema(root)) return false;

            return TryReadString(root, "channel", out var channel) && channel == Channel
                && root.TryGetProperty("v", out var version)
                && version.ValueKind == JsonValueKind.Number
                && version.TryGetInt32(out var parsedVersion)
                && parsedVersion == Version
                && TryReadString(root, "type", out var type) && type == TypeDragStart
                && TryReadString(root, "nonce", out var nonce)
                && string.Equals(nonce, expectedNonce, StringComparison.Ordinal)
                && TryReadString(root, "documentToken", out var documentToken)
                && string.Equals(documentToken, expectedDocumentToken, StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool HasExactSchema(JsonElement root)
    {
        var propertyCount = 0;
        foreach (var property in root.EnumerateObject())
        {
            propertyCount++;
            if (property.Name is not ("channel" or "v" or "type" or "nonce" or "documentToken"))
                return false;
        }

        // The required-field parser below rejects a missing member; this count also rejects
        // duplicate members so the capability contract has one unambiguous representation.
        return propertyCount == 5;
    }

    internal static bool IsTrustedYouTubeSource(string? source)
    {
        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri)) return false;
        return uri.Scheme == Uri.UriSchemeHttps && NavigationPolicy.IsYouTubeHost(uri.Host.ToLowerInvariant());
    }

    /// <summary>
    /// WebView2 can deliver a queued message after the top-level document has navigated. Host-only
    /// checks do not distinguish two watch documents on youtube.com, so bind every page capability
    /// to the current scheme/host/port/path/query. Fragments are intentionally ignored because a
    /// hash change does not replace the document that owns the injected nonce-bearing script.
    /// </summary>
    internal static bool IsCurrentYouTubeDocument(string? messageSource, string? currentSource)
    {
        if (!Uri.TryCreate(messageSource, UriKind.Absolute, out var messageUri)
            || !Uri.TryCreate(currentSource, UriKind.Absolute, out var currentUri)
            || !IsTrustedYouTubeSource(messageSource)
            || !IsTrustedYouTubeSource(currentSource))
        {
            return false;
        }

        return string.Equals(messageUri.Scheme, currentUri.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(messageUri.IdnHost, currentUri.IdnHost, StringComparison.OrdinalIgnoreCase)
            && messageUri.Port == currentUri.Port
            && string.Equals(messageUri.AbsolutePath, currentUri.AbsolutePath, StringComparison.Ordinal)
            && string.Equals(messageUri.Query, currentUri.Query, StringComparison.Ordinal);
    }

    private static bool TryReadString(JsonElement root, string name, out string? value)
    {
        value = null;
        if (!root.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
            return false;
        value = property.GetString();
        return value is not null;
    }
}
