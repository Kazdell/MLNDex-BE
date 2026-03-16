using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces.Queue
{
    public interface IModerationQueue
    {
        /// <summary>
        /// Đưa chapterId vào hàng đợi để AI xử lý.
        /// </summary>
        ValueTask EnqueueAsync(int chapterId, CancellationToken ct = default);
    }
}
