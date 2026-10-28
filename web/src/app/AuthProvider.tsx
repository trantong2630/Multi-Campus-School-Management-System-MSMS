import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { iamApi } from '@/features/iam/api';
import type { UserInfo } from '@/features/iam/types';
import { AUTH_LOST_EVENT, endMatchingSession, refreshSession, tokenStore } from './apiClient';
import { clearSessionQueries, subscribeSessionEnd } from './authSync';

interface AuthContextValue {
  user: UserInfo | null;
  loading: boolean;
  login: (username: string, password: string) => Promise<UserInfo>;
  logout: () => Promise<void>;
  loggingOut: boolean;
  refresh: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserInfo | null>(null);
  const [loading, setLoading] = useState(true);
  const [loggingOut, setLoggingOut] = useState(false);
  const logoutPending = useRef<Promise<void> | null>(null);
  const queryClient = useQueryClient();

  useEffect(() => {
    // Mở tab mới hoặc F5: access token trong bộ nhớ đã mất, lấy lại bằng cookie phiên nếu còn.
    let active = true;
    const expectedGeneration = tokenStore.generation();
    refreshSession()
      .then((s) => { if (active && tokenStore.isCurrent(expectedGeneration)) setUser(s.user); })
      .catch(() => { if (active && tokenStore.isCurrent(expectedGeneration)) tokenStore.clear(); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, []);

  useEffect(() => {
    const onLost = () => {
      setUser(null);
      setLoading(false);
      setLoggingOut(false);
      logoutPending.current = null;
      clearSessionQueries(queryClient);
    };
    window.addEventListener(AUTH_LOST_EVENT, onLost);
    const unsubscribe = subscribeSessionEnd(endMatchingSession);
    return () => {
      window.removeEventListener(AUTH_LOST_EVENT, onLost);
      unsubscribe();
    };
  }, [queryClient]);

  const login = useCallback(async (username: string, password: string) => {
    const pending = iamApi.login(username, password);
    const expectedGeneration = tokenStore.generation();
    const res = await pending;
    if (!tokenStore.isCurrent(expectedGeneration)) throw new Error('Phiên đăng nhập đã thay đổi.');
    setUser(res.user);
    setLoading(false);
    setLoggingOut(false);
    logoutPending.current = null;
    return res.user;
  }, []);

  const logout = useCallback(() => {
    const pending = iamApi.logout();
    logoutPending.current = pending;
    setLoggingOut(true);
    return pending.finally(() => {
      if (logoutPending.current === pending) {
        logoutPending.current = null;
        setLoggingOut(false);
      }
    });
  }, []);

  const refresh = useCallback(async () => {
    const expectedGeneration = tokenStore.generation();
    const info = await iamApi.me();
    if (tokenStore.isCurrent(expectedGeneration)) setUser(info);
  }, []);

  const value = useMemo(() => ({ user, loading, loggingOut, login, logout, refresh }), [user, loading, loggingOut, login, logout, refresh]);
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth phải nằm trong AuthProvider');
  return ctx;
}
