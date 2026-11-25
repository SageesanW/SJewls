import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:go_router/go_router.dart';
import '../../../core/theme/app_colors.dart';
import 'package:sjewls_mobile/core/network/api_client.dart';
import 'package:sjewls_mobile/features/auth/data/auth_repository.dart';

class OtpScreen extends StatefulWidget {
  final String contact;
  final bool isPhone;

  const OtpScreen({super.key, required this.contact, required this.isPhone});

  @override
  State<OtpScreen> createState() => _OtpScreenState();
}

class _OtpScreenState extends State<OtpScreen> {
  bool _isLoading = false;
  String? _errorMessage;
  // 6 controllers + 6 focus nodes for the oval boxes
  final List<TextEditingController> _controllers = List.generate(
    6,
    (_) => TextEditingController(),
  );
  final List<FocusNode> _focusNodes = List.generate(6, (_) => FocusNode());

  @override
  void dispose() {
    for (final c in _controllers) {
      c.dispose();
    }
    for (final f in _focusNodes) {
      f.dispose();
    }
    super.dispose();
  }

  // Helper: move to next box
  void _onChanged(String value, int index) {
    if (value.length == 1 && index < 5) {
      _focusNodes[index + 1].requestFocus();
    }
    if (value.isEmpty && index > 0) {
      _focusNodes[index - 1].requestFocus();
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.white,
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 24),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const SizedBox(height: 20),

              // ===== GREEN CIRCULAR BACK BUTTON + TITLE =====
              Row(
                children: [
                  // Green circle back button
                  GestureDetector(
                    onTap: () => context.go('/login'),
                    child: Container(
                      width: 40,
                      height: 40,
                      decoration: const BoxDecoration(
                        color: AppColors.primary, // deep green
                        shape: BoxShape.circle,
                      ),
                      child: const Icon(
                        Icons.arrow_back,
                        color: Colors.white,
                        size: 20,
                      ),
                    ),
                  ),
                  const SizedBox(width: 16),
                  Expanded(
                    child: Text(
                      widget.isPhone ? 'Verify Phone' : 'Verify Email',
                      style: const TextStyle(
                        fontSize: 22,
                        fontWeight: FontWeight.bold,
                        color: AppColors.dark,
                      ),
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                    ),
                  ),
                ],
              ),

              const SizedBox(height: 32),

              // OTP sent text
              Text(
                'OTP sent to ${widget.contact}',
                style: TextStyle(
                  fontSize: 15,
                  color: AppColors.dark.withValues(alpha: 0.7),
                ),
              ),

              const SizedBox(height: 40),

              // Centre icon (inbox / message)
              Center(
                child: Icon(
                  Icons.mark_email_read_outlined,
                  size: 64,
                  color: AppColors.primary.withValues(alpha: 0.7),
                ),
              ),

              const SizedBox(height: 40),

              // ===== 6 OVAL BOXES =====
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: List.generate(6, (index) {
                  return Flexible(
                    child: Container(
                      constraints: const BoxConstraints(maxWidth: 48),
                      height: 56,
                      margin: const EdgeInsets.symmetric(horizontal: 2),
                      child: TextField(
                        controller: _controllers[index],
                        focusNode: _focusNodes[index],
                        keyboardType: TextInputType.number,
                        textAlign: TextAlign.center,
                        maxLength: 1,
                        style: const TextStyle(
                          color: AppColors.inputText,
                          fontSize: 22,
                          fontWeight: FontWeight.bold,
                        ),
                        inputFormatters: [
                          FilteringTextInputFormatter.digitsOnly,
                        ],
                        decoration: InputDecoration(
                          hintStyle: const TextStyle(color: AppColors.dark),
                          counterText: '', // hide the 0/1 counter
                          contentPadding: EdgeInsets.zero,
                          enabledBorder: OutlineInputBorder(
                            borderRadius: BorderRadius.circular(
                              28,
                            ), // oval look
                            borderSide: BorderSide(
                              color: AppColors.dark.withValues(alpha: 0.3),
                            ),
                          ),
                          focusedBorder: OutlineInputBorder(
                            borderRadius: BorderRadius.circular(28),
                            borderSide: const BorderSide(
                              color: AppColors.primary,
                              width: 2,
                            ),
                          ),
                        ),
                        onChanged: (value) => _onChanged(value, index),
                      ),
                    ),
                  );
                }),
              ),

              const SizedBox(height: 40),
              if (_errorMessage != null) ...[
                Text(_errorMessage!, style: const TextStyle(color: Colors.red)),
                const SizedBox(height: 16),
              ],
              // Verify button
              SizedBox(
                width: double.infinity,
                child: ElevatedButton(
                  style: ElevatedButton.styleFrom(
                    backgroundColor: AppColors.primary,
                  ),
                  onPressed: _isLoading
                      ? null
                      : () async {
                          final otp = _controllers.map((c) => c.text).join();

                          if (otp.length != 6) {
                            setState(() {
                              _errorMessage =
                                  'Please enter the complete 6-digit OTP';
                            });
                            return;
                          }

                          setState(() {
                            _isLoading = true;
                            _errorMessage = null;
                          });

                          try {
                            final repo = AuthRepository(ApiClient());
                            final result = await repo.verifyOtp(
                              contact: widget.contact,
                              code: otp,
                            );

                            // Check what the backend decided
                            final nextAction = result['nextAction'];

                            if (mounted) {
                              if (nextAction == 'Dashboard') {
                                // ignore: use_build_context_synchronously
                                context.go('/home');
                              } else if (nextAction == 'CompleteProfile') {
                                // ignore: use_build_context_synchronously
                                context.go(
                                  '/complete-profile',
                                  extra: {
                                    'registrationToken':
                                        result['registrationToken'],
                                    'verifiedContact':
                                        result['verifiedContact'] ??
                                        widget.contact,
                                    'isPhone': widget.isPhone,
                                  },
                                );
                              } else {
                                // Unexpected response
                                setState(() {
                                  _errorMessage =
                                      'Unexpected response from server';
                                });
                              }
                            }
                          } catch (e) {
                            setState(() {
                              _errorMessage = e.toString().replaceAll(
                                'Exception: ',
                                '',
                              );
                            });
                          } finally {
                            if (mounted) {
                              setState(() {
                                _isLoading = false;
                              });
                            }
                          }
                        },
                  child: _isLoading
                      ? const SizedBox(
                          height: 20,
                          width: 20,
                          child: CircularProgressIndicator(
                            color: Colors.white,
                            strokeWidth: 2,
                          ),
                        )
                      : const Text('Verify'),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
