using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using RentalApp.Database.Models;

namespace RentalApp.Tests.Models;

public class ReviewTests
{
    [Fact]
    public void Defaults_AreSafe()
    {
        // Arrange: a new review with default values.
        var review = new Review();

        // Act + Assert: timestamps and display fields should be safe by default.
        Assert.Equal(DateTimeKind.Utc, review.CreatedAt.Kind);

        // Display-only API fields should be non-null strings by default.
        Assert.NotNull(review.ReviewerName);
        Assert.NotNull(review.ItemTitle);
    }

    [Fact]
    public void EfCore_And_DataAnnotations_ArePresent()
    {
        // Arrange: inspect model metadata via reflection.
        var type = typeof(Review);

        // Act: read EF Core / DataAnnotations attributes.
        var table = type.GetCustomAttributes(typeof(TableAttribute), inherit: false)
            .Cast<TableAttribute>()
            .SingleOrDefault();

        // Assert: table mapping exists and is correct.
        Assert.NotNull(table);
        Assert.Equal("reviews", table!.Name);

        var pk = type.GetCustomAttributes(typeof(PrimaryKeyAttribute), inherit: false)
            .Cast<PrimaryKeyAttribute>()
            .SingleOrDefault();

        // Assert: primary key is configured.
        Assert.NotNull(pk);
        Assert.Equal(new[] { nameof(Review.Id) }, pk!.PropertyNames);

        var index = type.GetCustomAttributes(typeof(IndexAttribute), inherit: false)
            .Cast<IndexAttribute>()
            .SingleOrDefault(i => i.PropertyNames.SequenceEqual(new[] { nameof(Review.RentalId) }));

        // Assert: each rental can have at most one review.
        Assert.NotNull(index);
        Assert.True(index!.IsUnique);

        var ratingProp = type.GetProperty(nameof(Review.Rating));

        // Assert: Rating is required and within the expected bounds.
        Assert.NotNull(ratingProp);
        Assert.NotNull(ratingProp!.GetCustomAttributes(typeof(RequiredAttribute), inherit: false).SingleOrDefault());
        var ratingRange = ratingProp.GetCustomAttributes(typeof(RangeAttribute), inherit: false)
            .Cast<RangeAttribute>()
            .SingleOrDefault();
        Assert.NotNull(ratingRange);
        Assert.Equal(1, (int)ratingRange!.Minimum);
        Assert.Equal(5, (int)ratingRange.Maximum);

        var commentProp = type.GetProperty(nameof(Review.Comment));

        // Assert: Comment has a maximum length.
        Assert.NotNull(commentProp);
        var commentMax = commentProp!.GetCustomAttributes(typeof(MaxLengthAttribute), inherit: false)
            .Cast<MaxLengthAttribute>()
            .SingleOrDefault();
        Assert.NotNull(commentMax);
        Assert.Equal(500, commentMax!.Length);

        var reviewerNameProp = type.GetProperty(nameof(Review.ReviewerName));

        // Assert: ReviewerName is display-only (not mapped).
        Assert.NotNull(reviewerNameProp);
        Assert.NotNull(reviewerNameProp!.GetCustomAttributes(typeof(NotMappedAttribute), inherit: false).SingleOrDefault());
    }
}
