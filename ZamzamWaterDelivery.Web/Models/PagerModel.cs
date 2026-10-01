using ZamzamWaterDelivery.Application.Common;

namespace ZamzamWaterDelivery.Web.Models;

public record PagerModel(int Page, int PageSize, int TotalPages, int TotalCount)
{
    public static PagerModel From<T>(PagedResult<T> result) =>
        new(result.Page, result.PageSize, result.TotalPages, result.TotalCount);
}
