using SupportDesk.Application.Common;

namespace SupportDesk.UnitTests.Common;

public class PagedResultTests
{
    [Theory]
    [InlineData(0, 20, 0)]
    [InlineData(20, 20, 1)]
    [InlineData(21, 20, 2)]
    [InlineData(37, 20, 2)]
    public void TotalPages_RoundsUpToCoverEveryItem(int totalCount, int pageSize, int expected)
    {
        var page = new PagedResult<string>([], Page: 1, PageSize: pageSize, TotalCount: totalCount);

        Assert.Equal(expected, page.TotalPages);
    }

    [Fact]
    public void TotalPages_WithoutAPageSize_IsZeroRatherThanDividingByZero()
    {
        var page = new PagedResult<string>([], Page: 1, PageSize: 0, TotalCount: 10);

        Assert.Equal(0, page.TotalPages);
    }
}
