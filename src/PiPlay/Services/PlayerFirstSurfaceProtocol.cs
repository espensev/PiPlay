using System.Text.Json;

namespace PiPlay.Services;

/// <summary>
/// Focused-overlay page-to-host action contract. The page may ask only for the existing Popout
/// window actions (close, pin, expand, Settings); playback actions remain local to the HTML video.
/// </summary>
internal static class PlayerFirstSurfaceProtocol
{
    internal const int Version = 1;
    internal const string Channel = "piplay.focused";
    internal const string TypeRequest = "request";
    internal const string TypeState = "state";
    internal const string ActionClose = "close";
    internal const string ActionPinToggle = "pinToggle";
    internal const string ActionFullscreenToggle = "fullscreenToggle";
    internal const string ActionSettings = "settings";

    internal static readonly IReadOnlySet<string> AllowedActions = new HashSet<string>(StringComparer.Ordinal)
    {
        ActionClose,
        ActionPinToggle,
        ActionFullscreenToggle,
        ActionSettings,
    };

    internal static bool TryParse(
        string? json,
        string? messageSource,
        string? currentSource,
        string expectedNonce,
        string? expectedDocumentToken,
        out string? action)
    {
        action = null;
        if (string.IsNullOrWhiteSpace(json) || string.IsNullOrEmpty(expectedNonce)
            || string.IsNullOrEmpty(expectedDocumentToken)) return false;
        if (!PlayerSurfaceDragProtocol.IsCurrentYouTubeDocument(messageSource, currentSource)) return false;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !HasExactSchema(root, "action")) return false;

            if (!HasValidEnvelope(root, TypeRequest, expectedNonce, expectedDocumentToken)
                || !ReadString(root, "action", out var parsedAction)
                || parsedAction is null
                || !AllowedActions.Contains(parsedAction))
            {
                return false;
            }

            action = parsedAction;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Parse the capability-free Focused surface state handshake. It keeps native WPF chrome
    /// available until the injected overlay actually exists, and restores it on non-watch pages.
    /// </summary>
    internal static bool TryParseState(
        string? json,
        string? messageSource,
        string? currentSource,
        string expectedNonce,
        string? expectedDocumentToken,
        out bool active)
    {
        active = false;
        if (string.IsNullOrWhiteSpace(json) || string.IsNullOrEmpty(expectedNonce)
            || string.IsNullOrEmpty(expectedDocumentToken)) return false;
        if (!PlayerSurfaceDragProtocol.IsCurrentYouTubeDocument(messageSource, currentSource)) return false;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !HasExactSchema(root, "active")
                || !HasValidEnvelope(root, TypeState, expectedNonce, expectedDocumentToken)
                || !root.TryGetProperty("active", out var activeProperty)
                || activeProperty.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                return false;
            }

            active = activeProperty.GetBoolean();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool HasExactSchema(JsonElement root, string payloadProperty)
    {
        var propertyCount = 0;
        foreach (var property in root.EnumerateObject())
        {
            propertyCount++;
            if (property.Name is "channel" or "v" or "type" or "nonce" or "documentToken") continue;
            if (!string.Equals(property.Name, payloadProperty, StringComparison.Ordinal)) return false;
        }

        // Required-field parsing rejects a missing payload; the count rejects duplicates.
        return propertyCount == 6;
    }

    private static bool HasValidEnvelope(
        JsonElement root,
        string expectedType,
        string expectedNonce,
        string expectedDocumentToken) =>
        ReadString(root, "channel", out var channel) && channel == Channel
        && root.TryGetProperty("v", out var version)
        && version.ValueKind == JsonValueKind.Number
        && version.TryGetInt32(out var parsedVersion)
        && parsedVersion == Version
        && ReadString(root, "type", out var type) && type == expectedType
        && ReadString(root, "nonce", out var nonce)
        && string.Equals(nonce, expectedNonce, StringComparison.Ordinal)
        && ReadString(root, "documentToken", out var documentToken)
        && string.Equals(documentToken, expectedDocumentToken, StringComparison.Ordinal);

    private static bool ReadString(JsonElement root, string name, out string? value)
    {
        value = null;
        if (!root.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
            return false;
        value = property.GetString();
        return value is not null;
    }
}
