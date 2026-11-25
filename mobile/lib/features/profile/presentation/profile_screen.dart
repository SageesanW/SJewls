import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/sjewls_widgets.dart';
import '../../auth/presentation/auth_provider.dart';
import '../../jewellery/presentation/jewellery_provider.dart';
import '../data/profile_models.dart';
import 'profile_provider.dart';

class ProfileScreen extends ConsumerStatefulWidget {
  const ProfileScreen({super.key});

  @override
  ConsumerState<ProfileScreen> createState() => _ProfileScreenState();
}

class _ProfileScreenState extends ConsumerState<ProfileScreen> {
  bool _isLoggingOut = false;

  Future<void> _handleLogout() async {
    setState(() => _isLoggingOut = true);
    try {
      await ref.read(authRepositoryProvider).logout();
      ref.invalidate(customerProfileProvider);
      ref.invalidate(customerEnrolmentsProvider);
      if (mounted) {
        context.go('/login');
      }
    } finally {
      if (mounted) {
        setState(() => _isLoggingOut = false);
      }
    }
  }

  Future<void> _openVerifySecondaryContactDialog({
    required String contact,
    required bool isPhone,
  }) async {
    final codeController = TextEditingController();
    bool isSending = false;
    bool isVerifying = false;
    bool otpSent = false;
    String? feedbackMessage;
    String? errorMessage;

    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: AppColors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (sheetContext) {
        return StatefulBuilder(
          builder: (context, setSheetState) {
            return Padding(
              padding: EdgeInsets.only(
                left: 22,
                right: 22,
                top: 22,
                bottom: MediaQuery.of(context).viewInsets.bottom + 24,
              ),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Text(
                        isPhone
                            ? 'Verify Secondary Phone'
                            : 'Verify Secondary Email',
                        style: const TextStyle(
                          color: AppColors.dark,
                          fontSize: 18,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                      IconButton(
                        icon: const Icon(Icons.close, color: AppColors.dark),
                        onPressed: () => Navigator.of(sheetContext).pop(),
                      ),
                    ],
                  ),
                  const SizedBox(height: 8),
                  Text(
                    'Contact: $contact',
                    style: TextStyle(
                      color: AppColors.dark.withValues(alpha: 0.75),
                      fontSize: 14,
                    ),
                  ),
                  const SizedBox(height: 16),
                  if (!otpSent) ...[
                    Text(
                      'Send a 6-digit verification code to activate this contact for your account.',
                      style: TextStyle(
                        color: AppColors.dark.withValues(alpha: 0.7),
                        fontSize: 13.5,
                      ),
                    ),
                    const SizedBox(height: 18),
                    ElevatedButton(
                      style: ElevatedButton.styleFrom(
                        backgroundColor: AppColors.primary,
                        foregroundColor: AppColors.white,
                      ),
                      onPressed: isSending
                          ? null
                          : () async {
                              setSheetState(() {
                                isSending = true;
                                errorMessage = null;
                              });
                              try {
                                final res = await ref
                                    .read(profileRepositoryProvider)
                                    .requestSecondaryContactOtp(contact);
                                setSheetState(() {
                                  otpSent = true;
                                  feedbackMessage =
                                      res.message ??
                                      'Verification code sent to $contact';
                                });
                              } catch (e) {
                                setSheetState(() {
                                  errorMessage = e.toString().replaceFirst(
                                    'Exception: ',
                                    '',
                                  );
                                });
                              } finally {
                                setSheetState(() => isSending = false);
                              }
                            },
                      child: isSending
                          ? const SizedBox(
                              width: 20,
                              height: 20,
                              child: CircularProgressIndicator(
                                strokeWidth: 2,
                                color: AppColors.white,
                              ),
                            )
                          : const Text('Send OTP Code'),
                    ),
                  ] else ...[
                    if (feedbackMessage != null) ...[
                      Container(
                        padding: const EdgeInsets.all(12),
                        decoration: BoxDecoration(
                          color: AppColors.primary.withValues(alpha: 0.08),
                          borderRadius: BorderRadius.circular(10),
                        ),
                        child: Text(
                          feedbackMessage!,
                          style: const TextStyle(
                            color: AppColors.primary,
                            fontSize: 13,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                      ),
                      const SizedBox(height: 14),
                    ],
                    TextField(
                      controller: codeController,
                      keyboardType: TextInputType.number,
                      maxLength: 6,
                      inputFormatters: [FilteringTextInputFormatter.digitsOnly],
                      decoration: const InputDecoration(
                        labelText: '6-Digit OTP Code',
                        counterText: '',
                      ),
                    ),
                    const SizedBox(height: 16),
                    ElevatedButton(
                      style: ElevatedButton.styleFrom(
                        backgroundColor: AppColors.primary,
                        foregroundColor: AppColors.white,
                      ),
                      onPressed: isVerifying
                          ? null
                          : () async {
                              final code = codeController.text.trim();
                              if (code.length != 6) {
                                setSheetState(() {
                                  errorMessage =
                                      'Please enter the complete 6-digit OTP.';
                                });
                                return;
                              }
                              setSheetState(() {
                                isVerifying = true;
                                errorMessage = null;
                              });
                              try {
                                await ref
                                    .read(profileRepositoryProvider)
                                    .verifySecondaryContactOtp(
                                      contact: contact,
                                      code: code,
                                    );
                                ref.invalidate(customerProfileProvider);
                                if (sheetContext.mounted) {
                                  Navigator.of(sheetContext).pop();
                                }
                                if (mounted) {
                                  ScaffoldMessenger.of(
                                    this.context,
                                  ).showSnackBar(
                                    const SnackBar(
                                      content: Text(
                                        'Secondary contact verified and activated!',
                                      ),
                                      backgroundColor: AppColors.primary,
                                    ),
                                  );
                                }
                              } catch (e) {
                                setSheetState(() {
                                  errorMessage = e.toString().replaceFirst(
                                    'Exception: ',
                                    '',
                                  );
                                });
                              } finally {
                                setSheetState(() => isVerifying = false);
                              }
                            },
                      child: isVerifying
                          ? const SizedBox(
                              width: 20,
                              height: 20,
                              child: CircularProgressIndicator(
                                strokeWidth: 2,
                                color: AppColors.white,
                              ),
                            )
                          : const Text('Verify & Activate Contact'),
                    ),
                  ],
                  if (errorMessage != null) ...[
                    const SizedBox(height: 12),
                    Text(
                      errorMessage!,
                      style: const TextStyle(color: Colors.red, fontSize: 13),
                    ),
                  ],
                ],
              ),
            );
          },
        );
      },
    );
  }

  Future<void> _openAccountClosureDialog() async {
    final codeController = TextEditingController();
    final reasonController = TextEditingController();
    bool isRequestingOtp = false;
    bool isConfirming = false;
    bool otpRequested = false;
    String? maskedDestination;
    String? errorMessage;

    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: AppColors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (sheetContext) {
        return StatefulBuilder(
          builder: (context, setSheetState) {
            return Padding(
              padding: EdgeInsets.only(
                left: 22,
                right: 22,
                top: 22,
                bottom: MediaQuery.of(context).viewInsets.bottom + 24,
              ),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      const Text(
                        'Close Account',
                        style: TextStyle(
                          color: AppColors.dark,
                          fontSize: 18,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                      IconButton(
                        icon: const Icon(Icons.close, color: AppColors.dark),
                        onPressed: () => Navigator.of(sheetContext).pop(),
                      ),
                    ],
                  ),
                  const SizedBox(height: 8),
                  Text(
                    'Closing your account deactivates mobile access and revokes active sessions. Financial and plan history is retained for regulatory compliance.',
                    style: TextStyle(
                      color: AppColors.dark.withValues(alpha: 0.72),
                      fontSize: 13.5,
                      height: 1.4,
                    ),
                  ),
                  const SizedBox(height: 16),
                  if (!otpRequested) ...[
                    OutlinedButton(
                      onPressed: isRequestingOtp
                          ? null
                          : () async {
                              setSheetState(() {
                                isRequestingOtp = true;
                                errorMessage = null;
                              });
                              try {
                                final res = await ref
                                    .read(profileRepositoryProvider)
                                    .requestAccountClosureOtp();
                                setSheetState(() {
                                  otpRequested = true;
                                  maskedDestination =
                                      res.maskedContact ?? res.message;
                                });
                              } catch (e) {
                                setSheetState(() {
                                  errorMessage = e.toString().replaceFirst(
                                    'Exception: ',
                                    '',
                                  );
                                });
                              } finally {
                                setSheetState(() => isRequestingOtp = false);
                              }
                            },
                      child: isRequestingOtp
                          ? const SizedBox(
                              width: 18,
                              height: 18,
                              child: CircularProgressIndicator(
                                strokeWidth: 2,
                                color: AppColors.primary,
                              ),
                            )
                          : const Text('Request Closure Verification OTP'),
                    ),
                  ] else ...[
                    if (maskedDestination != null) ...[
                      Text(
                        'OTP dispatched ($maskedDestination)',
                        style: const TextStyle(
                          color: AppColors.primary,
                          fontWeight: FontWeight.w600,
                          fontSize: 13,
                        ),
                      ),
                      const SizedBox(height: 12),
                    ],
                    TextField(
                      controller: codeController,
                      keyboardType: TextInputType.number,
                      maxLength: 6,
                      inputFormatters: [FilteringTextInputFormatter.digitsOnly],
                      decoration: const InputDecoration(
                        labelText: '6-Digit Closure OTP *',
                        counterText: '',
                      ),
                    ),
                    const SizedBox(height: 12),
                    TextField(
                      controller: reasonController,
                      decoration: const InputDecoration(
                        labelText: 'Reason for closure (optional)',
                      ),
                    ),
                    const SizedBox(height: 16),
                    ElevatedButton(
                      style: ElevatedButton.styleFrom(
                        backgroundColor: AppColors.dark,
                        foregroundColor: AppColors.white,
                      ),
                      onPressed: isConfirming
                          ? null
                          : () async {
                              final code = codeController.text.trim();
                              if (code.length != 6) {
                                setSheetState(() {
                                  errorMessage =
                                      'Please enter the 6-digit closure OTP.';
                                });
                                return;
                              }
                              setSheetState(() {
                                isConfirming = true;
                                errorMessage = null;
                              });
                              final router = GoRouter.of(this.context);
                              try {
                                await ref
                                    .read(profileRepositoryProvider)
                                    .confirmAccountClosure(
                                      code: code,
                                      reason: reasonController.text,
                                    );
                                if (sheetContext.mounted) {
                                  Navigator.of(sheetContext).pop();
                                }
                                if (mounted) {
                                  router.go('/login');
                                }
                              } catch (e) {
                                setSheetState(() {
                                  errorMessage = e.toString().replaceFirst(
                                    'Exception: ',
                                    '',
                                  );
                                });
                              } finally {
                                setSheetState(() => isConfirming = false);
                              }
                            },
                      child: isConfirming
                          ? const SizedBox(
                              width: 18,
                              height: 18,
                              child: CircularProgressIndicator(
                                strokeWidth: 2,
                                color: AppColors.white,
                              ),
                            )
                          : const Text('Confirm Account Closure'),
                    ),
                  ],
                  if (errorMessage != null) ...[
                    const SizedBox(height: 12),
                    Text(
                      errorMessage!,
                      style: const TextStyle(color: Colors.red, fontSize: 13),
                    ),
                  ],
                ],
              ),
            );
          },
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    final profileAsync = ref.watch(customerProfileProvider);
    final enrolmentsAsync = ref.watch(customerEnrolmentsProvider);

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: const Text('My Profile')),
      body: profileAsync.when(
        loading: () =>
            const SJewlsLoadingView(message: 'Loading your profile...'),
        error: (err, _) => SJewlsErrorView(
          message: err.toString().replaceFirst('Exception: ', ''),
          onRetry: () => ref.invalidate(customerProfileProvider),
        ),
        data: (CustomerProfileDto profile) {
          return RefreshIndicator(
            color: AppColors.primary,
            onRefresh: () async {
              ref.invalidate(customerProfileProvider);
              await ref.read(customerProfileProvider.future);
            },
            child: ListView(
              padding: const EdgeInsets.all(18),
              children: [
                // Header card
                Container(
                  padding: const EdgeInsets.all(20),
                  decoration: BoxDecoration(
                    color: AppColors.primary,
                    borderRadius: BorderRadius.circular(16),
                    border: Border.all(
                      color: AppColors.gold.withValues(alpha: 0.4),
                    ),
                  ),
                  child: Row(
                    children: [
                      Container(
                        width: 60,
                        height: 60,
                        alignment: Alignment.center,
                        decoration: BoxDecoration(
                          color: AppColors.gold.withValues(alpha: 0.2),
                          shape: BoxShape.circle,
                          border: Border.all(color: AppColors.gold, width: 1.5),
                        ),
                        child: Text(
                          _initials(profile.fullName),
                          style: const TextStyle(
                            color: AppColors.gold,
                            fontSize: 22,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                      ),
                      const SizedBox(width: 16),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              profile.fullName ?? 'Saranga Jewels Customer',
                              style: const TextStyle(
                                color: AppColors.white,
                                fontSize: 18,
                                fontWeight: FontWeight.bold,
                              ),
                            ),
                            const SizedBox(height: 6),
                            Row(
                              children: [
                                const Icon(
                                  Icons.location_on_outlined,
                                  color: AppColors.gold,
                                  size: 16,
                                ),
                                const SizedBox(width: 4),
                                Flexible(
                                  child: Text(
                                    'Branch: ${profile.primaryBranchName ?? profile.primaryBranchCode ?? 'Jaffna'}',
                                    style: TextStyle(
                                      color: AppColors.white.withValues(
                                        alpha: 0.85,
                                      ),
                                      fontSize: 13,
                                    ),
                                  ),
                                ),
                              ],
                            ),
                          ],
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 16),

                // Personal details card
                Card(
                  child: Padding(
                    padding: const EdgeInsets.all(18),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const Text(
                          'Personal Information',
                          style: TextStyle(
                            color: AppColors.dark,
                            fontSize: 16,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                        const SizedBox(height: 12),
                        SJewlsInfoRow(
                          label: 'Full Name',
                          value: profile.fullName ?? '—',
                          emphasizeValue: true,
                        ),
                        SJewlsInfoRow(
                          label: 'Date of Birth',
                          value: Formatters.date(profile.dateOfBirth),
                        ),
                        SJewlsInfoRow(
                          label: 'NIC Number',
                          value: profile.nic ?? '—',
                        ),
                        SJewlsInfoRow(
                          label: 'Primary Branch',
                          value:
                              profile.primaryBranchName != null &&
                                  profile.primaryBranchCode != null
                              ? '${profile.primaryBranchName} (${profile.primaryBranchCode})'
                              : (profile.primaryBranchName ??
                                    profile.primaryBranchCode ??
                                    'Jaffna'),
                        ),
                        SJewlsInfoRow(
                          label: 'Member Since',
                          value: Formatters.date(profile.createdAtUtc),
                        ),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 16),

                // Contacts & Verification card
                Card(
                  child: Padding(
                    padding: const EdgeInsets.all(18),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const Text(
                          'Registered Contacts & Verification',
                          style: TextStyle(
                            color: AppColors.dark,
                            fontSize: 16,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                        const SizedBox(height: 14),
                        _buildContactRow(
                          icon: Icons.phone_outlined,
                          label: 'Phone Number',
                          value: profile.phoneNumber,
                          isVerified: profile.isPhoneVerified,
                          verifiedAt: profile.phoneVerifiedAtUtc,
                          onVerify: profile.hasUnverifiedPhone
                              ? () => _openVerifySecondaryContactDialog(
                                  contact: profile.phoneNumber!,
                                  isPhone: true,
                                )
                              : null,
                        ),
                        const Divider(height: 24),
                        _buildContactRow(
                          icon: Icons.email_outlined,
                          label: 'Email Address',
                          value: profile.email,
                          isVerified: profile.isEmailVerified,
                          verifiedAt: profile.emailVerifiedAtUtc,
                          onVerify: profile.hasUnverifiedEmail
                              ? () => _openVerifySecondaryContactDialog(
                                  contact: profile.email!,
                                  isPhone: false,
                                )
                              : null,
                        ),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 16),

                // Linked plans overview card
                Card(
                  child: Padding(
                    padding: const EdgeInsets.all(18),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          mainAxisAlignment: MainAxisAlignment.spaceBetween,
                          children: [
                            const Expanded(
                              child: Text(
                                'Linked Plans Overview',
                                style: TextStyle(
                                  color: AppColors.dark,
                                  fontSize: 16,
                                  fontWeight: FontWeight.bold,
                                ),
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                              ),
                            ),
                            TextButton(
                              onPressed: () => context.go('/jewelry'),
                              child: const Text('View Plans'),
                            ),
                          ],
                        ),
                        enrolmentsAsync.when(
                          loading: () => const Padding(
                            padding: EdgeInsets.symmetric(vertical: 12),
                            child: LinearProgressIndicator(
                              color: AppColors.primary,
                            ),
                          ),
                          error: (_, _) => const Text(
                            'Visit the Jewelry tab to view your active enrolments.',
                            style: TextStyle(fontSize: 13),
                          ),
                          data: (paged) {
                            final totalGrams = paged.items.fold<double>(
                              0.0,
                              (s, e) => s + e.netAccumulatedGoldGrams,
                            );
                            return Column(
                              children: [
                                SJewlsInfoRow(
                                  label: 'Jewellery Plan Enrolments',
                                  value: '${paged.totalCount}',
                                  emphasizeValue: true,
                                ),
                                SJewlsInfoRow(
                                  label: 'Total Accumulated Gold',
                                  value: Formatters.grams(totalGrams),
                                  goldValue: true,
                                ),
                              ],
                            );
                          },
                        ),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 24),

                // Logout button
                ElevatedButton.icon(
                  style: ElevatedButton.styleFrom(
                    backgroundColor: AppColors.primary,
                    foregroundColor: AppColors.white,
                    padding: const EdgeInsets.symmetric(vertical: 15),
                  ),
                  onPressed: _isLoggingOut ? null : _handleLogout,
                  icon: _isLoggingOut
                      ? const SizedBox(
                          width: 18,
                          height: 18,
                          child: CircularProgressIndicator(
                            strokeWidth: 2,
                            color: AppColors.white,
                          ),
                        )
                      : const Icon(Icons.logout_rounded, color: AppColors.gold),
                  label: const Text('LOG OUT'),
                ),
                const SizedBox(height: 12),

                // Self-service account closure option
                Center(
                  child: TextButton(
                    onPressed: _openAccountClosureDialog,
                    child: Text(
                      'Request Account Closure',
                      style: TextStyle(
                        color: AppColors.dark.withValues(alpha: 0.6),
                        fontSize: 13,
                        decoration: TextDecoration.underline,
                      ),
                    ),
                  ),
                ),
              ],
            ),
          );
        },
      ),
    );
  }

  Widget _buildContactRow({
    required IconData icon,
    required String label,
    required String? value,
    required bool isVerified,
    String? verifiedAt,
    VoidCallback? onVerify,
  }) {
    final hasContact = value != null && value.trim().isNotEmpty;
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(icon, color: AppColors.primary, size: 22),
        const SizedBox(width: 12),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                label,
                style: TextStyle(
                  color: AppColors.dark.withValues(alpha: 0.65),
                  fontSize: 12.5,
                ),
              ),
              const SizedBox(height: 3),
              Text(
                hasContact ? value : 'Not provided',
                style: const TextStyle(
                  color: AppColors.dark,
                  fontSize: 14.5,
                  fontWeight: FontWeight.w600,
                ),
              ),
              if (isVerified && verifiedAt != null) ...[
                const SizedBox(height: 2),
                Text(
                  'Verified on ${Formatters.date(verifiedAt)}',
                  style: TextStyle(
                    color: AppColors.dark.withValues(alpha: 0.52),
                    fontSize: 11.5,
                  ),
                ),
              ],
            ],
          ),
        ),
        if (hasContact)
          Column(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              SJewlsStatusBadge(
                label: isVerified ? 'Verified' : 'Unverified',
                isHighlighted: isVerified,
                isWarning: !isVerified,
              ),
              if (!isVerified && onVerify != null) ...[
                const SizedBox(height: 6),
                TextButton(
                  style: TextButton.styleFrom(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 10,
                      vertical: 4,
                    ),
                    minimumSize: Size.zero,
                    tapTargetSize: MaterialTapTargetSize.shrinkWrap,
                  ),
                  onPressed: onVerify,
                  child: const Text(
                    'Verify via OTP',
                    style: TextStyle(
                      color: AppColors.primary,
                      fontWeight: FontWeight.bold,
                      fontSize: 12.5,
                    ),
                  ),
                ),
              ],
            ],
          ),
      ],
    );
  }

  String _initials(String? fullName) {
    if (fullName == null || fullName.trim().isEmpty) return 'SJ';
    final parts = fullName.trim().split(RegExp(r'\s+'));
    if (parts.length == 1) {
      return parts[0].substring(0, 1).toUpperCase();
    }
    return '${parts[0][0]}${parts[1][0]}'.toUpperCase();
  }
}
