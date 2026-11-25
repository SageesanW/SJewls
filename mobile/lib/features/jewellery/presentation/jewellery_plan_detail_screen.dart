import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/sjewls_widgets.dart';
import '../../notifications/presentation/notification_provider.dart';
import '../data/jewellery_models.dart';
import 'jewellery_provider.dart';

class JewelleryPlanDetailScreen extends ConsumerStatefulWidget {
  final String planId;

  const JewelleryPlanDetailScreen({super.key, required this.planId});

  @override
  ConsumerState<JewelleryPlanDetailScreen> createState() =>
      _JewelleryPlanDetailScreenState();
}

class _JewelleryPlanDetailScreenState
    extends ConsumerState<JewelleryPlanDetailScreen> {
  int? _selectedDuration;
  bool _isPreviewing = false;
  String? _errorMessage;

  Future<void> _previewAndJoin(CustomerJewelleryPlanDto plan) async {
    final duration =
        _selectedDuration ??
        (plan.offeredDurations.isNotEmpty ? plan.offeredDurations.first : 6);

    setState(() {
      _isPreviewing = true;
      _errorMessage = null;
    });

    try {
      final preview = await ref
          .read(jewelleryRepositoryProvider)
          .previewEnrolment(planId: plan.id, selectedDurationMonths: duration);

      if (!mounted) return;
      await _showEnrolmentConfirmationSheet(preview, fallbackPlanId: plan.id);
    } catch (e) {
      if (mounted) {
        final msg = e.toString().replaceFirst('Exception: ', '');
        setState(() {
          _errorMessage = msg;
        });
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(msg), backgroundColor: Colors.red.shade700),
        );
      }
    } finally {
      if (mounted) {
        setState(() => _isPreviewing = false);
      }
    }
  }

  Future<void> _showEnrolmentConfirmationSheet(
    EnrolmentPreviewResponse preview, {
    required String fallbackPlanId,
  }) async {
    final bool needsRepeatConfirmation =
        preview.requiresRepeatJoinConfirmation || preview.hasPreviouslyJoined;
    bool confirmRepeat = false;
    bool isSubmitting = false;
    String? sheetError;

    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      backgroundColor: AppColors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(22)),
      ),
      builder: (sheetContext) {
        return StatefulBuilder(
          builder: (sheetBuildContext, setSheetState) {
            final canSubmit =
                !isSubmitting && (!needsRepeatConfirmation || confirmRepeat);
            final bottomInset =
                MediaQuery.of(sheetBuildContext).viewInsets.bottom +
                MediaQuery.of(sheetBuildContext).viewPadding.bottom +
                24;

            return SafeArea(
              top: false,
              child: SingleChildScrollView(
                padding: EdgeInsets.only(
                  left: 22,
                  right: 22,
                  top: 22,
                  bottom: bottomInset,
                ),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        const Expanded(
                          child: Text(
                            'Confirm Enrolment Summary',
                            style: TextStyle(
                              color: AppColors.dark,
                              fontSize: 18,
                              fontWeight: FontWeight.bold,
                            ),
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                        IconButton(
                          icon: const Icon(Icons.close, color: AppColors.dark),
                          onPressed: () => Navigator.of(sheetContext).pop(),
                        ),
                      ],
                    ),
                    const SizedBox(height: 12),
                    Container(
                      padding: const EdgeInsets.all(14),
                      decoration: BoxDecoration(
                        color: AppColors.primary.withValues(alpha: 0.06),
                        borderRadius: BorderRadius.circular(12),
                      ),
                      child: Column(
                        children: [
                          SJewlsInfoRow(
                            label: 'Plan Name',
                            value: preview.planName,
                            emphasizeValue: true,
                          ),
                          if (preview.categoryName != null)
                            SJewlsInfoRow(
                              label: 'Category',
                              value: preview.categoryName!,
                            ),
                          SJewlsInfoRow(
                            label: 'Target Gold Weight',
                            value: Formatters.grams(
                              preview.targetGrams,
                              trimTrailingZeros: true,
                            ),
                            emphasizeValue: true,
                            goldValue: true,
                          ),
                          SJewlsInfoRow(
                            label: 'Selected Duration',
                            value: '${preview.selectedDurationMonths} Months',
                          ),
                          SJewlsInfoRow(
                            label: 'Making / Plan Charges',
                            value: Formatters.percentage(
                              preview.premiumPercentage,
                            ),
                          ),
                          SJewlsInfoRow(
                            label: 'Joining Date',
                            value: Formatters.date(preview.proposedJoiningDate),
                          ),
                          SJewlsInfoRow(
                            label: 'Target Deadline',
                            value: Formatters.date(preview.proposedDeadline),
                          ),
                        ],
                      ),
                    ),
                    if (needsRepeatConfirmation) ...[
                      const SizedBox(height: 14),
                      Container(
                        padding: const EdgeInsets.all(12),
                        decoration: BoxDecoration(
                          color: AppColors.gold.withValues(alpha: 0.16),
                          borderRadius: BorderRadius.circular(10),
                          border: Border.all(color: AppColors.gold),
                        ),
                        child: CheckboxListTile(
                          contentPadding: EdgeInsets.zero,
                          value: confirmRepeat,
                          activeColor: AppColors.primary,
                          onChanged: (val) {
                            setSheetState(() => confirmRepeat = val ?? false);
                          },
                          title: Text(
                            'You have ${preview.totalEnrolmentCount} existing enrolment(s) in this plan. Confirm creating an additional enrolment.',
                            style: const TextStyle(
                              color: AppColors.dark,
                              fontSize: 13,
                              fontWeight: FontWeight.w600,
                            ),
                          ),
                        ),
                      ),
                    ],
                    if (sheetError != null) ...[
                      const SizedBox(height: 12),
                      Text(
                        sheetError!,
                        style: const TextStyle(color: Colors.red, fontSize: 13),
                      ),
                    ],
                    const SizedBox(height: 18),
                    ElevatedButton(
                      style: ElevatedButton.styleFrom(
                        backgroundColor: AppColors.primary,
                        foregroundColor: AppColors.white,
                        padding: const EdgeInsets.symmetric(vertical: 15),
                      ),
                      onPressed: !canSubmit
                          ? null
                          : () async {
                              setSheetState(() {
                                isSubmitting = true;
                                sheetError = null;
                              });
                              try {
                                final resolvedPlanId =
                                    preview.planId.trim().isNotEmpty
                                    ? preview.planId.trim()
                                    : fallbackPlanId;
                                final created = await ref
                                    .read(jewelleryRepositoryProvider)
                                    .createEnrolment(
                                      planId: resolvedPlanId,
                                      selectedDurationMonths:
                                          preview.selectedDurationMonths,
                                      confirmRepeatEnrolment:
                                          needsRepeatConfirmation
                                          ? confirmRepeat
                                          : false,
                                    );
                                ref.invalidate(customerEnrolmentsProvider);
                                ref.invalidate(liveGoldRateProvider);
                                ref.invalidate(notificationsProvider);
                                if (sheetContext.mounted) {
                                  Navigator.of(sheetContext).pop();
                                }
                                if (mounted) {
                                  ScaffoldMessenger.of(context).showSnackBar(
                                    const SnackBar(
                                      content: Text(
                                        'Enrolment activated! You can now buy gold toward your target.',
                                      ),
                                      backgroundColor: AppColors.primary,
                                    ),
                                  );
                                  if (created.enrolmentId.isNotEmpty) {
                                    context.go(
                                      '/jewelry/enrolments/${created.enrolmentId}',
                                    );
                                  } else {
                                    context.go('/jewelry');
                                  }
                                }
                              } catch (e) {
                                if (sheetContext.mounted) {
                                  setSheetState(() {
                                    sheetError = e.toString().replaceFirst(
                                      'Exception: ',
                                      '',
                                    );
                                  });
                                }
                              } finally {
                                if (sheetContext.mounted) {
                                  setSheetState(() => isSubmitting = false);
                                }
                              }
                            },
                      child: isSubmitting
                          ? const SizedBox(
                              width: 20,
                              height: 20,
                              child: CircularProgressIndicator(
                                strokeWidth: 2,
                                color: AppColors.white,
                              ),
                            )
                          : const Text('Confirm & Join Plan'),
                    ),
                  ],
                ),
              ),
            );
          },
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    final planAsync = ref.watch(jewelleryPlanDetailProvider(widget.planId));

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: const Text('Plan Details')),
      body: planAsync.when(
        loading: () =>
            const SJewlsLoadingView(message: 'Loading plan details...'),
        error: (err, _) => SJewlsErrorView(
          message: err.toString().replaceFirst('Exception: ', ''),
          onRetry: () =>
              ref.invalidate(jewelleryPlanDetailProvider(widget.planId)),
        ),
        data: (plan) {
          final durations = plan.offeredDurations.isNotEmpty
              ? plan.offeredDurations
              : const [6, 8, 12];
          final activeDuration = _selectedDuration ?? durations.first;

          return ListView(
            padding: const EdgeInsets.all(18),
            children: [
              // Product Image Card
              ClipRRect(
                borderRadius: BorderRadius.circular(16),
                child: SizedBox(
                  height: 210,
                  child:
                      plan.imageUrl != null && plan.imageUrl!.trim().isNotEmpty
                      ? Image.network(
                          plan.imageUrl!,
                          fit: BoxFit.cover,
                          errorBuilder: (_, _, _) => _buildFallbackHeader(),
                        )
                      : _buildFallbackHeader(),
                ),
              ),
              const SizedBox(height: 18),

              // Plan Name & Category
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Expanded(
                    child: Text(
                      plan.name,
                      style: const TextStyle(
                        color: AppColors.dark,
                        fontSize: 22,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                  ),
                  if (plan.categoryName != null) ...[
                    const SizedBox(width: 8),
                    Flexible(
                      child: SJewlsStatusBadge(
                        label: plan.categoryName!,
                        isHighlighted: true,
                      ),
                    ),
                  ],
                ],
              ),
              if (plan.description != null &&
                  plan.description!.trim().isNotEmpty) ...[
                const SizedBox(height: 10),
                Text(
                  plan.description!,
                  style: TextStyle(
                    color: AppColors.dark.withValues(alpha: 0.78),
                    fontSize: 14,
                    height: 1.45,
                  ),
                ),
              ],
              const SizedBox(height: 18),

              // Target & Specifications Card
              Card(
                child: Padding(
                  padding: const EdgeInsets.all(18),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text(
                        'Plan Specifications',
                        style: TextStyle(
                          color: AppColors.dark,
                          fontSize: 16,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                      const SizedBox(height: 12),
                      SJewlsInfoRow(
                        label: 'Target Gold Weight',
                        value: Formatters.grams(
                          plan.targetGrams,
                          trimTrailingZeros: true,
                        ),
                        emphasizeValue: true,
                        goldValue: true,
                      ),
                      SJewlsInfoRow(
                        label: 'Making / Plan Charges',
                        value: Formatters.percentage(plan.premiumPercentage),
                      ),
                      SJewlsInfoRow(
                        label: 'Plan Start Date',
                        value: Formatters.date(plan.startDate),
                      ),
                      SJewlsInfoRow(
                        label: 'Contribution Mode',
                        value: 'Flexible (Amount or Grams)',
                      ),
                    ],
                  ),
                ),
              ),
              const SizedBox(height: 18),

              // Duration Selection Card
              Card(
                child: Padding(
                  padding: const EdgeInsets.all(18),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text(
                        'Select Saving Duration',
                        style: TextStyle(
                          color: AppColors.dark,
                          fontSize: 16,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                      const SizedBox(height: 6),
                      Text(
                        'Choose your preferred saving duration in months. You can contribute flexibly by LKR amount or gold grams within this period.',
                        style: TextStyle(
                          color: AppColors.dark.withValues(alpha: 0.7),
                          fontSize: 13,
                        ),
                      ),
                      const SizedBox(height: 14),
                      Wrap(
                        spacing: 10,
                        runSpacing: 10,
                        children: durations.map((months) {
                          final isSelected = activeDuration == months;
                          return ChoiceChip(
                            label: Text('$months Months'),
                            selected: isSelected,
                            selectedColor: AppColors.primary,
                            backgroundColor: AppColors.background,
                            labelStyle: TextStyle(
                              color: isSelected
                                  ? AppColors.gold
                                  : AppColors.dark,
                              fontWeight: isSelected
                                  ? FontWeight.bold
                                  : FontWeight.w500,
                            ),
                            onSelected: (_) {
                              setState(() => _selectedDuration = months);
                            },
                          );
                        }).toList(),
                      ),
                    ],
                  ),
                ),
              ),

              if (_errorMessage != null) ...[
                const SizedBox(height: 14),
                Container(
                  padding: const EdgeInsets.all(12),
                  decoration: BoxDecoration(
                    color: Colors.red.withValues(alpha: 0.08),
                    borderRadius: BorderRadius.circular(10),
                  ),
                  child: Text(
                    _errorMessage!,
                    style: const TextStyle(color: Colors.red, fontSize: 13.5),
                  ),
                ),
              ],

              const SizedBox(height: 22),
              ElevatedButton.icon(
                style: ElevatedButton.styleFrom(
                  backgroundColor: AppColors.primary,
                  foregroundColor: AppColors.white,
                  padding: const EdgeInsets.symmetric(vertical: 16),
                ),
                onPressed: _isPreviewing ? null : () => _previewAndJoin(plan),
                icon: _isPreviewing
                    ? const SizedBox(
                        width: 20,
                        height: 20,
                        child: CircularProgressIndicator(
                          strokeWidth: 2,
                          color: AppColors.white,
                        ),
                      )
                    : const Icon(
                        Icons.workspace_premium_outlined,
                        color: AppColors.gold,
                      ),
                label: const Text('Preview & Enrol in Plan'),
              ),
              const SizedBox(height: 20),
            ],
          );
        },
      ),
    );
  }

  Widget _buildFallbackHeader() {
    return Container(
      color: AppColors.primary,
      alignment: Alignment.center,
      child: const Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Icon(Icons.diamond_rounded, color: AppColors.gold, size: 56),
          SizedBox(height: 8),
          Text(
            'SARANGA JEWELS',
            style: TextStyle(
              color: AppColors.gold,
              fontSize: 14,
              letterSpacing: 3,
              fontWeight: FontWeight.bold,
            ),
          ),
        ],
      ),
    );
  }
}
