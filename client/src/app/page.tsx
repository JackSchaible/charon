"use client";

import Image from "next/image";
import SteamLogin from "./components/SteamLogin";

export default function Home() {
  return (
    <div className="font-sans grid grid-rows-[20px_1fr_20px] items-center justify-items-center min-h-screen p-8 pb-20 gap-16 sm:p-20 cursor-default select-none">
      <main className="flex flex-col gap-[32px] row-start-2 items-center sm:items-start max-w-1/2">
        <section className="text-center sm:text-left">
          <h1 className="text-9xl">Steam Wishlist Manager.</h1>
          <p className="mt-2">
            This app allows you to tag and add descriptions to your wishlist
            items (within the app only) so you can more easily organize and
            remember why the heck you have that weird indie title on your
            wishlist in the first place?
          </p>
        </section>

        <section className="text-center sm:text-left w-full">
          <p className="mb-4">
            To start off, you need to login with Steam, so we can grab your
            wishlist.
          </p>

          <div className="flex items-center justify-center w-full">
            <SteamLogin
              onLogin={(user) => {
                console.log("User logged in:", user);
              }}
              onError={(error) => {
                console.error("Login error:", error);
              }}
            />
          </div>
        </section>
      </main>
      <footer className="row-start-3 flex gap-[24px] flex-wrap items-center justify-center">
        <p>&copy; {new Date().getFullYear()} Cactus Studios</p>
      </footer>
    </div>
  );
}
