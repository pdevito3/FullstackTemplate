namespace FullstackTemplate.IntegrationTests;

using AutoBogus;

[Collection(nameof(TestFixture))]
public class TestBase : IDisposable
{
    public TestBase()
    {
        AutoFaker.Configure(builder =>
        {
            builder.WithDateTimeKind(DateTimeKind.Utc)
                .WithRecursiveDepth(3)
                .WithTreeDepth(1)
                .WithRepeatCount(1);
        });
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
