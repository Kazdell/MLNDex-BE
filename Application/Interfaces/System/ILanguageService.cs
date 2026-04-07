using Domain.Enums;
using System.Collections.Generic;

namespace Application.Interfaces.System
{
    public interface ILanguageService
    {
        IEnumerable<LanguageInfo> GetAllSupportedLanguages();
        LanguageInfo? ValidateLanguage(string languageCodeOrName);
    }
}
