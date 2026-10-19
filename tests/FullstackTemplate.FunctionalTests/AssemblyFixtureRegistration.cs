using FullstackTemplate.FunctionalTests;
using Xunit.Sdk;
using Xunit.v3;

// One TestingWebApplicationFactory (one database and one test server) for the whole assembly.
// Each test class is its own collection, so the classes run in parallel on this fixture.
[assembly: AssemblyFixture(typeof(TestingWebApplicationFactory))]

// Tests in the same class also run in parallel. Each test must create its own data.
[assembly: Parallelization(Mode = ParallelMode.All)]
