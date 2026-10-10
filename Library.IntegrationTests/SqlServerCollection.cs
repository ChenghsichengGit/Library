namespace Library.IntegrationTests;

/// <summary>
/// 標上 [Collection("SqlServer")] 的測試類別共用同一個 SqlServerFixture（同一個容器）。
/// </summary>
[CollectionDefinition("SqlServer")]
public class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
}
