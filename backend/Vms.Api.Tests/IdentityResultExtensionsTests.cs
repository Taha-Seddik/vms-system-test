using Microsoft.AspNetCore.Identity;
using Vms.Api.Utils;
using Xunit;

namespace Vms.Api.Tests;

public sealed class IdentityResultExtensionsTests
{
    [Fact]
    public void EnsureSucceeded_accepts_a_successful_result()
    {
        IdentityResult.Success.EnsureSucceeded("Create user");
    }

    [Fact]
    public void EnsureSucceeded_includes_identity_errors()
    {
        var result = IdentityResult.Failed(new IdentityError
        {
            Code = "DuplicateUserName",
            Description = "Username already exists."
        });

        var exception = Assert.Throws<InvalidOperationException>(
            () => result.EnsureSucceeded("Create user"));

        Assert.Contains("DuplicateUserName", exception.Message);
        Assert.Contains("Username already exists.", exception.Message);
    }
}
