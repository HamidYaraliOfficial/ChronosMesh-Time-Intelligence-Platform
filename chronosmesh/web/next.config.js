/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  eslint: { ignoreDuringBuilds: false },
  env: {
    NEXT_PUBLIC_API_URL: process.env.NEXT_PUBLIC_API_URL || 'https://localhost:5443',
    NEXT_PUBLIC_SCHEDULER_WS_URL: process.env.NEXT_PUBLIC_SCHEDULER_WS_URL || 'wss://localhost:8443/v1/ws',
  },
};

module.exports = nextConfig;
