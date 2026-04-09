using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Content.Client._Rat.Chat.StreamerMode;

/// <summary>
///     Streamer mode system that replaces profanity with innocuous alternatives
///     to protect privacy during streaming.
/// </summary>
public sealed partial class StreamerModeSystem
{
    private bool _enabled;

    /// <summary>
    ///     Whether streamer mode is currently active.
    /// </summary>
    public bool Enabled => _enabled;

    /// <summary>
    ///     Profane words (lowercase) mapped to innocuous replacements.
    /// </summary>
    private static readonly Dictionary<string, string> ProfanityReplacements = new()
    {

        // Russian profanity
        { "хохол", "человек"},
        { "хохолом", "человеком"},
        { "хохла", "человека"},
        { "хохлу", "человеку"},
        { "хохлов", "людей"},
        { "хохлы", "люди"},
        { "русня", "люди"},
        { "укроп", "человек"},
        { "москаль", "человек"},
        { "хач", "человек"},
        { "хача", "человека"},
        { "хачу", "человеку"},
        { "хачи", "люди"},
        { "хачей", "людей"},
        { "хачик", "человечек"},
        { "жид", "человек"},
        { "жиду", "человеку"},
        { "жида", "человека"},
        { "жидами", "людьми"},
        { "жиды", "люди"},
        { "жидом", "человеком"},
        { "жидяра", "человек"},
        { "жидяры", "люди"},
        { "жидовня", "люди"},
        { "чурка", "человек"},
        { "чурку", "человеку"},
        { "чурок", "люди"},
        { "чурки", "люди"},
        { "чуркой", "человеком"},
        { "nigger", "афро"},
        { "niger", "афро"},
        { "niga", "афро"},
        { "nigga", "афро"},
        { "naga", "человек"},
        { "нигер", "афро"},
        { "ниггер", "афро"},
        { "нигеры", "афросы"},
        { "ниггеры", "афросы"},
        { "ниггера", "афроса"},
        { "ниггеру", "афросу"},
        { "нигером", "афросом"},
        { "ниггером", "афросом"},
        { "нигера", "афроса"},
        { "нигеру", "афросу"},
        { "нига", "афро"},
        { "нигга", "афро"},
        { "черномаз", "афро"},
        { "чернозад", "афро"},
        { "черножоп", "афро"},
        { "черномазый", "афро"},
        { "черножопый", "афро"},
        { "черномазых", "афро"},
        { "черномазого", "афро"},
        { "негритос", "афро"},
        { "негр", "афро"},
        { "негры", "афросы"},
        { "негритоса", "афроса"},
        { "негритосов", "афросов"},
        { "нигритос", "афро"},
        { "нигритоса", "афроса"},
        { "нигритосов", "афросов"},
        { "нага", "афро"},
        { "faggot", "мужеложец"},
        { "fagot", "мужеложец"},
        { "fag", "мужеложец"},
        { "fagg", "мужеложец"},
        { "pidor", "мужеложец"},
        { "pidar", "мужеложец"},
        { "piddor", "мужеложец"},
        { "pidorr", "мужеложец"},
        { "pidarr", "мужеложец"},
        { "piddorr", "мужеложец"},
        { "пидр", "мужлжц"},
        { "пидор", "мужеложец"},
        { "пидар", "мужеложец"},
        { "пидорр", "мужеложец"},
        { "пидарр", "мужеложец"},
        { "ппидорр", "мужеложец"},
        { "ппидарр", "мужеложец"},
        { "пидоры", "мужеложцы"},
        { "пидары", "мужеложцы"},
        { "пидора", "мужеложца"},
        { "пидара", "мужеложца"},
        { "пидору", "мужеложцу"},
        { "пидару", "мужеложцу"},
        { "пидарам", "мужеложцам"},
        { "пидором", "мужеложцам"},
        { "пiдорас", "мужеложец"},
        { "пiдарас", "мужеложец"},
        { "пидорас", "мужеложец"},
        { "пидарас", "мужеложец"},
        { "пидорасс", "мужеложец"},
        { "пидарасс", "мужеложец"},
        { "ппидорас", "мужеложец"},
        { "ппидарас", "мужеложец"},
        { "пидорассc", "мужеложец"},
        { "пидарассc", "мужеложец"},
        { "пидорасу", "мужеложцу"},
        { "пидарасу", "мужеложцу"},
        { "пидораса", "мужеложца"},
        { "пидараса", "мужеложца"},
        { "пидорасы", "мужеложцы"},
        { "пидарасы", "мужеложцы"},
        { "пидорасов", "мужеложцев"},
        { "пидарасов", "мужеложцев"},
        { "пидорасина", "мужеложцев"},
        { "пидорасинка", "мужеложец"},
        { "пидарасинка", "мужеложец"},
        { "пидораска", "мужеложец"},
        { "пидараска", "мужеложец"},
        { "пидорасик", "мужеложец"},
        { "пидарасик", "мужеложец"},
        { "педбир", "мужеложец"},
        { "педабир", "мужеложец"},
        { "педеростический", "мужеложный"},
        { "пидеростический", "мужеложный"},
        { "педерастический", "мужеложный"},
        { "пидерастический", "мужеложный"},
        { "пидрил", "мужеложец"},
        { "педрил", "мужеложец"},
        { "пидрила", "мужеложец"},
        { "педрила", "мужеложец"},
        { "пидрилы", "мужеложцы"},
        { "педрилы", "мужеложцы"},
        { "пидрило", "мужеложец"},
        { "пидрик", "мужеложец"},
        { "педрик", "мужеложец"},
        { "педераст", "мужеложец"},
        { "пидераст", "мужеложец"},
        { "педираст", "мужеложец"},
        { "педарас", "мужеложец"},
        { "педарасы", "мужеложцы"},
        { "педерас", "мужеложец"},
        { "педерасы", "мужеложцы"},
        { "гомик", "мужеложец"},
        { "гомики", "мужеложцы"},
        { "гомиков", "мужеложцев"},
        { "гомикав", "мужеложцев"},
        { "гомосек", "мужеложец"},
        { "гомосекк", "мужеложец"},
        { "гомосеки", "мужеложцы"},
        { "гомосеков", "мужеложцев"},
        { "гомосятина", "мужеложец"},
        { "педик", "мужеложец"},
        { "педики", "мужеложцы"},
        { "педика", "мужеложца"},
        { "педиков", "мужеложцев"},
        { "педикав", "мужеложцев"},
        { "говноёб", "мужеложец"},
        { "глиномес", "мужеложец"},
        { "глинамес", "мужеложец"},
        { "глиномесс", "мужеложец"},
        { "глинамесс", "мужеложец"},
        { "уебаны", "дураки"},
        { "даун", "глупый"},
        { "дауны", "глупые"},
        { "даунов", "глупых"},
        { "аутист", "глупый"},
        { "аутисты", "глупые"},
        { "аутистов", "глупых"},
        { "retard", "глупый"},
        { "retards", "глупые"},
        { "ретард", "глупый"},
        { "ретарды", "глупые"},
        { "ретардов", "глупых"},
        { "virgin", "невинный"},
        { "simp", "человек"},
        { "cимп", "человек"},
        { "cимпа", "человека"},
        { "cимпу", "человеку"},
        { "куколд", "человек"},
        { "incel", "человек"},
        { "инцел", "человек"},
        { "cunt", "дурак"},
        { "циганин", "человек"},
        { "пиндос", "космический американец"},
        { "пендос", "космический американец"},
        { "пиндосс", "космический американец"},
        { "пендосс", "космический американец"},
        { "пиндосом", "космическим американцем"},
        { "пендосом", "космическим американцем"},
        { "пендосы", "космические американцы"},
        { "пиндосы", "космические американцы"},
        { "кацап", "человек"},
        { "аллах бабах", "аллах"},
        { "аллах бум", "аллах"},
        { "бабах аллах", "аллах"},
        { "бум аллах", "аллах"},
    };

    /// <summary>
    ///     Called by the configuration system when the streamer mode CVar changes.
    /// </summary>
    public void OnStreamerModeChanged(bool enabled)
    {
        _enabled = enabled;
    }

    /// <summary>
    ///     Applies streamer mode filtering to a chat message.
    ///     Filters profanity from the message.
    /// </summary>
    /// <param name="wrappedMessage">The wrapped message (may contain HTML tags like &lt;Name&gt;).</param>
    /// <returns>The filtered wrapped message.</returns>
    public string FilterWrappedMessage(string wrappedMessage)
    {
        if (!_enabled || string.IsNullOrEmpty(wrappedMessage))
            return wrappedMessage;

        return FilterTextOutsideTags(wrappedMessage);
    }

    /// <summary>
    ///     Filters profanity from plain text (no HTML tags expected).
    /// </summary>
    public string FilterMessage(string text)
    {
        if (!_enabled || string.IsNullOrEmpty(text))
            return text;

        var result = text;

        // Sort by length (longest first) to avoid partial replacements
        var sortedReplacements = ProfanityReplacements.OrderByDescending(x => x.Key.Length);

        foreach (var (profane, replacement) in sortedReplacements)
        {
            // Case-insensitive whole-word replacement
            var pattern = $@"\b{Regex.Escape(profane)}\b";
            result = Regex.Replace(result, pattern, replacement, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        return result;
    }

    /// <summary>
    ///     Filters profanity from text while preserving HTML tag contents.
    /// </summary>
    private string FilterTextOutsideTags(string input)
    {
        // Match HTML tags and their contents
        var tagPattern = new Regex(@"(<[^>]+>)", RegexOptions.Compiled);

        // Split by tags, filter only the text between them
        var parts = tagPattern.Split(input);
        var result = new StringBuilder();

        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part))
                continue;

            // If it's an HTML tag, keep it as-is
            if (part.StartsWith("<") && part.EndsWith(">"))
            {
                result.Append(part);
            }
            else
            {
                // Filter profanity from text content
                result.Append(FilterMessage(part));
            }
        }

        return result.ToString();
    }
}
