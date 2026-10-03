using ApiForge.Core;
using ApiForge.Infrastructure;

namespace ApiForge.UnitTests;

public sealed class StoreTests
{
    [Fact]
    public async Task Content_type_defaults_plural_and_fields()
    {
        // Arrange
        var store = new InMemoryContentTypeStore();
        var request = new ContentTypeDto(
            Id: null,
            Name: "Article",
            PluralName: null,
            ApiId: "article",
            Description: null,
            Fields:
            [
                new FieldDto(
                    Id: null,
                    Name: "Title",
                    FieldName: "title",
                    Type: FieldType.SHORT_TEXT,
                    Required: true,
                    Unique: true,
                    TargetContentType: null,
                    RelationType: null
                )
            ],
            CreatedAt: null,
            UpdatedAt: null
        );

        // Act
        var created = await store.Create(request, default);

        // Assert
        Assert.Equal("Articles", created.PluralName);
        Assert.NotNull(created.Fields);
        Assert.Single(created.Fields);
    }

    [Fact]
    public async Task Content_search_is_exact_and_ands_filters()
    {
        // Arrange
        var typeStore = new InMemoryContentTypeStore();
        var contentStore = new InMemoryContentStore(typeStore);

        await typeStore.Create(new ContentTypeDto(
            Id: null,
            Name: "Tag",
            PluralName: null,
            ApiId: "tag",
            Description: null,
            Fields: [],
            CreatedAt: null,
            UpdatedAt: null
        ), default);

        await contentStore.Create("tag", new Dictionary<string, object?>
        {
            ["label"] = "one",
            ["active"] = true
        }, default);

        await contentStore.Create("tag", new Dictionary<string, object?>
        {
            ["label"] = "one",
            ["active"] = false
        }, default);

        var searchFilters = new Dictionary<string, object?>
        {
            ["label"] = "one",
            ["active"] = true
        };

        // Act
        var matchingRows = await contentStore.Search("tag", searchFilters, default);

        // Assert
        Assert.Single(matchingRows);
    }

    [Fact]
    public void Password_hash_is_not_plaintext()
    {
        // Arrange
        const string rawPassword = "password123";

        // Act
        var hash = BCrypt.Net.BCrypt.HashPassword(rawPassword);

        // Assert
        Assert.NotEqual(rawPassword, hash);
        Assert.True(BCrypt.Net.BCrypt.Verify(rawPassword, hash));
    }
}
