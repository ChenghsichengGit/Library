namespace Library.IntegrationTests;

// 標上 [Collection("SqlServer")] 的測試類別，都共用同一個 SqlServerFixture
[CollectionDefinition("SqlServer")]
public class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
}