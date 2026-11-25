import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:sjewls_mobile/core/network/token_storage.dart';
import 'package:sjewls_mobile/core/utils/formatters.dart';
import 'package:sjewls_mobile/features/auth/presentation/complete_profile_screen.dart';
import 'package:sjewls_mobile/features/auth/presentation/login_screen.dart';
import 'package:sjewls_mobile/features/chitu/data/chitu_models.dart';
import 'package:sjewls_mobile/features/jewellery/presentation/buy_gold_screen.dart';
import 'package:sjewls_mobile/features/chitu/presentation/chitu_payment_screen.dart';
import 'package:sjewls_mobile/features/chitu/presentation/chitu_provider.dart';
import 'package:sjewls_mobile/features/chitu/presentation/chitu_tab_screen.dart';
import 'package:sjewls_mobile/features/home/presentation/home_dashboard_screen.dart';
import 'package:sjewls_mobile/features/jewellery/data/jewellery_models.dart';
import 'package:sjewls_mobile/features/jewellery/presentation/jewellery_provider.dart';
import 'package:sjewls_mobile/features/notifications/data/notification_models.dart';
import 'package:sjewls_mobile/features/notifications/presentation/notification_provider.dart';
import 'package:sjewls_mobile/features/profile/data/profile_models.dart';
import 'package:sjewls_mobile/features/profile/presentation/profile_provider.dart';
import 'package:sjewls_mobile/main.dart';
import 'package:sjewls_mobile/routing/app_router.dart';

void main() {
  testWidgets('SJewls splash screen renders branding and redirects to login', (
    WidgetTester tester,
  ) async {
    TokenStorage.useInMemoryOnly(true);
    const storage = TokenStorage();
    await storage.clearSession();

    await tester.pumpWidget(const ProviderScope(child: SJewlsApp()));

    expect(find.text('SARANGA'), findsOneWidget);
    expect(find.text('JEWELLERY'), findsOneWidget);

    // Advance past the 2-second splash timer
    await tester.pump(const Duration(seconds: 3));
    await tester.pumpAndSettle();

    expect(find.text('Enter your phone or email'), findsOneWidget);
  });

  group('Lottery MVP Disabling & Navigation Tests', () {
    testWidgets(
      'Lottery is absent from bottom navigation and /lottery redirects away',
      (WidgetTester tester) async {
        TokenStorage.useInMemoryOnly(true);
        const storage = TokenStorage();
        await storage.saveSession(
          accessToken: 'test-jwt-token',
          refreshToken: 'test-refresh-token',
        );

        await tester.pumpWidget(
          ProviderScope(
            overrides: [
              customerProfileProvider.overrideWith(
                (ref) async => CustomerProfileDto.fromJson({
                  'id': 'c1',
                  'fullName': 'K. Saranga',
                  'isPhoneVerified': true,
                  'isEmailVerified': true,
                  'primaryBranchId': 'b1',
                  'primaryBranchName': 'Jaffna Main',
                  'isProfileComplete': true,
                }),
              ),
              myChituInvestmentsProvider.overrideWith(
                (ref) async => const CustomerChituInvestmentsPagedResponse(
                  items: [],
                  totalCount: 0,
                  page: 1,
                  pageSize: 20,
                  totalPages: 1,
                ),
              ),
              customerEnrolmentsProvider.overrideWith(
                (ref) async => const CustomerEnrolmentsPagedResponse(
                  items: [],
                  totalCount: 0,
                  page: 1,
                  pageSize: 20,
                  totalPages: 1,
                ),
              ),
              jewelleryPlansProvider.overrideWith((ref) async => const []),
              liveGoldRateProvider.overrideWith(
                (ref) async => const CustomerGoldRateDto(
                  rate24KPerGram: 29500.0,
                  rate22KPerGram: 27040.0,
                  currency: 'LKR',
                ),
              ),
              unreadNotificationCountProvider.overrideWithValue(0),
            ],
            child: const SJewlsApp(),
          ),
        );

        // Navigate to /home and settle
        appRouter.go('/home');
        await tester.pumpAndSettle();

        // Verify 4 bottom navigation tabs: Home, Chittu, Jewelry, Payments (NO Lottery)
        final bottomNav = tester.widget<BottomNavigationBar>(
          find.byType(BottomNavigationBar),
        );
        expect(bottomNav.items.map((item) => item.label).toList(), [
          'Home',
          'Chittu',
          'Jewelry',
          'Payments',
        ]);
        expect(find.text('Lottery'), findsNothing);

        // Direct navigation to /lottery must redirect to /home
        appRouter.go('/lottery');
        await tester.pumpAndSettle();
        expect(find.text('Monthly Draws & Winners'), findsNothing);
        expect(find.text('My Chittu Plans & Slots'), findsOneWidget);
      },
    );
  });

  group('Home Screen Payment Shortcuts & Context Passing Tests', () {
    testWidgets(
      'Home Screen displays gold Pay Instalment and Contribute / Buy Gold buttons for eligible enrolments and passes exact IDs',
      (WidgetTester tester) async {
        String? navigatedPath;

        final testRouter = GoRouter(
          initialLocation: '/home',
          routes: [
            GoRoute(
              path: '/home',
              builder: (context, state) => const HomeDashboardScreen(),
            ),
            GoRoute(
              path: '/chittu/investments/:planId/pay',
              builder: (context, state) {
                navigatedPath = state.uri.toString();
                return Scaffold(
                  body: Text(
                    'Chittu Pay Plan: ${state.pathParameters['planId']}',
                  ),
                );
              },
            ),
            GoRoute(
              path: '/jewelry/enrolments/:enrolmentId/buy-gold',
              builder: (context, state) {
                navigatedPath = state.uri.toString();
                return Scaffold(
                  body: Text(
                    'Jewellery Pay Enrolment: ${state.pathParameters['enrolmentId']}',
                  ),
                );
              },
            ),
          ],
        );

        final activeChituPlan = CustomerChituInvestmentPlanDto.fromJson({
          'planId': 'chitu-plan-101',
          'branchId': 'b1',
          'code': 'CHT-2026-01',
          'name': 'Jaffna Gold Chittu 12M',
          'redemptionType': 'Gold',
          'currency': 'LKR',
          'durationMonths': 12,
          'startDate': '2026-01-01',
          'endDate': '2026-12-31',
          'currentRoundIndex': 3,
          'nextRaffleDate': '2026-11-05',
          'planStatus': 'Active',
          'totalOwnedSlotsCount': 2,
          'activeSlotsCount': 2,
          'winningSlotsCount': 0,
          'monthlySlotPrice': 25000.0,
          'normalMonthlyCommitment': 50000.0,
          'actualOutstandingDueThroughCurrentRound': 50000.0,
          'totalConfirmedContributions': 100000.0,
          'canPayExistingSlots': true,
          'slots': [],
        });

        final eligibleJewelleryEnrolment = CustomerEnrolmentDto.fromJson({
          'enrolmentId': 'jewel-enrol-202',
          'planId': 'jp-1',
          'planName': '22K Bridal Necklace Plan',
          'targetGrams': 16.0,
          'selectedDurationMonths': 12,
          'joiningDate': '2026-01-01',
          'deadline': '2027-01-01',
          'premiumPercentage': 8.0,
          'currency': 'LKR',
          'totalConfirmedMoneyPaid': 120000.0,
          'netAccumulatedGoldGrams': 4.0,
          'remainingGrams': 12.0,
          'individualProgressPercentage': 25.0,
          'enrolmentStatus': 'Active',
          'isContributionAllowed': true,
        });

        final completedJewelleryEnrolment = CustomerEnrolmentDto.fromJson({
          'enrolmentId': 'jewel-enrol-completed',
          'planId': 'jp-2',
          'planName': 'Completed Bangle Plan',
          'targetGrams': 8.0,
          'selectedDurationMonths': 6,
          'joiningDate': '2026-01-01',
          'deadline': '2026-07-01',
          'premiumPercentage': 5.0,
          'currency': 'LKR',
          'totalConfirmedMoneyPaid': 240000.0,
          'netAccumulatedGoldGrams': 8.0,
          'remainingGrams': 0.0,
          'individualProgressPercentage': 100.0,
          'enrolmentStatus': 'Completed',
          'isContributionAllowed': false,
        });

        await tester.pumpWidget(
          ProviderScope(
            overrides: [
              customerProfileProvider.overrideWith(
                (ref) async => CustomerProfileDto.fromJson({
                  'id': 'c1',
                  'fullName': 'K. Saranga',
                  'isPhoneVerified': true,
                  'isEmailVerified': true,
                  'primaryBranchId': 'b1',
                  'primaryBranchName': 'Jaffna Main',
                  'isProfileComplete': true,
                }),
              ),
              myChituInvestmentsProvider.overrideWith(
                (ref) async => CustomerChituInvestmentsPagedResponse(
                  items: [activeChituPlan],
                  totalCount: 1,
                  page: 1,
                  pageSize: 20,
                  totalPages: 1,
                ),
              ),
              customerEnrolmentsProvider.overrideWith(
                (ref) async => CustomerEnrolmentsPagedResponse(
                  items: [
                    eligibleJewelleryEnrolment,
                    completedJewelleryEnrolment,
                  ],
                  totalCount: 2,
                  page: 1,
                  pageSize: 20,
                  totalPages: 1,
                ),
              ),
              jewelleryPlansProvider.overrideWith((ref) async => const []),
              liveGoldRateProvider.overrideWith(
                (ref) async => const CustomerGoldRateDto(
                  rate24KPerGram: 29500.0,
                  rate22KPerGram: 27040.0,
                  currency: 'LKR',
                ),
              ),
              unreadNotificationCountProvider.overrideWithValue(0),
            ],
            child: MaterialApp.router(routerConfig: testRouter),
          ),
        );

        await tester.pumpAndSettle();

        // Verify Quick Actions is removed and replaced by Live Gold Rate card
        expect(find.text('Quick Actions'), findsNothing);
        expect(
          find.byKey(const Key('home_live_gold_rate_card')),
          findsOneWidget,
        );
        expect(find.text('Live Gold Rate'), findsOneWidget);

        // Verify Chittu Pay Instalment button is present and navigates with exact planId
        final chituPayButton = find.byKey(
          const Key('home_chitu_pay_chitu-plan-101'),
        );
        expect(chituPayButton, findsOneWidget);

        await tester.ensureVisible(chituPayButton);
        await tester.pumpAndSettle();
        await tester.tap(chituPayButton);
        await tester.pumpAndSettle();

        expect(navigatedPath, '/chittu/investments/chitu-plan-101/pay');
        expect(find.text('Chittu Pay Plan: chitu-plan-101'), findsOneWidget);

        // Pop back to Home and test Jewellery Contribute / Buy Gold button
        testRouter.pop();
        await tester.pumpAndSettle();

        final jewelleryPayButton = find.byKey(
          const Key('home_jewellery_pay_jewel-enrol-202'),
        );
        expect(jewelleryPayButton, findsOneWidget);

        // Completed enrolment must NOT have a Contribute / Buy Gold button
        expect(
          find.byKey(const Key('home_jewellery_pay_jewel-enrol-completed')),
          findsNothing,
        );

        await tester.ensureVisible(jewelleryPayButton);
        await tester.pumpAndSettle();
        await tester.tap(jewelleryPayButton);
        await tester.pumpAndSettle();

        expect(navigatedPath, '/jewelry/enrolments/jewel-enrol-202/buy-gold');
        expect(
          find.text('Jewellery Pay Enrolment: jewel-enrol-202'),
          findsOneWidget,
        );
      },
    );
  });

  group('Chittu Screens & Payment States Tests', () {
    testWidgets(
      'ChituTabScreen renders Available Plans and My Plans & Slots tabs',
      (WidgetTester tester) async {
        final samplePlan = CustomerChituPlanSummaryDto.fromJson({
          'planId': 'cp-1',
          'branchId': 'b1',
          'code': 'CHT-GOLD-10',
          'name': '10-Month Festive Gold Chittu',
          'redemptionType': 'Gold',
          'currency': 'LKR',
          'durationMonths': 10,
          'startDate': '2026-10-01',
          'endDate': '2027-07-31',
          'monthlySlotPrice': 20000.0,
          'winningEntitlementPerSlot': 200000.0,
          'totalSlotCapacity': 50,
          'issuedSlotsCount': 15,
          'availableSlotsCount': 35,
          'capacityPercentage': 30.0,
          'currentRoundIndex': 1,
          'customerOwnedSlotsCount': 1,
          'isAvailableForJoining': true,
        });

        await tester.pumpWidget(
          ProviderScope(
            overrides: [
              availableChituPlansProvider.overrideWith(
                (ref) async => CustomerChituPlansPagedResponse(
                  items: [samplePlan],
                  totalCount: 1,
                  page: 1,
                  pageSize: 20,
                  totalPages: 1,
                ),
              ),
              myChituInvestmentsProvider.overrideWith(
                (ref) async => const CustomerChituInvestmentsPagedResponse(
                  items: [],
                  totalCount: 0,
                  page: 1,
                  pageSize: 20,
                  totalPages: 1,
                ),
              ),
            ],
            child: const MaterialApp(home: ChituTabScreen()),
          ),
        );

        await tester.pumpAndSettle();

        expect(find.text('10-Month Festive Gold Chittu'), findsOneWidget);
        expect(find.text('CHT-GOLD-10'), findsOneWidget);
        expect(find.text('35 / 50 slots free'), findsOneWidget);
      },
    );

    testWidgets(
      'ChituPaymentScreen handles empty payable slots state gracefully',
      (WidgetTester tester) async {
        await tester.pumpWidget(
          ProviderScope(
            overrides: [
              chituPayableSlotsProvider('cp-empty').overrideWith(
                (ref) async => const CustomerPayableSlotsDto(
                  planId: 'cp-empty',
                  planName: 'Completed Chittu Plan',
                  monthlySlotPrice: 20000.0,
                  currency: 'LKR',
                  currentRoundIndex: 12,
                  slots: [],
                ),
              ),
            ],
            child: const MaterialApp(
              home: ChituPaymentScreen(planId: 'cp-empty'),
            ),
          ),
        );

        await tester.pumpAndSettle();

        expect(find.text('No Payable Instalments Available'), findsOneWidget);
      },
    );
  });

  group(
    'Formatters & Model Parsing Tests (Chittu, Jewellery, Unified Payments)',
    () {
      test('Currency and grams formatters format values accurately', () {
        expect(
          Formatters.currency(125450.5, currencyCode: 'LKR'),
          'LKR 125,450.50',
        );
        expect(Formatters.grams(4.5), '4.5000 g');
        expect(Formatters.grams(8.0, trimTrailingZeros: true), '8.00 g');
        expect(Formatters.percentage(12.5), '12.5%');
        expect(Formatters.percentage(10.0), '10%');
        final uuidKey = Formatters.generateIdempotencyKey();
        expect(
          RegExp(
            r'^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$',
          ).hasMatch(uuidKey),
          isTrue,
        );
      });

      test(
        'Chittu v1.2 DTOs parse plan details, slots, instalments, and payment responses',
        () {
          final planDetail = CustomerChituPlanDetailDto.fromJson({
            'planId': 'cp-1',
            'branchId': 'b-1',
            'code': 'CHT-01',
            'name': 'Jaffna Monthly Chittu',
            'redemptionType': 'Gold',
            'currency': 'LKR',
            'durationMonths': 12,
            'startDate': '2026-01-01',
            'endDate': '2026-12-31',
            'monthlySlotPrice': 15000.0,
            'winningEntitlementPerSlot': 180000.0,
            'totalSlotCapacity': 60,
            'issuedSlotsCount': 20,
            'availableSlotsCount': 40,
            'capacityPercentage': 33.3,
            'currentRoundIndex': 2,
            'customerOwnedSlotsCount': 1,
            'isAvailableForJoining': true,
            'schedule': [
              {
                'roundIndex': 1,
                'scheduledDate': '2026-01-25',
                'status': 'Completed',
                'hasRecordedDraw': true,
              },
            ],
            'terms': ['Full monthly instalments only.'],
          });
          expect(planDetail.name, 'Jaffna Monthly Chittu');
          expect(planDetail.schedule.length, 1);
          expect(planDetail.terms.first, 'Full monthly instalments only.');

          final slotDetail = CustomerChituSlotDetailDto.fromJson({
            'slotId': 's-1',
            'slotNumber': 7,
            'slotDisplayCode': 'CHT-01-S07',
            'joinedAtUtc': '2026-01-02T10:00:00Z',
            'monthlySlotPrice': 15000.0,
            'durationMonths': 12,
            'instalmentsPaidCount': 2,
            'paidThroughRound': 2,
            'arrearsMonths': 0,
            'arrearsAmount': 0.0,
            'advancePaidMonths': 0,
            'advancePaidAmount': 0.0,
            'isCurrentRoundPaid': true,
            'totalConfirmedContributions': 30000.0,
            'slotStatus': 'Active',
            'isWinner': false,
            'winningEntitlementClaimed': false,
            'isRaffleEligible': true,
            'planId': 'cp-1',
            'planName': 'Jaffna Monthly Chittu',
            'currency': 'LKR',
            'instalments': [
              {
                'id': 'i-1',
                'monthIndex': 1,
                'dueDate': '2026-01-20',
                'amountDue': 15000.0,
                'status': 'Paid',
                'paidAtUtc': '2026-01-02T10:00:00Z',
              },
            ],
          });
          expect(slotDetail.slotDisplayCode, 'CHT-01-S07');
          expect(slotDetail.canPayInstalment, isTrue);
          expect(slotDetail.instalments.first.status, 'Paid');

          final confirmResp = CustomerChituPaymentConfirmResponse.fromJson({
            'paymentId': 'pay-99',
            'planId': 'cp-1',
            'planName': 'Jaffna Monthly Chittu',
            'totalAmount': 15000.0,
            'currency': 'LKR',
            'paidAtUtc': '2026-02-15T08:30:00Z',
            'isSimulated': true,
            'receipts': [
              {
                'slotId': 's-1',
                'slotDisplayCode': 'CHT-01-S07',
                'amountAllocated': 15000.0,
                'monthsCovered': 1,
                'clearedMonthIndices': [2],
              },
            ],
          });
          expect(confirmResp.totalAmount, 15000.0);
          expect(confirmResp.receipts.first.clearedMonthIndices, [2]);
        },
      );

      test(
        'CustomerProfileDto parses secondary contact verification states',
        () {
          final profile = CustomerProfileDto.fromJson({
            'id': 'c1111111-1111-1111-1111-111111111111',
            'fullName': 'K. Saranga',
            'dateOfBirth': '1995-04-12',
            'nic': '199512345678',
            'phoneNumber': '+94771234567',
            'email': 'customer@example.com',
            'isPhoneVerified': true,
            'isEmailVerified': false,
            'primaryBranchId': 'b1111111-1111-1111-1111-111111111111',
            'primaryBranchCode': 'JAF',
            'primaryBranchName': 'Jaffna Main',
            'isProfileComplete': true,
          });

          expect(profile.fullName, 'K. Saranga');
          expect(profile.isPhoneVerified, isTrue);
          expect(profile.hasUnverifiedPhone, isFalse);
          expect(profile.hasUnverifiedEmail, isTrue);
        },
      );

      test(
        'PaymentInitiationResponse parses HTTP 501 deferred gateway payload',
        () {
          final response = PaymentInitiationResponse.fromJson({
            'code': 'PaymentProviderNotConfigured',
            'message': 'Online payment gateway is not configured.',
            'quoteId': 'q1',
            'amountToPay': 45000.0,
            'currency': 'LKR',
            'isConfigured': false,
          }, httpStatusCode: 501);

          expect(response.httpStatusCode, 501);
          expect(response.isConfigured, isFalse);
          expect(response.amountToPay, 45000.0);
        },
      );

      test(
        'CustomerNotificationsPagedResponse parses unreadCount and items',
        () {
          final paged = CustomerNotificationsPagedResponse.fromJson({
            'items': [
              {
                'id': 'n1',
                'title': 'Enrolment Activated',
                'message': 'Your jewellery plan enrolment is now active.',
                'type': 'EnrolmentActivated',
                'isRead': false,
                'createdAtUtc': '2026-10-19T12:00:00Z',
              },
            ],
            'totalCount': 1,
            'unreadCount': 1,
            'page': 1,
            'pageSize': 20,
            'totalPages': 1,
          });

          expect(paged.unreadCount, 1);
          expect(paged.items.first.title, 'Enrolment Activated');
          expect(paged.items.first.isRead, isFalse);
        },
      );
    },
  );

  group('Auth UI & Keyboard Tests', () {
    testWidgets(
      'LoginScreen switches keyboard between number pad for phone and QWERTY for email',
      (WidgetTester tester) async {
        await tester.pumpWidget(const MaterialApp(home: LoginScreen()));
        await tester.pumpAndSettle();

        // Initially Phone is selected
        TextField phoneField = tester.widget<TextField>(find.byType(TextField));
        expect(
          phoneField.keyboardType,
          const TextInputType.numberWithOptions(signed: false, decimal: false),
        );
        expect(phoneField.inputFormatters, isNotNull);

        // Tap Email button
        await tester.tap(find.widgetWithText(ElevatedButton, 'Email'));
        await tester.pumpAndSettle();

        TextField emailField = tester.widget<TextField>(find.byType(TextField));
        expect(emailField.keyboardType, TextInputType.emailAddress);
        expect(emailField.inputFormatters, isNull);

        // Tap Phone button again
        await tester.tap(find.widgetWithText(ElevatedButton, 'Phone'));
        await tester.pumpAndSettle();

        phoneField = tester.widget<TextField>(find.byType(TextField));
        expect(
          phoneField.keyboardType,
          const TextInputType.numberWithOptions(signed: false, decimal: false),
        );
        expect(phoneField.inputFormatters, isNotNull);
      },
    );

    testWidgets(
      'CompleteProfileScreen displays NIC * and removes optional text from NIC',
      (WidgetTester tester) async {
        await tester.pumpWidget(
          const MaterialApp(
            home: CompleteProfileScreen(
              registrationToken: 'test-token',
              verifiedContact: '0771234567',
              isPhone: true,
            ),
          ),
        );
        await tester.pumpAndSettle();

        // Verify NIC * is present
        expect(find.text('NIC *'), findsOneWidget);
        // Verify NIC (optional) is not present
        expect(find.text('NIC (optional)'), findsNothing);
      },
    );

    testWidgets(
      'Live Gold Rate card renders on narrow screen without horizontal overflow',
      (WidgetTester tester) async {
        // Set narrow screen width like in the overflow report (322px)
        tester.view.physicalSize = const Size(322, 800);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(() => tester.view.resetPhysicalSize());

        await tester.pumpWidget(
          ProviderScope(
            overrides: [
              customerProfileProvider.overrideWith(
                (ref) async => CustomerProfileDto.fromJson({
                  'id': 'c1',
                  'fullName': 'K. Saranga',
                  'isPhoneVerified': true,
                  'isEmailVerified': true,
                  'primaryBranchName': 'Saranga Jewels Jaffna Main Branch',
                  'primaryBranchCode': 'JAF',
                }),
              ),
              myChituInvestmentsProvider.overrideWith(
                (ref) async => const CustomerChituInvestmentsPagedResponse(
                  items: [],
                  totalCount: 0,
                  page: 1,
                  pageSize: 20,
                  totalPages: 1,
                ),
              ),
              customerEnrolmentsProvider.overrideWith(
                (ref) async => const CustomerEnrolmentsPagedResponse(
                  items: [],
                  totalCount: 0,
                  page: 1,
                  pageSize: 20,
                  totalPages: 1,
                ),
              ),
              jewelleryPlansProvider.overrideWith((ref) async => const []),
              liveGoldRateProvider.overrideWith(
                (ref) async => const CustomerGoldRateDto(
                  rate24KPerGram: 29500.0,
                  rate22KPerGram: 27040.0,
                  currency: 'LKR',
                  rateSource: 'Saranga Jewels Branch Rate',
                  isLiveFromBackend: false,
                ),
              ),
              unreadNotificationCountProvider.overrideWithValue(0),
            ],
            child: const MaterialApp(home: HomeDashboardScreen()),
          ),
        );
        await tester.pumpAndSettle();

        expect(
          find.byKey(const Key('home_live_gold_rate_card')),
          findsOneWidget,
        );
        expect(tester.takeException(), isNull);
      },
    );

    testWidgets(
      'BuyGoldScreen renders without horizontal overflow on narrow screens',
      (WidgetTester tester) async {
        tester.view.physicalSize = const Size(313, 800);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(() => tester.view.resetPhysicalSize());

        await tester.pumpWidget(
          ProviderScope(
            overrides: [
              enrolmentDetailProvider('e1').overrideWith(
                (ref) async => CustomerEnrolmentDto.fromJson({
                  'enrolmentId': 'e1',
                  'enrolmentDisplayId': 'ENR-001',
                  'planId': 'p1',
                  'planName': 'Traditional 22K Gold Thali Pendant',
                  'targetGrams': 8.0,
                  'netAccumulatedGoldGrams': 3.25,
                  'remainingGrams': 4.75,
                  'totalConfirmedMoneyPaid': 95000.0,
                  'currency': 'LKR',
                  'selectedDurationMonths': 12,
                  'currentMonthIndex': 5,
                  'isContributionAllowed': true,
                  'enrolmentStatus': 'Active',
                }),
              ),
            ],
            child: const MaterialApp(home: BuyGoldScreen(enrolmentId: 'e1')),
          ),
        );
        await tester.pumpAndSettle();

        expect(find.text('Traditional 22K Gold Thali Pendant'), findsOneWidget);
        expect(tester.takeException(), isNull);
      },
    );

    test(
      'CustomerGoldRateDto correctly parses live backend payload and overrides',
      () {
        final liveJson = {
          'rate24kPerGram': 41271.39,
          'rate22kPerGram': 37832.11,
          'currency': 'LKR',
          'source': 'LiveSpot (Market Feed)',
          'isOverridden': false,
          'effectiveFromUtc': '2026-11-16T22:40:07.1985292+00:00',
          'branchId': null,
          'branchName': null,
        };

        final liveDto = CustomerGoldRateDto.fromJson(liveJson);
        expect(liveDto.rate24KPerGram, 41271.39);
        expect(liveDto.rate22KPerGram, 37832.11);
        expect(liveDto.currency, 'LKR');
        expect(liveDto.rateSource, 'LiveSpot (Market Feed)');
        expect(liveDto.isOverridden, isFalse);
        expect(liveDto.sovereign22KRate, closeTo(37832.11 * 8.0, 0.01));
        expect(liveDto.sovereign24KRate, closeTo(41271.39 * 8.0, 0.01));

        final overrideJson = {
          'rate24kPerGram': 42500.0,
          'rate22kPerGram': 38958.33,
          'currency': 'LKR',
          'source': 'Store Override',
          'isOverridden': true,
          'effectiveFromUtc': '2026-11-16T23:00:00Z',
          'branchId': 'b-jaffna',
          'branchName': 'Jaffna Main Branch',
        };

        final overrideDto = CustomerGoldRateDto.fromJson(overrideJson);
        expect(overrideDto.rate24KPerGram, 42500.0);
        expect(overrideDto.rate22KPerGram, 38958.33);
        expect(overrideDto.isOverridden, isTrue);
        expect(overrideDto.branchName, 'Jaffna Main Branch');
        expect(overrideDto.rateSource, 'Store Override');
      },
    );

    test('PaymentInitiationResponse parses demo payment success fields', () {
      final successResponse = PaymentInitiationResponse(
        quoteId: 'quote-123',
        amountToPay: 25000.0,
        currency: 'LKR',
        isConfigured: true,
        isSuccess: true,
        goldGramsCredited: 0.6057,
        transactionId: 'TXN-DEMO-001',
        httpStatusCode: 200,
        message: 'Payment confirmed! 0.6057 g credited to enrolment.',
      );

      expect(successResponse.isSuccess, isTrue);
      expect(successResponse.goldGramsCredited, 0.6057);
      expect(successResponse.transactionId, 'TXN-DEMO-001');
      expect(successResponse.amountToPay, 25000.0);
      expect(successResponse.currency, 'LKR');
    });

    test(
      'CustomerChituJoinResponse and CustomerChituPaymentConfirmResponse handle simulated demo results',
      () {
        final joinResponse = CustomerChituJoinResponse(
          paymentId: 'PAY-JOIN-001',
          planId: 'plan-1',
          planName: 'Diwali Gold Savings Chittu',
          slotsCount: 2,
          totalAmountPaid: 10000.0,
          currency: 'LKR',
          isSimulated: true,
          isGatewayDeferred: false,
          assignedSlots: [
            CustomerAssignedSlotDto(
              slotId: 'slot-1',
              slotNumber: 1,
              slotDisplayCode: 'SLT-001',
              initialMonthsPaid: 1,
              activatedAtUtc: '2026-11-17T05:00:00Z',
            ),
            CustomerAssignedSlotDto(
              slotId: 'slot-2',
              slotNumber: 2,
              slotDisplayCode: 'SLT-002',
              initialMonthsPaid: 1,
              activatedAtUtc: '2026-11-17T05:00:00Z',
            ),
          ],
        );

        expect(joinResponse.isGatewayDeferred, isFalse);
        expect(joinResponse.isSimulated, isTrue);
        expect(joinResponse.slotsCount, 2);
        expect(joinResponse.assignedSlots.length, 2);
        expect(joinResponse.assignedSlots.first.slotDisplayCode, 'SLT-001');

        final paymentConfirmResponse = CustomerChituPaymentConfirmResponse(
          paymentId: 'PAY-INST-001',
          planId: 'plan-1',
          planName: 'Diwali Gold Savings Chittu',
          totalAmount: 5000.0,
          currency: 'LKR',
          paidAtUtc: '2026-11-17T05:00:00Z',
          isSimulated: true,
          isGatewayDeferred: false,
          receipts: [
            CustomerSlotPaymentReceiptDto(
              slotId: 'slot-1',
              slotDisplayCode: 'SLT-001',
              amountAllocated: 5000.0,
              monthsCovered: 1,
              clearedMonthIndices: [2],
            ),
          ],
        );

        expect(paymentConfirmResponse.isGatewayDeferred, isFalse);
        expect(paymentConfirmResponse.isSimulated, isTrue);
        expect(paymentConfirmResponse.receipts.length, 1);
        expect(paymentConfirmResponse.receipts.first.amountAllocated, 5000.0);
      },
    );
  });
}
