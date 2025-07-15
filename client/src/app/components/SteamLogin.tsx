"use client";

import { useState, useEffect } from "react";
import Image from "next/image";

interface User {
  Id: number;
  SteamId: string;
  CreatedAt: string;
  AvatarUrl: string;
  ProfileUrl: string;
  Username: string;
}

interface SteamLoginProps {
  onLogin?: (user: User) => void;
  onError?: (error: string) => void;
}

export default function SteamLogin({ onLogin, onError }: SteamLoginProps) {
  const [isLoading, setIsLoading] = useState(false);
  const [user, setUser] = useState<User | null>(null);

  const handleSteamLogin = async () => {
    setIsLoading(true);

    try {
      // Replace with your actual Amplify API endpoint
      const apiEndpoint =
        process.env.NEXT_PUBLIC_API_URL ||
        "https://your-api-gateway-url.amazonaws.com/dev";

      // Redirect to Steam login
      window.location.href = `${apiEndpoint}/auth/steam/login?returnUrl=${encodeURIComponent(
        window.location.origin + "/auth/callback"
      )}`;
    } catch (error) {
      setIsLoading(false);
      const errorMessage =
        error instanceof Error ? error.message : "Login failed";
      onError?.(errorMessage);
    }
  };

  const handleLogout = () => {
    setUser(null);
    // Clear any stored authentication data
    localStorage.removeItem("steam_user");
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

  if (user) {
    return (
      <div className="flex items-center gap-4 p-4 bg-gray-100 dark:bg-gray-800 rounded-lg">
        <Image
          src={user.AvatarUrl}
          alt={`${user.Username}'s avatar`}
          width={48}
          height={48}
          className="rounded-full"
        />
        <div className="flex-1">
          <p className="font-medium text-gray-900 dark:text-gray-100">
            {user.Username}
          </p>
          <p className="text-sm text-gray-500 dark:text-gray-400">
            Logged in via Steam
          </p>
        </div>
        <button
          onClick={handleLogout}
          className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-md hover:bg-gray-50 dark:bg-gray-700 dark:text-gray-200 dark:border-gray-600 dark:hover:bg-gray-600"
        >
          Logout
        </button>
      </div>
    );
  }

  return (
    <button
      onClick={handleSteamLogin}
      disabled={isLoading}
      className="flex items-center gap-3 px-6 py-3 font-medium text-white bg-black rounded-lg hover:bg-gray-800 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
    >
      {isLoading ? (
        <div className="w-5 h-5 border-2 border-white border-t-transparent rounded-full animate-spin" />
      ) : (
        <svg
          width="20"
          height="20"
          viewBox="0 0 24 24"
          fill="currentColor"
          className="text-white"
        >
          <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10c1.19 0 2.34-.21 3.41-.6.3-.11.49-.4.49-.72v-1.77c0-.83-.94-1.31-1.63-.84-.5.34-1.1.54-1.73.54-1.66 0-3-1.34-3-3s1.34-3 3-3c.63 0 1.23.2 1.73.54.69.47 1.63-.01 1.63-.84V4.32c0-.32-.19-.61-.49-.72C14.34 2.21 13.19 2 12 2z" />
        </svg>
      )}
      {isLoading ? "Connecting..." : "Login with Steam"}
    </button>
  );
}
