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

test('School Admin sees account management and never sees dashboard, schedule or role-bound actions', () => {
  const person = user('SchoolAdmin');
  assert.ok(paths(person).includes('/adm/accounts'));
  for (const path of ['/dashboard', '/scd/timetable', '/scd/schedule', '/adm/exports', '/lrn/assign', '/lrn/todo', '/par/inbox', '/par/leave-requests']) {
    assert.ok(!paths(person).includes(path), path);
    assert.equal(isPathAllowed(path, person), false, path);
  }
});

test('Principal reads the dashboard and schedule without inheriting account management or timetable writes', () => {
  const person = user('Principal');
  assert.equal(isPathAllowed('/dashboard', person), true);
  assert.equal(isPathAllowed('/scd/schedule', person), true);
  assert.equal(isPathAllowed('/scd/timetable', person), false);
  assert.equal(isPathAllowed('/adm/accounts', person), false);
});

test('Vice Principal navigation follows current campus duty', () => {
  const withoutDuty = user('VicePrincipal');
  const assigned = user('VicePrincipal', { ledCampusIds: [2] });
  for (const path of ['/dashboard', '/scd/timetable', '/scd/responsibilities', '/scd/schedule', '/scd/absences', '/ppl/staff']) {
    assert.equal(isPathAllowed(path, withoutDuty), false, path);
    assert.equal(isPathAllowed(path, assigned), true, path);
  }
  assert.equal(isPathAllowed('/adm/accounts', assigned), false);
});

test('Teacher gains only the navigation attached to each current responsibility', () => {
  const teacher = user('Teacher');
  assert.equal(isPathAllowed('/lrn/questions', teacher), true);
  assert.equal(isPathAllowed('/lrn/assign', teacher), false);
  assert.equal(isPathAllowed('/par/leave-requests', teacher), false);
  assert.equal(isPathAllowed('/lrn/department', teacher), false);
  const subjectTeacher = user('Teacher', { subjectResponsibilities: [{ classId: 1, subjectId: 2, semesterId: 1 }] });
  assert.equal(isPathAllowed('/lrn/assign', subjectTeacher), true);
  assert.equal(isPathAllowed('/par/leave-requests', subjectTeacher), false);
  const homeroomTeacher = user('Teacher', { homeroomClassIds: [1] });
  assert.equal(isPathAllowed('/par/leave-requests', homeroomTeacher), true);
  assert.equal(isPathAllowed('/lrn/department', homeroomTeacher), false);
  const head = user('Teacher', { headDepartmentIds: [2] });
  assert.equal(isPathAllowed('/lrn/department', head), true);
  assert.equal(isPathAllowed('/lrn/assign', head), false);
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
