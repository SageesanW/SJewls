import 'dart:math';

class Formatters {
  Formatters._();

  static final Random _random = Random();

  static const List<String> _months = [
    'Jan',
    'Feb',
    'Mar',
    'Apr',
    'May',
    'Jun',
    'Jul',
    'Aug',
    'Sep',
    'Oct',
    'Nov',
    'Dec',
  ];

  /// Formats a monetary amount with currency code and thousand separators, e.g. "LKR 25,000.00"
  static String currency(
    num? amount, {
    String? currencyCode,
    int decimals = 2,
  }) {
    final code = (currencyCode == null || currencyCode.trim().isEmpty)
        ? 'LKR'
        : currencyCode.trim().toUpperCase();
    final val = (amount ?? 0).toDouble();
    final isNegative = val < 0;
    final fixed = val.abs().toStringAsFixed(decimals);
    final parts = fixed.split('.');
    final intPart = parts[0];
    final buffer = StringBuffer();
    for (int i = 0; i < intPart.length; i++) {
      final reverseIndex = intPart.length - i;
      buffer.write(intPart[i]);
      if (reverseIndex > 1 && reverseIndex % 3 == 1) {
        buffer.write(',');
      }
    }
    final formattedNumber = parts.length > 1
        ? '${buffer.toString()}.${parts[1]}'
        : buffer.toString();
    return '${isNegative ? '-' : ''}$code $formattedNumber';
  }

  /// Formats gold weight in grams (up to 4 decimal places per backend specification)
  static String grams(
    num? value, {
    int decimals = 4,
    bool trimTrailingZeros = false,
  }) {
    final val = (value ?? 0).toDouble();
    String text = val.toStringAsFixed(decimals);
    if (trimTrailingZeros && text.contains('.')) {
      text = text.replaceFirst(RegExp(r'0+$'), '');
      if (text.endsWith('.')) {
        text = '${text}00';
      } else if (text.split('.')[1].length == 1) {
        text = '${text}0';
      }
    }
    return '$text g';
  }

  /// Formats a percentage value, e.g. "12.5%"
  static String percentage(num? value, {int decimals = 1}) {
    final val = (value ?? 0).toDouble();
    if (val == val.roundToDouble()) {
      return '${val.toInt()}%';
    }
    return '${val.toStringAsFixed(decimals)}%';
  }

  /// Formats a YYYY-MM-DD or ISO-8601 date string into "19 Oct 2026"
  static String date(String? raw) {
    if (raw == null || raw.trim().isEmpty) return '—';
    try {
      final parsed = DateTime.parse(raw.trim());
      // Convert UTC timestamps to Asia/Colombo (+05:30)
      final local = raw.contains('T')
          ? parsed.toUtc().add(const Duration(hours: 5, minutes: 30))
          : parsed;
      final day = local.day.toString().padLeft(2, '0');
      final month = _months[local.month - 1];
      return '$day $month ${local.year}';
    } catch (_) {
      return raw;
    }
  }

  /// Formats an ISO-8601 UTC timestamp into Asia/Colombo date & time, e.g. "19 Oct 2026, 05:30 PM"
  static String dateTime(String? raw) {
    if (raw == null || raw.trim().isEmpty) return '—';
    try {
      final parsed = DateTime.parse(
        raw.trim(),
      ).toUtc().add(const Duration(hours: 5, minutes: 30));
      final day = parsed.day.toString().padLeft(2, '0');
      final month = _months[parsed.month - 1];
      final hour24 = parsed.hour;
      final period = hour24 >= 12 ? 'PM' : 'AM';
      final hour12 = hour24 % 12 == 0 ? 12 : hour24 % 12;
      final min = parsed.minute.toString().padLeft(2, '0');
      return '$day $month ${parsed.year}, ${hour12.toString().padLeft(2, '0')}:$min $period';
    } catch (_) {
      return raw;
    }
  }

  /// Generates a valid RFC 4122 UUID v4 string for X-Idempotency-Key headers
  static String generateIdempotencyKey() {
    final bytes = List<int>.generate(16, (_) => _random.nextInt(256));
    bytes[6] = (bytes[6] & 0x0f) | 0x40; // version 4
    bytes[8] = (bytes[8] & 0x3f) | 0x80; // variant 1
    String hex(int b) => b.toRadixString(16).padLeft(2, '0');
    final h = bytes.map(hex).join();
    return '${h.substring(0, 8)}-${h.substring(8, 12)}-${h.substring(12, 16)}-${h.substring(16, 20)}-${h.substring(20, 32)}';
  }
}
