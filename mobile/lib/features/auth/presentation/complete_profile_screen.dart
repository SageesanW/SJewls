import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:go_router/go_router.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/network/api_client.dart';
import '../data/auth_repository.dart';

class CompleteProfileScreen extends StatefulWidget {
  final String registrationToken;
  final String verifiedContact;
  final bool isPhone; // true if original contact was phone

  const CompleteProfileScreen({
    super.key,
    required this.registrationToken,
    required this.verifiedContact,
    required this.isPhone,
  });

  @override
  State<CompleteProfileScreen> createState() => _CompleteProfileScreenState();
}

class _CompleteProfileScreenState extends State<CompleteProfileScreen> {
  final _nameController = TextEditingController();
  final _nicController = TextEditingController();
  final _secondaryController = TextEditingController();
  DateTime? _dob;

  bool _isLoading = false;
  String? _errorMessage;

  @override
  void dispose() {
    _nameController.dispose();
    _nicController.dispose();
    _secondaryController.dispose();
    super.dispose();
  }

  Future<void> _pickDob() async {
    final now = DateTime.now();
    final picked = await showDatePicker(
      context: context,
      initialDate: DateTime(now.year - 20),
      firstDate: DateTime(1950),
      lastDate: DateTime(now.year - 18), // must be 18+
    );
    if (picked != null) {
      setState(() => _dob = picked);
    }
  }

  String _formatDob(DateTime d) {
    // yyyy-MM-dd
    final m = d.month.toString().padLeft(2, '0');
    final day = d.day.toString().padLeft(2, '0');
    return '${d.year}-$m-$day';
  }

  Future<void> _submit() async {
    final name = _nameController.text.trim();
    if (name.length < 2) {
      setState(() => _errorMessage = 'Please enter your full name');
      return;
    }
    if (_dob == null) {
      setState(() => _errorMessage = 'Please select date of birth');
      return;
    }

    final nic = _nicController.text.trim();
    if (nic.isEmpty) {
      setState(() => _errorMessage = 'Please enter your NIC');
      return;
    }

    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });

    try {
      final repo = AuthRepository(ApiClient());

      // If original contact was email, secondary can be phone (and vice versa)
      await repo.completeRegistration(
        registrationToken: widget.registrationToken,
        fullName: name,
        dateOfBirth: _formatDob(_dob!),
        nic: nic,
        email: widget.isPhone
            ? (_secondaryController.text.trim().isEmpty
                  ? null
                  : _secondaryController.text.trim())
            : widget.verifiedContact,
        phoneNumber: widget.isPhone
            ? widget.verifiedContact
            : (_secondaryController.text.trim().isEmpty
                  ? null
                  : _secondaryController.text.trim()),
      );

      if (mounted) {
        context.go('/home');
      }
    } catch (e) {
      setState(() {
        _errorMessage = e.toString().replaceAll('Exception: ', '');
      });
    } finally {
      if (mounted) {
        setState(() => _isLoading = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Complete Profile'),
        backgroundColor: AppColors.primary,
      ),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                'Verified: ${widget.verifiedContact}',
                style: TextStyle(color: AppColors.dark.withValues(alpha: 0.7)),
              ),

              const SizedBox(height: 24),

              TextField(
                controller: _nameController,
                decoration: const InputDecoration(
                  labelText: 'Full Name *',
                  border: OutlineInputBorder(),
                ),
              ),
              const SizedBox(height: 16),

              // Date of birth
              InkWell(
                onTap: _pickDob,
                child: InputDecorator(
                  decoration: const InputDecoration(
                    labelText: 'Date of Birth * (18+)',
                    border: OutlineInputBorder(),
                  ),
                  child: Text(_dob == null ? 'Select date' : _formatDob(_dob!)),
                ),
              ),
              const SizedBox(height: 16),

              TextField(
                controller: _nicController,
                textCapitalization: TextCapitalization.characters,
                decoration: const InputDecoration(
                  labelText: 'NIC *',
                  border: OutlineInputBorder(),
                ),
              ),
              const SizedBox(height: 16),

              TextField(
                controller: _secondaryController,
                keyboardType: widget.isPhone
                    ? TextInputType.emailAddress
                    : const TextInputType.numberWithOptions(
                        signed: false,
                        decimal: false,
                      ),
                inputFormatters: widget.isPhone
                    ? null
                    : [FilteringTextInputFormatter.digitsOnly],
                decoration: InputDecoration(
                  labelText: widget.isPhone
                      ? 'Email (optional)'
                      : 'Phone (optional)',
                  border: const OutlineInputBorder(),
                ),
              ),

              if (_errorMessage != null) ...[
                const SizedBox(height: 12),
                Text(_errorMessage!, style: const TextStyle(color: Colors.red)),
              ],

              const SizedBox(height: 24),
              SizedBox(
                width: double.infinity,
                child: ElevatedButton(
                  onPressed: _isLoading ? null : _submit,
                  child: _isLoading
                      ? const SizedBox(
                          width: 20,
                          height: 20,
                          child: CircularProgressIndicator(
                            strokeWidth: 2,
                            color: Colors.white,
                          ),
                        )
                      : const Text('Complete Registration'),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
