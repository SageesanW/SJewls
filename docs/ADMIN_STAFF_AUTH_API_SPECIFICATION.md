# SJewls Admin Portal & Staff Authentication API Specification

## 1. Overview & Architecture

Staff and Super Admin authentication in SJewls is completely isolated from customer OTP mobile authentication.
Staff members authenticate via credential-based JWT sessions (username or email + password), while customers authenticate exclusively via phone/email OTP verification.

```
┌────────────────────────────────────────────────────────┐
│             SJewls Admin Portal (Next.js)              │
│       /login  /dashboard  /users  /reset-password      │
└──────────────────────────┬─────────────────────────────┘
                           │ Bearer JWT (role: Super Admin / Branch Admin / Staff)
                           ▼
┌────────────────────────────────────────────────────────┐
│               .NET 8 Web API Backend                   │
│   • PasswordHasher<Staff> (ASP.NET Core Identity)      │
│   • FixedWindowRateLimiter (15 req/min on auth routes) │
│   • Single-Use SHA-256 Hashed Password Reset Tokens    │
│   • SecurityStamp Session Invalidation                 │
│   • Audit Logging for all administrative events        │
└──────────────────────────┬─────────────────────────────┘
                           │
                           ▼
┌────────────────────────────────────────────────────────┐
│             PostgreSQL (Supabase) Database             │
│   StaffMembers, Roles, StaffRoles, StaffBranches,      │
│   AuditLogs                                            │
└────────────────────────────────────────────────────────┘
```

---

## 2. Initial Super Admin Seeding & Configuration

The system provides an automated, idempotent seeder that provisions the initial Super Admin on system startup if configured. Credentials are never hardcoded.

### Environment Variables / Configuration
```json
"Admin": {
  "InitialUsername": "superadmin",
  "InitialEmail": "admin@sjewls.lk",
  "InitialPassword": "SuperAdmin@2026!",
  "InitialName": "SJewls Super Administrator",
  "InitialPhone": "+94770000000",
  "PortalUrl": "http://localhost:3000"
}
```
Environment variables:
- `ADMIN_INITIAL_USERNAME`
- `ADMIN_INITIAL_EMAIL`
- `ADMIN_INITIAL_PASSWORD`
- `ADMIN_INITIAL_NAME`
- `ADMIN_INITIAL_PHONE`

Passwords are saved using **ASP.NET Core Identity PBKDF2/HMAC-SHA512 password hashing** (`PasswordHasher<Staff>`).

---

## 3. Endpoints Reference

### 3.1. Staff / Super Admin Login
- **Endpoint**: `POST /api/v1/admin/auth/login`
- **Rate Limit**: 15 requests / minute
- **Summary**: Authenticates a staff member using their username or email and password.

#### Request Body
```json
{
  "usernameOrEmail": "superadmin",
  "password": "SuperAdmin@2026!"
}
```

#### Success Response (`200 OK`)
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "tokenType": "Bearer",
  "expiresAtUtc": "2026-10-09T04:54:30.50795Z",
  "user": {
    "id": "c0000000-0000-0000-0000-000000000001",
    "fullName": "SJewls Head Office Admin",
    "email": "admin@sjewls.lk",
    "username": "superadmin",
    "phoneNumber": "+94770000000",
    "isActive": true,
    "roles": [
      "Super Admin"
    ],
    "assignedBranches": [
      {
        "branchId": "a0000000-0000-0000-0000-000000000001",
        "code": "JAF-01",
        "name": "SJewls Jaffna Main Branch",
        "city": "Jaffna"
      }
    ],
    "createdAtUtc": "2026-10-07T05:07:00.134395Z",
    "updatedAtUtc": "2026-10-08T04:53:36.98891Z"
  }
}
```

#### Error Responses
- `400 Bad Request`: Missing username or password.
- `401 Unauthorized`: `"message": "Invalid credentials or account is inactive."` (No sensitive details exposed).
- `429 Too Many Requests`: Rate limit exceeded.

---

### 3.2. Staff Logout
- **Endpoint**: `POST /api/v1/admin/auth/logout`
- **Authentication**: `Bearer <StaffToken>` (Policy: `StaffOnly`)
- **Summary**: Invalidates current active sessions by updating the staff member's `SecurityStamp` in the database and records an audit log.

#### Success Response (`200 OK`)
```json
{
  "message": "Successfully logged out."
}
```

---

### 3.3. Get Current Staff Profile
- **Endpoint**: `GET /api/v1/admin/auth/me`
- **Authentication**: `Bearer <StaffToken>` (Policy: `StaffOnly`)
- **Summary**: Returns current staff member details, permissions, and assigned branches. Validates that the account is active and the security stamp has not been revoked.

#### Success Response (`200 OK`)
```json
{
  "id": "c0000000-0000-0000-0000-000000000001",
  "fullName": "SJewls Head Office Admin",
  "email": "admin@sjewls.lk",
  "username": "superadmin",
  "phoneNumber": "+94770000000",
  "isActive": true,
  "roles": [
    "Super Admin"
  ],
  "assignedBranches": [
    {
      "branchId": "a0000000-0000-0000-0000-000000000001",
      "code": "JAF-01",
      "name": "SJewls Jaffna Main Branch",
      "city": "Jaffna"
    }
  ],
  "createdAtUtc": "2026-10-07T05:07:00.134395Z",
  "updatedAtUtc": "2026-10-08T04:53:36.98891Z"
}
```

---

### 3.4. Forgot Password Request
- **Endpoint**: `POST /api/v1/admin/auth/forgot-password`
- **Rate Limit**: 15 requests / minute
- **Summary**: Sends an expiring single-use password reset link to the staff member's email if an active account exists.

#### Request Body
```json
{
  "email": "admin@sjewls.lk"
}
```

#### Success Response (`200 OK`)
*Always returns the identical generic response regardless of whether the account exists to prevent email enumeration:*
```json
{
  "message": "If an account exists for this email, password reset instructions have been sent."
}
```

---

### 3.5. Reset Password
- **Endpoint**: `POST /api/v1/admin/auth/reset-password`
- **Rate Limit**: 15 requests / minute
- **Summary**: Validates the single-use token against the stored SHA-256 hash, validates password policy, updates the password hash, marks the token consumed, and invalidates all active sessions by rotating the security stamp.

#### Request Body
```json
{
  "email": "admin@sjewls.lk",
  "token": "4a7b9c1d...",
  "newPassword": "NewSecurePassword@2026!",
  "confirmPassword": "NewSecurePassword@2026!"
}
```

#### Success Response (`200 OK`)
```json
{
  "message": "Password has been successfully reset. Please log in with your new password."
}
```

#### Error Responses
- `400 Bad Request`: `"Invalid or expired password reset token."` or password policy failure.

---

### 3.6. List Staff Users
- **Endpoint**: `GET /api/v1/admin/users`
- **Authentication**: `Bearer <StaffToken>` (Policy: `BranchAdminOrSuperAdmin`)
- **Summary**: Super Admin receives all staff users across the system. Branch Admin receives staff users assigned to their branch.

#### Success Response (`200 OK`)
```json
[
  {
    "id": "c0000000-0000-0000-0000-000000000001",
    "fullName": "SJewls Head Office Admin",
    "email": "admin@sjewls.lk",
    "username": "superadmin",
    "phoneNumber": "+94770000000",
    "isActive": true,
    "roles": [
      "Super Admin"
    ],
    "assignedBranches": [
      {
        "branchId": "a0000000-0000-0000-0000-000000000001",
        "code": "JAF-01",
        "name": "SJewls Jaffna Main Branch",
        "city": "Jaffna"
      }
    ],
    "createdAtUtc": "2026-10-07T05:07:00.134395Z"
  }
]
```

---

### 3.7. Create Staff User
- **Endpoint**: `POST /api/v1/admin/users`
- **Authentication**: `Bearer <StaffToken>` (Policy: `SuperAdminOnly`)
- **Summary**: Allows Super Admin to provision a new staff user. Enforces email, phone, and password policy, and prevents duplicate emails and usernames.

#### Request Body
```json
{
  "fullName": "Kavitha Raman",
  "email": "kavitha@sjewls.lk",
  "phoneNumber": "+94779876543",
  "username": "kavitha",
  "password": "StaffPassword@2026!",
  "role": "Staff",
  "branchId": "a0000000-0000-0000-0000-000000000001"
}
```

#### Success Response (`201 Created`)
```json
{
  "id": "0dc14b84-1439-4f0e-bfab-f54a8b4177f5",
  "fullName": "Kavitha Raman",
  "email": "kavitha@sjewls.lk",
  "username": "kavitha",
  "phoneNumber": "+94779876543",
  "isActive": true,
  "roles": [
    "Staff"
  ],
  "assignedBranches": [
    {
      "branchId": "a0000000-0000-0000-0000-000000000001",
      "code": "JAF-01",
      "name": "SJewls Jaffna Main Branch",
      "city": "Jaffna"
    }
  ],
  "createdAtUtc": "2026-10-08T05:16:14.312Z"
}
```

#### Error Responses
- `400 Bad Request`: Field validation error (invalid email, phone, or weak password).
- `403 Forbidden`: Non-Super Admin attempted to create a user.
- `409 Conflict`: Duplicate email or username already exists.

---

## 4. Security & Compliance Checklist

| Feature | Implementation | Verified |
|---|---|---|
| Password Hashing | `Microsoft.AspNetCore.Identity.PasswordHasher<Staff>` (PBKDF2/HMAC-SHA512) | Yes |
| Password Exposure | Password hashes are NEVER returned in any API response DTO | Yes |
| Rate Limiting | ASP.NET Core `FixedWindowLimiter` (15/min) on login, forgot-password, reset-password | Yes |
| Token Isolation | Staff tokens contain `token_type: "staff"`, strictly separated from customer tokens | Yes |
| Reset Token Security | Only SHA-256 hashes of reset tokens are stored in the database | Yes |
| Single-Use Reset | Token hash and expiry are nullified immediately upon reset | Yes |
| Session Invalidation | `SecurityStamp` rotated on password reset and logout | Yes |
| Audit Trail | `STAFF_LOGIN_SUCCESS`, `STAFF_LOGIN_FAILED`, `STAFF_CREATED`, `PASSWORD_RESET_REQUESTED`, `PASSWORD_RESET_COMPLETED`, `STAFF_LOGOUT` | Yes |
| Credential Logging | Credentials, plain passwords, and reset links are completely excluded from logs | Yes |
| Customer OTP Flow | Untouched and operating independently for mobile customers | Yes |
