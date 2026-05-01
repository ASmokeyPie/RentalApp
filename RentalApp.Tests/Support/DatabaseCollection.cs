namespace RentalApp.Tests.Support;

/// <summary>
/// Marks the "Database" xUnit collection. All integration test classes that
/// carry <c>[Collection("Database")]</c> are run sequentially (not in parallel)
/// so concurrent TRUNCATE statements don't race against each other.
/// </summary>
[CollectionDefinition("Database", DisableParallelization = true)]
public sealed class DatabaseCollection { }
