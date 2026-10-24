using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Moq;
using StockManager.Abstract;
using StockManager.Service.Data;
using StockManager.Service.Mappings;

namespace Tests.TestHelpers;

public static class TestSetup
{
    public const string UserId = "user-1";
    public const string UserName = "tester@stock.local";

    /// <summary>Fresh, isolated in-memory database with a mocked "current user".</summary>
    public static ApplicationDbContext CreateDb()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(u => u.UserId).Returns(UserId);
        currentUser.SetupGet(u => u.UserName).Returns(UserName);
        currentUser.SetupGet(u => u.IpAddress).Returns("127.0.0.1");

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"unit-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options, currentUser.Object);
    }

    public static IMapper CreateMapper() =>
        new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>()).CreateMapper();

    public static Mock<ICurrentUserService> CurrentUser()
    {
        var mock = new Mock<ICurrentUserService>();
        mock.SetupGet(u => u.UserId).Returns(UserId);
        mock.SetupGet(u => u.UserName).Returns(UserName);
        return mock;
    }
}
