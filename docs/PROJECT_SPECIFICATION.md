# SJewls — Project Specification

Version: 1.0  
Prepared: 2 October 2026  
Release target: Client demonstration within approximately 2–3 weeks of development commencement  
Business: One jewellery company with multiple branches; initial active branch in Jaffna

## 1. Purpose and authority

Build SJewls from scratch: an administration web portal, customer mobile application and shared backend for Chitu Plans and Jewellery Plans. Existing screens and Figma designs are visual references, not existing code to reuse. The confirmed rules in this document take precedence over older reference-system behaviour.

This specification consolidates the supplied clarification conversation. Implementation recommendations and unresolved calculation details are identified separately; they must not be treated as newly approved business rules.

## 2. Delivery scope

| Area | Demonstration scope |
|---|---|
| Branch | Jaffna only; LKR; Asia/Colombo |
| Future expansion | Toronto and additional countries supported by the data model, not active in the demo |
| Business modules | Chitu Plan and Jewellery Plan only |
| Admin portal | Next.js; English |
| Mobile | React Native; Android and iOS test builds; English for first demo |
| Tamil | Preserve localisation structure; Tamil content added after the initial English demonstration |
| Backend | ASP.NET Core; PostgreSQL; Entity Framework Core |
| Database and images | Supabase from the beginning |
| Online payments | Simulated provider; no real money movement |
| Cash payments | Authorised staff record and confirm immediately |
| Authentication | Test OTP initially; phone or email login |
| Delivery environment | Online staging; not production |
| Sample data | Realistic fictional customers, plans, payments, draws and claims |
| QA | Both developers |

Excluded: Gold Investment, separate inventory/stock management, bank-transfer proof workflow, app-store publication, automated reminder scheduler and online claim approval workflows.

## 3. Branches, access and accounts

### 3.1 Branch model

SJewls is a single-company platform, not a multi-company marketplace. Each branch has configurable currency, timezone, plans, gold rates, payment provider and notification settings. Customers have one account and a primary branch. Head-office users can access all branches; branch staff can access only their assigned branch.

Every financial record retains its originating branch and currency. Do not sum different currencies into one monetary total. Store timestamps in UTC and display dates using the applicable branch timezone. Adding Toronto later must not require replacing customer identities or the core data model.

### 3.2 Staff

Required roles: Super Admin, Branch Admin and custom permission-based staff. Suggested permissions cover customer creation/editing/merge, Chitu and Jewellery Plan management, rate management, cash payment recording, draw recording, slot closure, extension decisions, refunds, claims, announcements, reports, audit access and staff administration.

Enforce branch and action permissions in the API. Hidden buttons are not an authorisation boundary. Staff login method is an implementation decision; email/password with secure password hashing is recommended for the demo.

### 3.3 Customer registration

- Collect customer name and either phone number or email address.
- Verify the selected contact using OTP; activate the customer immediately after verification.
- Customers can add and verify the second contact later.
- Authorised admins may add contacts to profiles; adding a contact does not itself prove ownership.
- Staff-created customers verify by OTP before obtaining mobile account access.
- Block duplicate verified phone numbers and emails.
- Allow authorised admins to merge genuine duplicate accounts, preserving plans, slots, transactions and audit history.
- In-store purchases must remain possible for staff-created customers; customer app verification is distinct from staff authority to record a purchase.

Use fixed test OTP only in development/staging. Clearly identify test authentication and prevent it from being enabled in a production configuration. Real SMS and email OTP providers are deferred.

## 4. Chitu Plan

### 4.1 Configuration

Admins create Chitu Plans with a name/code, description, branch, monthly instalment amount, Chitu benefit value, available slot capacity, plan duration/start information and one monthly due date. These fields are required implementation inputs; exact commercial values are supplied through admin configuration.

The physical draw date is recorded separately when the draw takes place. No automatic random selection is required.

### 4.2 Joining and slots

1. Customer selects a Chitu Plan and a quantity of slots.
2. Customer pays the initial required full instalment for the selected quantity.
3. After successful payment confirmation, activate the enrolment and assign the lowest available slot numbers in ascending order.
4. Customers cannot select individual slot numbers.
5. One customer may own multiple slots in the same plan with no customer-specific fixed limit, subject to remaining capacity.
6. Staff may create or locate a customer and purchase slots in-store using the same allocation rules.

Each slot is independently payable, eligible, closable and claimable. A winning slot does not close the customer's other slots.

Allocation must be concurrency-safe. Reserve capacity while checkout is pending or otherwise guarantee sufficient capacity before accepting payment. Pending reservations expire; final slot activation occurs only after confirmed success. Never duplicate slot numbers or oversell a plan. Define closed-slot reuse explicitly before implementation; do not silently reallocate historical winning numbers.

### 4.3 Instalments and overdue payments

Customers may pay one or multiple complete monthly instalments in advance. Partial instalments are not supported. Allocate every payment to identifiable slot/month instalments so individual histories and draw eligibility remain reliable.

Unpaid due instalments become overdue and remain payable later. No automatic late fee or suspension has been approved. Only authorised admins may voluntarily close a slot. Closure preserves payment and activity history.

Recommended allocation: clear earliest unpaid instalments first, then future instalments. Reject payments for instalments already paid or for future collection on closed slots.

### 4.4 Draw and winner

- The draw happens physically outside the system.
- An authorised admin records the draw month and winning slot.
- Only active slots paid through the current draw month are eligible.
- On winner confirmation, immediately close that winning slot for further collection and draws.
- Create an unclaimed winner-benefit record and notify the customer in-app.
- The winner receives jewellery equal to the Chitu value, not cash.
- Staff mark the benefit Claimed after physical collection, recording administrator, date and remarks.

Financial slot closure and physical benefit collection are separate states. A closed winning slot can still have an unclaimed benefit.

### 4.5 Rules requiring explicit configuration

The discussion did not settle treatment of advance instalments after an early win, refunds for voluntary admin closure, late joining into a running plan, due dates such as the 31st in shorter months, or correction of an incorrectly recorded winner. Keep these visible as configuration/validation decisions; do not assume forfeiture, refunds or arbitrary winner edits.

## 5. Jewellery Plan

### 5.1 Plan definition

Admin creates plans directly with displayed product details and images, product gold weight, selected karat, target, included making charges/VAT/other approved charges, duration and conditions. No separate inventory module is required.

Customers browse the displayed product and select Buy Gold. Supported durations are 6, 8 and 12 months. Joining is a saving arrangement toward that product, not immediate product purchase or stock reservation.

### 5.2 Contributions and gold balance

Contributions are flexible in amount and timing within the selected duration. Customer can enter either a currency amount or grams.

For each successfully confirmed contribution:

```text
Gold grams = Currency contribution / Applicable rate per gram
Currency required = Requested grams × Applicable rate per gram
```

Use the plan's configured karat and applicable branch rate. Save the currency amount, grams, karat, rate and confirmation time permanently for every contribution. Subsequent rate changes must not recalculate past transactions.

The system displays both contributed currency and saved gold grams. Failed, cancelled and pending payments do not increase balances.

Recommended checkout behaviour: issue a server-generated quote containing rate, calculated amount/grams and expiry; confirm against the accepted quote. If it expires before payment, require a refreshed quote. The rate-lock period and rounding precision are implementation decisions, not confirmed commercial rules.

### 5.3 Completion and extension

The plan reaches its completion assessment when either its selected duration ends or its required target is reached earlier. Duration expiry with insufficient savings does not automatically make the product claimable.

For a shortfall, customer requests extension; admin approves or rejects it. Approval records the revised end date and reason. If rejected, refund the customer under the cancellation-fee rule. Customers can also request early closure; record the request and administrative outcome.

The original target includes making charges, VAT and other approved charges. Before coding eligibility, define exactly how the monetary charges and gold-weight target interact: a grams balance alone must not accidentally count charges as physical gold. Maintain explicit product weight, charge breakdown and target fields rather than inventing a conversion policy.

### 5.4 Refund

```text
Refund = Total confirmed currency paid − Cancellation fee
```

Cancellation fee is configured per Jewellery Plan as either a fixed currency amount or a percentage of total currency paid. Refunds are based on original currency contributions, not the current market value of saved grams.

Implementation must prevent a negative refund and duplicate refunds. Record gross contribution amount, fee policy, fee amount, approved net refund and processing outcome. Treatment of a fixed fee exceeding contributions must be agreed; do not silently collect extra money.

Online refunds use the payment provider; cash refunds are performed outside the system and recorded by authorised admins. The demonstration uses simulated online refunds.

### 5.5 Product changes and offers

Only the displayed product is normally claimable. If unavailable, admin and customer decide the outcome, including waiting, an agreed alternative or refund. Record that exception and agreement.

Admin decides whether product/plan edits affect only new customers or existing active enrolments too. Keep prior values, affected customers and audit records; preserve historical payment rates regardless of plan edits.

Admins can configure discounts, promotions, bonus gold and bonus currency per plan. Bonus award timing, conditions, rounding and refund treatment must be explicitly configured; no default bonus policy was confirmed.

### 5.6 Physical claim

When an enrolment becomes eligible, notify the customer in-app. Customer visits the store and staff complete the physical handover. Staff mark it Claimed and record date, administrator and remarks. There is no customer claim submission, review queue or multi-stage online claim approval.

## 6. Payments and financial integrity

Support mock online payments and immediately confirmed cash payments. Payment records include branch, customer, module, plan/enrolment, relevant slot allocations, currency, amount, method, status, reference, timestamp and responsible staff member where applicable.

Suggested payment states: Pending, Succeeded, Failed, Cancelled, Partially Refunded and Refunded. Any reversal/correction must preserve the original transaction and record a compensating entry; do not delete or overwrite confirmed history.

Use provider-neutral interfaces for checkout, confirmation and refund. Mock provider must demonstrate successful, failed, cancelled and pending outcomes. Clients never decide that a payment succeeded; the API validates provider outcomes. Use idempotency keys and unique provider references, including for repeated callbacks and retries.

Create balances, instalment allocations and enrolments atomically after confirmation. Do not credit twice. Online refund processing must tolerate pending/failed provider responses without falsely reporting completion.

After confirmation, show in-app payment feedback and update the mobile payment section with amount, method, date, reference and status. No PDF or email payment receipts are required.

## 7. Gold rates

Authorised admins manually enter branch-specific rates by karat, amount per gram and effective date/time. Retain rate history and responsible administrator. Reject jewellery checkout if no valid applicable rate exists.

Provide a replaceable gold-rate provider interface for a future API. Automated external rate integration is deferred. Rate changes do not alter historical contributions.

## 8. Notifications and announcements

Required channel architecture: in-app, mobile push, SMS for OTP and email. Real OTP delivery is deferred; external push/email delivery requires configured providers. The demo must at least show persisted in-app notifications and clearly distinguish mocked channel delivery.

Notify customers of payment outcomes, Chitu draws/winners and Jewellery contributions, completion assessments, extensions, closures and refunds. Reminders for upcoming/overdue instalments are sent manually by authorised staff; no automatic reminder schedule.

Announcements can target all customers, a module, a plan or selected individuals. Admin chooses English or Tamil when sending; initial demonstration content is English. For system-generated events, use English demo templates and a localisation structure for later Tamil templates; automated-language policy remains to be configured.

Deliver email only where an email address is available and verified. Persist recipient, event, message, selected language, read state and delivery attempts. Notification failures must not undo confirmed financial operations; use an outbox/retry approach.

## 9. Admin portal

Required screens:

- Login and permission-aware navigation.
- Dashboard: total customers, active plans, collected payments, outstanding/overdue Chitu instalments, active slots, winners and pending extension/closure/refund work.
- Customers: search, create, edit, verification state, contacts, plans, payment records and controlled duplicate merge.
- Chitu: create/edit/list, slot allocation overview, paid/unpaid instalments, physical draw recording, admin closure and claimed/unclaimed winners.
- Jewellery: plan/product setup, images, charges, offers, enrolments, contributions, extension/closure decisions, refunds and claimed/unclaimed products.
- Payments: customer/plan/slot search, cash recording, status and refund history.
- Gold rates: entry and history.
- Notifications: audience selection, announcements and manual reminders.
- Reports; staff/roles/permissions; branch settings; audit history.

## 10. Mobile application

Required flows: onboarding; phone/email OTP registration/login; home; Chitu discovery/details; slot-quantity checkout; owned slots and instalment payments; Jewellery discovery/product details; Buy Gold by amount or grams; balances and contribution history; extension/early-closure request; payment history; notification centre; profile/contact verification.

Show winning/eligible/claimed status and physical collection guidance. Never offer an online claim workflow. Handle loading, validation, empty states, failed/cancelled payments, pending confirmation, insufficient capacity and missing rates.

English is delivered first; prepare localisation keys rather than hard-coding interface text.

## 11. UI/UX references

Mobile Figma reference: [Shared reference design](https://www.figma.com/design/IHZHawLHdgJisiHjIdfSeA/Western-Jewellers?node-id=5872-10661).

Use supplied mobile/admin screenshots and the earlier reference notes for visual direction: maroon/gold palette, onboarding, authentication, dashboard, plan cards/details, payments, history, notifications and profile. Replace all Western Jewellers naming with SJewls. Screens missing from the reference must be designed to support the confirmed flows.

This document does not claim a fresh inspection or pixel-level audit of the Figma file or screenshot attachments. Visual references cannot override slot allocation, flexible contributions, physical claims or other confirmed requirements.

## 12. Architecture and repositories

| Repository | Responsibility | Owner |
|---|---|---|
| sjewls-api | ASP.NET Core API, business logic, EF Core migrations, OpenAPI, Scalar, tests, seed data | Developer 1 |
| sjewls-admin | Next.js administration portal | Developer 1 |
| sjewls-mobile | React Native Android/iOS app | Developer 2 |

Use Supabase PostgreSQL and Storage from the beginning; EF Core owns schema migrations. Keep Supabase credentials and privileged storage operations server-side. Admin/mobile use the API for protected business data and financial mutations.

Recommended backend organisation: domain models and rules, application services, infrastructure/provider adapters and API endpoints. Shared business rules live in the backend rather than duplicated in clients.

Use decimal arithmetic for money and gold, explicit currencies and database precision. Secure image uploads by type/size and authorisation. Keep secrets in environment configuration, never repositories or mobile bundles.

## 13. Proposed data model

| Entity | Main responsibility |
|---|---|
| Branch | Country, currency, timezone and configuration |
| Customer / CustomerContact | Identity, primary branch, verified phone/email |
| Staff / Role / Permission / StaffBranch | Administration and scoped access |
| OtpChallenge / Session | OTP expiry/attempts and authenticated sessions |
| ChituPlan / ChituSlot | Commercial settings, numbered ownership and slot state |
| ChituInstalment / PaymentAllocation | Slot/month liabilities and confirmed allocations |
| ChituDraw / WinnerBenefit | Physical draw result and collection record |
| JewelleryPlan / PlanVersion | Product, duration, karat, targets, charges and conditions |
| JewelleryEnrolment / Contribution | Customer duration, target snapshot and confirmed gold ledger |
| GoldRate / PaymentQuote | Historical rates and accepted checkout calculation |
| Offer / BenefitAward | Configured promotions and awarded benefits |
| Payment / PaymentAttempt / Refund | Financial lifecycle and provider references |
| ExtensionRequest / ClosureRequest | Customer requests and admin decisions |
| PhysicalClaim | Collection date, staff and remarks |
| Notification / Delivery / Outbox | Messages, read/delivery state and durable event handling |
| AuditLog / CustomerMerge | Administrative history and identity consolidation |

Use foreign keys, branch ownership checks and unique constraints. Version changing plan settings. Separate mutable workflow state from immutable financial records. Account merge retains an auditable mapping to source identities.

## 14. API and Scalar contract

Suggested URLs:

```text
/api/v1/...
/openapi/v1.json
/scalar/v1
```

Every endpoint must appear in Scalar with purpose, method/URL, authentication and permissions, parameters, request schema, validation, enum values, sample requests/responses, success/error status codes and standard errors. Provide pagination and filter schemas where applicable.

Use stable operation IDs and versioned DTOs. Use a consistent error body, preferably Problem Details with field validation errors and a trace identifier. Generate TypeScript clients/types for admin and mobile from the published OpenAPI contract.

| API group | Required operations |
|---|---|
| Authentication | Request/verify test OTP, sessions, logout, contact verification |
| Customers | Profile, staff creation/update, search and authorised merge |
| Branches/staff | Settings, roles, permissions and assignments |
| Chitu | Plan management, availability, slot checkout, owned slots, instalments, draw recording, closure and collection |
| Jewellery | Plan management, enrolment, contribution quote/checkout, balances, requests, decisions and collection |
| Payments | Initiation, mock confirmation, cash recording, history, refunds and provider callbacks |
| Gold rates | Effective rate lookup, admin entry and history |
| Notifications | Inbox/read state, device registration, announcements/manual reminders |
| Reporting | Filtered data, PDF generation and printable views |
| Audit | Authorised filtered access |

An endpoint is complete only after implementation, meaningful tests and documented request/response/error behaviour in Scalar. Share the staging API base URL and OpenAPI file with Developer 2 early.

## 15. Reports and audit

Reports are limited to Chitu-wise reports and individual customer payment records for both modules. Support date range, customer, plan, payment method/status and branch where relevant. Apply filters before PDF generation or printing. Show filter context and branch currency on output. Excel/CSV exports are not required.

Audit all administrative actions, including successful/failed login attempts, creation, updates, approvals, rejections, closures, merges, rates, payments, refunds and claims. Record actor, action, target, branch, timestamp, reason and relevant before/after values. Never log OTPs, passwords, tokens or payment credentials.

## 16. Staging plan

Previously selected candidate stack:

| Component | Planned provider |
|---|---|
| ASP.NET Core API | Render Docker web service |
| Admin static export | Cloudflare Pages |
| PostgreSQL/images | Supabase |
| Android/iOS builds | Expo EAS or compatible test-build workflow |
| Source/CI | GitHub and GitHub Actions |
| Error monitoring | Sentry, optional |

These are planning choices from the clarification discussion, not freshly verified free-tier guarantees. Check current terms, quotas and build compatibility before deployment. Keep the Next.js admin compatible with static export if hosted on Pages; protected logic stays in ASP.NET Core.

iOS test installation requires a valid signing/distribution route; a free build service does not itself supply Apple signing privileges. Arrange a simulator build or properly signed device build according to available credentials.

Allow cold starts in demo planning, use fictional data, provide a repeatable seed/reset process and avoid real charges. Real payment gateway, real OTP delivery, production hosting and app-store release are post-demo work.

## 17. Three-week delivery plan

| Period | Developer 1: Backend/admin | Developer 2: Mobile |
|---|---|---|
| Week 1 | Repositories, Supabase, migrations, authentication, branches/roles, customers, Scalar and basic plan APIs/admin | App foundation, English/localisation structure, OTP screens, navigation and API integration |
| Week 2 | Chitu slots/instalments/draws, Jewellery contributions/rates, cash and mock payment flows | Discovery/details, quantity checkout, owned slots, Buy Gold, balances and payment history |
| Week 3 | Notifications, requests/refunds/claims, reports/audit, sample data, staging and fixes | Notifications, request flows, physical-collection states, integration QA and test builds |

Two to three weeks is a planning target, not a guarantee. Account merge, configurable offers, multi-channel delivery and report formatting can materially affect effort. Prioritise a complete tested demonstration path; explicitly document any incomplete agreed feature instead of treating it as implemented.

## 18. Acceptance and verification

Both developers verify:

1. Phone and email test-OTP accounts activate correctly; duplicate verified contacts are blocked.
2. Staff permissions and branch restrictions are enforced by the API.
3. Simultaneous Chitu purchases cannot oversell or duplicate numbers; successful payment assigns ascending available slots.
4. Failed/cancelled checkout creates no paid slot or credited balance; repeated confirmation credits once.
5. Multiple slots and advance full instalments remain independently traceable; overdue payments remain payable.
6. Draw rejects unpaid/ineligible slots; confirmed winner closes only that slot and creates an unclaimed jewellery benefit.
7. Amount/grams checkout uses the correct karat/rate; past contributions remain unchanged after rate edits.
8. Duration expiry with shortfall leads to assessment/extension/refund rather than automatic physical claim.
9. Fixed/percentage cancellation fees, online mock refunds and recorded cash refunds are correctly reflected once.
10. Staff marking physical collection records claim details and changes displayed state without an online approval flow.
11. Admin announcements and event notifications reach intended in-app recipients; manual reminders work.
12. Chitu/customer reports honour filters in both PDF and print views.
13. Sensitive administrative actions and login outcomes are audited without secrets.
14. All shipped endpoints are documented in Scalar; admin/mobile share the same contract.
15. Demonstration builds handle pending/error/empty states and run the fictional sample-data journey.

## 19. Remaining implementation decisions

The main product direction is confirmed. Resolve these narrow details while implementing, before hard-coding financial policy:

- Chitu capacity/duration/value configuration, late joining, closed-number reuse and advance-payment treatment after winning.
- Admin Chitu closure financial consequences and controlled draw corrections.
- Jewellery target calculation when charges are included, quote rate-lock duration and rounding precision.
- Early-closure approval handling, fee exceeding contributions, bonus conditions and bonus treatment on refunds.
- Automatic notification template language and externally delivered push/email scope for the demo.
- Staff authentication, staging credentials and iOS signing/distribution route.

These gaps do not require repeating the completed clarification discussion. Record the chosen implementation details alongside the affected feature and distinguish them from already confirmed rules.
