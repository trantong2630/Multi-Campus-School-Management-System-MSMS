import type { AccountType } from '@/shared/lib/labels';

export interface UserInfo {
  accountId: number;
  personId: number;
  personCode: string;
  fullName: string;
  accountType: AccountType;
  status: string;
  mustChangePassword: boolean;
  avatarPath: string | null;
}

export interface LoginResponse {
  token: string;
  expiresAt: string;
  user: UserInfo;
}

export interface LockImpact {
  sessionCount: number;
  campuses: { campus: string; count: number }[];
  duties: { kind: string; targetName: string }[];
}

export interface Account {
  accountId: number;
  personId: number;
  personCode: string;
  fullName: string;
  personType: string;
  username: string;
  accountType: AccountType;
  status: string;
  lastLoginAt: string | null;
  campusName: string | null;
}

export interface TempPasswordItem {
  personId: number;
  personCode: string;
  fullName: string;
  username: string;
  accountType: string;
  tempPassword: string;
}

export interface BulkCreateResult {
  created: TempPasswordItem[];
  skipped: { personId: number; fullName: string; reason: string }[];
}

export interface ResetPasswordResult {
  username: string;
  tempPassword: string;
}
