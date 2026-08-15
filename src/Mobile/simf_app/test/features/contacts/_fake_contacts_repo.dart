import 'package:simf_app/features/contacts/data/contact_models.dart';
import 'package:simf_app/features/contacts/data/contacts_repository.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Shared fake for the contacts repository used by the FDS-014 screen tests.
/// Each method can be made to throw an [ApiFailure] with a chosen HTTP status,
/// and every call is counted so the tests can assert the wiring.
class FakeContactsRepo implements ContactsRepository {
  FakeContactsRepo({
    this.token = 'SHARE-TOKEN-1',
    this.rotatedToken = 'SHARE-TOKEN-2',
    this.card,
    this.savedRow,
    List<SavedContactRow>? saved,
    this.tokenStatus,
    this.rotateStatus,
    this.resolveStatus,
    this.saveStatus,
    this.listStatus,
    this.removeStatus,
    this.vcard = 'BEGIN:VCARD\r\nVERSION:3.0\r\nFN:Test\r\nEND:VCARD\r\n',
  }) : saved = saved ?? <SavedContactRow>[];

  final String token;
  final String rotatedToken;
  final VisitorCard? card;
  final SavedContactRow? savedRow;
  List<SavedContactRow> saved;
  final int? tokenStatus;
  final int? rotateStatus;
  final int? resolveStatus;
  final int? saveStatus;
  final int? listStatus;
  final int? removeStatus;
  final String vcard;

  int getTokenCalls = 0;
  int rotateCalls = 0;
  int resolveCalls = 0;
  int saveCalls = 0;
  int listCalls = 0;
  int removeCalls = 0;
  int vcardCalls = 0;
  String? lastResolvedToken;
  String? lastSavedToken;
  String? lastSavedNote;
  String? lastRemovedId;

  ApiFailure _fail(int status) => ApiFailure(
      code: ApiErrorCodes.clientNetwork, message: 'x', httpStatus: status,);

  @override
  Future<String> getMyShareToken() async {
    getTokenCalls++;
    if (tokenStatus != null) {
      throw _fail(tokenStatus!);
    }
    return token;
  }

  @override
  Future<String> rotateShareToken() async {
    rotateCalls++;
    if (rotateStatus != null) {
      throw _fail(rotateStatus!);
    }
    return rotatedToken;
  }

  @override
  Future<VisitorCard> resolve(String token) async {
    resolveCalls++;
    lastResolvedToken = token;
    if (resolveStatus != null) {
      throw _fail(resolveStatus!);
    }
    return card ??
        const VisitorCard(
          userId: 'u1',
          name: 'Sara Ahmed',
          nameArabic: 'سارة أحمد',
          available: true,
          jobTitle: 'Captain',
          organisation: 'Royal Saudi Naval Forces',
        );
  }

  @override
  Future<SavedContactRow> save(String token, String? note) async {
    saveCalls++;
    lastSavedToken = token;
    lastSavedNote = note;
    if (saveStatus != null) {
      throw _fail(saveStatus!);
    }
    return savedRow ??
        const SavedContactRow(
          id: 's1',
          subjectUserId: 'u1',
          name: 'Sara Ahmed',
          nameArabic: 'سارة أحمد',
          subjectAvailable: true,
        );
  }

  @override
  Future<List<SavedContactRow>> listSaved() async {
    listCalls++;
    if (listStatus != null) {
      throw _fail(listStatus!);
    }
    return saved;
  }

  @override
  Future<void> remove(String id) async {
    removeCalls++;
    lastRemovedId = id;
    if (removeStatus != null) {
      throw _fail(removeStatus!);
    }
    saved = saved.where((r) => r.id != id).toList();
  }

  @override
  Future<String> getVcard(String id) async {
    vcardCalls++;
    return vcard;
  }
}
