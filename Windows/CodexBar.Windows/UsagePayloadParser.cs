using System.Globalization;
using System.Text.Json;

namespace CodexBar.Windows;

internal sealed class UsagePayloadRow
{
    public string Provider { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Account { get; init; } = "";
    public string Source { get; init; } = "";
    public string Status { get; init; } = "";
    public string Primary { get; init; } = "";
    public string Secondary { get; init; } = "";
    public string Tertiary { get; init; } = "";
    public string Credits { get; init; } = "";
    public string Cost { get; init; } = "";
    public string Updated { get; init; } = "";
    public string Error { get; init; } = "";
    public IReadOnlyList<UsagePayloadMetric> Metrics { get; init; } = [];
}

internal sealed class UsagePayloadMetric
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public double UsedPercent { get; init; }
    public string Reset { get; init; } = "";
    public string Detail { get; init; } = "";

    public double RemainingPercent => Math.Clamp(100 - UsedPercent, 0, 100);
}

internal static class UsagePayloadParser
{
    public static IReadOnlyList<UsagePayloadRow> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var rows = new List<UsagePayloadRow>();

        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    rows.Add(ParseProvider(item));
                }
            }
        }
        else if (document.RootElement.ValueKind == JsonValueKind.Object)
        {
            rows.Add(ParseProvider(document.RootElement));
        }

        return rows;
    }

    private static UsagePayloadRow ParseProvider(JsonElement item)
    {
        var usage = ReadObject(item, "usage");
        var credits = ReadObject(item, "credits");
        var providerCost = usage.HasValue ? ReadObject(usage.Value, "providerCost") : null;
        var error = ReadObject(item, "error");
        var provider = ReadString(item, "provider") ?? "unknown";

        return new UsagePayloadRow
        {
            Provider = provider,
            DisplayName = ProviderCatalog.DisplayNameFor(provider),
            Account = ReadString(item, "account") ?? ReadNestedString(usage, "identity", "accountEmail") ?? "",
            Source = ReadString(item, "source") ?? "",
            Status = FormatStatus(ReadObject(item, "status")),
            Primary = usage.HasValue ? FormatWindow(ReadObject(usage.Value, "primary")) : "",
            Secondary = usage.HasValue ? FormatWindow(ReadObject(usage.Value, "secondary")) : "",
            Tertiary = usage.HasValue ? FormatWindow(ReadObject(usage.Value, "tertiary")) : "",
            Credits = FormatCredits(credits),
            Cost = FormatCost(providerCost),
            Updated = ReadString(usage, "updatedAt") ?? ReadString(credits, "updatedAt") ?? "",
            Error = ReadString(error, "message") ?? "",
            Metrics = usage.HasValue ? BuildMetrics(usage.Value) : [],
        };
    }

    private static IReadOnlyList<UsagePayloadMetric> BuildMetrics(JsonElement usage)
    {
        var metrics = new List<UsagePayloadMetric>();
        AddMetric(metrics, "primary", "Primary", ReadObject(usage, "primary"));
        AddMetric(metrics, "secondary", "Secondary", ReadObject(usage, "secondary"));
        AddMetric(metrics, "tertiary", "Tertiary", ReadObject(usage, "tertiary"));

        if (usage.TryGetProperty("extraRateWindows", out var extraRateWindows) &&
            extraRateWindows.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in extraRateWindows.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var id = ReadString(item, "id") ?? $"extra-{metrics.Count}";
                var title = ReadString(item, "title") ?? "Extra";
                AddMetric(metrics, id, title, ReadObject(item, "window"));
            }
        }

        return metrics;
    }

    private static void AddMetric(
        List<UsagePayloadMetric> metrics,
        string id,
        string title,
        JsonElement? window)
    {
        var used = ReadDouble(window, "usedPercent");
        if (!used.HasValue)
        {
            return;
        }

        var reset = ReadString(window, "resetDescription") ?? ReadString(window, "resetsAt") ?? "";
        metrics.Add(new UsagePayloadMetric
        {
            Id = id,
            Title = title,
            UsedPercent = Math.Clamp(used.Value, 0, 100),
            Reset = reset,
            Detail = FormatWindow(window),
        });
    }

    private static string FormatStatus(JsonElement? status)
    {
        if (!status.HasValue)
        {
            return "";
        }

        var indicator = ReadString(status, "indicator") ?? "";
        var description = ReadString(status, "description") ?? "";
        if (string.IsNullOrWhiteSpace(description))
        {
            return indicator;
        }

        return string.IsNullOrWhiteSpace(indicator) ? description : $"{indicator}: {description}";
    }

    private static string FormatWindow(JsonElement? window)
    {
        if (!window.HasValue)
        {
            return "";
        }

        var used = ReadDouble(window, "usedPercent");
        var reset = ReadString(window, "resetDescription") ?? ReadString(window, "resetsAt");
        var label = used.HasValue
            ? $"{used.Value:0.#}% used / {Math.Max(0, 100 - used.Value):0.#}% left"
            : "";

        if (!string.IsNullOrWhiteSpace(reset))
        {
            return string.IsNullOrWhiteSpace(label) ? reset : $"{label} - {reset}";
        }

        return label;
    }

    private static string FormatCredits(JsonElement? credits)
    {
        var remaining = ReadDouble(credits, "remaining");
        return remaining.HasValue ? $"{remaining.Value:0.##} left" : "";
    }

    private static string FormatCost(JsonElement? cost)
    {
        var used = ReadDouble(cost, "used");
        var limit = ReadDouble(cost, "limit");
        var currency = ReadString(cost, "currencyCode") ?? "";
        if (used.HasValue && limit.HasValue)
        {
            return $"{used.Value:0.##}/{limit.Value:0.##} {currency}".Trim();
        }

        return used.HasValue ? $"{used.Value:0.##} {currency}".Trim() : "";
    }

    private static JsonElement? ReadObject(JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(name, out var child) &&
            child.ValueKind == JsonValueKind.Object
                ? child
                : null;
    }

    private static string? ReadNestedString(JsonElement? element, string objectName, string name)
    {
        if (!element.HasValue)
        {
            return null;
        }

        return ReadString(ReadObject(element.Value, objectName), name);
    }

    private static string? ReadString(JsonElement? element, string name)
    {
        if (!element.HasValue ||
            element.Value.ValueKind != JsonValueKind.Object ||
            !element.Value.TryGetProperty(name, out var child) ||
            child.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return child.ValueKind == JsonValueKind.String
            ? child.GetString()
            : child.ToString();
    }

    private static double? ReadDouble(JsonElement? element, string name)
    {
        if (!element.HasValue ||
            element.Value.ValueKind != JsonValueKind.Object ||
            !element.Value.TryGetProperty(name, out var child) ||
            child.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (child.ValueKind == JsonValueKind.Number && child.TryGetDouble(out var value))
        {
            return value;
        }

        return double.TryParse(child.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            ? value
            : null;
    }
}
