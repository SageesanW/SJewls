import 'package:flutter/material.dart';
import '../theme/app_colors.dart';

class SJewlsLoadingView extends StatelessWidget {
  final String? message;

  const SJewlsLoadingView({super.key, this.message});

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const CircularProgressIndicator(
              color: AppColors.primary,
              strokeWidth: 2.8,
            ),
            if (message != null) ...[
              const SizedBox(height: 16),
              Text(
                message!,
                textAlign: TextAlign.center,
                style: TextStyle(
                  color: AppColors.dark.withValues(alpha: 0.7),
                  fontSize: 14,
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class SJewlsEmptyView extends StatelessWidget {
  final IconData icon;
  final String title;
  final String subtitle;
  final String? actionLabel;
  final VoidCallback? onAction;

  const SJewlsEmptyView({
    super.key,
    required this.icon,
    required this.title,
    required this.subtitle,
    this.actionLabel,
    this.onAction,
  });

  @override
  Widget build(BuildContext context) {
    return Center(
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(28),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              width: 72,
              height: 72,
              decoration: BoxDecoration(
                color: AppColors.primary.withValues(alpha: 0.08),
                shape: BoxShape.circle,
                border: Border.all(
                  color: AppColors.gold.withValues(alpha: 0.4),
                  width: 1.5,
                ),
              ),
              child: Icon(icon, size: 34, color: AppColors.primary),
            ),
            const SizedBox(height: 18),
            Text(
              title,
              textAlign: TextAlign.center,
              style: const TextStyle(
                color: AppColors.dark,
                fontSize: 18,
                fontWeight: FontWeight.bold,
              ),
            ),
            const SizedBox(height: 8),
            Text(
              subtitle,
              textAlign: TextAlign.center,
              style: TextStyle(
                color: AppColors.dark.withValues(alpha: 0.68),
                fontSize: 14,
                height: 1.45,
              ),
            ),
            if (actionLabel != null && onAction != null) ...[
              const SizedBox(height: 22),
              ElevatedButton(
                onPressed: onAction,
                style: ElevatedButton.styleFrom(
                  backgroundColor: AppColors.primary,
                  foregroundColor: AppColors.white,
                ),
                child: Text(actionLabel!),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class SJewlsErrorView extends StatelessWidget {
  final String? title;
  final String message;
  final VoidCallback? onRetry;

  const SJewlsErrorView({
    super.key,
    this.title,
    required this.message,
    this.onRetry,
  });

  @override
  Widget build(BuildContext context) {
    return Center(
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(28),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              width: 68,
              height: 68,
              decoration: BoxDecoration(
                color: AppColors.primary.withValues(alpha: 0.08),
                shape: BoxShape.circle,
              ),
              child: const Icon(
                Icons.error_outline_rounded,
                size: 34,
                color: AppColors.primary,
              ),
            ),
            const SizedBox(height: 16),
            Text(
              title ?? 'Unable to Load Data',
              textAlign: TextAlign.center,
              style: const TextStyle(
                color: AppColors.dark,
                fontSize: 17,
                fontWeight: FontWeight.bold,
              ),
            ),
            const SizedBox(height: 8),
            Text(
              message,
              textAlign: TextAlign.center,
              style: TextStyle(
                color: AppColors.dark.withValues(alpha: 0.72),
                fontSize: 14,
                height: 1.4,
              ),
            ),
            if (onRetry != null) ...[
              const SizedBox(height: 20),
              OutlinedButton.icon(
                onPressed: onRetry,
                icon: const Icon(Icons.refresh_rounded, size: 18),
                label: const Text('Try Again'),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class SJewlsStatusBadge extends StatelessWidget {
  final String label;
  final bool isHighlighted;
  final bool isWarning;
  final Color? backgroundColor;
  final Color? textColor;

  const SJewlsStatusBadge({
    super.key,
    required this.label,
    this.isHighlighted = false,
    this.isWarning = false,
    this.backgroundColor,
    this.textColor,
  });

  factory SJewlsStatusBadge.fromStatus(String? rawStatus) {
    final status = (rawStatus ?? 'Active').trim();
    final lower = status.toLowerCase();
    final isGold =
        lower.contains('complete') ||
        lower.contains('win') ||
        lower.contains('eligib') ||
        lower.contains('verify') ||
        lower.contains('confirm') ||
        lower.contains('succeed');
    final isWarn =
        lower.contains('block') ||
        lower.contains('expir') ||
        lower.contains('fail') ||
        lower.contains('cancel') ||
        lower.contains('close') ||
        lower.contains('unverif') ||
        lower.contains('overdue');
    return SJewlsStatusBadge(
      label: status,
      isHighlighted: isGold,
      isWarning: isWarn,
    );
  }

  @override
  Widget build(BuildContext context) {
    final Color bgColor;
    final Color borderColor;
    final Color resolvedTextColor;

    if (backgroundColor != null || textColor != null) {
      bgColor = backgroundColor ?? AppColors.primary.withValues(alpha: 0.1);
      resolvedTextColor = textColor ?? AppColors.primary;
      borderColor = resolvedTextColor.withValues(alpha: 0.35);
    } else if (isHighlighted) {
      bgColor = AppColors.gold.withValues(alpha: 0.18);
      borderColor = AppColors.gold;
      resolvedTextColor = AppColors.primary;
    } else if (isWarning) {
      bgColor = AppColors.dark.withValues(alpha: 0.08);
      borderColor = AppColors.dark.withValues(alpha: 0.28);
      resolvedTextColor = AppColors.dark;
    } else {
      bgColor = AppColors.primary.withValues(alpha: 0.1);
      borderColor = AppColors.primary.withValues(alpha: 0.3);
      resolvedTextColor = AppColors.primary;
    }

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: bgColor,
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: borderColor, width: 1),
      ),
      child: Text(
        label,
        style: TextStyle(
          color: resolvedTextColor,
          fontSize: 12,
          fontWeight: FontWeight.w600,
        ),
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
      ),
    );
  }
}

class SJewlsInfoRow extends StatelessWidget {
  final String label;
  final String value;
  final bool emphasizeValue;
  final bool goldValue;

  const SJewlsInfoRow({
    super.key,
    required this.label,
    required this.value,
    this.emphasizeValue = false,
    this.goldValue = false,
  });

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Flexible(
            flex: 5,
            child: Text(
              label,
              style: TextStyle(
                color: AppColors.dark.withValues(alpha: 0.68),
                fontSize: 13.5,
              ),
            ),
          ),
          const SizedBox(width: 12),
          Flexible(
            flex: 6,
            child: Text(
              value,
              textAlign: TextAlign.right,
              style: TextStyle(
                color: goldValue ? AppColors.primary : AppColors.dark,
                fontSize: emphasizeValue ? 15 : 13.5,
                fontWeight: emphasizeValue || goldValue
                    ? FontWeight.bold
                    : FontWeight.w500,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
