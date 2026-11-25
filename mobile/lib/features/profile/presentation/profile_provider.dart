import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../auth/presentation/auth_provider.dart';
import '../data/profile_models.dart';
import '../data/profile_repository.dart';

final profileRepositoryProvider = Provider<ProfileRepository>((ref) {
  final apiClient = ref.watch(apiClientProvider);
  final storage = ref.watch(tokenStorageProvider);
  return ProfileRepository(apiClient, tokenStorage: storage);
});

final customerProfileProvider = FutureProvider.autoDispose<CustomerProfileDto>((
  ref,
) async {
  final repo = ref.watch(profileRepositoryProvider);
  return repo.getProfile();
});
