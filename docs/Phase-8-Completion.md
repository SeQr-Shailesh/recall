# Phase 8 completion

## Completed

- Re-inspected customer contracts, the `Customer` entity, photo storage (`Photos/Customers`), and the mobile notes-only navigator.
- Implemented owned customer CRUD under `/api/v1/customers`. `UserId` comes from JWT claims only. Another user's id returns 404.
- Finalized photo as `hasPhoto` plus authorized `POST/GET /api/v1/customers/{id}/photo` (JPEG/PNG/WebP, 5 MB). Stored file names are not returned. Re-upload replaces the previous file.
- Android: Notes | Customers tabs, customer list/search/create/edit/details/delete, optional camera or library photo. Camera is requested only when taking a photo. Interaction timeline is not included.

## Files created

- [src/SeQrRecall.Application/Abstractions/Customers/ICustomersService.cs](../src/SeQrRecall.Application/Abstractions/Customers/ICustomersService.cs)
- [src/SeQrRecall.Application/Services/CustomersService.cs](../src/SeQrRecall.Application/Services/CustomersService.cs)
- [src/SeQrRecall.Application/Customers/PhotoUploadRules.cs](../src/SeQrRecall.Application/Customers/PhotoUploadRules.cs)
- [src/SeQrRecall.Application/Mapping/CustomerMapping.cs](../src/SeQrRecall.Application/Mapping/CustomerMapping.cs)
- [src/SeQrRecall.Api/Controllers/CustomersController.cs](../src/SeQrRecall.Api/Controllers/CustomersController.cs)
- [tests/SeQrRecall.UnitTests/Customers/PhotoUploadRulesTests.cs](../tests/SeQrRecall.UnitTests/Customers/PhotoUploadRulesTests.cs)
- [tests/SeQrRecall.UnitTests/Customers/CustomersServiceTests.cs](../tests/SeQrRecall.UnitTests/Customers/CustomersServiceTests.cs)
- [tests/SeQrRecall.IntegrationTests/CustomersTests.cs](../tests/SeQrRecall.IntegrationTests/CustomersTests.cs)
- [mobile/SeQrRecall.Mobile/src/api/customersApi.ts](../mobile/SeQrRecall.Mobile/src/api/customersApi.ts)
- [mobile/SeQrRecall.Mobile/src/store/customersStore.ts](../mobile/SeQrRecall.Mobile/src/store/customersStore.ts)
- [mobile/SeQrRecall.Mobile/src/screens/customers/CustomersListScreen.tsx](../mobile/SeQrRecall.Mobile/src/screens/customers/CustomersListScreen.tsx)
- [mobile/SeQrRecall.Mobile/src/screens/customers/CustomerFormScreen.tsx](../mobile/SeQrRecall.Mobile/src/screens/customers/CustomerFormScreen.tsx)
- [mobile/SeQrRecall.Mobile/src/screens/customers/CustomerDetailsScreen.tsx](../mobile/SeQrRecall.Mobile/src/screens/customers/CustomerDetailsScreen.tsx)
- [mobile/SeQrRecall.Mobile/src/services/cameraPermission.ts](../mobile/SeQrRecall.Mobile/src/services/cameraPermission.ts)
- [mobile/SeQrRecall.Mobile/src/services/customerPhoto.ts](../mobile/SeQrRecall.Mobile/src/services/customerPhoto.ts)

## Database changes

None. `Customers` and `PhotoUrl` already exist from `InitialCreate`.

## API changes

Customer list/detail JSON uses `hasPhoto` instead of `photoUrl`. Photo bytes move through authorized stream endpoints.

## Mobile changes

Bottom tabs (Notes | Customers). Customer stack: list, form, details. Optional photo via `react-native-image-picker`.

## Tests

```text
dotnet test SeQrRecall.sln      SUCCESS  128 passed, 0 failed
  UnitTests                     102 passed
  IntegrationTests               26 passed
npx tsc --noEmit                SUCCESS  (mobile/SeQrRecall.Mobile)
```

## Known issues

1. Git is still not on PATH.
2. Integration tests share the Development SQL Server database (`SeQrRecall`).
3. Customer interaction timeline, audio, and interaction photos remain Phase 9.
4. Audio and photo size caps remain code constants (Phase 10). Rate limiting remains Phase 10.
5. In-app audio playback of a saved note is not included.

## Next phase

**Phase 9 — Customer interactions**

Owned interaction timeline for a customer: create interaction, upload audio and optional photo, process with the same STT/AI pipeline as notes, list newest first. Do not start until this phase is accepted.
