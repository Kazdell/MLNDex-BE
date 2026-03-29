using System.Collections.Generic;
using System.Threading.Tasks;

namespace Application.Interfaces.Translation
{
    public interface IGoogleTranslationClient
    {
        Task<List<string>> TranslateTextsAsync(List<string> texts, string sourceLang = "auto", string targetLang = "vi");
    }
}
