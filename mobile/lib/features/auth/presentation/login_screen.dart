import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:go_router/go_router.dart';
import 'package:sjewls_mobile/features/auth/data/auth_repository.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/network/api_client.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  bool isPhone = true; // Default to phone login
  bool _isLoading = false;
  String? _errorMessage;
  final TextEditingController _controller = TextEditingController();

  @override
  void dispose() {
    _controller.dispose(); // always clean up controllers
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.primary,
      body: SingleChildScrollView(
        child: SafeArea(
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 24),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const SizedBox(height: 150),
                Center(
                  child: Image.asset(
                    'assets/images/logo.jpeg',
                    height: 200,
                    width: 200,
                  ),
                ),
                const SizedBox(height: 120),
                Text(
                  'Enter your phone or email',
                  style: TextStyle(
                    fontSize: 16,
                    color: AppColors.white.withValues(alpha: 0.7),
                  ),
                ),
                const SizedBox(height: 20),
                Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    // Toggle buttons for Phone and Email
                    ElevatedButton(
                      onPressed: () {
                        setState(() {
                          isPhone = true;
                          _errorMessage = null;
                          _controller.clear();
                        });
                      },
                      style: ElevatedButton.styleFrom(
                        backgroundColor: isPhone
                            ? AppColors.button
                            : AppColors
                                  .primary, // Use primary color for selected button
                        foregroundColor: isPhone
                            ? AppColors.white
                            : AppColors.white,
                        padding: const EdgeInsets.symmetric(
                          horizontal: 24,
                          vertical: 14,
                        ),
                        shape: RoundedRectangleBorder(
                          // borderRadius: BorderRadius.circular(8),
                          side: BorderSide(color: AppColors.primary),
                        ),
                      ),
                      child: const Text('Phone'),
                    ),
                    // const SizedBox(width: 16),
                    // Email button
                    ElevatedButton(
                      onPressed: () {
                        setState(() {
                          isPhone = false;
                          _errorMessage = null;
                          _controller.clear();
                        });
                      },
                      style: ElevatedButton.styleFrom(
                        backgroundColor: !isPhone
                            ? AppColors.button
                            : AppColors
                                  .primary, // Use primary color for selected button
                        foregroundColor: !isPhone
                            ? AppColors.white
                            : AppColors.white,
                        padding: const EdgeInsets.symmetric(
                          horizontal: 24,
                          vertical: 14,
                        ),
                        shape: RoundedRectangleBorder(
                          // borderRadius: BorderRadius.circular(8),
                          side: BorderSide(color: AppColors.primary),
                        ),
                      ),
                      child: const Text('Email'),
                    ),
                  ],
                ),
                const SizedBox(height: 20),
                // Phone / Email field
                TextField(
                  key: ValueKey<bool>(isPhone),
                  style: const TextStyle(color: AppColors.inputText),
                  cursorColor: AppColors.primary,
                  controller: _controller,
                  keyboardType: isPhone
                      ? const TextInputType.numberWithOptions(
                          signed: false,
                          decimal: false,
                        )
                      : TextInputType.emailAddress,
                  inputFormatters: isPhone
                      ? [FilteringTextInputFormatter.digitsOnly]
                      : null,
                  autocorrect: false,
                  enableSuggestions: !isPhone,
                  textCapitalization: TextCapitalization.none,
                  textInputAction: TextInputAction.done,
                  decoration: InputDecoration(
                    labelText: isPhone ? 'Phone Number' : 'Email Address',
                    hintText: isPhone
                        ? 'Enter phone number'
                        : 'Enter email address',
                    hintStyle: TextStyle(
                      color: AppColors.inputText.withValues(alpha: 0.5),
                    ),
                    labelStyle: const TextStyle(color: AppColors.primary),
                    floatingLabelStyle: const TextStyle(
                      color: AppColors.gold,
                      fontWeight: FontWeight.bold,
                    ),
                    filled: true,
                    fillColor: AppColors.otpBackground,
                    border: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(8),
                    ),
                    enabledBorder: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(8),
                      borderSide: BorderSide(color: AppColors.button),
                    ),
                    focusedBorder: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(8),
                      borderSide: const BorderSide(
                        color: AppColors.gold,
                        width: 2,
                      ),
                    ),
                  ),
                ),
                if (_errorMessage != null) ...[
                  const SizedBox(height: 12),
                  Text(
                    _errorMessage!,
                    style: const TextStyle(color: Colors.red, fontSize: 14),
                  ),
                ],
                const SizedBox(height: 20),

                // Continue button
                SizedBox(
                  width: double.infinity,
                  child: ElevatedButton(
                    onPressed: _isLoading
                        ? null
                        : () async {
                            final value = _controller.text.trim();

                            // Basic validation
                            if (value.isEmpty) {
                              setState(() {
                                _errorMessage = isPhone
                                    ? 'Please enter phone number'
                                    : 'Please enter email address';
                              });
                              return;
                            }

                            if (!isPhone) {
                              final emailRegex = RegExp(
                                r'^[\w-\.]+@([\w-]+\.)+[\w-]{2,4}$',
                              );
                              if (!emailRegex.hasMatch(value)) {
                                setState(() {
                                  _errorMessage = 'Please enter a valid email';
                                });
                                return;
                              }
                            }

                            // Start loading
                            setState(() {
                              _isLoading = true;
                              _errorMessage = null;
                            });

                            try {
                              // Create repository and call the API
                              final repo = AuthRepository(ApiClient());
                              await repo.requestOtp(value);

                              // Success → go to OTP screen
                              if (mounted) {
                                // ignore: use_build_context_synchronously
                                context.push(
                                  '/otp',
                                  extra: {'contact': value, 'isPhone': isPhone},
                                );
                              }
                            } catch (e) {
                              // Show error
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
                    child: const Text(
                      'LOGIN',
                      style: TextStyle(color: AppColors.primary),
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
