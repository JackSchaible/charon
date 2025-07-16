"use client";

import { useEffect, useState } from "react";
import { useSearchParams, useRouter } from "next/navigation";

interface User {
  Id: number;
  SteamId: string;
  CreatedAt: string;
  AvatarUrl: string;
  ProfileUrl: string;
  Username: string;
}

export default function AuthCallback() {
  const [status, setStatus] = useState<"loading" | "success" | "error">(
    "loading"
  );
  const [error, setError] = useState<string>("");
  const searchParams = useSearchParams();
  const router = useRouter();

  useEffect(() => {
    const handleCallback = async () => {
      try {
        // Check if we got user data directly from URL params (from our backend redirect)
        const userParam = searchParams.get("user");
        const errorParam = searchParams.get("error");

        if (errorParam) {
          throw new Error(decodeURIComponent(errorParam));
        }

        if (userParam) {
          // Parse user data from URL parameter
          const userData = JSON.parse(decodeURIComponent(userParam));

          // Store user data in localStorage
          localStorage.setItem("steam_user", JSON.stringify(userData));
          setStatus("success");

          // Redirect to home page after a short delay
          setTimeout(() => {
            router.push("/");
          }, 2000);
        } else {
          throw new Error("No user data received from authentication");
        }
      } catch (error) {
        console.error("Authentication error:", error);
        setError(
          error instanceof Error ? error.message : "Authentication failed"
        );
        setStatus("error");
      }
    };

    handleCallback();
  }, [router, searchParams]);

  if (status === "loading") {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="text-center">
          <div className="w-16 h-16 border-4 border-blue-500 border-t-transparent rounded-full animate-spin mx-auto mb-4"></div>
          <h2 className="text-xl font-semibold mb-2">
            Authenticating with Steam...
          </h2>
          <p className="text-gray-600">
            Please wait while we verify your Steam account.
          </p>
        </div>
      </div>
    );
  }

  if (status === "success") {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="text-center">
          <div className="w-16 h-16 bg-green-500 rounded-full flex items-center justify-center mx-auto mb-4">
            <svg
              className="w-8 h-8 text-white"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M5 13l4 4L19 7"
              />
            </svg>
          </div>
          <h2 className="text-xl font-semibold mb-2">
            Authentication Successful!
          </h2>
          <p className="text-gray-600">
            Redirecting you back to the application...
          </p>
        </div>
      </div>
    );
  }

  if (status === "error") {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="text-center">
          <div className="w-16 h-16 bg-red-500 rounded-full flex items-center justify-center mx-auto mb-4">
            <svg
              className="w-8 h-8 text-white"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M6 18L18 6M6 6l12 12"
              />
            </svg>
          </div>
          <h2 className="text-xl font-semibold mb-2">Authentication Failed</h2>
          <p className="text-gray-600 mb-4">{error}</p>
          <button
            onClick={() => router.push("/")}
            className="px-4 py-2 bg-blue-500 text-white rounded hover:bg-blue-600"
          >
            Return Home
          </button>
        </div>
      </div>
    );
  }

  return null;
}
