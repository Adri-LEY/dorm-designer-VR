using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>What the player asked for.</summary>
public enum VoiceAction
{
    Unknown,
    Place,
    Move,
    Delete,
    Undo,
}

/// <summary>Result of parsing one sentence.</summary>
public sealed class VoiceCommand
{
    public string Transcript;
    public VoiceAction Action;

    /// <summary>Catalogue item named in the sentence ("chair", "red chair"), or null ("this", "this furniture").</summary>
    public FurnitureItem Item;
    public FurnitureSizeOption Size;
    public FurnitureColorOption Color;

    /// <summary>The object word the player used ("chair", "closet"...), for messages.</summary>
    public string SpokenObject;

    /// <summary>True when a specific item was named ("the red chair"), not only a type ("the chair").</summary>
    public bool NamesSpecificItem;

    /// <summary>True when the sentence refers to the pointed object ("this", "that", "it").</summary>
    public bool RefersToPointedObject;

    /// <summary>True when the sentence refers to a pointed place ("here", "there").</summary>
    public bool RefersToPointedPlace;

    /// <summary>Kept for compatibility: object or place reference.</summary>
    public bool IsDeictic => RefersToPointedObject || RefersToPointedPlace;
}

/// <summary>
/// Turns a transcript such as "place a red chair here", "move this furniture here", "delete this" or "undo"
/// into a <see cref="VoiceCommand"/> by matching words against the furniture catalogue. No cloud intent training
/// needed: Wit.ai only provides the text, and the meaning is decided here so the team can extend it.
/// </summary>
public static class VoiceCommandParser
{
    // ------------------------------------------------------------------ Vocabulary (extend freely)

    private static readonly HashSet<string> PlaceVerbs = new HashSet<string>
    {
        "place", "add", "spawn", "drop", "install", "position", "create",
    };

    /// <summary>Verbs that mean "move" when they refer to an existing object ("put this there"), "place" otherwise ("put a chair here").</summary>
    private static readonly HashSet<string> PutVerbs = new HashSet<string>
    {
        "put", "bring", "set",
    };

    private static readonly HashSet<string> MoveVerbs = new HashSet<string>
    {
        "move", "shift", "relocate", "drag", "slide", "push", "pull", "displace",
    };

    private static readonly HashSet<string> DeleteVerbs = new HashSet<string>
    {
        "delete", "remove", "erase", "destroy", "trash", "discard", "bin",
    };

    private static readonly string[] DeletePhrases =
    {
        "get rid of", "throw away", "throw out", "take away", "take out",
    };

    private static readonly string[] UndoPhrases =
    {
        "undo", "cancel that", "cancel the last", "go back", "revert", "take that back", "put it back", "oops",
    };

    // Multi-word ways to ask for a new item, checked on the whole sentence.
    private static readonly string[] PlacePhrases =
    {
        "i want", "i would like", "i'd like", "give me", "can i have", "could i have", "let me have",
    };

    /// <summary>Words pointing at an object ("this furniture", "that", "it").</summary>
    private static readonly HashSet<string> ObjectDeictics = new HashSet<string>
    {
        "this", "that", "it", "these", "those",
    };

    /// <summary>Words pointing at a place.</summary>
    private static readonly HashSet<string> PlaceDeictics = new HashSet<string>
    {
        "here", "there",
    };

    /// <summary>Generic words for "a furniture" (no specific type).</summary>
    private static readonly HashSet<string> GenericObjectWords = new HashSet<string>
    {
        "furniture", "thing", "object", "item", "piece", "one",
    };

    /// <summary>
    /// Words speech recognition often returns instead of ours ("share" for "chair"...). Applied before matching.
    /// Extend it with what you see in the [Voice] logs.
    /// </summary>
    private static readonly Dictionary<string, string> SoundAlikes = new Dictionary<string, string>
    {
        { "share", "chair" }, { "cheer", "chair" }, { "chare", "chair" }, { "cher", "chair" }, { "chai", "chair" }, { "shair", "chair" },
        { "bad", "bed" }, { "bet", "bed" }, { "bid", "bed" }, { "bud", "bed" },
        { "disk", "desk" }, { "dusk", "desk" }, { "desc", "desk" }, { "deck", "desk" },
        { "wardrop", "wardrobe" }, { "wardroom", "wardrobe" },
        { "closed", "closet" },
        // "plays" is singularised to "play" before this table is applied.
        { "play", "place" }, { "plaice", "place" }, { "please", "place" }, { "blaze", "place" }, { "lace", "place" },
        { "palace", "place" }, { "plate", "place" }, { "plus", "place" },
        { "hear", "here" }, { "hair", "here" },
        { "delight", "delete" }, { "deleted", "delete" }, { "delite", "delete" }, { "dilute", "delete" },
        { "moved", "move" }, { "removed", "remove" },
        { "undue", "undo" }, { "undone", "undo" },
        { "furnitures", "furniture" }, { "furnishing", "furniture" },
    };

    /// <summary>Two-word mishearings merged into one word ("war drop" -> "wardrobe").</summary>
    private static readonly Dictionary<string, string> SoundAlikePairs = new Dictionary<string, string>
    {
        { "war drop", "wardrobe" }, { "war robe", "wardrobe" }, { "ward robe", "wardrobe" }, { "word robe", "wardrobe" },
        { "war drobe", "wardrobe" }, { "clothes it", "closet" }, { "close it", "closet" },
        { "the lead", "delete" }, { "de lead", "delete" }, { "and do", "undo" }, { "un do", "undo" }, { "on do", "undo" },
    };

    /// <summary>Extra words for each category (lower case). Extend freely.</summary>
    private static readonly Dictionary<string, string[]> CategorySynonyms = new Dictionary<string, string[]>
    {
        { "chair", new[] { "chair", "seat", "stool", "armchair" } },
        { "bed", new[] { "bed", "mattress", "bunk" } },
        { "desk", new[] { "desk", "table", "workstation", "bureau", "worktable" } },
        { "wardrobe", new[] { "wardrobe", "closet", "cupboard", "cabinet", "armoire", "dresser" } },
    };

    // ------------------------------------------------------------------ Parsing

    public static VoiceCommand Parse(string transcript, IReadOnlyList<FurnitureItem> catalog)
    {
        var command = new VoiceCommand { Transcript = transcript ?? string.Empty, Action = VoiceAction.Unknown };
        catalog ??= new List<FurnitureItem>();
        List<string> words = FixMishearings(Tokenize(transcript));
        if (words.Count == 0) return command;

        string sentence = " " + string.Join(" ", words) + " ";
        command.RefersToPointedObject = words.Any(ObjectDeictics.Contains);
        command.RefersToPointedPlace = words.Any(PlaceDeictics.Contains);

        // Which catalogue item is named, if any ("a chair", "the red chair", "this closet").
        int bestScore = 0;
        foreach (FurnitureItem item in catalog)
        {
            if (item == null || item.prefab == null) continue;

            int score = ScoreItem(item, words, out string spokenNoun, out bool specific);
            if (score > bestScore)
            {
                bestScore = score;
                command.Item = item;
                command.SpokenObject = spokenNoun;
                command.NamesSpecificItem = specific;
            }
        }

        if (command.Item == null)
        {
            command.SpokenObject = GuessObjectWord(words);
            if (command.SpokenObject != null && (GenericObjectWords.Contains(command.SpokenObject) ||
                                                 ObjectDeictics.Contains(command.SpokenObject)))
            {
                command.SpokenObject = null; // "this furniture" / "the thing": not an unknown object
            }
        }

        command.Action = DecideAction(words, sentence, command);

        if (command.Item != null)
        {
            command.Size = MatchOption(command.Item.sizes, s => s.label, words) ?? FirstOrNull(command.Item.sizes);
            command.Color = MatchOption(command.Item.colors, c => c.label, words) ?? FirstOrNull(command.Item.colors);
        }

        return command;
    }

    /// <summary>Order matters: undo, then delete, then move, then place.</summary>
    private static VoiceAction DecideAction(List<string> words, string sentence, VoiceCommand command)
    {
        if (UndoPhrases.Any(p => sentence.Contains(" " + p + " "))) return VoiceAction.Undo;

        if (HasVerb(words, DeleteVerbs) || DeletePhrases.Any(p => sentence.Contains(" " + p + " "))) return VoiceAction.Delete;

        if (HasVerb(words, MoveVerbs)) return VoiceAction.Move;

        bool explicitPlace = HasVerb(words, PlaceVerbs) || PlacePhrases.Any(p => sentence.Contains(" " + p + " "));
        if (explicitPlace) return VoiceAction.Place;

        if (HasVerb(words, PutVerbs))
        {
            // "put this here" / "put the red chair there" -> move an existing one; "put a chair here" -> new one.
            bool existingObject = command.RefersToPointedObject || IsDefinite(words, command.SpokenObject) ||
                                  (command.Item == null && words.Any(GenericObjectWords.Contains));
            return existingObject ? VoiceAction.Move : VoiceAction.Place;
        }

        // No verb: "a chair here" / "chair" still places one.
        return command.Item != null ? VoiceAction.Place : VoiceAction.Unknown;
    }

    private static bool HasVerb(List<string> words, HashSet<string> verbs) =>
        words.Any(w => verbs.Contains(w) || verbs.Any(v => IsCloseEnough(w, v)));

    /// <summary>"the chair" (an existing one) vs "a chair" (a new one).</summary>
    private static bool IsDefinite(List<string> words, string noun)
    {
        if (string.IsNullOrEmpty(noun)) return false;

        for (int i = 0; i < words.Count; i++)
        {
            if (words[i] != noun && !IsCloseEnough(words[i], noun)) continue;
            for (int back = 1; back <= 3 && i - back >= 0; back++)
            {
                string previous = words[i - back];
                if (previous == "the" || ObjectDeictics.Contains(previous)) return true;
                if (previous == "a" || previous == "an" || previous == "another" || previous == "new") return false;
            }
        }

        return false;
    }

    /// <summary>All the object words the parser knows, for help messages ("chair, bed, desk...").</summary>
    public static IEnumerable<string> KnownCategories(IReadOnlyList<FurnitureItem> catalog)
    {
        return (catalog ?? new List<FurnitureItem>())
            .Where(i => i != null && !string.IsNullOrEmpty(i.category))
            .Select(i => i.category.ToLowerInvariant())
            .Distinct();
    }

    /// <summary>True when <paramref name="category"/> (a catalogue category) is the one the player named with <paramref name="spokenNoun"/>.</summary>
    public static bool CategoryMatches(string category, string spokenNoun)
    {
        if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(spokenNoun)) return false;
        string key = category.ToLowerInvariant();
        return key == spokenNoun || (CategorySynonyms.TryGetValue(key, out string[] synonyms) && synonyms.Contains(spokenNoun));
    }

    private static int ScoreItem(FurnitureItem item, List<string> words, out string spokenNoun, out bool specific)
    {
        spokenNoun = null;
        specific = false;
        int score = 0;

        // Category (with synonyms): strong signal.
        string category = (item.category ?? string.Empty).ToLowerInvariant();
        IEnumerable<string> nouns = CategorySynonyms.TryGetValue(category, out string[] synonyms)
            ? synonyms
            : new[] { category };
        foreach (string noun in nouns)
        {
            if (!string.IsNullOrEmpty(noun) && words.Any(w => w == noun || IsCloseEnough(w, noun)))
            {
                score += 3;
                spokenNoun = noun;
                break;
            }
        }

        // Words of the item's name ("Red_Chair" -> red, chair): disambiguates between items.
        foreach (string nameWord in Tokenize((item.itemName ?? string.Empty).Replace('_', ' ')))
        {
            if (nameWord.Length < 3 || !words.Contains(nameWord)) continue;
            score += 2;

            bool isTypeWord = CategorySynonyms.Values.Any(s => s.Contains(nameWord));
            if (!isTypeWord) specific = true; // "red", "double", "modern"...
            if (spokenNoun == null && isTypeWord) spokenNoun = nameWord;
        }

        // A category word is mandatory: a colour alone is not enough.
        if (spokenNoun == null)
        {
            specific = false;
            return 0;
        }

        // Small bonus when a size or colour option of this item was mentioned ("double bed").
        if (MatchOption(item.sizes, s => s.label, words) != null) score += 1;
        if (MatchOption(item.colors, c => c.label, words) != null) score += 1;

        return score;
    }

    private static T MatchOption<T>(List<T> options, System.Func<T, string> label, List<string> words) where T : class
    {
        if (options == null) return null;

        T best = null;
        int bestHits = 0;
        foreach (T option in options)
        {
            int hits = Tokenize(label(option)).Count(w => w.Length >= 3 && words.Contains(w));
            if (hits > bestHits)
            {
                bestHits = hits;
                best = option;
            }
        }

        return best;
    }

    private static T FirstOrNull<T>(List<T> list) where T : class => list != null && list.Count > 0 ? list[0] : null;

    private static string GuessObjectWord(List<string> words)
    {
        // The word right after "a/an/the/some/this" is usually the object ("place a sofa here" -> sofa).
        for (int i = 0; i < words.Count - 1; i++)
        {
            string w = words[i];
            if (w == "a" || w == "an" || w == "the" || w == "some" || w == "this" || w == "that")
            {
                return words[i + 1];
            }
        }

        return null;
    }

    // ------------------------------------------------------------------ Words

    /// <summary>Lower case words without punctuation; plurals reduced ("chairs" -> "chair").</summary>
    public static List<string> Tokenize(string text)
    {
        var words = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return words;

        var current = new StringBuilder();
        foreach (char raw in text.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(raw) || raw == '\'')
            {
                current.Append(raw);
            }
            else if (current.Length > 0)
            {
                words.Add(Singular(current.ToString()));
                current.Clear();
            }
        }

        if (current.Length > 0) words.Add(Singular(current.ToString()));
        return words;
    }

    private static List<string> FixMishearings(List<string> words)
    {
        var fixedWords = new List<string>(words.Count);
        for (int i = 0; i < words.Count; i++)
        {
            if (i + 1 < words.Count && SoundAlikePairs.TryGetValue(words[i] + " " + words[i + 1], out string merged))
            {
                fixedWords.Add(merged);
                i++;
                continue;
            }

            fixedWords.Add(SoundAlikes.TryGetValue(words[i], out string replacement) ? replacement : words[i]);
        }

        return fixedWords;
    }

    /// <summary>
    /// Tolerates one wrong/missing letter for words of 5+ letters and two for 8+ ("chairr", "wardobe").
    /// Short words must match exactly (too many false positives: bed/red...).
    /// </summary>
    private static bool IsCloseEnough(string heard, string expected)
    {
        if (expected.Length < 5 || Mathf.Abs(heard.Length - expected.Length) > 2) return false;
        int allowed = expected.Length >= 8 ? 2 : 1;
        return Levenshtein(heard, expected) <= allowed;
    }

    private static int Levenshtein(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++) previous[j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = System.Math.Min(System.Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }

    // Words ending in "s" that are not plurals.
    private static readonly HashSet<string> NotPlural = new HashSet<string>
    {
        "this", "his", "is", "was", "has", "does", "its", "us", "yes", "plus", "as", "oops", "those", "these",
    };

    private static string Singular(string word)
    {
        if (NotPlural.Contains(word)) return word;
        if (word.Length > 4 && word.EndsWith("ies")) return word.Substring(0, word.Length - 3) + "y";
        if (word.Length > 4 && (word.EndsWith("ches") || word.EndsWith("shes") || word.EndsWith("xes"))) return word.Substring(0, word.Length - 2);
        if (word.Length > 3 && word.EndsWith("s") && !word.EndsWith("ss")) return word.Substring(0, word.Length - 1);
        return word;
    }
}
