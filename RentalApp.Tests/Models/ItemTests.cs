using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RentalApp.Database.Models;

namespace RentalApp.Tests.Models;

public class ItemTests
{
    [Fact]
    public void Defaults_AreSafe()
    {
        // Arrange: a new item with default values.
        var item = new Item();

        // Act + Assert: defaults should be non-null and sensible.
        Assert.True(item.IsAvailable);
        Assert.NotNull(item.Rentals);
        Assert.Empty(item.Rentals);

        // Display-only API fields should default to safe values.
        Assert.NotNull(item.OwnerName);
        Assert.NotNull(item.CategoryName);
        Assert.NotNull(item.CategorySlug);
        Assert.NotNull(item.Reviews);
        Assert.Empty(item.Reviews);

        Assert.Equal(DateTimeKind.Utc, item.CreatedAt.Kind);
        Assert.Equal(DateTimeKind.Utc, item.UpdatedAt.Kind);
    }

    [Fact]
    public void DescriptionPreview_IsNull_WhenDescriptionIsNull()
    {
        // Arrange: description is not provided.
        var item = new Item { Description = null };

        // Act + Assert: preview stays null.
        Assert.Null(item.DescriptionPreview);
    }

    [Fact]
    public void DescriptionPreview_ReturnsFullDescription_WhenShort()
    {
        // Arrange: description is shorter than the preview limit.
        var item = new Item { Description = "short" };

        // Act + Assert: preview returns the full string.
        Assert.Equal("short", item.DescriptionPreview);
    }

    [Fact]
    public void DescriptionPreview_Truncates_WhenLong()
    {
        // Arrange: a long description that should be truncated.
        var item = new Item { Description = new string('a', 101) };

        // Act + Assert: preview is truncated and suffixed with ellipsis.
        Assert.NotNull(item.DescriptionPreview);
        Assert.Equal(new string('a', 100) + "...", item.DescriptionPreview);
    }

    [Fact]
    public void EfCore_And_DataAnnotations_ArePresent()
    {
        // Arrange: inspect model metadata via reflection.
        var type = typeof(Item);

        // Act: read EF Core / DataAnnotations attributes.
        var table = type.GetCustomAttributes(typeof(TableAttribute), inherit: false)
            .Cast<TableAttribute>()
            .SingleOrDefault();

        // Assert: table mapping exists and is correct.
        Assert.NotNull(table);
        Assert.Equal("items", table!.Name);

        var pk = type.GetCustomAttributes(typeof(PrimaryKeyAttribute), inherit: false)
            .Cast<PrimaryKeyAttribute>()
            .SingleOrDefault();

        // Assert: primary key is configured.
        Assert.NotNull(pk);
        Assert.Equal(new[] { nameof(Item.Id) }, pk!.PropertyNames);

        var titleProp = type.GetProperty(nameof(Item.Title));

        // Assert: Title has required + length constraints.
        Assert.NotNull(titleProp);
        Assert.NotNull(titleProp!.GetCustomAttributes(typeof(RequiredAttribute), inherit: false).SingleOrDefault());
        var titleMin = titleProp.GetCustomAttributes(typeof(MinLengthAttribute), inherit: false)
            .Cast<MinLengthAttribute>()
            .SingleOrDefault();
        Assert.NotNull(titleMin);
        Assert.Equal(5, titleMin!.Length);
        var titleMax = titleProp.GetCustomAttributes(typeof(MaxLengthAttribute), inherit: false)
            .Cast<MaxLengthAttribute>()
            .SingleOrDefault();
        Assert.NotNull(titleMax);
        Assert.Equal(100, titleMax!.Length);

        var rateProp = type.GetProperty(nameof(Item.DailyRate));

        // Assert: DailyRate has required + range + column type constraints.
        Assert.NotNull(rateProp);
        Assert.NotNull(rateProp!.GetCustomAttributes(typeof(RequiredAttribute), inherit: false).SingleOrDefault());
        var rateRange = rateProp.GetCustomAttributes(typeof(RangeAttribute), inherit: false)
            .Cast<RangeAttribute>()
            .SingleOrDefault();
        Assert.NotNull(rateRange);
        Assert.Equal(0.01, (double)rateRange!.Minimum);
        Assert.Equal(1000.00, (double)rateRange.Maximum);
        var rateColumn = rateProp.GetCustomAttributes(typeof(ColumnAttribute), inherit: false)
            .Cast<ColumnAttribute>()
            .SingleOrDefault();
        Assert.NotNull(rateColumn);
        Assert.Equal("numeric(10,2)", rateColumn!.TypeName);

        var latProp = type.GetProperty(nameof(Item.Latitude));

        // Assert: latitude is constrained to valid coordinate range.
        var latRange = latProp!.GetCustomAttributes(typeof(RangeAttribute), inherit: false)
            .Cast<RangeAttribute>()
            .Single();
        Assert.Equal(-90.0, (double)latRange.Minimum);
        Assert.Equal(90.0, (double)latRange.Maximum);

        var lonProp = type.GetProperty(nameof(Item.Longitude));

        // Assert: longitude is constrained to valid coordinate range.
        var lonRange = lonProp!.GetCustomAttributes(typeof(RangeAttribute), inherit: false)
            .Cast<RangeAttribute>()
            .Single();
        Assert.Equal(-180.0, (double)lonRange.Minimum);
        Assert.Equal(180.0, (double)lonRange.Maximum);

        // Location is optional, but should be mapped (no [NotMapped]).
        var locationProp = type.GetProperty(nameof(Item.Location));

        // Assert: Location is mapped as a NetTopologySuite Point.
        Assert.NotNull(locationProp);
        Assert.Equal(typeof(Point), Nullable.GetUnderlyingType(locationProp!.PropertyType) ?? locationProp.PropertyType);
        Assert.Empty(locationProp.GetCustomAttributes(typeof(NotMappedAttribute), inherit: false));

        // DescriptionPreview is computed client-side.
        var previewProp = type.GetProperty(nameof(Item.DescriptionPreview));

        // Assert: computed preview is not mapped.
        Assert.NotNull(previewProp);
        Assert.NotNull(previewProp!.GetCustomAttributes(typeof(NotMappedAttribute), inherit: false).SingleOrDefault());
    }
}
