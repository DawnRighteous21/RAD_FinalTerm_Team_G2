using System.IO;
using Microsoft.Data.Sqlite;
using SaigonBite.OFDH.Controllers;
using SaigonBite.OFDH.DataAccess;

namespace SaigonBite.OFDH.Tests;

public class CatalogAdminTests
{
    private static (DatabaseConnection Db, string Path) CreateTempDb()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"saigonbite_uc14_{Guid.NewGuid():N}.db");
        var db = new DatabaseConnection(tempPath);
        db.InitializeSchema();
        return (db, tempPath);
    }

    private static void Cleanup(string path)
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(path)) File.Delete(path);
    }

    [Fact]
    public void LoadCatalog_ReturnsSeededBrandsAndItemsWithAssignedHubs()
    {
        var (db, path) = CreateTempDb();
        try
        {
            var ctrl = new CatalogAdminController(db);
            var (brands, items) = ctrl.LoadCatalog();

            Assert.Equal(2, brands.Count);
            Assert.Equal(3, items.Count);
            Assert.Contains("HUB-D1", items.First(i => i.ItemId == "ITEM-01").AssignedHubsText);
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void CreateMenuItem_ValidBilingualData_PersistsItemAndHubs()
    {
        var (db, path) = CreateTempDb();
        try
        {
            var ctrl = new CatalogAdminController(db);
            var created = ctrl.CreateMenuItem(
                "BR-01",
                "Bun Thit Nuong",
                "Grilled pork vermicelli bowl",
                "Bun thit nuong cha gio",
                65000m,
                "Main",
                new[] { "HUB-D1", "HUB-D3" });

            Assert.Equal("ITEM-04", created.ItemId);
            Assert.True(created.IsActive);

            var hubs = ctrl.GetAssignedHubIds(created.ItemId);
            Assert.Equal(2, hubs.Count);
            Assert.Contains("HUB-D1", hubs);
            Assert.Contains("HUB-D3", hubs);
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void CreateMenuItem_DuplicateNameInSameBrand_ThrowsInvalidOperationException()
    {
        var (db, path) = CreateTempDb();
        try
        {
            var ctrl = new CatalogAdminController(db);
            Assert.Throws<InvalidOperationException>(() =>
                ctrl.CreateMenuItem("BR-02", "Pho Tai Nam", "Duplicate", "Trung ten", 75000m, "Main", new[] { "HUB-D1" }));
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void CreateMenuItem_NonPositivePrice_ThrowsArgumentException()
    {
        var (db, path) = CreateTempDb();
        try
        {
            var ctrl = new CatalogAdminController(db);
            Assert.Throws<ArgumentException>(() =>
                ctrl.CreateMenuItem("BR-01", "Bun Bo Hue", "Spicy beef noodle", "Bun bo Hue", 0m, "Main", new[] { "HUB-D1" }));
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void UpdateMenuItem_ValidChanges_UpdatesDatabaseRecord()
    {
        var (db, path) = CreateTempDb();
        try
        {
            var ctrl = new CatalogAdminController(db);
            ctrl.UpdateMenuItem("ITEM-01", "BR-01", "Com Tam Dac Biet", "Special broken rice", "Com tam dac biet", 72000m, "Main", new[] { "HUB-D1" });

            var (_, items) = ctrl.LoadCatalog();
            var updated = items.First(i => i.ItemId == "ITEM-01");
            Assert.Equal("Com Tam Dac Biet", updated.ItemName);
            Assert.Equal(72000m, updated.UnitPrice);
            Assert.Equal("HUB-D1", updated.AssignedHubsText);
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void ArchiveAndReactivateCatalogEntities_TogglesIsActiveWithoutDeleting()
    {
        var (db, path) = CreateTempDb();
        try
        {
            var ctrl = new CatalogAdminController(db);

            Assert.True(ctrl.ArchiveCatalogEntity("ITEM-02", "MenuItem"));
            var (_, afterArchive) = ctrl.LoadCatalog();
            Assert.False(afterArchive.First(i => i.ItemId == "ITEM-02").IsActive);

            Assert.True(ctrl.ReactivateCatalogEntity("ITEM-02", "MenuItem"));
            var (_, afterReactivate) = ctrl.LoadCatalog();
            Assert.True(afterReactivate.First(i => i.ItemId == "ITEM-02").IsActive);

            var newBrand = ctrl.CreateBrand("Banh Mi Saigon", "Vietnamese Sandwich", "Crispy baguette");
            Assert.Equal("BR-03", newBrand.BrandId);
            Assert.True(ctrl.ArchiveCatalogEntity(newBrand.BrandId, "Brand"));
            var (brands, _) = ctrl.LoadCatalog();
            Assert.False(brands.First(b => b.BrandId == "BR-03").IsActive);
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void CreateMenuItem_MissingOrNumericOnlyDescription_ThrowsArgumentException()
    {
        var (db, path) = CreateTempDb();
        try
        {
            var ctrl = new CatalogAdminController(db);
            Assert.Throws<ArgumentException>(() =>
                ctrl.CreateMenuItem("BR-01", "Banh Xeo", "2222", "Banh xeo mien Tay", 55000m, "Main", new[] { "HUB-D1" }));
            Assert.Throws<ArgumentException>(() =>
                ctrl.CreateMenuItem("BR-01", "Banh Xeo", "Crispy pancake", "", 55000m, "Main", new[] { "HUB-D1" }));
        }
        finally { Cleanup(path); }
    }
}
