"use client";

import React, { createContext, useContext, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { adminApi, getToken, removeToken } from "./api";
import { StaffUser } from "../types/auth";

interface AuthContextType {
  user: StaffUser | null;
  token: string | null;
  isLoading: boolean;
  login: (usernameOrEmail: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  refreshUser: () => Promise<void>;
  isSuperAdmin: boolean;
  isBranchAdmin: boolean;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<StaffUser | null>(null);
  const [token, setTokenState] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const router = useRouter();

  const loadUser = async () => {
    const savedToken = getToken();
    if (!savedToken) {
      setUser(null);
      setTokenState(null);
      setIsLoading(false);
      return;
    }

    setTokenState(savedToken);
    try {
      const staffUser = await adminApi.getMe();
      setUser(staffUser);
    } catch {
      removeToken();
      setUser(null);
      setTokenState(null);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadUser();

    const handleUnauthorized = () => {
      setUser(null);
      setTokenState(null);
      router.push("/login?expired=true");
    };

    window.addEventListener("sjewls:unauthorized", handleUnauthorized);
    return () => {
      window.removeEventListener("sjewls:unauthorized", handleUnauthorized);
    };
  }, []);

  const login = async (usernameOrEmail: string, password: string) => {
    const response = await adminApi.login(usernameOrEmail, password);
    setTokenState(response.accessToken);
    setUser(response.user);
  };

  const logout = async () => {
    try {
      await adminApi.logout();
    } catch {
      // Ignored - ensure client-side state is cleared
    } finally {
      removeToken();
      setUser(null);
      setTokenState(null);
      router.push("/login");
    }
  };

  const refreshUser = async () => {
    try {
      const staffUser = await adminApi.getMe();
      setUser(staffUser);
    } catch (err) {
      console.error("Failed to refresh user profile", err);
    }
  };

  const isSuperAdmin = Boolean(
    user?.roles?.some((r) => r.toLowerCase() === "super admin")
  );

  const isBranchAdmin = Boolean(
    user?.roles?.some((r) => r.toLowerCase() === "branch admin")
  );

  return (
    <AuthContext.Provider
      value={{
        user,
        token,
        isLoading,
        login,
        logout,
        refreshUser,
        isSuperAdmin,
        isBranchAdmin,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthContextType {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used within an AuthProvider");
  }
  return context;
}
