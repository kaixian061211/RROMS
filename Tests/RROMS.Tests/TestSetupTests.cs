namespace RROMS.Tests;

/// <summary>The safety guard: the tests must never touch the real database.</summary>
[Trait("Type", "Unit")]
[Trait("Technique", "Security")]
public class TestSetupTests
{
    [Theory(DisplayName = "TC-00-01 The test setup refuses any database except RROMS_Tests")]
    [InlineData("Server=localhost;Database=RROMS_Payment;Trusted_Connection=True")]
    [InlineData("Server=localhost;Database=rroms_tests;Trusted_Connection=True")]
    [InlineData("Server=localhost;Trusted_Connection=True")]
    public void RefusesOtherDatabases(string connectionString)
    {
        var error = Assert.Throws<InvalidOperationException>(() => TestDatabase.CheckIsTestDatabase(connectionString));
        Assert.Contains("Nothing was changed", error.Message);
    }

    [Fact(DisplayName = "TC-00-02 The test setup accepts RROMS_Tests")]
    public void AcceptsTheTestDatabase()
    {
        var connectionString = "Server=localhost;Database=RROMS_Tests;Trusted_Connection=True";
        Assert.Equal(connectionString, TestDatabase.CheckIsTestDatabase(connectionString));
    }
}
