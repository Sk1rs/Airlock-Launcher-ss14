using System;
using System.Collections.Generic;
using System.Text.Json;
using Serilog;
using SS14.Launcher.Models.Data;

namespace SS14.Launcher.Models;

/// <summary>
/// Where notes are kept: the launcher's config in practice, memory in tests.
/// </summary>
public interface IServerNoteStore
{
    string NotesJson { get; set; }
}

/// <summary>
/// Notes stored as one config value.
/// </summary>
public sealed class CVarServerNoteStore(DataManager cfg) : IServerNoteStore
{
    public string NotesJson
    {
        get => cfg.GetCVar(CVars.ServerNotes);
        set
        {
            cfg.SetCVar(CVars.ServerNotes, value);
            cfg.CommitConfig();
        }
    }
}

/// <summary>
/// The player's own note on a server: why it is in their favourites at all.
/// </summary>
/// <remarks>
/// A favourites list of a dozen servers with names like "Station 14 RU #3" tells you nothing about
/// which one has the good chemistry, which one bans for swearing, and which one you are still on a
/// timeout from. Notes are kept in the launcher's config rather than the favourites table because
/// they are personal scribbles, not part of the server entry, and this needs no schema change.
/// </remarks>
public sealed class ServerNotes(IServerNoteStore store)
{
    /// <summary>
    /// Longest note kept. Room for a sentence, not an essay that would wreck the list layout.
    /// </summary>
    public const int MaxLength = 120;

    private Dictionary<string, string> Notes
    {
        get
        {
            try
            {
                var stored = JsonSerializer.Deserialize<Dictionary<string, string>>(store.NotesJson);

                // Rebuilt rather than used as-is: a deserialized dictionary compares keys
                // case-sensitively, and then the same server saved as ss14://X and SS14://x
                // would carry two different notes.
                return stored == null
                    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(stored, StringComparer.OrdinalIgnoreCase);
            }
            catch (JsonException e)
            {
                Log.Warning(e, "Server notes were unreadable, starting over");

                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    public string Get(string address) => Notes.GetValueOrDefault(address, "");

    public void Set(string address, string note)
    {
        var notes = new Dictionary<string, string>(Notes, StringComparer.OrdinalIgnoreCase);

        note = note.Trim();
        if (note.Length > MaxLength)
            note = note[..MaxLength];

        if (note.Length == 0)
            notes.Remove(address);
        else
            notes[address] = note;

        store.NotesJson = JsonSerializer.Serialize(notes);
    }
}
