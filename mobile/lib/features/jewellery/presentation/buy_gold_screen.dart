import 'dart:async';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/sjewls_widgets.dart';
import '../../chitu/presentation/chitu_provider.dart';
import '../data/jewellery_models.dart';
import 'jewellery_provider.dart';

class BuyGoldScreen extends ConsumerStatefulWidget {
  final String enrolmentId;

  const BuyGoldScreen({super.key, required this.enrolmentId});

  @override
  ConsumerState<BuyGoldScreen> createState() => _BuyGoldScreenState();
}

class _BuyGoldScreenState extends ConsumerState<BuyGoldScreen> {
  bool _isAmountMode = true;
  final TextEditingController _inputController = TextEditingController();

  bool _isCalculatingPreview = false;
  bool _isLockingQuote = false;
  bool _isInitiatingPayment = false;

  JewelleryCalculationPreviewResponse? _preview;
  CustomerPaymentQuoteDto? _lockedQuote;
  PaymentInitiationResponse? _paymentInitiationResult;
  String? _errorMessage;

  Timer? _countdownTimer;
  Duration _remainingQuoteTime = Duration.zero;

  @override
  void dispose() {
    _countdownTimer?.cancel();
    _inputController.dispose();
    super.dispose();
  }

  void _startQuoteCountdown(String? expiresAtUtc) {
    _countdownTimer?.cancel();
    if (expiresAtUtc == null || expiresAtUtc.isEmpty) return;

    final expiry = DateTime.tryParse(expiresAtUtc)?.toUtc();
    if (expiry == null) return;

    void updateRemaining() {
      final diff = expiry.difference(DateTime.now().toUtc());
      if (!mounted) return;
      setState(() {
        _remainingQuoteTime = diff.isNegative ? Duration.zero : diff;
      });
      if (diff.isNegative) {
        _countdownTimer?.cancel();
      }
    }

    updateRemaining();
    _countdownTimer = Timer.periodic(
      const Duration(seconds: 1),
      (_) => updateRemaining(),
    );
  }

  double? _parseInput() {
    final raw = _inputController.text.trim().replaceAll(',', '');
    if (raw.isEmpty) return null;
    final val = double.tryParse(raw);
    if (val == null || val <= 0) return null;
    return val;
  }

  Future<void> _calculatePreview() async {
    final val = _parseInput();
    if (val == null) {
      setState(() {
        _errorMessage = _isAmountMode
            ? 'Please enter a valid positive amount in LKR.'
            : 'Please enter a valid gold weight in grams.';
      });
      return;
    }

    setState(() {
      _isCalculatingPreview = true;
      _errorMessage = null;
      _paymentInitiationResult = null;
    });

    try {
      final res = await ref
          .read(jewelleryRepositoryProvider)
          .previewCalculation(
            enrolmentId: widget.enrolmentId,
            isAmountMode: _isAmountMode,
            enteredAmount: _isAmountMode ? val : null,
            requestedGrams: !_isAmountMode ? val : null,
          );
      if (mounted) {
        setState(() {
          _preview = res;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _errorMessage = e.toString().replaceFirst('Exception: ', '');
        });
      }
    } finally {
      if (mounted) {
        setState(() => _isCalculatingPreview = false);
      }
    }
  }

  Future<void> _lockQuote() async {
    final val = _parseInput();
    if (val == null) {
      setState(() {
        _errorMessage = _isAmountMode
            ? 'Please enter a valid positive amount in LKR.'
            : 'Please enter a valid gold weight in grams.';
      });
      return;
    }

    setState(() {
      _isLockingQuote = true;
      _errorMessage = null;
      _paymentInitiationResult = null;
    });

    try {
      final quote = await ref
          .read(jewelleryRepositoryProvider)
          .createPaymentQuote(
            enrolmentId: widget.enrolmentId,
            isAmountMode: _isAmountMode,
            enteredAmount: _isAmountMode ? val : null,
            requestedGrams: !_isAmountMode ? val : null,
          );
      if (mounted) {
        setState(() {
          _lockedQuote = quote;
        });
        _startQuoteCountdown(quote.expiresAtUtc);
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _errorMessage = e.toString().replaceFirst('Exception: ', '');
        });
      }
    } finally {
      if (mounted) {
        setState(() => _isLockingQuote = false);
      }
    }
  }

  Future<void> _refreshQuoteStatus() async {
    final quote = _lockedQuote;
    if (quote == null) return;
    setState(() {
      _isLockingQuote = true;
      _errorMessage = null;
    });
    try {
      final updated = await ref
          .read(jewelleryRepositoryProvider)
          .getPaymentQuote(quote.quoteId);
      if (mounted) {
        setState(() => _lockedQuote = updated);
        _startQuoteCountdown(updated.expiresAtUtc);
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _errorMessage = e.toString().replaceFirst('Exception: ', '');
        });
      }
    } finally {
      if (mounted) {
        setState(() => _isLockingQuote = false);
      }
    }
  }

  Future<void> _initiatePayment() async {
    final quote = _lockedQuote;
    if (quote == null) return;

    setState(() {
      _isInitiatingPayment = true;
      _errorMessage = null;
    });

    try {
      final result = await ref
          .read(jewelleryRepositoryProvider)
          .initiateQuotePayment(
            quote.quoteId,
            enrolmentId: widget.enrolmentId,
            fallbackAmount: quote.amountToPay,
            fallbackGrams: quote.totalGoldCredit,
            ratePerGram: quote.ratePerGram,
            fallbackCurrency: quote.currency,
            isAmountMode: _isAmountMode,
          );
      if (mounted) {
        setState(() {
          _paymentInitiationResult = result;
        });
        ref.invalidate(enrolmentDetailProvider(widget.enrolmentId));
        ref.invalidate(customerEnrolmentsProvider);
        ref.invalidate(unifiedCustomerPaymentsProvider);
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _errorMessage = e.toString().replaceFirst('Exception: ', '');
        });
      }
    } finally {
      if (mounted) {
        setState(() => _isInitiatingPayment = false);
      }
    }
  }

  String _formatCountdown(Duration d) {
    final mins = d.inMinutes.remainder(60).toString().padLeft(2, '0');
    final secs = d.inSeconds.remainder(60).toString().padLeft(2, '0');
    return '$mins:$secs';
  }

  @override
  Widget build(BuildContext context) {
    final enrolmentAsync = ref.watch(
      enrolmentDetailProvider(widget.enrolmentId),
    );
    final isQuoteExpired =
        _lockedQuote != null &&
        (_lockedQuote!.isExpired || _remainingQuoteTime == Duration.zero);

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: const Text('Buy Gold Contribution')),
      body: enrolmentAsync.when(
        loading: () =>
            const SJewlsLoadingView(message: 'Loading enrolment details...'),
        error: (err, _) => SJewlsErrorView(
          message: err.toString().replaceFirst('Exception: ', ''),
          onRetry: () =>
              ref.invalidate(enrolmentDetailProvider(widget.enrolmentId)),
        ),
        data: (enrolment) {
          return ListView(
            padding: const EdgeInsets.all(18),
            children: [
              // Enrolment Target Banner
              Container(
                padding: const EdgeInsets.all(16),
                decoration: BoxDecoration(
                  color: AppColors.primary,
                  borderRadius: BorderRadius.circular(14),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      enrolment.planName,
                      style: const TextStyle(
                        color: AppColors.white,
                        fontSize: 16.5,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    const SizedBox(height: 10),
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Flexible(
                          child: Text(
                            'Saved: ${Formatters.grams(enrolment.netAccumulatedGoldGrams)}',
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: const TextStyle(
                              color: AppColors.gold,
                              fontWeight: FontWeight.w600,
                              fontSize: 13,
                            ),
                          ),
                        ),
                        const SizedBox(width: 8),
                        Flexible(
                          child: Text(
                            'Remaining: ${Formatters.grams(enrolment.remainingGrams)}',
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: const TextStyle(
                              color: AppColors.white,
                              fontWeight: FontWeight.w600,
                              fontSize: 13,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 18),

              // Mode Toggle: Enter Amount (LKR) vs Enter Grams
              Card(
                child: Padding(
                  padding: const EdgeInsets.all(18),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      const Text(
                        'Contribution Input Mode',
                        style: TextStyle(
                          color: AppColors.dark,
                          fontSize: 16,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                      const SizedBox(height: 12),
                      Row(
                        children: [
                          Expanded(
                            child: ChoiceChip(
                              label: const Text('Enter Amount (LKR)'),
                              selected: _isAmountMode,
                              selectedColor: AppColors.primary,
                              backgroundColor: AppColors.background,
                              labelStyle: TextStyle(
                                color: _isAmountMode
                                    ? AppColors.gold
                                    : AppColors.dark,
                                fontWeight: _isAmountMode
                                    ? FontWeight.bold
                                    : FontWeight.w500,
                              ),
                              onSelected: (_) {
                                setState(() {
                                  _isAmountMode = true;
                                  _inputController.clear();
                                  _preview = null;
                                  _lockedQuote = null;
                                  _paymentInitiationResult = null;
                                  _errorMessage = null;
                                });
                              },
                            ),
                          ),
                          const SizedBox(width: 10),
                          Expanded(
                            child: ChoiceChip(
                              label: const Text('Enter Gold (Grams)'),
                              selected: !_isAmountMode,
                              selectedColor: AppColors.primary,
                              backgroundColor: AppColors.background,
                              labelStyle: TextStyle(
                                color: !_isAmountMode
                                    ? AppColors.gold
                                    : AppColors.dark,
                                fontWeight: !_isAmountMode
                                    ? FontWeight.bold
                                    : FontWeight.w500,
                              ),
                              onSelected: (_) {
                                setState(() {
                                  _isAmountMode = false;
                                  _inputController.clear();
                                  _preview = null;
                                  _lockedQuote = null;
                                  _paymentInitiationResult = null;
                                  _errorMessage = null;
                                });
                              },
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: 16),
                      TextField(
                        controller: _inputController,
                        keyboardType: const TextInputType.numberWithOptions(
                          decimal: true,
                        ),
                        decoration: InputDecoration(
                          labelText: _isAmountMode
                              ? 'Contribution Amount (${enrolment.currency})'
                              : 'Requested Gold Weight (Grams)',
                          hintText: _isAmountMode
                              ? 'e.g. 25000'
                              : 'e.g. 1.0000',
                          suffixText: _isAmountMode
                              ? enrolment.currency
                              : 'grams',
                        ),
                      ),
                      const SizedBox(height: 16),
                      Row(
                        children: [
                          Expanded(
                            child: OutlinedButton(
                              onPressed: _isCalculatingPreview
                                  ? null
                                  : _calculatePreview,
                              child: _isCalculatingPreview
                                  ? const SizedBox(
                                      width: 18,
                                      height: 18,
                                      child: CircularProgressIndicator(
                                        strokeWidth: 2,
                                        color: AppColors.primary,
                                      ),
                                    )
                                  : const Text('Preview Breakdown'),
                            ),
                          ),
                          const SizedBox(width: 10),
                          Expanded(
                            child: ElevatedButton(
                              style: ElevatedButton.styleFrom(
                                backgroundColor: AppColors.primary,
                                foregroundColor: AppColors.white,
                              ),
                              onPressed: _isLockingQuote ? null : _lockQuote,
                              child: _isLockingQuote
                                  ? const SizedBox(
                                      width: 18,
                                      height: 18,
                                      child: CircularProgressIndicator(
                                        strokeWidth: 2,
                                        color: AppColors.white,
                                      ),
                                    )
                                  : const Text('Lock Quote'),
                            ),
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
              ),

              if (_errorMessage != null) ...[
                const SizedBox(height: 14),
                Container(
                  padding: const EdgeInsets.all(14),
                  decoration: BoxDecoration(
                    color: Colors.red.withValues(alpha: 0.08),
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: Text(
                    _errorMessage!,
                    style: const TextStyle(color: Colors.red, fontSize: 13.5),
                  ),
                ),
              ],

              // Calculation Preview Card
              if (_preview != null && _lockedQuote == null) ...[
                const SizedBox(height: 18),
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
                                'Calculation Preview',
                                style: TextStyle(
                                  color: AppColors.dark,
                                  fontSize: 16,
                                  fontWeight: FontWeight.bold,
                                ),
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                              ),
                            ),
                            const SizedBox(width: 8),
                            const SJewlsStatusBadge(label: 'Estimate'),
                          ],
                        ),
                        const SizedBox(height: 12),
                        SJewlsInfoRow(
                          label: 'Applicable Gold Rate',
                          value:
                              '${Formatters.currency(_preview!.ratePerGram, currencyCode: _preview!.currency)} / g',
                        ),
                        SJewlsInfoRow(
                          label: 'Base Gold Grams',
                          value: Formatters.grams(_preview!.baseGoldGrams),
                        ),
                        SJewlsInfoRow(
                          label:
                              'Making / Plan Charges (${Formatters.percentage(_preview!.premiumPercentage)})',
                          value: Formatters.grams(_preview!.premiumGrams),
                        ),
                        const Divider(height: 20),
                        SJewlsInfoRow(
                          label: 'Net Gold Credited',
                          value: Formatters.grams(_preview!.totalGoldCredit),
                          emphasizeValue: true,
                          goldValue: true,
                        ),
                        SJewlsInfoRow(
                          label: 'Total Payable Amount',
                          value: Formatters.currency(
                            _preview!.amountToPay,
                            currencyCode: _preview!.currency,
                          ),
                          emphasizeValue: true,
                        ),
                        SJewlsInfoRow(
                          label: 'Remaining After Payment',
                          value: Formatters.grams(
                            _preview!.remainingGramsAfterPayment,
                          ),
                        ),
                        if (_preview!.willExceedTarget) ...[
                          const SizedBox(height: 12),
                          Container(
                            padding: const EdgeInsets.all(12),
                            decoration: BoxDecoration(
                              color: AppColors.gold.withValues(alpha: 0.16),
                              borderRadius: BorderRadius.circular(10),
                              border: Border.all(color: AppColors.gold),
                            ),
                            child: Text(
                              'This amount exceeds your remaining target weight.${_preview!.maximumPayableAmountForTarget != null ? ' Maximum payable for target: ${Formatters.currency(_preview!.maximumPayableAmountForTarget, currencyCode: _preview!.currency)}.' : ''}',
                              style: const TextStyle(
                                color: AppColors.dark,
                                fontSize: 13,
                                fontWeight: FontWeight.w600,
                              ),
                            ),
                          ),
                        ],
                      ],
                    ),
                  ),
                ),
              ],

              // Locked 5-Minute Payment Quote Card
              if (_lockedQuote != null) ...[
                const SizedBox(height: 18),
                Card(
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(14),
                    side: const BorderSide(color: AppColors.gold, width: 1.5),
                  ),
                  child: Padding(
                    padding: const EdgeInsets.all(18),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        Row(
                          mainAxisAlignment: MainAxisAlignment.spaceBetween,
                          children: [
                            const Expanded(
                              child: Text(
                                'Locked Payment Quote',
                                style: TextStyle(
                                  color: AppColors.dark,
                                  fontSize: 16,
                                  fontWeight: FontWeight.bold,
                                ),
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                              ),
                            ),
                            const SizedBox(width: 8),
                            SJewlsStatusBadge(
                              label: isQuoteExpired
                                  ? 'Expired'
                                  : 'Expires in ${_formatCountdown(_remainingQuoteTime)}',
                              isHighlighted: !isQuoteExpired,
                              isWarning: isQuoteExpired,
                            ),
                          ],
                        ),
                        const SizedBox(height: 12),
                        SJewlsInfoRow(
                          label: 'Locked Rate',
                          value:
                              '${Formatters.currency(_lockedQuote!.ratePerGram, currencyCode: _lockedQuote!.currency)} / g',
                        ),
                        SJewlsInfoRow(
                          label: 'Net Gold Credit',
                          value: Formatters.grams(
                            _lockedQuote!.totalGoldCredit,
                          ),
                          emphasizeValue: true,
                          goldValue: true,
                        ),
                        SJewlsInfoRow(
                          label: 'Amount to Pay',
                          value: Formatters.currency(
                            _lockedQuote!.amountToPay,
                            currencyCode: _lockedQuote!.currency,
                          ),
                          emphasizeValue: true,
                        ),
                        SJewlsInfoRow(
                          label: 'Remaining After Payment',
                          value: Formatters.grams(
                            _lockedQuote!.remainingGramsAfterPayment,
                          ),
                        ),
                        SJewlsInfoRow(
                          label: 'Valid Until',
                          value: Formatters.dateTime(
                            _lockedQuote!.expiresAtUtc,
                          ),
                        ),
                        const SizedBox(height: 16),
                        if (isQuoteExpired)
                          ElevatedButton.icon(
                            style: ElevatedButton.styleFrom(
                              backgroundColor: AppColors.primary,
                              foregroundColor: AppColors.white,
                            ),
                            onPressed: _isLockingQuote ? null : _lockQuote,
                            icon: const Icon(
                              Icons.refresh_rounded,
                              color: AppColors.gold,
                            ),
                            label: const Text('Quote Expired — Lock New Quote'),
                          )
                        else
                          Row(
                            children: [
                              Expanded(
                                flex: 4,
                                child: OutlinedButton(
                                  onPressed: _isLockingQuote
                                      ? null
                                      : _refreshQuoteStatus,
                                  child: const Text(
                                    'Check Status',
                                    maxLines: 1,
                                    overflow: TextOverflow.ellipsis,
                                  ),
                                ),
                              ),
                              const SizedBox(width: 10),
                              Expanded(
                                flex: 5,
                                child: ElevatedButton(
                                  style: ElevatedButton.styleFrom(
                                    backgroundColor: AppColors.primary,
                                    foregroundColor: AppColors.white,
                                  ),
                                  onPressed: _isInitiatingPayment
                                      ? null
                                      : _initiatePayment,
                                  child: _isInitiatingPayment
                                      ? const SizedBox(
                                          width: 18,
                                          height: 18,
                                          child: CircularProgressIndicator(
                                            strokeWidth: 2,
                                            color: AppColors.white,
                                          ),
                                        )
                                      : const Text(
                                          'Pay Now',
                                          maxLines: 1,
                                          overflow: TextOverflow.ellipsis,
                                        ),
                                ),
                              ),
                            ],
                          ),
                      ],
                    ),
                  ),
                ),
              ],

              // Payment Initiation Outcome
              if (_paymentInitiationResult != null) ...[
                const SizedBox(height: 18),
                if (_paymentInitiationResult!.isSuccess)
                  Container(
                    padding: const EdgeInsets.all(18),
                    decoration: BoxDecoration(
                      color: AppColors.primary.withValues(alpha: 0.08),
                      borderRadius: BorderRadius.circular(14),
                      border: Border.all(color: AppColors.primary, width: 1.5),
                    ),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        Row(
                          children: [
                            const Icon(
                              Icons.check_circle_rounded,
                              color: AppColors.primary,
                              size: 26,
                            ),
                            const SizedBox(width: 10),
                            const Expanded(
                              child: Text(
                                'Payment Confirmed!',
                                style: TextStyle(
                                  color: AppColors.primary,
                                  fontSize: 16.5,
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                            ),
                            Container(
                              padding: const EdgeInsets.symmetric(
                                horizontal: 8,
                                vertical: 3,
                              ),
                              decoration: BoxDecoration(
                                color: AppColors.gold,
                                borderRadius: BorderRadius.circular(12),
                              ),
                              child: const Text(
                                'DEMO MODE',
                                style: TextStyle(
                                  color: AppColors.dark,
                                  fontSize: 10,
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: 10),
                        Text(
                          _paymentInitiationResult!.message ??
                              'Your contribution has been successfully processed and credited.',
                          style: TextStyle(
                            color: AppColors.dark.withValues(alpha: 0.85),
                            fontSize: 13.5,
                            height: 1.4,
                          ),
                        ),
                        const SizedBox(height: 12),
                        SJewlsInfoRow(
                          label: 'Amount Paid',
                          value: Formatters.currency(
                            _paymentInitiationResult!.amountToPay,
                            currencyCode: _paymentInitiationResult!.currency,
                          ),
                          emphasizeValue: true,
                        ),
                        if (_paymentInitiationResult!.goldGramsCredited > 0)
                          SJewlsInfoRow(
                            label: 'Gold Credited',
                            value:
                                '+${Formatters.grams(_paymentInitiationResult!.goldGramsCredited)}',
                            emphasizeValue: true,
                            goldValue: true,
                          ),
                        if (_paymentInitiationResult!.transactionId != null)
                          SJewlsInfoRow(
                            label: 'Transaction ID',
                            value: _paymentInitiationResult!.transactionId!,
                          ),
                        const SizedBox(height: 16),
                        ElevatedButton.icon(
                          style: ElevatedButton.styleFrom(
                            backgroundColor: AppColors.primary,
                            foregroundColor: AppColors.white,
                            padding: const EdgeInsets.symmetric(vertical: 12),
                          ),
                          icon: const Icon(Icons.arrow_back_rounded, size: 18),
                          label: const Text('Back to Enrolment Details'),
                          onPressed: () => Navigator.of(context).pop(),
                        ),
                      ],
                    ),
                  )
                else
                  Container(
                    padding: const EdgeInsets.all(18),
                    decoration: BoxDecoration(
                      color: AppColors.gold.withValues(alpha: 0.16),
                      borderRadius: BorderRadius.circular(14),
                      border: Border.all(color: AppColors.gold, width: 1.4),
                    ),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            const Icon(
                              Icons.storefront_outlined,
                              color: AppColors.primary,
                            ),
                            const SizedBox(width: 10),
                            Expanded(
                              child: Text(
                                _paymentInitiationResult!.isConfigured
                                    ? 'Payment Initiated'
                                    : 'Online Payment Gateway Not Configured',
                                style: const TextStyle(
                                  color: AppColors.primary,
                                  fontSize: 15.5,
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: 8),
                        Text(
                          _paymentInitiationResult!.message ??
                              'Online payment gateway integration is deferred. Your balance has not been modified. Please visit your Saranga Jewels branch to record an in-store cash contribution.',
                          style: TextStyle(
                            color: AppColors.dark.withValues(alpha: 0.85),
                            fontSize: 13.5,
                            height: 1.45,
                          ),
                        ),
                        const SizedBox(height: 10),
                        SJewlsInfoRow(
                          label: 'Quoted Payable Amount',
                          value: Formatters.currency(
                            _paymentInitiationResult!.amountToPay,
                            currencyCode: _paymentInitiationResult!.currency,
                          ),
                          emphasizeValue: true,
                        ),
                      ],
                    ),
                  ),
              ],
              const SizedBox(height: 24),
            ],
          );
        },
      ),
    );
  }
}
