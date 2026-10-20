# SJewls — Complete API Specification & Scalar Testing Guide

This documentation covers the separated API specifications and interactive testing instructions for **Customer Mobile** and **Admin & Staff Portal**:

| Specification | OpenAPI JSON Spec | OpenAPI YAML Spec | Scalar Interactive Explorer |
| :--- | :--- | :--- | :--- |
| **Mobile App (Customer)** | [`SJewls_Mobile_API_OpenAPI.json`](file:///Users/sagee/Desktop/SJew/SJewls/docs/SJewls_Mobile_API_OpenAPI.json) (`/openapi/mobile.json`) | [`SJewls_Mobile_API_OpenAPI.yaml`](file:///Users/sagee/Desktop/SJew/SJewls/docs/SJewls_Mobile_API_OpenAPI.yaml) | 👉 **[http://localhost:5230/scalar/mobile](http://localhost:5230/scalar/mobile)** |
| **Admin & Staff Portal** | [`SJewls_Admin_API_OpenAPI.json`](file:///Users/sagee/Desktop/SJew/SJewls/docs/SJewls_Admin_API_OpenAPI.json) (`/openapi/admin.json`) | [`SJewls_Admin_API_OpenAPI.yaml`](file:///Users/sagee/Desktop/SJew/SJewls/docs/SJewls_Admin_API_OpenAPI.yaml) | 👉 **[http://localhost:5230/scalar/admin](http://localhost:5230/scalar/admin)** |
| **Full Combined (v1)** | `/openapi/v1.json` | — | 👉 **[http://localhost:5230/scalar/v1](http://localhost:5230/scalar/v1)** |

---

## 1. Environment & Scalar Explorer Access

- **API Base URL**: `http://localhost:5230`
- **Customer Mobile Scalar Reference**: 👉 **[http://localhost:5230/scalar/mobile](http://localhost:5230/scalar/mobile)**
- **Admin & Staff Portal Scalar Reference**: 👉 **[http://localhost:5230/scalar/admin](http://localhost:5230/scalar/admin)**
- **Unified Catalog Scalar Reference**: 👉 **[http://localhost:5230/scalar/v1](http://localhost:5230/scalar/v1)**
- **Admin Frontend Web App**: `http://localhost:3000`

### Seeded Super Admin Credentials
- **Username / Email**: `superadmin` / `admin@sjewls.lk`
- **Password**: `SuperAdmin@2026!`

---

## 2. Customer Authentication Flow (Tag: `Customer Authentication`)

All public authentication endpoints are unauthenticated and rate-limited via `AuthLimiter` (15 requests/minute).

### 2.1 Step 0: Check Contact Existence
- **Endpoint**: `POST /api/v1/auth/check`
- **Security**: Public
- **Request Body**:
  ```json
  {
    "contact": "+94771234567"
  }
  ```
- **Responses**:
  - `200 OK` (Existing Customer):
    ```json
    {
      "exists": true,
      "isProfileComplete": true,
      "normalizedContact": "+94771234567",
      "contactType": 1,
      "nextAction": "Login",
      "message": "Customer account found. Please request and verify an OTP to log in."
    }
    ```
  - `200 OK` (New Customer):
    ```json
    {
      "exists": false,
      "isProfileComplete": false,
      "normalizedContact": "+94771234567",
      "contactType": 1,
      "nextAction": "Register",
      "message": "Customer account not found. Please request an OTP to proceed with registration."
    }
    ```
  - `400 Bad Request`: Contact is empty or format is invalid.

---

### 2.2 Step 1: Request OTP
- **Endpoint**: `POST /api/v1/auth/otp/request`
- **Security**: Public
- **Request Body**:
  ```json
  {
    "contact": "+94771234567"
  }
  ```
- **Delivery Providers**:
  - **Phone (`+94...`)**: Dispatched via Text.lk SMS Gateway (`TextLKDemo` sender ID).
  - **Email (`...`)**: Dispatched via Gmail SMTP (`smtp.gmail.com:587`, STARTTLS) from `w.sagee5@gmail.com`.
- **Response (200 OK)**:
  ```json
  {
    "success": true,
    "message": "Verification code sent to your phone number.",
    "normalizedContact": "+94771234567",
    "contactType": 1,
    "expiresInSeconds": 300,
    "cooldownSeconds": 60,
    "devOtp": "123456",
    "isExistingCustomer": false,
    "nextAction": "Register"
  }
  ```
  *(Note: `devOtp` is returned strictly in local non-production environments to accelerate mobile testing).*
- **Error Responses**:
  - `400 Bad Request`: Active resend cooldown in effect (60s), invalid contact format, or SMS/SMTP provider error.

---

### 2.3 Step 2: Verify OTP
- **Endpoint**: `POST /api/v1/auth/otp/verify`
- **Security**: Public
- **Request Body**:
  ```json
  {
    "contact": "+94771234567",
    "code": "123456"
  }
  ```
- **Responses**:
  - `200 OK` (Existing Customer Login -> `Dashboard`):
    ```json
    {
      "nextAction": "Dashboard",
      "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
      "refreshToken": "4892c2de-f8d9-4821-b45a-c5ad4e2c0e81",
      "expiresInSeconds": 3600,
      "customer": {
        "id": "b3e0c058-29cb-4c28-98e9-56bf0a67e9c3",
        "fullName": "Kasun Perera",
        "nic": "199012345678",
        "phoneNumber": "+94771234567",
        "email": "kasun@example.com",
        "primaryContact": "+94771234567",
        "primaryBranchCode": "JAF-01",
        "isProfileComplete": true
      }
    }
    ```
  - `200 OK` (New Customer Registration -> `CompleteProfile`):
    ```json
    {
      "nextAction": "CompleteProfile",
      "registrationToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
      "registrationTokenExpiresInSeconds": 3600,
      "verifiedContact": "+94771234567",
      "verifiedContactType": 1,
      "requiredAdditionalContactType": "Email"
    }
    ```
  - `400 Bad Request`: Invalid code, attempts exhausted (max 5), or challenge expired.
  - `401 Unauthorized`: Account deactivated or soft-closed.

---

### 2.4 Step 3: Complete Customer Registration
- **Endpoint**: `POST /api/v1/auth/registration/complete`
- **Security**: Public (validated via `registrationToken`)
- **Request Body**:
  ```json
  {
    "registrationToken": "<REGISTRATION_TOKEN_FROM_STEP_2>",
    "fullName": "Kasun Perera",
    "dateOfBirth": "1994-05-15",
    "nic": "199412345678",
    "email": "kasun.perera@example.com",
    "preferredBranchId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
  }
  ```
- **Responses**:
  - `200 OK`: Returns full `VerifyOtpResponse` with `accessToken`, `refreshToken`, and complete customer profile.
  - `400 Bad Request`: Customer under 18 years old, invalid Sri Lankan NIC format, or duplicate email/phone.
  - `401 Unauthorized`: Expired or invalid registration token.

---

### 2.5 Refresh Access Token & Logout
- **Refresh Token Endpoint**: `POST /api/v1/auth/token/refresh`
  - **Body**: `{ "refreshToken": "..." }`
  - **Response (200 OK)**: `{ "accessToken": "...", "refreshToken": "...", "expiresInSeconds": 3600 }`
  - **Error (401 Unauthorized)**: Refresh token expired or revoked.
- **Logout Endpoint**: `POST /api/v1/auth/logout`
  - **Body**: `{ "refreshToken": "..." }`
  - **Response (200 OK)**: `{ "message": "Logged out successfully." }`

---

## 3. Customer Profile & Contacts (Tag: `Customer Profile & Contacts`)

All endpoints in this section require `Bearer <CUSTOMER_ACCESS_TOKEN>`.

### 3.1 Get Current Profile
- **Endpoint**: `GET /api/v1/customers/me`
- **Response (200 OK)**:
  ```json
  {
    "id": "b3e0c058-29cb-4c28-98e9-56bf0a67e9c3",
    "fullName": "Kasun Perera",
    "dateOfBirth": "1994-05-15",
    "nic": "199412345678",
    "phoneNumber": "+94771234567",
    "email": "kasun.perera@example.com",
    "isPhoneVerified": true,
    "isEmailVerified": true,
    "primaryBranchId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "primaryBranchCode": "JAF-01",
    "primaryBranchName": "Jaffna Main Branch",
    "isProfileComplete": true,
    "createdAtUtc": "2026-03-15T08:30:00Z"
  }
  ```

---

### 3.2 Request & Verify Secondary Contact OTP
Enables adding and activating a secondary email or phone number for dual login support.
- **Request Secondary OTP**: `POST /api/v1/customers/me/contacts/otp/request`
  - **Body**: `{ "contact": "kasun.secondary@example.com" }`
  - **Response (200 OK)**: `RequestOtpResponse`
  - **Error (409 Conflict)**: Contact already verified on this or another profile.
- **Verify Secondary OTP**: `POST /api/v1/customers/me/contacts/otp/verify`
  - **Body**: `{ "contact": "kasun.secondary@example.com", "code": "123456" }`
  - **Response (200 OK)**:
    ```json
    {
      "success": true,
      "message": "Additional contact verified successfully. You can now use this contact to sign in."
    }
    ```

---

### 3.3 Customer Self-Service Account Closure
Strictly requires customer to have **zero active jewellery plan enrolments**.
- **Request Closure OTP**: `POST /api/v1/customers/me/account-closure/otp/request`
  - **Response (200 OK)**: `RequestClosureOtpResponse` (`maskedContact`, `cooldownSeconds: 60`, `expiresInSeconds: 300`)
  - **Error (403 Forbidden)**: Customer has active plan enrolments.
- **Confirm Closure**: `POST /api/v1/customers/me/account-closure/confirm`
  - **Body**: `{ "code": "123456", "reason": "Moving abroad" }`
  - **Response (200 OK)**:
    ```json
    {
      "success": true,
      "message": "Your account has been successfully closed. All active sessions have been terminated.",
      "closedAtUtc": "2026-10-16T08:30:00Z",
      "status": "Inactive"
    }
    ```
  - **Error (400 Bad Request)**: Incorrect OTP code.
  - **Error (403 Forbidden)**: Active jewellery investments exist.

---

## 4. Customer Jewellery Plans (Tag: `Customer Jewellery Plans`)

Allows customers to browse plans tailored to their registered branch. Plans with future start dates or inactive status are filtered out.

### 4.1 Browse Categories
- **Endpoint**: `GET /api/v1/customer/jewellery-categories`
- **Response (200 OK)**:
  ```json
  [
    {
      "id": "c1a2b3c4-d5e6-7f8a-9b0c-1d2e3f4a5b6c",
      "name": "Bridal Gold",
      "description": "Exclusive gold jewellery savings plans for wedding collections.",
      "imageUrl": "https://supabase.../bridal.jpg",
      "availablePlansCount": 3
    }
  ]
  ```

---

### 4.2 Browse Plans in Category
- **Endpoint**: `GET /api/v1/customer/jewellery-categories/{categoryId}/plans`
- **Response (200 OK)**: Array of `CustomerJewelleryPlanDto`.

---

### 4.3 List All Available Plans
- **Endpoint**: `GET /api/v1/customer/jewellery-plans`
- **Query Parameter**: `categoryId` *(optional UUID filter)*
- **Response (200 OK)**:
  ```json
  [
    {
      "id": "d2b3c4d5-e6f7-8a9b-0c1d-2e3f4a5b6c7d",
      "name": "Sovereign Gold Accumulator",
      "categoryId": "c1a2b3c4-d5e6-7f8a-9b0c-1d2e3f4a5b6c",
      "categoryName": "Bridal Gold",
      "imageUrl": "https://supabase.../plan.jpg",
      "targetGrams": 8.0,
      "offeredDurations": [6, 12, 18],
      "premiumPercentage": 2.5,
      "startDate": "2026-01-01",
      "description": "Save for a 22K Sovereign gold coin over 6 to 18 months."
    }
  ]
  ```

---

### 4.4 Get Plan Details
- **Endpoint**: `GET /api/v1/customer/jewellery-plans/{planId}`
- **Response (200 OK)**: `CustomerJewelleryPlanDto`
- **Error (404 Not Found)**: Plan not found or not active.

---

## 5. Customer Jewellery Enrolments & Investments (Tag: `Customer Jewellery Enrolments`)

### 5.1 Preview Enrolment Confirmation
Before creating an enrolment, the mobile app fetches a confirmation summary.
- **Endpoint**: `POST /api/v1/customer/jewellery-enrolments/preview`
- **Request Body**:
  ```json
  {
    "planId": "d2b3c4d5-e6f7-8a9b-0c1d-2e3f4a5b6c7d",
    "selectedDurationMonths": 12
  }
  ```
- **Response (200 OK)**:
  ```json
  {
    "planId": "d2b3c4d5-e6f7-8a9b-0c1d-2e3f4a5b6c7d",
    "planName": "Sovereign Gold Accumulator",
    "categoryName": "Bridal Gold",
    "targetGrams": 8.0,
    "selectedDurationMonths": 12,
    "premiumPercentage": 2.5,
    "proposedJoiningDate": "2026-10-16",
    "proposedDeadline": "2027-10-16",
    "currency": "LKR",
    "hasPreviouslyJoined": true,
    "totalEnrolmentCount": 1,
    "activeEnrolmentCount": 1,
    "requiresRepeatJoinConfirmation": true
  }
  ```

---

### 5.2 Confirm & Join Jewellery Plan (Enrolment Creation)
- **Endpoint**: `POST /api/v1/customer/jewellery-enrolments`
- **Header**: `X-Idempotency-Key` *(optional UUID to guarantee single creation across network retries)*
- **Request Body**:
  ```json
  {
    "planId": "d2b3c4d5-e6f7-8a9b-0c1d-2e3f4a5b6c7d",
    "selectedDurationMonths": 12,
    "confirmRepeatEnrolment": false,
    "idempotencyKey": "a4f81014-9b21-4f18-bcf6-152865910efc"
  }
  ```
- **Repeat Enrolment Guard (`409 Conflict`)**:
  If customer has previously enrolled in this plan and `confirmRepeatEnrolment` is `false`, server rejects with HTTP 409:
  ```json
  {
    "code": "RepeatEnrolmentConfirmationRequired",
    "message": "You have previously enrolled in this jewellery plan. Confirmation is required to create an additional separate enrolment.",
    "totalEnrolments": 1,
    "activeEnrolments": 1,
    "totalEnrolmentCount": 1,
    "activeEnrolmentCount": 1
  }
  ```
  *(Mobile displays confirmation dialog. When user confirms, client re-sends with `"confirmRepeatEnrolment": true`).*
- **Response (201 Created)**:
  ```json
  {
    "enrolmentId": "8f12a4b8-c3d5-4e78-9a0b-123456789abc",
    "planId": "d2b3c4d5-e6f7-8a9b-0c1d-2e3f4a5b6c7d",
    "planName": "Sovereign Gold Accumulator",
    "categoryName": "Bridal Gold",
    "targetGrams": 8.0,
    "selectedDurationMonths": 12,
    "joiningDate": "2026-10-16",
    "deadline": "2027-10-16",
    "premiumPercentage": 2.5,
    "currency": "LKR",
    "totalConfirmedMoneyPaid": 0.0,
    "netAccumulatedGoldGrams": 0.0,
    "remainingGrams": 8.0,
    "individualProgressPercentage": 0.0,
    "enrolmentStatus": "Active",
    "isContributionAllowed": true,
    "createdAtUtc": "2026-10-16T10:00:00Z"
  }
  ```

---

### 5.3 List Customer Enrolments (My Investments)
- **Endpoint**: `GET /api/v1/customer/jewellery-enrolments`
- **Query Parameters**: `page` *(default: 1)*, `pageSize` *(default: 20)*
- **Response (200 OK)**:
  ```json
  {
    "items": [
      {
        "enrolmentId": "8f12a4b8-c3d5-4e78-9a0b-123456789abc",
        "planName": "Sovereign Gold Accumulator",
        "targetGrams": 8.0,
        "netAccumulatedGoldGrams": 2.5,
        "remainingGrams": 5.5,
        "individualProgressPercentage": 31.25,
        "enrolmentStatus": "Active",
        "isContributionAllowed": true
      }
    ],
    "totalCount": 1,
    "page": 1,
    "pageSize": 20,
    "totalPages": 1
  }
  ```

---

### 5.4 Get Enrolment Details & Contribution Ledger
- **Enrolment Details**: `GET /api/v1/customer/jewellery-enrolments/{enrolmentId}`
  - **Response (200 OK)**: `CustomerEnrolmentDto`
- **Contribution History Ledger**: `GET /api/v1/customer/jewellery-enrolments/{enrolmentId}/payments`
  - **Response (200 OK)**:
    ```json
    [
      {
        "paymentId": "5e1a2b3c-4d5e-6f7a-8b9c-0d1e2f3a4b5c",
        "contributionId": "7a8b9c0d-1e2f-3a4b-5c6d-7e8f9a0b1c2d",
        "currencyAmount": 75000.0,
        "currency": "LKR",
        "goldGramsCredited": 2.5,
        "appliedRatePerGram": 30000.0,
        "confirmedAtUtc": "2026-10-16T10:15:00Z",
        "status": "Succeeded",
        "paymentMethod": "Online",
        "paymentReference": "TXN-20261016-0001"
      }
    ]
    ```

---

## 6. Gold Quotes & Payments (Tag: `Customer Jewellery Quotes & Payments`)

Supports two input calculation modes:
1. `BuyGram`: Customer enters desired gold grams (e.g., `2.0` grams). Server computes required LKR payable.
2. `BuyAmount`: Customer enters money budget (e.g., `50000.0` LKR). Server computes credited gold grams.

### 6.1 Preview Gold Payment Calculation
Informational breakdown with live gold rate without creating a database quote or holding money.
- **Endpoint**: `POST /api/v1/customer/jewellery-enrolments/{enrolmentId}/preview`
- **Request Body (BuyGram Mode)**:
  ```json
  {
    "inputMode": "BuyGram",
    "requestedGrams": 2.0
  }
  ```
- **Response (200 OK)**:
  ```json
  {
    "inputMode": "BuyGram",
    "requestedGrams": 2.0,
    "amountToPay": 61500.0,
    "currency": "LKR",
    "ratePerGram": 30000.0,
    "premiumPercentage": 2.5,
    "baseGoldGrams": 2.0,
    "premiumGrams": 0.05,
    "chargeableGoldGrams": 2.05,
    "totalGoldCredit": 2.0,
    "remainingGramsAfterPayment": 6.0,
    "remainingTargetGrams": 8.0,
    "willExceedTarget": false,
    "rateSource": "GoldAPI.io",
    "rateAsOf": "2026-10-16T10:00:00Z"
  }
  ```
- **Errors**:
  - `400 Bad Request`: Grams or Amount <= 0.
  - `409 Conflict`: Resulting grams would exceed enrolment target or plan is completed/closed.
  - `503 Service Unavailable`: Gold rate service temporarily unreachable.

---

### 6.2 Lock Authoritative Payment Quote (Pay Now)
Locks an authoritative quote for **5 minutes** guaranteeing payable amount and credited grams.
- **Endpoint**: `POST /api/v1/customer/jewellery-enrolments/{enrolmentId}/quotes`
- **Request Body**:
  ```json
  {
    "inputMode": "BuyAmount",
    "enteredAmount": 50000.0
  }
  ```
- **Response (201 Created)**:
  ```json
  {
    "quoteId": "f7a8b9c0-1d2e-3f4a-5b6c-7d8e9f0a1b2c",
    "enrolmentId": "8f12a4b8-c3d5-4e78-9a0b-123456789abc",
    "inputMode": "BuyAmount",
    "enteredAmount": 50000.0,
    "amountToPay": 50000.0,
    "totalGoldCredit": 1.626,
    "ratePerGram": 30000.0,
    "premiumPercentage": 2.5,
    "status": "Active",
    "createdAtUtc": "2026-10-16T10:20:00Z",
    "expiresAtUtc": "2026-10-16T10:25:00Z",
    "isExpired": false
  }
  ```

---

### 6.3 Get Payment Quote Status
- **Endpoint**: `GET /api/v1/customer/jewellery-quotes/{quoteId}`
- **Response (200 OK)**: `CustomerPaymentQuoteDto`

---

### 6.4 Initiate Payment (Deferred Gateway Contract)
- **Endpoint**: `POST /api/v1/customer/jewellery-quotes/{quoteId}/pay`
- **Response (501 Not Implemented)**:
  Per architectural specification, external gateway checkout integration is deferred. No fake payment URL is emitted.
  ```json
  {
    "code": "PaymentProviderNotConfigured",
    "message": "Payment gateway integration is currently deferred. No online payment provider is configured.",
    "quoteId": "f7a8b9c0-1d2e-3f4a-5b6c-7d8e9f0a1b2c",
    "amountToPay": 50000.0,
    "currency": "LKR",
    "isConfigured": false
  }
  ```

---

## 7. Customer Notifications (Tag: `Customer Notifications`)

Notifications are automatically generated upon enrolment confirmation, completion, and milestone events.

### 7.1 Fetch Notifications Feed
- **Endpoint**: `GET /api/v1/customer/notifications`
- **Query Parameters**: `page` *(default: 1)*, `pageSize` *(default: 20)*
- **Response (200 OK)**:
  ```json
  {
    "items": [
      {
        "id": "e1f2a3b4-c5d6-7e8f-9a0b-1c2d3e4f5a6b",
        "title": "Plan Enrolment Confirmed",
        "message": "Congratulations! You have joined Sovereign Gold Accumulator (Target: 8.0g).",
        "type": "enrolment",
        "isRead": false,
        "readAtUtc": null,
        "createdAtUtc": "2026-10-16T10:00:00Z"
      }
    ],
    "totalCount": 1,
    "unreadCount": 1,
    "page": 1,
    "pageSize": 20,
    "totalPages": 1
  }
  ```

---

### 7.2 Mark Single Notification as Read
- **Endpoint**: `POST /api/v1/customer/notifications/{notificationId}/read`
- **Response (200 OK)**:
  ```json
  {
    "success": true,
    "message": "Notification marked as read.",
    "markedCount": 1
  }
  ```
- **Error (404 Not Found)**: Notification not found for this customer.

---

### 7.3 Mark All Notifications as Read
- **Endpoint**: `POST /api/v1/customer/notifications/read-all`
- **Response (200 OK)**:
  ```json
  {
    "success": true,
    "message": "3 notifications marked as read.",
    "markedCount": 3
  }
  ```

---

## 8. Store Branches (Tag: `Branches`)

Publicly accessible list of active store branches for customer onboarding and store pickers.
- **Endpoint**: `GET /api/v1/branches`
- **Security**: Public
- **Response (200 OK)**:
  ```json
  [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "code": "JAF-01",
      "name": "Jaffna Main Branch",
      "address": "120 Main Street",
      "city": "Jaffna",
      "country": "LK",
      "timezone": "Asia/Colombo",
      "currency": "LKR",
      "isActive": true
    }
  ]
  ```

---

## 9. System Health (Tag: `System`)

- **Endpoint**: `GET /api/v1/health`
- **Security**: Public
- **Response (200 OK)**:
  ```json
  {
    "status": "Healthy",
    "timestampUtc": "2026-10-16T10:00:00Z",
    "databaseConnected": true,
    "databaseError": null,
    "environment": "Development"
  }
  ```

---

## 10. Complete Mobile End-to-End Curl Suite

Run the script below in your terminal to test the complete customer mobile workflow from authentication to jewellery plan browsing, enrolment, gold rate quote locking, and notifications:

```bash
#!/bin/bash
set -e

BASE_URL="http://localhost:5230"
TEST_PHONE="+94779998877"

echo "=== 1. Health Check ==="
curl -s "$BASE_URL/api/v1/health" | jq .

echo "=== 2. Check Contact ==="
CHECK_RES=$(curl -s -X POST "$BASE_URL/api/v1/auth/check" \
  -H "Content-Type: application/json" \
  -d "{\"contact\": \"$TEST_PHONE\"}")
echo "$CHECK_RES" | jq .

echo "=== 3. Request OTP ==="
OTP_REQ_RES=$(curl -s -X POST "$BASE_URL/api/v1/auth/otp/request" \
  -H "Content-Type: application/json" \
  -d "{\"contact\": \"$TEST_PHONE\"}")
echo "$OTP_REQ_RES" | jq .

DEV_OTP=$(echo "$OTP_REQ_RES" | jq -r '.devOtp // "123456"')

echo "=== 4. Verify OTP (Code: $DEV_OTP) ==="
VERIFY_RES=$(curl -s -X POST "$BASE_URL/api/v1/auth/otp/verify" \
  -H "Content-Type: application/json" \
  -d "{\"contact\": \"$TEST_PHONE\", \"code\": \"$DEV_OTP\"}")
echo "$VERIFY_RES" | jq .

NEXT_ACTION=$(echo "$VERIFY_RES" | jq -r '.nextAction')

if [ "$NEXT_ACTION" == "CompleteProfile" ]; then
  REG_TOKEN=$(echo "$VERIFY_RES" | jq -r '.registrationToken')
  echo "=== 5. Complete Registration ==="
  COMPLETE_RES=$(curl -s -X POST "$BASE_URL/api/v1/auth/registration/complete" \
    -H "Content-Type: application/json" \
    -d "{
      \"registrationToken\": \"$REG_TOKEN\",
      \"fullName\": \"Dev Test Customer\",
      \"dateOfBirth\": \"1995-08-20\",
      \"nic\": \"199512345678\",
      \"email\": \"dev.test.$RANDOM@example.com\"
    }")
  echo "$COMPLETE_RES" | jq .
  ACCESS_TOKEN=$(echo "$COMPLETE_RES" | jq -r '.accessToken')
else
  ACCESS_TOKEN=$(echo "$VERIFY_RES" | jq -r '.accessToken')
fi

echo "=== 6. Get Customer Profile ==="
curl -s "$BASE_URL/api/v1/customers/me" \
  -H "Authorization: Bearer $ACCESS_TOKEN" | jq .

echo "=== 7. Browse Jewellery Categories ==="
CATEGORIES_RES=$(curl -s "$BASE_URL/api/v1/customer/jewellery-categories" \
  -H "Authorization: Bearer $ACCESS_TOKEN")
echo "$CATEGORIES_RES" | jq .

echo "=== 8. Browse Available Jewellery Plans ==="
PLANS_RES=$(curl -s "$BASE_URL/api/v1/customer/jewellery-plans" \
  -H "Authorization: Bearer $ACCESS_TOKEN")
echo "$PLANS_RES" | jq .

FIRST_PLAN_ID=$(echo "$PLANS_RES" | jq -r '.[0].id // empty')

if [ -n "$FIRST_PLAN_ID" ]; then
  echo "=== 9. Preview Enrolment for Plan: $FIRST_PLAN_ID ==="
  curl -s -X POST "$BASE_URL/api/v1/customer/jewellery-enrolments/preview" \
    -H "Authorization: Bearer $ACCESS_TOKEN" \
    -H "Content-Type: application/json" \
    -d "{\"planId\": \"$FIRST_PLAN_ID\", \"selectedDurationMonths\": 12}" | jq .

  echo "=== 10. Confirm & Join Jewellery Plan ==="
  JOIN_RES=$(curl -s -X POST "$BASE_URL/api/v1/customer/jewellery-enrolments" \
    -H "Authorization: Bearer $ACCESS_TOKEN" \
    -H "Content-Type: application/json" \
    -H "X-Idempotency-Key: $(uuidgen)" \
    -d "{\"planId\": \"$FIRST_PLAN_ID\", \"selectedDurationMonths\": 12, \"confirmRepeatEnrolment\": true}")
  echo "$JOIN_RES" | jq .

  ENROLMENT_ID=$(echo "$JOIN_RES" | jq -r '.enrolmentId // empty')

  if [ -n "$ENROLMENT_ID" ]; then
    echo "=== 11. Preview Payment Calculation (BuyGram) ==="
    curl -s -X POST "$BASE_URL/api/v1/customer/jewellery-enrolments/$ENROLMENT_ID/preview" \
      -H "Authorization: Bearer $ACCESS_TOKEN" \
      -H "Content-Type: application/json" \
      -d '{"inputMode": "BuyGram", "requestedGrams": 1.0}' | jq .

    echo "=== 12. Create Locked Payment Quote (5-min lock) ==="
    QUOTE_RES=$(curl -s -X POST "$BASE_URL/api/v1/customer/jewellery-enrolments/$ENROLMENT_ID/quotes" \
      -H "Authorization: Bearer $ACCESS_TOKEN" \
      -H "Content-Type: application/json" \
      -d '{"inputMode": "BuyAmount", "enteredAmount": 25000.0}')
    echo "$QUOTE_RES" | jq .

    QUOTE_ID=$(echo "$QUOTE_RES" | jq -r '.quoteId // empty')

    if [ -n "$QUOTE_ID" ]; then
      echo "=== 13. Initiate Payment (Deferred Gateway - Returns HTTP 501) ==="
      curl -s -X POST "$BASE_URL/api/v1/customer/jewellery-quotes/$QUOTE_ID/pay" \
        -H "Authorization: Bearer $ACCESS_TOKEN" | jq .
    fi
  fi
fi

echo "=== 14. Fetch Customer Notifications ==="
curl -s "$BASE_URL/api/v1/customer/notifications" \
  -H "Authorization: Bearer $ACCESS_TOKEN" | jq .

echo "=== 15. Mark All Notifications as Read ==="
curl -s -X POST "$BASE_URL/api/v1/customer/notifications/read-all" \
  -H "Authorization: Bearer $ACCESS_TOKEN" | jq .

echo "=== Mobile Testing Suite Completed Successfully! ==="
```

---

## 11. Admin & Staff Portal Reference (Scalar `/scalar/admin`)

For full admin documentation including Staff Authentication, Customer Directory (with Full Unmasked NIC), Plan Tracking with Lifetime Aggregates, and Branch Management, visit 👉 **[http://localhost:5230/scalar/admin](http://localhost:5230/scalar/admin)** or review [`SJewls_Admin_API_OpenAPI.json`](file:///Users/sagee/Desktop/SJew/SJewls/docs/SJewls_Admin_API_OpenAPI.json).
