import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  /* config options here */
  cacheComponents: true,
  partialPrefetching: true,
  turbopack: {
    root: process.cwd(),
    rules: {
      "*.css": {
        loaders: ["@tailwindcss/turbopack"],
        as: "*.css",
      },
    },
  },
  async redirects() {
    return [
      {
        source: "/jewelry-plan",
        destination: "/jewellery-plans/categories",
        permanent: false,
      },
      {
        source: "/jewelry-plans",
        destination: "/jewellery-plans/categories",
        permanent: false,
      },
      {
        source: "/jewellery-plan",
        destination: "/jewellery-plans/categories",
        permanent: false,
      },
      {
        source: "/jewelry-plan/categories",
        destination: "/jewellery-plans/categories",
        permanent: false,
      },
      {
        source: "/jewelry-plans/categories",
        destination: "/jewellery-plans/categories",
        permanent: false,
      },
      {
        source: "/jewellery-plan/categories",
        destination: "/jewellery-plans/categories",
        permanent: false,
      },
      {
        source: "/categories",
        destination: "/jewellery-plans/categories",
        permanent: false,
      },
    ];
  },
  async rewrites() {
    const backendUrl =
      process.env.BACKEND_INTERNAL_URL ||
      process.env.NEXT_PUBLIC_API_URL ||
      "http://localhost:5230";

    return [
      {
        source: "/api/:path*",
        destination: `${backendUrl.replace(/\/+$/, "")}/api/:path*`,
      },
    ];
  },
};

export default nextConfig;
