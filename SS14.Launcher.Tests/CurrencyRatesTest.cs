using System;
using NUnit.Framework;
using SS14.Launcher.Models;

namespace SS14.Launcher.Tests;

[TestFixture]
[TestOf(typeof(CurrencyRates))]
public sealed class CurrencyRatesTest
{
    // Trimmed down copy of a real response, declared encoding and comma decimals included.
    private const string SampleXml =
        """
        <?xml version="1.0" encoding="windows-1251"?>
        <ValCurs Date="09.09.2026" name="Foreign Currency Market">
            <Valute ID="R01235"><NumCode>840</NumCode><CharCode>USD</CharCode><Nominal>1</Nominal><Name>Dollar</Name><Value>86,4730</Value><VunitRate>86,473</VunitRate></Valute>
            <Valute ID="R01239"><NumCode>978</NumCode><CharCode>EUR</CharCode><Nominal>1</Nominal><Name>Euro</Name><Value>100,4989</Value><VunitRate>100,4989</VunitRate></Valute>
            <Valute ID="R01565"><NumCode>985</NumCode><CharCode>PLN</CharCode><Nominal>1</Nominal><Name>Zloty</Name><Value>23,2661</Value><VunitRate>23,2661</VunitRate></Valute>
            <Valute ID="R01720"><NumCode>934</NumCode><CharCode>TMT</CharCode><Nominal>1</Nominal><Name>Manat</Name><Value>24,7066</Value><VunitRate>24,7066</VunitRate></Valute>
        </ValCurs>
        """;

    // Currencies quoted per 10 or 100 units have to be divided down by Nominal.
    private const string NominalXml =
        """
        <?xml version="1.0" encoding="windows-1251"?>
        <ValCurs Date="09.09.2026" name="Foreign Currency Market">
            <Valute ID="R01565"><NumCode>985</NumCode><CharCode>PLN</CharCode><Nominal>10</Nominal><Name>Zloty</Name><Value>232,661</Value></Valute>
        </ValCurs>
        """;

    [Test]
    public void TestParseDailyRates()
    {
        var (date, rates) = CurrencyRates.ParseCbrDaily(SampleXml);

        Assert.That(date, Is.EqualTo(new DateTime(2026, 9, 9)));
        Assert.That(rates, Has.Count.EqualTo(3), "currencies outside the list should be skipped");
        Assert.That(rates["USD"], Is.EqualTo(86.473m));
        Assert.That(rates["EUR"], Is.EqualTo(100.4989m));
        Assert.That(rates["PLN"], Is.EqualTo(23.2661m));
    }

    [Test]
    public void TestParseNominal()
    {
        var (_, rates) = CurrencyRates.ParseCbrDaily(NominalXml);

        Assert.That(rates["PLN"], Is.EqualTo(23.2661m));
    }

    [Test]
    public void TestCrossRate()
    {
        var (_, rates) = CurrencyRates.ParseCbrDaily(SampleXml);

        // 100 euro is 100 * 100.4989 rubles, at 86.473 rubles to the dollar.
        Assert.That(CurrencyRates.Convert(100m, "EUR", "USD", rates),
            Is.EqualTo(100m * 100.4989m / 86.473m));
        Assert.That(CurrencyRates.Convert(1m, "USD", "RUB", rates), Is.EqualTo(86.473m));
        Assert.That(CurrencyRates.Convert(1m, "RUB", "RUB", rates), Is.EqualTo(1m),
            "the ruble does not need a rate of its own");
        Assert.That(CurrencyRates.Convert(1m, "USD", "GBP", rates), Is.Null,
            "unknown currencies give no result");
    }
}
