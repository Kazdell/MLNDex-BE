using Application.DTOs.Chapter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces.Creator
{
    public interface IChapterService
    {
        Task<CreateChapterResponseDto> CreateAsync(
            int creatorId,
            CreateChapterDto dto,
            CancellationToken cancellationToken = default);
    }
}
