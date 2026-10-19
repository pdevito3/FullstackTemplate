using FullstackTemplate.IntegrationTests;
using Xunit.Sdk;
using Xunit.v3;

// One TestFixture (one database and one service provider) for the whole assembly.
// Each test class is its own collection, so the classes run in parallel on this fixture.
[assembly: AssemblyFixture(typeof(TestFixture))]

// Tests in the same class also run in parallel. Each test must create its own data
// and its own TestingServiceScope.
[assembly: Parallelization(Mode = ParallelMode.All)]
