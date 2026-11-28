# 4.3.6 Testing Report — UC14: Manage Brands & Menu Items

- **Author:** Trần Quang Duy (524H0012)
- **Test Framework:** xUnit (.NET 10, `SaigonBite.OFDH.Tests/CatalogAdminTests.cs`) + Manual WPF UI Verification
- **Execution Date:** 2026-11-28
- **Suite Result:** 7/7 UC14 Automated Tests Passed (37/37 Total Solution Tests Passed)

## Automated Unit & Integration Test Cases

| Test ID    | Test Method                                                               | Scenario / Precondition                                          | Expected Result                                                  | Actual Result                                    | Status |
| :--------- | :------------------------------------------------------------------------ | :--------------------------------------------------------------- | :--------------------------------------------------------------- | :----------------------------------------------- | :----- |
| TC-UC14-01 | `LoadCatalog_ReturnsSeededBrandsAndItemsWithAssignedHubs`                 | Load catalog from freshly seeded SQLite DB                       | Returns 2 brands, 3 items, and mapped hub lists                  | Loaded 2 brands, 3 items with `HUB-D1` mapped    | Pass   |
| TC-UC14-02 | `CreateMenuItem_ValidBilingualData_PersistsItemAndHubs`                   | Create `Bun Thit Nuong` (EN/VI, 65,000 VND, `HUB-D1`, `HUB-D3`)  | Persists `ITEM-04` (`is_active = 1`) and 2 `HubMenuItems` rows   | Created `ITEM-04` assigned to `HUB-D1`, `HUB-D3` | Pass   |
| TC-UC14-03 | `CreateMenuItem_DuplicateNameInSameBrand_ThrowsInvalidOperationException` | Create `Pho Tai Nam` under `BR-02`                               | Blocked due to duplicate item name within brand                  | Threw `InvalidOperationException`                | Pass   |
| TC-UC14-04 | `CreateMenuItem_NonPositivePrice_ThrowsArgumentException`                 | Create item with `UnitPrice = 0`                                 | Blocked by domain price validation (`> 0 VND`)                   | Threw `ArgumentException`                        | Pass   |
| TC-UC14-05 | `UpdateMenuItem_ValidChanges_UpdatesDatabaseRecord`                       | Update `ITEM-01` name, price (`72,000`), and hub list (`HUB-D1`) | Updates `MenuItems` and syncs `HubMenuItems`                     | Record updated and verified on reload            | Pass   |
| TC-UC14-06 | `ArchiveAndReactivateCatalogEntities_TogglesIsActiveWithoutDeleting`      | Archive & reactivate `ITEM-02`; create & archive `BR-03`         | Soft-delete toggles `is_active` (`0` / `1`) without SQL `DELETE` | Both item and brand toggled cleanly              | Pass   |
| TC-UC14-07 | `CreateMenuItem_MissingOrNumericOnlyDescription_ThrowsArgumentException`  | Submit numeric-only EN (`"2222"`) or blank VI (`""`)             | Rejected by bilingual description validation                     | Threw `ArgumentException`                        | Pass   |

## Manual UI Verification

| Check                 | Action                                                             | Expected Behavior                                                | Status |
| :-------------------- | :----------------------------------------------------------------- | :--------------------------------------------------------------- | :----- |
| Search & Brand Filter | Type keyword in search box and select a brand in `Filter` dropdown | Filters both Virtual Brands and Bilingual Menu Items tables live | Pass   |
| Modal Validation      | Submit `MenuItemModal` with missing EN/VI text or invalid price    | Displays inline red validation error without closing modal       | Pass   |
| Soft-Delete Toggle    | Click `Archive` then `Reactivate` on a row                         | Status badge switches between `Active` and `Archived` cleanly    | Pass   |
