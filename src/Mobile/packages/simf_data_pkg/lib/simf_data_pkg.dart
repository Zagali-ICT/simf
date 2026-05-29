/// SIMF mobile data package — the single HTTP layer.
///
/// Public surface for consumers (the main app, [simf_auth_pkg], and any
/// future feature package). Importing this barrel is enough for everything
/// a feature repository needs to call the SIMF backend.
///
/// See SIMF-MAA-001 v1.2 section 9.1 and DECISIONS_LOG D-002/D-003 for the
/// rules that govern this package — most importantly, this package is the
/// only place in the codebase that depends on `dio`.
library simf_data_pkg;

export 'src/api/api_error_codes.dart';
export 'src/api/api_failure.dart';
export 'src/api/api_result.dart';
export 'src/api/auth_token_source.dart';
export 'src/api/simf_api_client.dart';
export 'src/config/simf_data_config.dart';
export 'src/providers.dart';
export 'src/storage/prefs_storage.dart';
export 'src/storage/secure_storage.dart';
export 'src/storage/storage_keys.dart';
