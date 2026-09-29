using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Serilog;

namespace SS14.Launcher.Models;

/// <summary>
/// Exchange rates for the currency converter, straight from the Central Bank of Russia.
/// </summary>
/// <remarks>
/// The CBR publishes one rate per currency against the ruble, once per business day, so every
/// cross rate here (say EUR to PLN) is calculated through the ruble.
/// </remarks>
public sealed class CurrencyRates
{
    /// <summary>
    /// Everything is quoted against this one, since that is what the CBR publishes.
    /// </summary>
    public const string BaseCurrency = "RUB";

    /// <summary>
    /// Currencies offered in the UI, in display order.
    /// </summary>
    public static readonly ImmutableArray<string> Currencies = ["RUB", "USD", "EUR", "PLN"];

    private const string CbrDailyUrl = "https://www.cbr.ru/scripts/XML_daily.asp";

    private readonly HttpClient _http;

    /// <summary>
    /// How many rubles one unit of each currency is worth. Always contains <see cref="BaseCurrency"/>.
    /// </summary>
    public ImmutableDictionary<string, decimal> RublesPerUnit { get; private set; } =
        ImmutableDictionary<string, decimal>.Empty.Add(BaseCurrency, 1m);

    /// <summary>
    /// The day the CBR published these rates for, null if we have never got any.
    /// </summary>
    public DateTime? RatesDate { get; private set; }

    public bool HasRates => RublesPerUnit.Count > 1;

    public bool Refreshing { get; private set; }

    public event Action? Changed;

    public CurrencyRates()
    {
        // Deliberately not the launcher's shared HttpClient: that one attaches this install's
        // fingerprint header to every request it makes, and the Central Bank has no business
        // receiving it.
        _http = HappyEyeballsHttp.CreateHttpClient();
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SS14.Launcher", "0.36.1"));
        _http.Timeout = TimeSpan.FromSeconds(20);
    }

    private static string CachePath => Path.Combine(LauncherPaths.DirLocalData, "currency_rates.json");

    /// <summary>
    /// Loads the rates saved by the last successful fetch, so the tab isn't empty while offline.
    /// </summary>
    public void Initialize()
    {
        try
        {
            if (!File.Exists(CachePath))
                return;

            var cached = JsonSerializer.Deserialize<CachedRates>(File.ReadAllText(CachePath));
            if (cached?.Rates == null || cached.Rates.Count == 0)
                return;

            Apply(cached.Date, cached.Rates);
        }
        catch (Exception e)
        {
            Log.Warning(e, "Failed to read cached currency rates");
        }
    }

    /// <summary>
    /// Fetches today's rates. Does nothing if a fetch is already running.
    /// </summary>
    public async Task RefreshAsync(CancellationToken cancel = default)
    {
        if (Refreshing)
            return;

        Refreshing = true;
        Changed?.Invoke();

        try
        {
            using var response = await _http.GetAsync(CbrDailyUrl, cancel);
            response.EnsureSuccessStatusCode();

            // The document is windows-1251, which .NET has no built-in decoder for. Everything we
            // actually read out of it (currency codes, numbers, the date) is plain ASCII, and
            // Latin1 maps bytes to chars one to one, so the Russian names come out as mojibake and
            // nothing else is harmed. XDocument ignores the encoding declaration when it is handed
            // already-decoded text.
            await using var stream = await response.Content.ReadAsStreamAsync(cancel);
            using var reader = new StreamReader(stream, Encoding.Latin1);

            var (date, rates) = ParseCbrDaily(await reader.ReadToEndAsync(cancel));
            if (rates.Count == 0)
                throw new InvalidDataException("No known currencies in the CBR response");

            Apply(date, rates);
            Save(date, rates);
        }
        catch (Exception e)
        {
            Log.Warning(e, "Failed to fetch currency rates from the CBR");
        }
        finally
        {
            Refreshing = false;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Pulls the rates we care about out of the CBR's daily XML.
    /// </summary>
    public static (DateTime? Date, Dictionary<string, decimal> Rates) ParseCbrDaily(string xml)
    {
        var document = XDocument.Parse(xml);
        var root = document.Root ?? throw new InvalidDataException("Empty CBR document");

        DateTime? date = null;
        if (DateTime.TryParseExact(root.Attribute("Date")?.Value, "dd.MM.yyyy",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
        {
            date = parsedDate;
        }

        var rates = new Dictionary<string, decimal>();

        foreach (var valute in root.Elements("Valute"))
        {
            var code = valute.Element("CharCode")?.Value;
            if (code == null || !Currencies.Contains(code))
                continue;

            // VunitRate is the rate for a single unit; Value is for Nominal units of it.
            if (TryParseNumber(valute.Element("VunitRate")?.Value, out var perUnit))
            {
                rates[code] = perUnit;
                continue;
            }

            if (TryParseNumber(valute.Element("Value")?.Value, out var value)
                && TryParseNumber(valute.Element("Nominal")?.Value, out var nominal)
                && nominal != 0)
            {
                rates[code] = value / nominal;
            }
        }

        return (date, rates);
    }

    /// <summary>
    /// Converts between two currencies through the ruble.
    /// </summary>
    /// <returns>Null if we don't have a rate for one of them.</returns>
    public decimal? Convert(decimal amount, string from, string to)
    {
        return Convert(amount, from, to, RublesPerUnit);
    }

    /// <summary>
    /// Converts between two currencies using the given ruble rates.
    /// The base currency does not need to be in there, it is worth one of itself.
    /// </summary>
    /// <returns>Null if there is no rate for one of them.</returns>
    public static decimal? Convert(
        decimal amount,
        string from,
        string to,
        IReadOnlyDictionary<string, decimal> rublesPerUnit)
    {
        if (RateFor(from, rublesPerUnit) is not { } fromRate
            || RateFor(to, rublesPerUnit) is not { } toRate
            || toRate == 0)
        {
            return null;
        }

        return amount * fromRate / toRate;
    }

    private static decimal? RateFor(string currency, IReadOnlyDictionary<string, decimal> rublesPerUnit)
    {
        if (currency == BaseCurrency)
            return 1m;

        return rublesPerUnit.TryGetValue(currency, out var rate) ? rate : null;
    }

    private void Apply(DateTime? date, Dictionary<string, decimal> rates)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, decimal>();
        builder.Add(BaseCurrency, 1m);

        foreach (var (code, rate) in rates)
        {
            if (code != BaseCurrency)
                builder[code] = rate;
        }

        RublesPerUnit = builder.ToImmutable();
        RatesDate = date;

        Changed?.Invoke();
    }

    private static void Save(DateTime? date, Dictionary<string, decimal> rates)
    {
        try
        {
            File.WriteAllText(CachePath, JsonSerializer.Serialize(new CachedRates
            {
                Date = date,
                Rates = rates,
            }));
        }
        catch (Exception e)
        {
            Log.Warning(e, "Failed to cache currency rates");
        }
    }

    /// <summary>
    /// The CBR writes numbers the Russian way, with a comma for the decimal point.
    /// </summary>
    private static bool TryParseNumber(string? text, out decimal value)
    {
        value = 0;

        return text != null
               && decimal.TryParse(text.Replace(',', '.'), NumberStyles.Number,
                   CultureInfo.InvariantCulture, out value);
    }

    private sealed class CachedRates
    {
        [JsonPropertyName("date")] public DateTime? Date { get; set; }
        [JsonPropertyName("rates")] public Dictionary<string, decimal>? Rates { get; set; }
    }
}
