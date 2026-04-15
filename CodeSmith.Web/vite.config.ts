import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";
import fs from "node:fs";
import path from "node:path";

// == Vite Configuration == //
// Dev server serves HTTPS using locally-trusted mkcert certificates. Run
// `mkcert -install` once per machine, then generate certs into ./certs/
// (see README). Certs are gitignored.
const certDir = path.resolve(__dirname, "certs");
const keyPath = path.join(certDir, "localhost-key.pem");
const certPath = path.join(certDir, "localhost.pem");
const httpsConfig = fs.existsSync(keyPath) && fs.existsSync(certPath)
  ? { key: fs.readFileSync(keyPath), cert: fs.readFileSync(certPath) }
  : undefined;

export default defineConfig({
  plugins: [react(), tailwindcss()],
  test: {
    environment: "jsdom",
    globals: true,
    setupFiles: "./src/test/setup.ts",
    include: ["src/**/*.test.{ts,tsx}"],
  },
  server: {
    port: 5173,
    open: true,
    https: httpsConfig,
    proxy: {
      "/api": {
        target: "https://localhost:7111",
        changeOrigin: true,
        secure: false,
      },
    },
  },
});
