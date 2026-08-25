using ApiForge.Core;
using ApiForge.Infrastructure;

namespace ApiForge.UnitTests;

public sealed class StoreTests
{
    [Fact] public async Task Content_type_defaults_plural_and_fields() { var s = new InMemoryContentTypeStore(); var x = await s.Create(new(null, "Article", null, "article", null, [new(null, "Title", "title", FieldType.SHORT_TEXT, true, true, null, null)], null, null), default); Assert.Equal("Articles", x.PluralName); Assert.Single(x.Fields!); }
    [Fact] public async Task Content_search_is_exact_and_ands_filters() { var t = new InMemoryContentTypeStore(); var c = new InMemoryContentStore(t); await t.Create(new(null,"Tag",null,"tag",null,[],null,null),default); await c.Create("tag", new Dictionary<string,object?> { ["label"]="one", ["active"]=true },default); await c.Create("tag", new Dictionary<string,object?> { ["label"]="one", ["active"]=false },default); var rows = await c.Search("tag", new Dictionary<string,object?> { ["label"]="one", ["active"]=true },default); Assert.Single(rows); }
    [Fact] public void Password_hash_is_not_plaintext() { var hash = BCrypt.Net.BCrypt.HashPassword("password123"); Assert.NotEqual("password123", hash); Assert.True(BCrypt.Net.BCrypt.Verify("password123", hash)); }
}
