using Application.DTOs.Common;
using Application.Exceptions;
using Application.Interfaces.Creator;
using Microsoft.AspNetCore.Mvc;

namespace mlndex_backend.Controllers.Creator
{
  [Route("api/[controller]")]
  [ApiController]
  public class GenreController : BaseController
  {
    private readonly IGenreService _genreService;

    public GenreController(IGenreService genreService)
    {
      _genreService = genreService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllGenres()
    {
      var result = await _genreService.GetAllGenresAsync();
      return OkResponse(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetGenre(int id)
    {
      var result = await _genreService.GetGenreByIdAsync(id);
      if (result == null)
        throw new AppException(ErrorCodes.NOT_FOUND);

      return OkResponse(result);
    }
  }
}
