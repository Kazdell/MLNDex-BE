using Application.Interfaces.System;
using Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace mlndex_backend.Controllers
{
  [Route("api/[controller]")]
  public class LanguagesController : BaseController
  {
    private readonly ILanguageService _languageService;

    public LanguagesController(ILanguageService languageService)
    {
      _languageService = languageService;
    }

    /// <summary>
    /// Get all supported languages.
    /// Used by frontend to populate language dropdowns dynamically.
    /// </summary>
    [HttpGet]
    public IActionResult GetAll()
    {
      return OkResponse(_languageService.GetAllSupportedLanguages());
    }

    /// <summary>
    /// Validate if a language code or name is supported.
    /// </summary>
    [HttpGet("validate")]
    public IActionResult Validate([FromQuery] string language)
    {
      if (string.IsNullOrWhiteSpace(language))
        return BadRequestResponse("Language parameter is required.");

      var info = _languageService.ValidateLanguage(language);
      if (info == null)
        return NotFoundResponse($"Language '{language}' is not supported.");

      return OkResponse(info, "Language is valid.");
    }
  }
}
