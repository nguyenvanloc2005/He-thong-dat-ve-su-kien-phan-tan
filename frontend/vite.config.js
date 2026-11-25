import { defineConfig } from "vite";

export default defineConfig({
  server: {
    host: "127.0.0.1",
    port: 5173,
    proxy: {
      "/events": "http://127.0.0.1:5080",
      "/bookings": "http://127.0.0.1:5080",
      "/users": "http://127.0.0.1:5080",
      "/health": "http://127.0.0.1:5080",
      "/swagger": "http://127.0.0.1:5080"
    }
  }
});
