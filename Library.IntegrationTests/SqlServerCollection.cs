namespace Library.IntegrationTests;

/// <summary>
/// 宣告一組叫 "SqlServer" 的測試集合，集合裡的測試類別共用同一個 SqlServerFixture（同一個容器）。
/// 裡面不用寫任何東西，它只是一個宣告。之後新增的整合測試類別，加上 [Collection("SqlServer")] 就能共用。
/// </summary>
[CollectionDefinition("SqlServer")]
public class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
}
