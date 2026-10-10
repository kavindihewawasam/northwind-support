using SupportDesk.Infrastructure.Security;

namespace SupportDesk.UnitTests.Infrastructure.Security;

public class IdentityPasswordHasherTests
{
    private readonly IdentityPasswordHasher _hasher = new();

    [Fact]
    public void The_hash_is_not_the_password()
    {
        var hash = _hasher.Hash("LocalDevOnly!123");

        Assert.NotEqual("LocalDevOnly!123", hash);
        Assert.DoesNotContain("LocalDevOnly", hash);
    }

    [Fact]
    public void The_same_password_hashes_differently_each_time_because_each_hash_has_its_own_salt()
    {
        Assert.NotEqual(_hasher.Hash("LocalDevOnly!123"), _hasher.Hash("LocalDevOnly!123"));
    }

    [Fact]
    public void Verification_passes_for_the_right_password_and_fails_for_a_wrong_one()
    {
        var hash = _hasher.Hash("LocalDevOnly!123");

        Assert.True(_hasher.Verify(hash, "LocalDevOnly!123"));
        Assert.False(_hasher.Verify(hash, "localdevonly!123"));
        Assert.False(_hasher.Verify(hash, ""));
    }

    [Fact]
    public void A_missing_hash_never_verifies()
    {
        Assert.False(_hasher.Verify(null, "LocalDevOnly!123"));
    }
}