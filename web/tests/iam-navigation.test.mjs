import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { registerHooks } from 'node:module';
import { test } from 'node:test';
import ts from 'typescript';

// Menu chỉ chứa dữ liệu/hàm; transpile TSX để test đúng cấu hình đang dùng ở sidebar và chặn URL.
const hook = registerHooks({
  load(url, context, nextLoad) {
    if (url.endsWith('/src/app/menu.tsx')) {
      const source = ts.transpileModule(readFileSync(new URL(url), 'utf8'), {
        compilerOptions: { module: ts.ModuleKind.ESNext, jsx: ts.JsxEmit.ReactJSX },
      }).outputText;
      return { format: 'module', source, shortCircuit: true };
    }
    return nextLoad(url, context);
  },
});
const { visibleMenu, isPathAllowed } = await import('../src/app/menu.tsx');
hook.deregister();

function user(accountType, scope = {}) {
  return { accountType, scope: {
    ledCampusIds: [], headDepartmentIds: [], homeroomClassIds: [], subjectResponsibilities: [], ...scope,
  } };
}
function paths(person) {
  return visibleMenu(person).flatMap((group) => group.children ? group.children.map((item) => item.path) : [group.path]);
}

test('School Admin sees account management and its own dashboard, never schedule or role-bound actions', () => {
  const person = user('SchoolAdmin');
  assert.ok(paths(person).includes('/adm/accounts'));
  assert.equal(isPathAllowed('/dashboard', person), true);
  for (const path of ['/sch/homeroom-students', '/scd/timetable', '/scd/schedule', '/adm/exports', '/lrn/assign', '/lrn/todo', '/par/inbox', '/par/leave-requests', '/com/posts']) {
    assert.ok(!paths(person).includes(path), path);
    assert.equal(isPathAllowed(path, person), false, path);
  }
  assert.ok(paths(person).includes('/com/feed'));
});

test('Principal reads the dashboard and schedule without inheriting account management', () => {
  const person = user('Principal');
  assert.equal(isPathAllowed('/dashboard', person), true);
  assert.equal(isPathAllowed('/sch/homeroom-students', person), false);
  assert.equal(isPathAllowed('/dashboard/school', person), true);
  // /dashboard chỉ chuyển tới mục đầu tiên được mở (Dev_Conventions §4.7); Học sinh, Phụ huynh không có màn Tình hình nào.
  for (const role of ['Student', 'Parent']) assert.equal(isPathAllowed('/dashboard/school', user(role)), false, role);
  assert.equal(isPathAllowed('/scd/schedule', person), true);
  // Hiệu trưởng xem thời khóa biểu chỉ đọc (SCD-01); quyền ghi thuộc Hiệu phó, kiểm ở Backend.
  assert.equal(isPathAllowed('/scd/timetable', person), true);
  assert.equal(isPathAllowed('/adm/accounts', person), false);
});

test('Structure screens with statistics: School Admin edits, Principal and campus-leading Vice Principal only read', () => {
  const structure = ['/sch/subjects', '/sch/years', '/sch/classes'];
  const adminOnly = ['/sch/holidays', '/sch/period-slots', '/sch/rollover'];
  for (const path of structure) {
    assert.equal(isPathAllowed(path, user('SchoolAdmin')), true, path);
    assert.equal(isPathAllowed(path, user('Principal')), true, path);
    assert.equal(isPathAllowed(path, user('VicePrincipal', { ledCampusIds: [1] })), true, path);
    assert.equal(isPathAllowed(path, user('VicePrincipal')), false, path);
    for (const role of ['Teacher', 'Student', 'Parent']) assert.equal(isPathAllowed(path, user(role, { headDepartmentIds: [1], homeroomClassIds: [1] })), false, `${role}: ${path}`);
  }
  // Phân hiệu: School Admin sửa, Hiệu trưởng chỉ xem; Hiệu phó không có màn này.
  assert.equal(isPathAllowed('/sch/campuses', user('SchoolAdmin')), true);
  assert.equal(isPathAllowed('/sch/campuses', user('Principal')), true);
  assert.equal(isPathAllowed('/sch/campuses', user('VicePrincipal', { ledCampusIds: [1] })), false);
  for (const path of adminOnly) {
    assert.equal(isPathAllowed(path, user('SchoolAdmin')), true, path);
    for (const role of ['Principal', 'VicePrincipal']) assert.equal(isPathAllowed(path, user(role, { ledCampusIds: [1] })), false, `${role}: ${path}`);
  }
});

test('Vice Principal navigation follows current campus duty', () => {
  const withoutDuty = user('VicePrincipal');
  const assigned = user('VicePrincipal', { ledCampusIds: [2] });
  for (const path of ['/dashboard/campus', '/scd/timetable', '/scd/responsibilities', '/scd/schedule', '/scd/absences', '/ppl/staff']) {
    assert.equal(isPathAllowed(path, withoutDuty), false, path);
    assert.equal(isPathAllowed(path, assigned), true, path);
  }
  assert.equal(isPathAllowed('/adm/accounts', assigned), false);
});

test('Teacher gains only the navigation attached to each current responsibility', () => {
  const teacher = user('Teacher');
  assert.equal(isPathAllowed('/lrn/questions', teacher), true);
  assert.equal(isPathAllowed('/dashboard/department', teacher), false);
  assert.equal(isPathAllowed('/dashboard/homeroom', teacher), false);
  assert.equal(isPathAllowed('/sch/homeroom-students', teacher), false);
  assert.equal(isPathAllowed('/lrn/assign', teacher), false);
  assert.equal(isPathAllowed('/par/leave-requests', teacher), false);
  const subjectTeacher = user('Teacher', { subjectResponsibilities: [{ classId: 1, subjectId: 2, semesterId: 1 }] });
  assert.equal(isPathAllowed('/lrn/assign', subjectTeacher), true);
  assert.equal(isPathAllowed('/par/leave-requests', subjectTeacher), false);
  const homeroomTeacher = user('Teacher', { homeroomClassIds: [1] });
  assert.equal(isPathAllowed('/par/leave-requests', homeroomTeacher), true);
  assert.equal(isPathAllowed('/sch/homeroom-students', homeroomTeacher), true);
  assert.equal(isPathAllowed('/dashboard/homeroom', homeroomTeacher), true);
  assert.equal(isPathAllowed('/dashboard/department', homeroomTeacher), false);
  const head = user('Teacher', { headDepartmentIds: [2] });
  assert.equal(isPathAllowed('/dashboard/department', head), true);
  assert.equal(isPathAllowed('/sch/homeroom-students', head), false);
  assert.equal(isPathAllowed('/lrn/assign', head), false);
});

test('Data export opens for the Principal, homeroom teachers and campus-leading Vice Principals (ADM-03)', () => {
  assert.equal(isPathAllowed('/adm/exports', user('Principal')), true);
  assert.equal(isPathAllowed('/adm/exports', user('Teacher', { homeroomClassIds: [1] })), true);
  assert.equal(isPathAllowed('/adm/exports', user('VicePrincipal', { ledCampusIds: [1] })), true);
  assert.equal(isPathAllowed('/adm/exports', user('VicePrincipal')), false);
  // Không có mục kết quả bài tập: giáo viên chỉ phụ trách chuyên môn hoặc tổ trưởng không có màn xuất.
  assert.equal(isPathAllowed('/adm/exports', user('Teacher', { subjectResponsibilities: [{ classId: 1, subjectId: 2, semesterId: 1 }], headDepartmentIds: [1] })), false);
  for (const role of ['SchoolAdmin', 'Student', 'Parent']) assert.equal(isPathAllowed('/adm/exports', user(role, { ledCampusIds: [1], homeroomClassIds: [1] })), false, role);
});

test('School report page is gone: no role can open /adm/report (ADM-01)', () => {
  for (const role of ['SchoolAdmin', 'Principal', 'VicePrincipal', 'Teacher', 'Student', 'Parent']) {
    assert.equal(isPathAllowed('/adm/report', user(role, { ledCampusIds: [1], homeroomClassIds: [1] })), false, role);
  }
});

test('Weekly report is its own item right below Bảng tin, for homeroom teachers, students and parents', () => {
  const homeroom = user('Teacher', { homeroomClassIds: [1] });
  for (const u of [homeroom, user('Student'), user('Parent')]) {
    assert.equal(isPathAllowed('/par/weekly-reports', u), true, u.accountType);
    const groups = visibleMenu(u);
    const at = groups.findIndex((g) => g.key === 'par-report');
    assert.equal(groups[at].path, '/par/weekly-reports');
    assert.equal(groups[at].section, 'overview');
    assert.equal(groups[at - 1].key, 'com');
  }
  assert.equal(isPathAllowed('/par/weekly-reports', user('Teacher')), false);
  for (const role of ['SchoolAdmin', 'Principal', 'VicePrincipal']) assert.equal(isPathAllowed('/par/weekly-reports', user(role, { ledCampusIds: [1], homeroomClassIds: [1] })), false, role);
});

test('Student and Parent keep separate action menus', () => {
  const student = user('Student');
  const parent = user('Parent');
  assert.equal(isPathAllowed('/lrn/todo', student), true);
  assert.equal(isPathAllowed('/lrn/todo', parent), false);
  assert.equal(isPathAllowed('/par/leave-requests', parent), true);
  assert.equal(isPathAllowed('/par/leave-requests', student), false);
  assert.equal(isPathAllowed('/par/inbox', student), false);
  assert.ok(paths(parent).includes('/scd/my-class'));
});

test('Every rendered leaf is reachable, hidden URLs stay denied and profile remains common to all six roles', () => {
  for (const role of ['SchoolAdmin', 'Principal', 'VicePrincipal', 'Teacher', 'Student', 'Parent']) {
    const person = user(role);
    for (const path of paths(person)) assert.equal(isPathAllowed(path, person), true, `${role}: ${path}`);
    assert.equal(isPathAllowed('/profile', person), true);
    assert.equal(isPathAllowed('/change-password', person), true);
    assert.equal(isPathAllowed('/unknown', person), false);
    assert.equal(isPathAllowed('/adm/accounts-unrelated', person), false);
    assert.equal(isPathAllowed('/lrn/assign/', person), isPathAllowed('/lrn/assign', person));
  }
});
