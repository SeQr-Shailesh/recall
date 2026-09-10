# Phase 9 completion

## Completed

- Re-inspected interaction contracts, `CustomerInteraction` / action-item entities, the notes processing pipeline, and the customer details screen.
- Implemented `ProcessCustomerInteractionAsync` on the shared `INoteProcessingService`. Same rules as notes: STT fail → `Failed`; AI fail after a transcript → `Completed` with `processingError`. Background worker also marks a stuck interaction as `Failed`.
- Owned APIs: timeline `GET /api/v1/customers/{customerId}/interactions`, create/get/status, audio upload/stream, photo upload/stream. JWT claims only. Another user's customer or interaction is 404.
- Android: customer details now shows a conversation timeline. Record conversation, poll status, open details, optional photo. No CRM entities.

## Files created

- [src/SeQrRecall.Application/Abstractions/Interactions/ICustomerInteractionsService.cs](../src/SeQrRecall.Application/Abstractions/Interactions/ICustomerInteractionsService.cs)
- [src/SeQrRecall.Application/Services/CustomerInteractionsService.cs](../src/SeQrRecall.Application/Services/CustomerInteractionsService.cs)
- [src/SeQrRecall.Application/Mapping/CustomerInteractionMapping.cs](../src/SeQrRecall.Application/Mapping/CustomerInteractionMapping.cs)
- [src/SeQrRecall.Api/Controllers/CustomerInteractionsController.cs](../src/SeQrRecall.Api/Controllers/CustomerInteractionsController.cs)
- [tests/SeQrRecall.UnitTests/Interactions/CustomerInteractionsServiceTests.cs](../tests/SeQrRecall.UnitTests/Interactions/CustomerInteractionsServiceTests.cs)
- [tests/SeQrRecall.IntegrationTests/CustomerInteractionsTests.cs](../tests/SeQrRecall.IntegrationTests/CustomerInteractionsTests.cs)
- [mobile/SeQrRecall.Mobile/src/api/interactionsApi.ts](../mobile/SeQrRecall.Mobile/src/api/interactionsApi.ts)
- [mobile/SeQrRecall.Mobile/src/store/interactionsStore.ts](../mobile/SeQrRecall.Mobile/src/store/interactionsStore.ts)
- [mobile/SeQrRecall.Mobile/src/screens/customers/RecordInteractionScreen.tsx](../mobile/SeQrRecall.Mobile/src/screens/customers/RecordInteractionScreen.tsx)
- [mobile/SeQrRecall.Mobile/src/screens/customers/InteractionDetailsScreen.tsx](../mobile/SeQrRecall.Mobile/src/screens/customers/InteractionDetailsScreen.tsx)

## Database changes

None. `CustomerInteractions` and `CustomerInteractionActionItems` already exist from `InitialCreate`.

## API changes

Interaction list/detail JSON uses `hasAudio` / `hasPhoto`. Photo and audio bytes move through authorized stream endpoints.

## Mobile changes

Customer details includes a newest-first conversation list and **Tap to speak**. Record and details screens poll the same status contract as notes.

## Tests

```text
dotnet test SeQrRecall.sln      SUCCESS  144 passed, 0 failed
  UnitTests                     114 passed
  IntegrationTests               30 passed
npx tsc --noEmit                SUCCESS  (mobile/SeQrRecall.Mobile)
```

## Known issues

1. Git is still not on PATH.
2. Integration tests share the Development SQL Server database (`SeQrRecall`).
3. Audio and photo size caps remain code constants (Phase 10). Rate limiting remains Phase 10.
4. In-app audio playback of a saved note or conversation is not included.

## Next phase

**Phase 10 — Hardening**

Rate limiting, configurable upload caps, production security headers/CORS as specified, and related host hardening. Do not start until this phase is accepted.
