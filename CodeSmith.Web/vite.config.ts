import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";
import basicSsl from "@vitejs/plugin-basic-ssl";

// == Vite Configuration == //
export default defineConfig({
  plugins: [react(), tailwindcss(), basicSsl()],
  test: {
    environment: "jsdom",
    globals: true,
    setupFiles: "./src/test/setup.ts",
    include: ["src/**/*.test.{ts,tsx}"],
  },
  server: {
    port: 5173,
    proxy: {
      "/api": {
        target: "https://localhost:7111",
        changeOrigin: true,
        secure: false,
      },
    },
  },
});
