using System;
using System.Globalization;

namespace SS14.Launcher;

/// <summary>
/// The random one-liners shown on the connecting/loading screen.
/// </summary>
/// <remarks>
/// These used to be pulled from the remote <c>info.json</c>, which meant whoever runs that server
/// decides what your launcher says. They live here instead, so edit this file to change them.
/// </remarks>
public static class LoadingMessages
{
    private static readonly Random Random = new();

    private static readonly string[] Russian =
    [
        "Развожу монтажную пену…",
        "Ищу шкафчик с инструментами…",
        "Проверяю, кто съел весь бекон…",
        "Прошу ИИ открыть дверь…",
        "Заполняю форму на замену лампочки…",
        "Ремонтирую то, что ещё не сломалось…",
        "Кручу трубы в атмосе…",
        "Готовлю сингулярность к запуску…",
        "Считаю патроны в оружейке…",
        "Пишу заявление в отдел кадров…",
        "Согреваю кофе на плазменном огне…",
        "Настраиваю телекоммуникации (опять)…",
        "Уговариваю уборщика вымыть мостик…",
        "Проверяю скафандр на дырки…",
        "Ищу гаечный ключ, который кто-то унёс…",
        "Объявляю зелёный уровень тревоги…",
    ];

    private static readonly string[] English =
    [
        "Mixing up some foam…",
        "Looking for the toolbox…",
        "Finding out who ate all the bacon…",
        "Asking the AI to open the door…",
        "Filing paperwork for a light bulb…",
        "Fixing things that aren't broken yet…",
        "Rotating pipes in atmos…",
        "Spinning up the singularity…",
        "Counting rounds in the armory…",
        "Submitting a form to HR…",
        "Reheating coffee with plasma…",
        "Reconfiguring telecomms (again)…",
        "Convincing the janitor to mop the bridge…",
        "Checking the hardsuit for holes…",
        "Looking for the wrench somebody walked off with…",
        "Declaring a green alert…",
    ];

    /// <summary>
    /// Gets a random message in the launcher's current UI language, falling back to English.
    /// </summary>
    public static string Get()
    {
        var messages = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru" ? Russian : English;

        return messages[Random.Next(messages.Length)];
    }
}
