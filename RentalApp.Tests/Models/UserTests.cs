using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using RentalApp.Database.Models;

namespace RentalApp.Tests.Models;

public class UserTests
{
    [Fact]
    public void Defaults_AreSafe()
    {
        // Arrange: a new user with default values.
        var user = new User();

        // Act + Assert: defaults should be non-null and sensible.
        Assert.True(user.IsActive);
        Assert.Null(user.DeletedAt);
        Assert.Equal(DateTimeKind.Utc, user.CreatedAt.Kind);
        Assert.Equal(DateTimeKind.Utc, user.UpdatedAt.Kind);

        Assert.NotNull(user.ItemsOwned);
        Assert.Empty(user.ItemsOwned);
        Assert.NotNull(user.RentalsAsBorrower);
        Assert.Empty(user.RentalsAsBorrower);
        Assert.NotNull(user.ReviewsWritten);
        Assert.Empty(user.ReviewsWritten);

        // API-only aggregate value.
        Assert.Null(user.AverageRating);
    }

    [Fact]
    public void FullName_Combines_FirstAndLastName()
    {
        // Arrange: set a first/last name.
        var user = new User { FirstName = "Ada", LastName = "Lovelace" };

        // Act + Assert: FullName is a simple concatenation.
        Assert.Equal("Ada Lovelace", user.FullName);
    }

    [Fact]
    public void EfCore_And_DataAnnotations_ArePresent()
    {
        // Arrange: inspect model metadata via reflection.
        var type = typeof(User);

        // Act: read EF Core / DataAnnotations attributes.
        var table = type.GetCustomAttributes(typeof(TableAttribute), inherit: false)
            .Cast<TableAttribute>()
            .SingleOrDefault();

        // Assert: table mapping exists and is correct.
        Assert.NotNull(table);
        Assert.Equal("users", table!.Name);

        var pk = type.GetCustomAttributes(typeof(PrimaryKeyAttribute), inherit: false)
            .Cast<PrimaryKeyAttribute>()
            .SingleOrDefault();

        // Assert: primary key is configured.
        Assert.NotNull(pk);
        Assert.Equal(new[] { nameof(User.Id) }, pk!.PropertyNames);

        var emailIndex = type.GetCustomAttributes(typeof(IndexAttribute), inherit: false)
            .Cast<IndexAttribute>()
            .SingleOrDefault(i => i.PropertyNames.SequenceEqual(new[] { nameof(User.Email) }));

        // Assert: email is uniquely indexed.
        Assert.NotNull(emailIndex);
        Assert.True(emailIndex!.IsUnique);

        var emailProp = type.GetProperty(nameof(User.Email));

        // Assert: Email is required, validated, and length-limited.
        Assert.NotNull(emailProp);
        Assert.NotNull(emailProp!.GetCustomAttributes(typeof(RequiredAttribute), inherit: false).SingleOrDefault());
        Assert.NotNull(emailProp.GetCustomAttributes(typeof(EmailAddressAttribute), inherit: false).SingleOrDefault());
        var emailMax = emailProp.GetCustomAttributes(typeof(MaxLengthAttribute), inherit: false)
            .Cast<MaxLengthAttribute>()
            .SingleOrDefault();
        Assert.NotNull(emailMax);
        Assert.Equal(255, emailMax!.Length);

        var fullNameProp = type.GetProperty(nameof(User.FullName));

        // Assert: FullName is a computed property (not mapped).
        Assert.NotNull(fullNameProp);
        Assert.NotNull(fullNameProp!.GetCustomAttributes(typeof(NotMappedAttribute), inherit: false).SingleOrDefault());
    }
}
