"use client";

import { useState, useEffect } from "react";
import { User } from "../types";

interface SteamLoginProps {
  onLogin?: (user: User) => void;
  onError?: (error: string) => void;
}

export default function SteamLogin({ onLogin, onError }: SteamLoginProps) {
  const [isLoading, setIsLoading] = useState(false);
  const [user, setUser] = useState<User | null>(null);

  const handleSteamLogin = async () => {
    try {
      const apiEndpoint = process.env.NEXT_PUBLIC_API_URL;
      if (apiEndpoint == null)
        throw new Error("NEXT_PUBLIC_API_URL is not defined");

      setIsLoading(true);

      window.location.href = `${apiEndpoint}/auth/login?returnUrl=${encodeURIComponent(
        window.location.origin + "/auth/callback"
      )}`;
    } catch (error) {
      setIsLoading(false);
      const errorMessage =
        error instanceof Error ? error.message : "Login failed";
      onError?.(errorMessage);
    }
  };

  // Check for user data in localStorage on component mount
  useEffect(() => {
    const storedUser = localStorage.getItem("steam_user");
    if (storedUser) {
      try {
        const userData = JSON.parse(storedUser);
        setUser(userData);
        onLogin?.(userData);
      } catch (error) {
        console.error("Error parsing stored user data:", error);
        localStorage.removeItem("steam_user");
      }
    }
  }, [onLogin]);

  return (
    <>
      {isLoading ? (
        <button
          disabled
          className="flex items-center gap-3 px-6 py-3 font-medium text-white bg-black rounded-lg hover:bg-gray-800 opacity-50 cursor-not-allowed transition-colors"
        >
          <div className="w-5 h-5 border-2 border-white border-t-transparent rounded-full animate-spin" />
        </button>
      ) : (
        <button
          onClick={handleSteamLogin}
          className="hover:opacity-75 cursor-pointer"
        >
          <img src="/img/sits_01.png" alt="Sign In with Steam Badge" />
        </button>
      )}
    </>
  );
}
