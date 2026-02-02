using Xunit;

[assembly: AssemblyFixture(typeof(Authra.IntegrationTests.Fixtures.DatabaseFixture))]

namespace Authra.IntegrationTests.Fixtures;

// Assembly fixture registration is done via attribute above.
// This allows the DatabaseFixture to be shared across all tests in the assembly.
