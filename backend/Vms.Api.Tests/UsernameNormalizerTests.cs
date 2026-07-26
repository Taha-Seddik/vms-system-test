using Vms.Api.Utils;
using Xunit;

namespace Vms.Api.Tests;

public sealed class UsernameNormalizerTests
{
    [Theory]
    [InlineData(" viewer ", "VIEWER")]
    [InlineData("Admin", "ADMIN")]
    public void Normalize_trims_and_applies_invariant_uppercase(
        string input,
        string expected)
    {
        Assert.Equal(expected, UsernameNormalizer.Normalize(input));
    }
}
