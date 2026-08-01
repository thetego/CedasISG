import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";
import { fileURLToPath, URL } from "node:url";

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: { "@": fileURLToPath(new URL("./src", import.meta.url)) },
  },
  build: {
    rolldownOptions: {
      output: {
        codeSplitting: {
          groups: [
            {
              name: "charts",
              test: (id) => id.includes("/node_modules/recharts/") || id.includes("/node_modules/d3-"),
              priority: 30,
            },
            {
              name: "react",
              test: (id) => id.includes("/node_modules/react/") || id.includes("/node_modules/react-dom/"),
              priority: 20,
            },
            {
              name: "ui",
              test: (id) => id.includes("/node_modules/@radix-ui/") || id.includes("/node_modules/lucide-react/"),
              priority: 15,
            },
            { name: "vendor", test: /node_modules/, maxSize: 250_000, priority: 5 },
          ],
        },
      },
    },
  },
  server: {
    proxy: {
      "/api": "http://127.0.0.1:4173",
      "/health": "http://127.0.0.1:4173",
    },
  },
});
