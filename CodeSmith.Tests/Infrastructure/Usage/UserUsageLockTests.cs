// == User Usage Lock Tests == //
using CodeSmith.Infrastructure.Services.Usage;

namespace CodeSmith.Tests.Infrastructure.Usage;

public class UserUsageLockTests
{
    [Fact]
    public void GetLock_SameObjectId_ReturnsSameInstance()
    {
        var registry = new UserUsageLock();

        var a = registry.GetLock("user-1");
        var b = registry.GetLock("user-1");

        Assert.Same(a, b);
    }

    [Fact]
    public void GetLock_DifferentObjectIds_ReturnDifferentInstances()
    {
        var registry = new UserUsageLock();

        var a = registry.GetLock("user-1");
        var b = registry.GetLock("user-2");

        Assert.NotSame(a, b);
    }
}
