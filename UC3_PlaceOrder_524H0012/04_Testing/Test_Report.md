# 4.3.6 Testing Report — UC3: Place an Order

- **Author:** Trần Quang Duy (524H0012)
- **Test Framework:** xUnit (.NET 10, `SaigonBite.OFDH.Tests/PlaceOrderTests.cs`) + Manual WPF UI Verification
- **Execution Date:** 2026-11-28
- **Suite Result:** 8/8 UC3 Automated Tests Passed (37/37 Total Solution Tests Passed)

## Automated Unit & Integration Test Cases

| Test ID | Test Method | Scenario / Precondition | Expected Result | Actual Result | Status |
| :--- | :--- | :--- | :--- | :--- | :--- |
| TC-UC3-01 | `InitiateCheckout_BusyHub_ThrowsInvalidOperationException` | Select `HUB-D7` (`15/15`, `Busy`) | Checkout blocked with capacity exception | Threw `InvalidOperationException` | Pass |
| TC-UC3-02 | `InitiateCheckout_QuietHub_AppliesTenThousandVndDeliveryDiscount` | Select `HUB-D1` (`2/15`, `Quiet`) + `ADDR-001` (2.4 km) | Base fee 15,000 − 10,000 Quiet discount = `5,000 VND` | `DeliveryFee == 5000m` | Pass |
| TC-UC3-03 | `InitiateCheckout_AddressExceedsSevenKmRadius_ThrowsInvalidOperationException` | Select address at `9.5 km` (> 7.0 km hub radius) | Checkout blocked with out-of-range exception | Threw `InvalidOperationException` | Pass |
| TC-UC3-04 | `ConfirmAndPlaceOrder_LocalCodWithVoucher_PersistsOrderAndIncrementsHubCount` | `CUST-001`, `HUB-D3`, `ADDR-002`, 2x `ITEM-01`, `VOU20`, `COD` | Creates `ORD-1004` (`Placed`, `PendingCOD`, `125,000 VND`); `HUB-D3` active orders `6 -> 7` | Persisted in `Orders` & `OrderItems`; `HUB-D3` = 7 | Pass |
| TC-UC3-05 | `ConfirmAndPlaceOrder_ForeignTouristWithCod_ThrowsInvalidOperationException` | `CUST-002` (Tourist) attempts `COD` | Blocked; requires online payment | Threw `InvalidOperationException` | Pass |
| TC-UC3-06 | `ConfirmAndPlaceOrder_ForeignTouristWithCard_MarksPaidAndIncrementsHubCount` | `CUST-002` (Tourist), `HUB-D1`, 1x `ITEM-02`, `Card` | Status `Placed`, `Paid`, `TXN-CARD-...` generated, `HUB-D1` `2 -> 3` | Persisted as `Paid` with transaction ref; `HUB-D1` = 3 | Pass |
| TC-UC3-07 | `ApplyVoucher_ExpiredOrBelowMinimumSubtotal_ThrowsInvalidOperationException` | Apply `EXPIRED50` or `VOU20` on 15,000 VND subtotal | Rejected for expired/below minimum order value | Threw `InvalidOperationException` | Pass |
| TC-UC3-08 | `AddItemToOrder_OutOfStockItemAtHub_ThrowsInvalidOperationException` | `ITEM-02` marked out-of-stock at `HUB-D1` | Blocked from adding to cart | Threw `InvalidOperationException` | Pass |

## Manual UI & Cross-UC Verification

| Check | Action | Expected Behavior | Status |
| :--- | :--- | :--- | :--- |
| Busy-Hub Redirect | Select `HUB-D7` in `OrderCheckoutView` | Auto-redirects to nearest available hub and displays blue notice banner | Pass |
| Tourist COD Lock | Switch customer dropdown to `CUST-002` | `COD` radio button disabled; auto-switches to `Card` | Pass |
| Kitchen Handoff | Place `ORD-1004` at `HUB-D3`, open `Kitchen Display (UC8)` and select `HUB-D3` | `ORD-1004` appears in live queue as `Placed` | Pass |
