using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using RentalApp.Database.Models;

namespace RentalApp.Tests.Models;

public class CategoryTests
{
    [Fact]
    public void ItemCount_IsZero_WhenNoItems()
    {
        var category = new Category();
        Assert.Equal(0, category.ItemCount);
    }

    [Fact]
    public void ItemCount_Matches_ItemsCount()
    {
        var category = new Category
        {
            Items = new List<Item> { new(), new(), new() }
        };

        Assert.Equal(3, category.ItemCount);
    }

    [Fact]
    public void EfCore_And_DataAnnotations_ArePresent()
    {
        var type = typeof(Category);

        var table = type.GetCustomAttributes(typeof(TableAttribute), inherit: false)
            .Cast<TableAttribute>()
            .SingleOrDefault();
        Assert.NotNull(table);
        Assert.Equal("categories", table!.Name);

        var pk = type.GetCustomAttributes(typeof(PrimaryKeyAttribute), inherit: false)
            .Cast<PrimaryKeyAttribute>()
            .SingleOrDefault();
        Assert.NotNull(pk);
        Assert.Equal(new[] { nameof(Category.Id) }, pk!.PropertyNames);

        var index = type.GetCustomAttributes(typeof(IndexAttribute), inherit: false)
            .Cast<IndexAttribute>()
            .SingleOrDefault(i => i.PropertyNames.SequenceEqual(new[] { nameof(Category.Slug) }));
        Assert.NotNull(index);
        Assert.True(index!.IsUnique);

        var nameProp = type.GetProperty(nameof(Category.Name));
        Assert.NotNull(nameProp);
        Assert.NotNull(nameProp!.GetCustomAttributes(typeof(RequiredAttribute), inherit: false).SingleOrDefault());
        var nameMax = nameProp.GetCustomAttributes(typeof(MaxLengthAttribute), inherit: false)
            .Cast<MaxLengthAttribute>()
            .SingleOrDefault();
        Assert.NotNull(nameMax);
        Assert.Equal(100, nameMax!.Length);

        var slugProp = type.GetProperty(nameof(Category.Slug));
        Assert.NotNull(slugProp);
        Assert.NotNull(slugProp!.GetCustomAttributes(typeof(RequiredAttribute), inherit: false).SingleOrDefault());
        var slugMax = slugProp.GetCustomAttributes(typeof(MaxLengthAttribute), inherit: false)
            .Cast<MaxLengthAttribute>()
            .SingleOrDefault();
        Assert.NotNull(slugMax);
        Assert.Equal(100, slugMax!.Length);

        var itemCountProp = type.GetProperty(nameof(Category.ItemCount));
        Assert.NotNull(itemCountProp);
        Assert.NotNull(itemCountProp!.GetCustomAttributes(typeof(NotMappedAttribute), inherit: false).SingleOrDefault());
    }
}
