# SJewls Mobile (Saranga Jewels)

Customer mobile application built with Flutter for **Saranga Jewels**.

## Locked Brand Colour Palette

| Colour | Hex Code | Usage |
|---|---|---|
| Primary Green | `#0A3D2E` | App bars, primary buttons, bottom navigation bar |
| Gold Accent | `#D4AF37` | Icons, active tab indicators, payment buttons, status badges |
| Dark Text | `#1C2421` | Headings, body text, secondary interface elements |
| Background | `#FBFBFA` | Screen backgrounds, cards, light surfaces |

## Architecture & Tech Stack

- **Framework**: Flutter & Dart
- **State Management**: `flutter_riverpod`
- **Navigation**: `go_router` (`StatefulShellRoute.indexedStack` 4-tab MVP bottom navigation: **Home**, **Chittu**, **Jewelry**, **Payments**, plus top-level **Notifications** and **Profile** routes; **Lottery** is disabled for MVP via `kEnableLotteryMvp = false`)
- **Networking**: `dio` (`ApiClient` with automatic Bearer JWT token attachment, `401` token refresh via `/api/v1/auth/token/refresh`, and `ProblemDetails` error extraction)
- **Session Storage**: `flutter_secure_storage` wrapped by `TokenStorage`

## Integrated Backend APIs (`SJewls Customer Mobile API v1.2`)

1. **Customer Authentication (`/api/v1/auth/...`)**
   - `POST /api/v1/auth/otp/request`
   - `POST /api/v1/auth/otp/verify`
   - `POST /api/v1/auth/registration/complete`
   - `POST /api/v1/auth/token/refresh`
   - `POST /api/v1/auth/logout`
2. **Customer Chittu Plans, Slots & Instalments (`/api/v1/customer/chitu-...`)**
   - `GET /api/v1/customer/chitu-plans`
   - `GET /api/v1/customer/chitu-plans/{planId}`
   - `POST /api/v1/customer/chitu-plans/{planId}/join-preview`
   - `POST /api/v1/customer/chitu-plans/{planId}/join` (with `X-Idempotency-Key`)
   - `GET /api/v1/customer/chitu-investments` (with fallback to `/api/v1/customer/my-plans`)
   - `GET /api/v1/customer/chitu-investments/{planId}`
   - `GET /api/v1/customer/chitu-slots/{slotId}`
   - `GET /api/v1/customer/chitu-slots/{slotId}/payments`
   - `GET /api/v1/customer/chitu-plans/{planId}/payable-slots`
   - `POST /api/v1/customer/chitu-plans/{planId}/payments/preview`
   - `POST /api/v1/customer/chitu-plans/{planId}/payments` (with `X-Idempotency-Key`)
3. **Customer Jewellery Plans & Enrolments (`/api/v1/customer/jewellery-...`)**
   - `GET /api/v1/customer/jewellery-categories`
   - `GET /api/v1/customer/jewellery-categories/{categoryId}/plans`
   - `GET /api/v1/customer/jewellery-plans`
   - `GET /api/v1/customer/jewellery-plans/{planId}`
   - `POST /api/v1/customer/jewellery-enrolments/preview`
   - `POST /api/v1/customer/jewellery-enrolments` (with `X-Idempotency-Key`)
   - `GET /api/v1/customer/jewellery-enrolments`
   - `GET /api/v1/customer/jewellery-enrolments/{enrolmentId}`
   - `GET /api/v1/customer/jewellery-enrolments/{enrolmentId}/payments`
   - `POST /api/v1/customer/jewellery-enrolments/{enrolmentId}/preview`
   - `POST /api/v1/customer/jewellery-enrolments/{enrolmentId}/quotes`
   - `GET /api/v1/customer/jewellery-quotes/{quoteId}`
   - `POST /api/v1/customer/jewellery-quotes/{quoteId}/pay`
4. **Customer Unified Payments & Notifications (`/api/v1/customer/...`)**
   - `GET /api/v1/customer/payments`
   - `GET /api/v1/customer/payments/{paymentId}`
   - `GET /api/v1/customer/notifications`
   - `POST /api/v1/customer/notifications/{notificationId}/read`
   - `POST /api/v1/customer/notifications/read-all`
5. **Customer Profile & Contacts (`/api/v1/customers/me/...`)**
   - `GET /api/v1/customers/me`
   - `POST /api/v1/customers/me/contacts/otp/request`
   - `POST /api/v1/customers/me/contacts/otp/verify`
   - `POST /api/v1/customers/me/account-closure/otp/request`
   - `POST /api/v1/customers/me/account-closure/confirm`

## Running Locally & Overriding Backend Base URL

```bash
flutter pub get
dart format lib test
flutter analyze
flutter test
flutter run --dart-define=API_BASE_URL=https://sjewls-production.up.railway.app
```
