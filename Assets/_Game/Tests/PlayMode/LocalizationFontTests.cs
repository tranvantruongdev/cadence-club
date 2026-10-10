using NUnit.Framework;
using Template.UI;

namespace CadenceClub.PlayModeTests
{
    public sealed class LocalizationFontTests
    {
        [TestCase("en")]
        [TestCase("vi")]
        [TestCase("ja")]
        public void Shipped_font_chain_contains_pause_labels(string language)
        {
            foreach (var fontStyle in new[] { UiFont.Display, UiFont.Body })
            {
                var font = UiTheme.Current.Font(fontStyle);
                Assert.That(font, Is.Not.Null);
                foreach (var key in new[] { "Paused", "Continue", "Home" })
                {
                    string translated = Club.Master.Translate(key, language);
                    bool covered = font.HasCharacters(translated, out uint[] missing, searchFallbacks: true, tryAddCharacter: false);
                    Assert.That(covered, Is.True,
                        $"{fontStyle}/{language}/{key}: shipped atlas missing {string.Join(",", missing ?? new uint[0])}");
                }
            }
        }
    }
}
