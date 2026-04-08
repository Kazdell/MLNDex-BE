using Application.Interfaces.System;
using Domain.Enums;
using System.Collections.Generic;

namespace Application.Services.System
{
    public class LanguageService : ILanguageService
    {
        public IEnumerable<LanguageInfo> GetAllSupportedLanguages()
        {
            return SupportedLanguages.All;
        }

        public LanguageInfo? ValidateLanguage(string languageCodeOrName)
        {
            return SupportedLanguages.GetByCodeOrName(languageCodeOrName);
        }
    }
}
