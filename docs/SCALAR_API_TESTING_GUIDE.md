# SJewls — Complete API Specification & Scalar Testing Guide

This documentation covers the separated API specifications for **Customer Mobile** and **Admin & Staff Portal**:

| Specification | OpenAPI JSON Spec | OpenAPI YAML Spec | Scalar Interactive Explorer |
| :--- | :--- | :--- | :--- |
| **Mobile App (Customer)** | [`SJewls_Mobile_API_OpenAPI.json`](file:///Users/sagee/Desktop/SJew/SJewls/docs/SJewls_Mobile_API_OpenAPI.json) (`/openapi/mobile.json`) | [`SJewls_Mobile_API_OpenAPI.yaml`](file:///Users/sagee/Desktop/SJew/SJewls/docs/SJewls_Mobile_API_OpenAPI.yaml) | 👉 **[http://localhost:5230/scalar/mobile](http://localhost:5230/scalar/mobile)** |
| **Admin & Staff Portal** | [`SJewls_Admin_API_OpenAPI.json`](file:///Users/sagee/Desktop/SJew/SJewls/docs/SJewls_Admin_API_OpenAPI.json) (`/openapi/admin.json`) | [`SJewls_Admin_API_OpenAPI.yaml`](file:///Users/sagee/Desktop/SJew/SJewls/docs/SJewls_Admin_API_OpenAPI.yaml) | 👉 **[http://localhost:5230/scalar/admin](http://localhost:5230/scalar/admin)** |
| **Full Combined (v1)** | `/openapi/v1.json` | — | 👉 **[http://localhost:5230/scalar/v1](http://localhost:5230/scalar/v1)** |

---

## 1. Environment & Scalar Explorer Access

- **API Base URL**: `http://localhost:5230`
- **Mobile Scalar Explorer**: 👉 **[http://localhost:5230/scalar/mobile](http://localhost:5230/scalar/mobile)**
- **Admin Scalar Explorer**: 👉 **[http://localhost:5230/scalar/admin](http://localhost:5230/scalar/admin)**
- **Admin Frontend**: `http://localhost:3000`

### Initial Super Admin Credentials (Seeded)
- **Username / Email**: `superadmin` / `admin@sjewls.lk`
- **Password**: `SuperAdmin@2026!`

---

## 2. Customer Authentication & Registration Flow (Mobile)

All customer authentication endpoints are grouped under the **Customer Authentication** tag.

### Step 0: Check Contact Existence
- **Endpoint**: `POST /api/v1/auth/check`
- **Request Body**:
  ```json
  {
    "contact": "+94771234567"
  }
  ```
- **Responses**:
  - Existing Customer:
    ```json
    {
      "exists": true,
      "isProfileComplete": true,
      "nextAction": "Login",
      "message": "Customer account found. Please request and verify an OTP to log in."
    }
    ```
  - Existing Customer with Incomplete Profile (e.g. Admin-Created without DOB):
    ```json
    {
      "exists": true,
      "isProfileComplete": false,
      "normalizedContact": "+94771234567",
      "contactType": 1,
      "nextAction": "CompleteProfile",
      "message": "Customer account found. Please request and verify an OTP to complete your profile."
    }
    ```
  - Unregistered / New Customer:
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

---

### Step 1: Request OTP
- **Endpoint**: `POST /api/v1/auth/otp/request`
- **Request Body**:
  ```json
  {
    "contact": "+94771234567",
    "channel": "Sms",
    "purpose": "LoginOrRegister"
  }
  ```
- **Delivery Mechanisms**:
  - **Phone (`+94...`)**: Dispatched via Text.lk SMS Gateway (`TextLKDemo` sender ID). Returns `200 OK` once accepted by the gateway.
  - **Email (`...`)**: Dispatched via Gmail SMTP (`smtp.gmail.com:587`, STARTTLS) from `w.sageesan@gmail.com`.
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
  *(Note: `devOtp` is provided only in non-production environments to streamline automated and developer testing).*

---

### Step 2: Verify OTP
- **Endpoint**: `POST /api/v1/auth/otp/verify`
- **Request Body**:
  ```json
  {
    "contact": "+94771234567",
    "code": "123456"
  }
  ```
- **Outcomes**:
  - **Complete Active Customer** (`nextAction: "Dashboard"`): Returns active JWT `accessToken`, `refreshToken`, and customer profile summary.
  - **New or Incomplete Profile / Admin-Created Customer** (`nextAction: "CompleteProfile"`):
    ```json
    {
      "nextAction": "CompleteProfile",
      "accessToken": null,
      "refreshToken": null,
      "customer": {
        "id": "3358ccae-e77d-415a-8c5c-5ca0b9811e9e",
        "fullName": "sai",
        "nic": "199928918232",
        "phoneNumber": "+94789832243",
        "email": "w.sageesan@gmail.com",
        "primaryContact": "w.sageesan@gmail.com",
        "primaryBranchCode": "JAF-01",
        "isProfileComplete": false
      },
      "registrationToken": "reg_6bff9140e5a7...",
      "registrationTokenExpiresInSeconds": 3600,
      "verifiedContact": "w.sageesan@gmail.com",
      "verifiedContactType": 2,
      "requiredAdditionalContactType": "Phone"
    }
    ```
  - **Inactive / Deactivated / Closed Account** (`401 Unauthorized`):
    ```json
    {
      "message": "Customer account has been deactivated or closed. Please contact customer support."
    }
    ```
    *Rule: Customer OTP verification or profile completion never automatically reactivates an inactive account.*

---

### Step 3: Complete Registration
- **Endpoint**: `POST /api/v1/auth/registration/complete`
- **Request Body**:
  ```json
  {
    "registrationToken": "reg_6bff9140e5a7...",
    "dateOfBirth": "1995-05-15",
    "fullName": "Oliver Brown",
    "nic": "199512345678",
    "email": "oliver.brown@example.com"
  }
  ```
  *(Note: For admin-created customers where Full Name and NIC were already entered by admin, only `registrationToken` and `dateOfBirth` are mandatory. Full Name and NIC automatically fall back to the existing record if omitted).*
- **Account Linking Behavior**:
  - If an admin previously created a customer record with matching unverified contacts or NIC, the system **connects directly to the existing record** without creating a duplicate.
  - Marks the verified contact as verified (`IsPhoneVerified = true` / `IsEmailVerified = true`), records DOB, sets `IsProfileComplete = true`, and issues active tokens.

---

## 3. Customer Self-Service Account Closure (Mobile Contract)

This section contains the official API contract for mobile developers implementing the customer self-service "Delete Account" action.

### Business & Compliance Rules
1. **Soft Closure, Not Permanent Hard Deletion**:
   - Deactivates the account (`IsActive = false`).
   - Retains the customer record, financial obligations, Chitu slots, Jewellery plans, payments, and audit logs for regulatory and statutory compliance.
   - Sets `ClosedAtUtc` to current timestamp and `ClosureReason` to `"CustomerRequestedClosure"`.
2. **Immediate Session & Token Revocation**:
   - Immediately revokes all active refresh tokens in the database.
   - Real-time token validation (`OnTokenValidated`) blocks existing JWT access tokens immediately on subsequent requests (`401 Unauthorized`).
3. **Fresh Verification Challenge**:
   - Requires fresh verification of an existing verified contact via OTP.
   - The OTP challenge is bound specifically to the customer and the `AccountClosure` purpose. A general login OTP cannot authorize account closure.
4. **Reactivation Governance**:
   - Reopening an account requires an authorized admin action after confirming customer request. Customer OTP login alone cannot reactivate an inactive account.

---

### Step 1: Request Account Closure OTP
- **Endpoint**: `POST /api/v1/customers/me/account-closure/otp/request`
- **Authentication**: `Bearer <CustomerAccessToken>`
- **Request Headers**:
  - `Authorization: Bearer <CustomerAccessToken>`
  - `Content-Type: application/json`
- **Request Body**:
  ```json
  {
    "channel": "Sms" 
  }
  ```
  *(channel can be `"Sms"` or `"Email"`)*
- **Response (200 OK)**:
  ```json
  {
    "success": true,
    "message": "Verification code sent to your phone number.",
    "deliveryChannel": "SMS",
    "maskedContact": "+94****67",
    "expiresInSeconds": 300,
    "cooldownSeconds": 60,
    "devOtp": "898640"
  }
  ```
- **Error Responses**:
  - `401 Unauthorized`: Token missing, expired, or customer is already inactive.
  - `400 Bad Request`: Active resend cooldown in effect or delivery channel failure.

---

### Step 2: Confirm Account Closure
- **Endpoint**: `POST /api/v1/customers/me/account-closure/confirm`
- **Authentication**: `Bearer <CustomerAccessToken>`
- **Request Headers**:
  - `Authorization: Bearer <CustomerAccessToken>`
  - `Content-Type: application/json`
- **Request Body**:
  ```json
  {
    "code": "898640"
  }
  ```
- **Response (200 OK)**:
  ```json
  {
    "success": true,
    "message": "Your account has been deactivated. For regulatory and statutory compliance, your profile, payment records, and jewellery plan histories are safely retained. To reactivate your account in the future, please contact customer support or visit your branch.",
    "closedAtUtc": "2026-10-08T07:42:20.818625+00:00",
    "status": "Inactive"
  }
  ```
- **Error Responses**:
  - `400 Bad Request`:
    ```json
    {
      "message": "Incorrect verification code. 4 attempts remaining."
    }
    ```
  - `400 Bad Request` (Invalid or expired challenge):
    ```json
    {
      "message": "Invalid, expired, or already consumed closure challenge. Please request a new verification code."
    }
    ```
  - `401 Unauthorized`: Customer token is invalid or account is already inactive.

---

## 4. Admin Customers Directory & Lifecycle Management

All admin customer endpoints require `StaffOnly` authorization with a valid staff JWT Bearer token.

### 1. Customer Summary Statistics
- **Endpoint**: `GET /api/v1/admin/customers/statistics`
- **Query Parameters**:
  - `search` *(optional)*: Search query by name, phone, email, or NIC.
  - `branchId` *(optional)*: Filter by branch GUID.
- **Statistics Isolation Rule**:
  - Calculates statistics across matching records prior to pagination.
  - **Does NOT apply table status filter** so Active and Inactive cards maintain separate, independent counts and never display misleading zero values.
- **Response (200 OK)**:
  ```json
  {
    "totalCustomers": 12,
    "activeCustomers": 10,
    "inactiveCustomers": 2
  }
  ```

---

### 2. Paginated Customer Table
- **Endpoint**: `GET /api/v1/admin/customers`
- **Query Parameters**:
  - `page` *(default: 1)*: Current page number.
  - `pageSize` *(default: 10)*: Page size.
  - `search` *(optional)*: Filter by customer full name, phone number, email, or NIC.
  - `branchId` *(optional)*: Restrict to branch (Super Admin only; branch staff automatically restricted to assigned branch).
  - `status` *(optional)*: Filter table by status (`"ALL"`, `"Active"`, `"Inactive"`).
- **Verification Rule**:
  - In all administrative responses (both table listing and customer detail), the customer NIC is returned **full and unmasked** (e.g. `199512345678` or `851234567V`) so store administrators can physically verify the customer's identity card when collecting gold jewellery or prize draws in store.
  - Status is determined strictly by customer account status (`isActive`), not contact verification or plan activity.
- **Response (200 OK)**:
  ```json
  {
    "items": [
      {
        "id": "3a5d7f38-0a45-41ef-b281-6bf53a538a38",
        "fullName": "Oliver Brown",
        "phoneNumber": "+94771234567",
        "email": "oliver.brown@example.com",
        "nic": "199512345678",
        "isPhoneVerified": true,
        "isEmailVerified": false,
        "primaryBranchId": "a0000000-0000-0000-0000-000000000001",
        "primaryBranchName": "SJewls Jaffna Main Branch",
        "primaryBranchCode": "JAF-01",
        "isActive": true,
        "isProfileComplete": true,
        "activePlansCount": 0,
        "totalSlotsCount": 0,
        "createdAtUtc": "2026-10-08T07:33:08.207523+00:00",
        "deactivatedAtUtc": null,
        "deactivationReason": null,
        "closedAtUtc": null,
        "closureReason": null
      }
    ],
    "page": 1,
    "pageSize": 10,
    "totalCount": 1,
    "totalPages": 1,
    "hasPreviousPage": false,
    "hasNextPage": false
  }
  ```

---

### 3. Detailed Customer Profile (Full Unmasked NIC)
- **Endpoint**: `GET /api/v1/admin/customers/{id}`
- **Permissions**: Authorized staff within branch scope or Super Admin.
- **Full Visibility Rule**:
  - Returns the **full unmasked NIC** (e.g. `199512345678`), date of birth, verification timestamps, and associated Chitu / Jewellery plan slots.
- **Response (200 OK)**:
  ```json
  {
    "id": "3a5d7f38-0a45-41ef-b281-6bf53a538a38",
    "fullName": "Oliver Brown",
    "dateOfBirth": "1995-05-15",
    "nic": "199512345678",
    "phoneNumber": "+94771234567",
    "email": "oliver.brown@example.com",
    "isPhoneVerified": true,
    "isEmailVerified": false,
    "primaryBranchId": "a0000000-0000-0000-0000-000000000001",
    "primaryBranchName": "SJewls Jaffna Main Branch",
    "primaryBranchCode": "JAF-01",
    "isActive": true,
    "isProfileComplete": true,
    "createdAtUtc": "2026-10-08T07:33:08.207523+00:00",
    "chituSlots": [],
    "jewelleryEnrolments": []
  }
  ```

---

### 4. Create Customer Profile by Staff
- **Endpoint**: `POST /api/v1/admin/customers`
- **Request Body**:
  ```json
  {
    "fullName": "Amelia Clarke",
    "phoneNumber": "+94772345678",
    "email": "amelia.clarke@example.com",
    "nic": "199612345678",
    "branchId": "a0000000-0000-0000-0000-000000000001"
  }
  ```
- **Validation & Duplicate Prevention Rules**:
  - FullName: min 2 chars, max 150.
  - Phone: normalized E.164.
  - Email: valid email format.
  - NIC: validated Sri Lankan NIC format (10-char old format or 12-char new format).
  - Duplicate check: If phone, email, or NIC already exists, returns `409 Conflict`.
  - Branch restriction: Branch staff can create customers only in their permitted branch scope. Super Admin can specify any active branch.
  - Contact verification: Contacts entered by staff remain unverified until the customer verifies via OTP. A password is not required.
- **Response (201 Created)**:
  Returns full customer detail with `isPhoneVerified: false`, `isEmailVerified: false`, `isProfileComplete: false`.

---

### 5. Activate or Deactivate Customer Account
- **Endpoint**: `PATCH /api/v1/admin/customers/{id}/status`
- **Request Body**:
  ```json
  {
    "isActive": false,
    "reason": "Suspension requested pending KYC re-verification"
  }
  ```
- **Rules**:
  - `reason` is required when deactivating.
  - Deactivation immediately revokes all customer refresh tokens and invalidates active JWT tokens in real-time.
  - Preserves plans, payments, and audit history.
  - Inactive customers cannot log in or make transactions.
  - Customer OTP verification or profile completion cannot automatically reactivate an inactive account. Reactivation requires an authorized admin action (`"isActive": true`).

---

## 5. Staff Management & Deactivation Protection

All staff management endpoints are under `Admin Users`.

### 1. Update Staff Status (Activate / Deactivate)
- **Endpoint**: `PATCH /api/v1/admin/users/{id}/status`
- **Request Body**:
  ```json
  {
    "isActive": false,
    "reason": "Employee departure - immediate access termination"
  }
  ```
- **Security Protections Enforced by Server**:
  1. **Self-Deactivation Prevention**:
     - Staff members cannot deactivate their own account. Attempting to deactivate self returns `409 Conflict`:
       ```json
       {
         "message": "You cannot deactivate or change the status of your own staff account."
       }
       ```
  2. **Last Super Admin Protection**:
     - System protects the last active Super Admin. Attempting to deactivate the only active Super Admin returns `409 Conflict`:
       ```json
       {
         "message": "Cannot deactivate the last active Super Admin on the platform."
       }
       ```
  3. **Privilege Boundary**:
     - Only Super Admin can manage Super Admin accounts. Branch Admin cannot deactivate Super Admins or grant higher roles.
  4. **Mandatory Deactivation Reason**:
     - Reason is mandatory for audit trail (`400 Bad Request` if blank).
  5. **Immediate Token & Session Invalidation**:
     - Regenerates `SecurityStamp` in database and revokes all active refresh tokens.
     - `OnTokenValidated` middleware checks database security stamp on every request, immediately blocking existing access tokens from protected admin APIs (`401 Unauthorized`).

---

## 6. End-to-End Curl Testing Suite

Run this bash script to verify all core flows in sequence:

```bash
#!/usr/bin/env bash
set -e

BASE_URL="http://localhost:5230"

echo "=== 1. Super Admin Login ==="
LOGIN_RES=$(curl -s -X POST "$BASE_URL/api/v1/admin/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"usernameOrEmail":"admin@sjewls.lk","password":"SuperAdmin@2026!"}')

ADMIN_TOKEN=$(echo "$LOGIN_RES" | grep -o '"accessToken":"[^"]*' | cut -d'"' -f4)
echo "Admin Token obtained: ${ADMIN_TOKEN:0:20}..."

echo "=== 2. Check Customer Statistics ==="
curl -s -X GET "$BASE_URL/api/v1/admin/customers/statistics" \
  -H "Authorization: Bearer $ADMIN_TOKEN"
echo ""

echo "=== 3. Create New Customer Profile ==="
CREATE_RES=$(curl -s -X POST "$BASE_URL/api/v1/admin/customers" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "fullName": "Test Customer",
    "phoneNumber": "+94770001122",
    "email": "test.customer@sjewls.test",
    "nic": "199411223344",
    "branchId": "a0000000-0000-0000-0000-000000000001"
  }')
echo "$CREATE_RES"
CUSTOMER_ID=$(echo "$CREATE_RES" | grep -o '"id":"[^"]*' | cut -d'"' -f4)
echo "Created Customer ID: $CUSTOMER_ID"

echo "=== 4. Verify Duplicate Prevention (409 Conflict) ==="
curl -s -w "\nHTTP_STATUS:%{http_code}\n" -X POST "$BASE_URL/api/v1/admin/customers" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "fullName": "Duplicate Customer",
    "phoneNumber": "+94770001122",
    "email": "diff@sjewls.test",
    "nic": "199411223344"
  }'

echo "=== 5. Deactivate Customer with Reason ==="
curl -s -X PATCH "$BASE_URL/api/v1/admin/customers/$CUSTOMER_ID/status" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"isActive": false, "reason": "Administrative suspension test"}'
echo ""

echo "=== 6. Reactivate Customer ==="
curl -s -X PATCH "$BASE_URL/api/v1/admin/customers/$CUSTOMER_ID/status" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"isActive": true, "reason": "Reinstated"}'
echo ""

echo "=== 7. Staff Self-Deactivation Prevention Check ==="
SUPER_ADMIN_ID=$(echo "$LOGIN_RES" | grep -o '"id":"[^"]*' | head -1 | cut -d'"' -f4)
curl -s -w "\nHTTP_STATUS:%{http_code}\n" -X PATCH "$BASE_URL/api/v1/admin/users/$SUPER_ADMIN_ID/status" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"isActive": false, "reason": "Testing self-deactivation"}'
```
