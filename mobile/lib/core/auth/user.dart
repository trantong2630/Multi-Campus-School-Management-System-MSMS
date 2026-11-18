import '../json.dart';

enum AccountType {
  teacher('Teacher'),
  student('Student'),
  parent('Parent'),
  other('');

  const AccountType(this.value);
  final String value;

  static AccountType parse(String v) => values.firstWhere((t) => t.value == v, orElse: () => AccountType.other);
}

/// Phạm vi dữ liệu tại ngày hiện tại (BR-01, BR-02); mobile chỉ dùng để ẩn hiện lối vào, Backend vẫn kiểm quyền.
class AccountScope {
  AccountScope({required this.homeroomClassIds, required this.headDepartmentIds, required this.linkedStudentIds});

  final List<int> homeroomClassIds;
  final List<int> headDepartmentIds;
  final List<int> linkedStudentIds;

  factory AccountScope.fromJson(Json j) => AccountScope(
    homeroomClassIds: j.ints('homeroomClassIds'),
    headDepartmentIds: j.ints('headDepartmentIds'),
    linkedStudentIds: j.ints('linkedStudentIds'),
  );

  static final empty = AccountScope(homeroomClassIds: const [], headDepartmentIds: const [], linkedStudentIds: const []);
}

class UserInfo {
  UserInfo({
    required this.accountId,
    required this.personId,
    required this.personCode,
    required this.fullName,
    required this.accountType,
    required this.rawAccountType,
    required this.status,
    required this.mustChangePassword,
    required this.scope,
  });

  final int accountId;
  final int personId;
  final String personCode;
  final String fullName;
  final AccountType accountType;
  final String rawAccountType;
  final String status;
  final bool mustChangePassword;
  final AccountScope scope;

  bool get isTeacher => accountType == AccountType.teacher;
  bool get isStudent => accountType == AccountType.student;
  bool get isParent => accountType == AccountType.parent;
  bool get isHomeroom => isTeacher && scope.homeroomClassIds.isNotEmpty;

  /// School Admin, Principal, Vice Principal chỉ dùng web (spec §8.1).
  bool get hasMobile => accountType != AccountType.other;

  /// Tên gọi trong lời chào: chữ cuối của họ tên.
  String get givenName => fullName.trim().split(RegExp(r'\s+')).last;

  factory UserInfo.fromJson(Json j) => UserInfo(
    accountId: j.integer('accountId'),
    personId: j.integer('personId'),
    personCode: j.str('personCode'),
    fullName: j.str('fullName'),
    accountType: AccountType.parse(j.str('accountType')),
    rawAccountType: j.str('accountType'),
    status: j.str('status'),
    mustChangePassword: j.flag('mustChangePassword'),
    scope: j.objOrNull('scope') == null ? AccountScope.empty : AccountScope.fromJson(j.obj('scope')),
  );
}

class LoginResult {
  LoginResult({required this.token, required this.expiresAt, required this.refreshToken, required this.user});

  final String token;
  final DateTime expiresAt;
  final String? refreshToken;
  final UserInfo user;

  factory LoginResult.fromJson(Json j) => LoginResult(
    token: j.str('token'),
    expiresAt: j.date('expiresAt'),
    refreshToken: j.strOrNull('refreshToken'),
    user: UserInfo.fromJson(j.obj('user')),
  );
}
