using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using ReactiveUI;
using Splat;
using SS14.Launcher.Localization;
using SS14.Launcher.Models;
using SS14.Launcher.Utility;

namespace SS14.Launcher.ViewModels.MainWindowTabs;

/// <summary>
/// Exchange rates between the ruble, dollar, euro and zloty, plus a converter.
/// </summary>
public sealed class CurrencyTabViewModel : MainWindowTabViewModel
{
    private readonly LocalizationManager _loc = LocalizationManager.Instance;
    private readonly CurrencyRates _rates;

    private string _amount = "1000";
    private string _fromCurrency = CurrencyRates.BaseCurrency;
    private string _toCurrency = "USD";

    public override string Name => _loc.GetString("tab-currency-title");

    public string[] Currencies { get; } = CurrencyRates.Currencies.ToArray();

    /// <summary>
    /// One row per currency, each listing what a single unit of it is worth in the others.
    /// </summary>
    public ObservableCollection<CurrencyRateRowViewModel> Rows { get; } = new();

    public CurrencyTabViewModel()
    {
        _rates = Locator.Current.GetRequiredService<CurrencyRates>();
        _rates.Changed += OnRatesChanged;

        UpdateRows();
    }

    public string Amount
    {
        get => _amount;
        set
        {
            this.RaiseAndSetIfChanged(ref _amount, value);
            this.RaisePropertyChanged(nameof(Result));
        }
    }

    public string FromCurrency
    {
        get => _fromCurrency;
        set
        {
            this.RaiseAndSetIfChanged(ref _fromCurrency, value);
            this.RaisePropertyChanged(nameof(Result));
        }
    }

    public string ToCurrency
    {
        get => _toCurrency;
        set
        {
            this.RaiseAndSetIfChanged(ref _toCurrency, value);
            this.RaisePropertyChanged(nameof(Result));
        }
    }

    public string Result
    {
        get
        {
            if (!TryParseAmount(Amount, out var amount))
                return _loc.GetString("currency-result-invalid");

            if (_rates.Convert(amount, FromCurrency, ToCurrency) is not { } converted)
                return _loc.GetString("currency-result-no-rates");

            return $"{Format(converted)} {ToCurrency}";
        }
    }

    public string UpdatedText
    {
        get
        {
            if (_rates.Refreshing)
                return _loc.GetString("currency-updating");

            if (_rates.RatesDate is not { } date)
                return _loc.GetString("currency-not-loaded");

            return _loc.GetString("currency-updated", ("date", date.ToString("d", CultureInfo.CurrentUICulture)));
        }
    }

    public bool CanRefresh => !_rates.Refreshing;

    public override void Selected()
    {
        // The CBR publishes once a business day, so only go looking if what we have isn't from today.
        if (_rates.RatesDate?.Date != DateTime.Now.Date)
            RefreshPressed();
    }

    public void RefreshPressed()
    {
        _ = _rates.RefreshAsync();
    }

    public void SwapPressed()
    {
        (FromCurrency, ToCurrency) = (ToCurrency, FromCurrency);
    }

    private void OnRatesChanged()
    {
        UpdateRows();

        this.RaisePropertyChanged(nameof(Result));
        this.RaisePropertyChanged(nameof(UpdatedText));
        this.RaisePropertyChanged(nameof(CanRefresh));
    }

    private void UpdateRows()
    {
        Rows.Clear();

        if (!_rates.HasRates)
            return;

        foreach (var from in CurrencyRates.Currencies)
        {
            var values = CurrencyRates.Currencies
                .Where(to => to != from)
                .Select(to => _rates.Convert(1m, from, to) is { } rate
                    ? $"{Format(rate)} {to}"
                    : $"- {to}");

            Rows.Add(new CurrencyRateRowViewModel($"1 {from}", string.Join("   ", values)));
        }
    }

    /// <summary>
    /// Accepts both decimal separators, since a Russian keyboard layout and a numpad disagree on it.
    /// </summary>
    private static bool TryParseAmount(string text, out decimal amount)
    {
        return decimal.TryParse(
            text.Replace(',', '.').Trim(),
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out amount);
    }

    private static string Format(decimal value)
    {
        // Big numbers do not need four decimals, small cross rates very much do.
        var rounded = Math.Round(value, value >= 100 ? 2 : 4);

        return rounded.ToString("0.####", CultureInfo.CurrentUICulture);
    }
}

/// <summary>
/// One line of the rate table.
/// </summary>
public sealed class CurrencyRateRowViewModel(string unit, string values)
{
    public string Unit { get; } = unit;
    public string Values { get; } = values;
}
