# SJewls — Customer Authentication & Registration Scalar Testing Guide

This guide walks you through testing the entire Customer Registration and Authentication flow interactively using **Scalar API Explorer**, including **real Gmail Email OTP delivery** and development SMS testing.

---

## 1. Setting Up Gmail SMTP for Real Email OTPs

Email verification codes are dispatched via **Gmail SMTP** (`smtp.gmail.com:587`, STARTTLS) from `w.sageesan@gmail.com`.

### Setting Your Gmail App Password Locally (User Secrets)
Run the following command from the `backend/` directory:
```bash
dotnet user-secrets set "Email:Password" "<your-16-character-gmail-app-password>" --project src/SJewls.Api
```

> **Note on Deployment**: In production or staging environments, supply the app password via the environment variable `Email__Password`. Never commit passwords to source control.

### Delivery Channel Behavior:
- **Email Address (`w.sageesan@gmail.com` etc.)**:
  - Sent via real Gmail SMTP with STARTTLS (`smtp.gmail.com:587`).
  - Returns `200 OK` **only after** Gmail SMTP accepts the message.
  - If SMTP fails or the password is missing, returns `400 Bad Request` and immediately invalidates the challenge.
- **Phone Number (`+947...` or `07...`)**:
  - Sent via real **Text.lk SMS Gateway** (`https://app.text.lk/api/v3/sms/send`, Bearer auth) with sender ID `TextLKDemo`.
  - Returns `200 OK` **only after** Text.lk accepts and delivers the SMS.
  - If SMS delivery fails or the API token is missing, returns `400 Bad Request` and immediately invalidates the challenge.

---

## 2. Accessing Scalar in Your Browser

1. Ensure the backend API is running (currently live at `http://localhost:5230`).
2. Open your web browser and navigate to:  
   👉 **[http://localhost:5230/scalar/v1](http://localhost:5230/scalar/v1)**

You will see the dark-themed **SJewls API Explorer** with all endpoints grouped under **Customer Authentication**, **Customer Profile & Contacts**, **Branches**, and **System**.

---

## 3. Interactive Testing Flow (Step-by-Step)

### Step 0: Check Contact (Phone or Email)
- In the left sidebar, click **Customer Authentication** &rarr; **`POST /api/v1/auth/check`**.
- Click the **Body** tab and enter a phone number or email:
  ```json
  {
    "contact": "0769882118"
  }
  ```
- Click **Send Request**.
- **Outcomes:**
  - If existing in DB: `{"exists": true, "nextAction": "Login", "message": "Customer account found. Please request and verify an OTP to log in."}`
  - If new customer: `{"exists": false, "nextAction": "Register", "message": "Customer account not found or registration incomplete. Please request an OTP to proceed with registration."}`

---

### Step 1A: Request an Email OTP (Real Gmail Delivery)
- In the left sidebar, click **Customer Authentication** &rarr; **`POST /api/v1/auth/otp/request`**.
- Click the **Body** tab and enter your email address:
  ```json
  {
    "contact": "w.sageesan@gmail.com"
  }
  ```
- Click **Send Request**.
- **Delivery Outcomes:**
  - Returns `200 OK` with `message: "Verification code sent to your email address."`. Check your Gmail inbox for the subject *"Your SJewls verification code"*.

### Step 1B: Request a Phone OTP (Real Text.lk SMS Delivery)
- Click the **Body** tab and enter your phone number:
  ```json
  {
    "contact": "+94759712375"
  }
  ```
- Click **Send Request**.
- **Delivery Outcomes:**
  - Returns `200 OK` with `message: "Verification code sent to your phone number."`.
  - An actual SMS message from `TextLKDemo` arrives directly on your mobile phone:
    *"Your SJewls verification code is 123456. Valid for 5 minutes. Never share this code with anyone."*
- **Expected Response (200 OK):**
  ```json
  {
    "success": true,
    "message": "Verification code sent to your phone number.",
    "normalizedContact": "+94759712375",
    "contactType": 1,
    "expiresInSeconds": 300,
    "cooldownSeconds": 60,
    "devOtp": null,
    "isExistingCustomer": false,
    "nextAction": "Register"
  }
  ```

---

### Step 2: Verify the OTP (First-Time Customer)
- In the left sidebar, click **`POST /api/v1/auth/otp/verify`**.
- Under **Body**, enter:
  ```json
  {
    "contact": "0759712375",
    "code": "YOUR_RECEIVED_OTP"
  }
  ```
- Click **Send Request**.
- **Expected Response (200 OK):**
  ```json
  {
    "nextAction": "CompleteProfile",
    "registrationToken": "reg_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
    "registrationTokenExpiresInSeconds": 3600,
    "verifiedContact": "+94759712375",
    "verifiedContactType": 1,
    "requiredAdditionalContactType": "Email"
  }
  ```
  > 📋 **Important:** Copy the `registrationToken` string from the response! You will need it for Step 3.

---

### Step 3: Complete Profile Registration
- In the left sidebar, click **`POST /api/v1/auth/registration/complete`**.
- Under **Body**, paste the `registrationToken` you copied. You can provide your `email` (or `additionalContact`). The initially entered and OTP-verified phone number is automatically preserved and saved to your customer profile!
  ```json
  {
    "registrationToken": "PASTE_YOUR_REGISTRATION_TOKEN_HERE",
    "fullName": "Anojan",
    "dateOfBirth": "2000-08-28",
    "nic": "200012637289",
    "email": "anojan@gmail.com"
  }
  ```
  *(Sri Lankan NIC format: 9 digits + V/X like `962340567V` or 12 digits like `200012637289`)*.
- Click **Send Request**.
- **Expected Response (200 OK):**
  ```json
  {
    "nextAction": "Dashboard",
    "accessToken": "eyJhbGciOiJIUzI1NiIs...",
    "refreshToken": "XN5zzrxyZytXY3...",
    "expiresInSeconds": 86400,
    "customer": {
      "id": "e24a56b7-...",
      "fullName": "Anojan",
      "phoneNumber": "+94759712375",
      "email": "anojan@gmail.com",
      "primaryContact": "+94759712375",
      "primaryBranchCode": "JAF-01",
      "isProfileComplete": true
    }
  }
  ```
  > 📋 **Important:** Copy the `accessToken` (without quotes).

---

### Step 4: Authorize in Scalar to Test Protected Endpoints
1. At the top of the Scalar window (or next to the endpoint name), look for the **Auth** / **Security** / **Bearer** field.
2. Paste the `accessToken` into the Token box.
3. Scalar will now automatically attach the `Authorization: Bearer <token>` header to all your protected requests!

---

### Step 5: View Customer Profile
- In the left sidebar, click **Customer Profile & Contacts** &rarr; **`GET /api/v1/customers/me`**.
- Click **Send Request**.
- **Expected Response (200 OK):**
  ```json
  {
    "id": "e24a56b7-...",
    "fullName": "Kandeepan Tharmalingam",
    "dateOfBirth": "1996-08-20",
    "nic": "199623405678",
    "primaryBranchCode": "JAF-01",
    "primaryBranchName": "SJewls Jaffna Main Branch",
    "isProfileComplete": true,
    "contacts": [
      {
        "type": 1,
        "value": "+94772223344",
        "isVerified": true,
        "isPrimary": true
      },
      {
        "type": 2,
        "value": "kandeepan@example.com",
        "isVerified": false,
        "isPrimary": false
      }
    ]
  }
  ```
  *(Notice: The initial phone number is verified, while the email is unverified)*.

---

### Step 6: Verify the Secondary Contact (Email)
1. **Request code for the email**:
   - Click **`POST /api/v1/customers/me/contacts/otp/request`**.
   - Body:
     ```json
     {
       "contact": "kandeepan@example.com"
     }
     ```
   - Click **Send Request** &rarr; returns `success: true`.

2. **Submit code for the email**:
   - Click **`POST /api/v1/customers/me/contacts/otp/verify`**.
   - Body:
     ```json
     {
       "contact": "kandeepan@example.com",
       "code": "123456"
     }
     ```
   - Click **Send Request** &rarr; returns:
     ```json
     {
       "success": true,
       "message": "Additional contact verified successfully. You can now use this contact to sign in."
     }
     ```

---

### Step 7: Test Login with the Newly Verified Email
Now that the email is verified, the customer can sign in using **either** phone or email!
- Go to **`POST /api/v1/auth/otp/request`**:
  ```json
  { "contact": "kandeepan@example.com" }
  ```
- Go to **`POST /api/v1/auth/otp/verify`**:
  ```json
  { "contact": "kandeepan@example.com", "code": "123456" }
  ```
- **Expected Response (200 OK):**
  Directly returns `nextAction: "Dashboard"` with fresh tokens! No profile completion needed.

---

### Step 8: Token Refresh & Logout
- **Refresh Token**:
  - Click **`POST /api/v1/auth/token/refresh`**.
  - Body:
    ```json
    {
      "refreshToken": "PASTE_REFRESH_TOKEN_HERE"
    }
    ```
  - Returns a brand new access token and rotated refresh token.
- **Logout**:
  - Click **`POST /api/v1/auth/logout`**.
  - Body:
    ```json
    {
      "refreshToken": "PASTE_REFRESH_TOKEN_HERE"
    }
    ```
  - Revokes the refresh token.

---

## 3. Negative / Validation Tests

You can also test error handling in Scalar:

| Scenario | Payload | Expected Outcome |
|---|---|---|
| **Duplicate NIC** | Use the same NIC (`199623405678`) on another registration | `400 Bad Request`: *"A customer account with this NIC is already registered. Duplicate NICs are not permitted."* |
| **Invalid NIC Format** | `"nic": "12345"` | `400 Bad Request`: *"Invalid Sri Lankan NIC format."* |
| **Underage Customer** | `"dateOfBirth": "2015-01-01"` | `400 Bad Request`: *"Customer must be at least 18 years of age to register."* |
| **Invalid Code** | `"code": "999999"` | `400 Bad Request`: *"Incorrect verification code. 4 attempts remaining."* |
| **Cooldown Resend** | Request OTP twice within 60 seconds | `400 Bad Request`: *"Please wait X seconds before requesting a new verification code."* |
| **Expired Session** | Use an old or fake `registrationToken` | `400 Bad Request`: *"Registration session has expired or is invalid."* |
