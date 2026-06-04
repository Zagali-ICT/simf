using SIMF.Common;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>Unit tests for the API response envelope (SIMF-API-001 section 6).</summary>
public sealed class ApiResultTests
{
    [Fact]
    public void Ok_builds_a_success_result()
    {
        var result = ApiResult<string>.Ok("value");

        Assert.True(result.Success);
        Assert.Equal("value", result.Data);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Fail_builds_a_failure_result()
    {
        var error = new ApiError
        {
            Code = ErrorCodes.InternalError,
            Message = "x",
            MessageArabic = "س",
        };

        var result = ApiResult<string>.Fail(error);

        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Same(error, result.Error);
    }
}
