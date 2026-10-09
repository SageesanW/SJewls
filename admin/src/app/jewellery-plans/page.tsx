"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";

export default function JewelleryPlansIndexPage() {
  const router = useRouter();

  useEffect(() => {
    router.replace("/jewellery-plans/categories");
  }, [router]);

  return (
    <div className="min-h-screen flex items-center justify-center bg-[#FAF9F6]">
      <div className="w-8 h-8 border-3 border-[#0B3D0B] border-t-[#FFD700] rounded-full animate-spin" />
    </div>
  );
}
