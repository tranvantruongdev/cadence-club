using Template.Infra;
using Template.Infra.Settings;
using UnityEngine;

namespace CadenceClub
{
    /// <summary>
    /// UI text in the player's language (Settings → Language), from MasterData/strings.csv, where the English is the key.
    /// Wrap every player-facing literal: <c>Loc.T("Shop")</c>, <c>Loc.F("Play level {0}", n)</c>.
    /// </summary>
    public static class Loc
    {
        public static string Language => Services.TryGet<SettingsService>(out var settings) ? settings.Current.language : "en";

        public static string T(string english) => Club.Master.Translate(english, Language);

        public static string F(string english, params object[] args) => string.Format(T(english), args);

        /// <summary>The device language if the game has it, else English: the first launch's choice.</summary>
        public static string FromSystem() =>
            Application.systemLanguage == SystemLanguage.Japanese ? "ja" : Application.systemLanguage == SystemLanguage.Vietnamese ? "vi" : "en";
    }
}
