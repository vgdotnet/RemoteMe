using System.Globalization;

namespace RemoteMe.Core.Model;

// The letters in the circle of a connection in the compact list (decided on 2026-11-19): the first two
// letters of a one-word name, or the first letters of its first two words, in upper case. Spaces,
// underscores and hyphens part the words: PRO_AGENT is PA.
public static class ConnectionInitials {
    private static readonly char[] Separators = [' ', '\t', '_', '-'];

    public static string Of(string name) {
        var words = name.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        var letters = words.Length switch {
            0 => "",
            1 => First(words[0], 2),
            _ => First(words[0], 1) + First(words[1], 1),
        };
        return letters.ToUpper(CultureInfo.CurrentCulture);
    }

    // Whole text elements, so a letter written with two chars is not cut in half.
    private static string First(string word, int count) {
        var elements = StringInfo.GetTextElementEnumerator(word);
        var letters = "";
        for (var i = 0; i < count && elements.MoveNext(); i++) {
            letters += elements.GetTextElement();
        }
        return letters;
    }
}
