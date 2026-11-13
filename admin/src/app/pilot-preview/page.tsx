"use client";

import React, { useState } from "react";
import { AdminLayout } from "../../components/AdminLayout";
import { SummaryCard } from "../../components/dashboard/SummaryCard";
import { ChituPlanCard } from "../../components/chitu/ChituPlanCard";
import { JewelleryPlanCard } from "../../components/jewellery-plans/JewelleryPlanCard";
import { StaffUser } from "../../types/auth";
import { ChituPlanCardDto, ChituRedemptionType } from "../../types/chitu";
import { JewelleryPlan } from "../../types/jewelleryPlan";
import { Card, CardHeader, CardTitle, CardContent } from "../../components/ui/card";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import {
  UserCheck,
  Layers,
  TrendingUp,
  Users,
  Coins,
  Gem,
  CheckCircle2,
  Sparkles,
  Smartphone,
  Monitor,
  Eye,
} from "lucide-react";

// Mock Preview Staff User
const previewStaffUser: StaffUser = {
  id: "staff-001",
  fullName: "Nadeesha Perera",
  email: "nadeesha.p@sjewls.lk",
  username: "nadeesha.p",
  phoneNumber: "+94 77 123 4567",
  isActive: true,
  roles: ["Super Admin", "Operations Manager"],
  assignedBranches: [
    {
      branchId: "branch-colombo-01",
      name: "Colombo Flagship",
      code: "CMB-01",
      city: "Colombo 03",
    },
  ],
  createdAtUtc: "2026-01-01T00:00:00Z",
  updatedAtUtc: null,
};

// 1. Chitu Plan Mock Records
const standardChituPlan: ChituPlanCardDto = {
  id: "chitu-standard-01",
  code: "CHITU-50K-G",
  name: "SJewls Sovereign Gold 50K Savings",
  branchId: "branch-colombo-01",
  branchName: "Colombo Flagship (CMB-01)",
  status: "Active",
  statusLabel: "Active",
  redemptionType: ChituRedemptionType.Gold,
  redemptionTypeLabel: "Gold",
  monthlyInstalmentAmount: 5000,
  chituBenefitValue: 50000,
  durationMonths: 10,
  totalSlotCapacity: 50,
  assignedSlotsCount: 42,
  availableSlotsCount: 8,
  currentRoundIndex: 4,
  currentRoundDueDate: "2026-11-25",
  currentRoundPaidSlotsCount: 34,
  currentRoundUnpaidSlotsCount: 8,
  winningSlotsCount: 3,
  closedSlotsCount: 0,
  hasOverdueRaffle: false,
  overdueRaffleRoundIndex: null,
  startDate: "2026-08-01",
  endDate: "2027-05-31",
  isUpcoming: false,
  description: "Exclusive gold accumulation chitu scheme",
  createdAtUtc: "2026-08-01T00:00:00Z",
};

const overdueRaffleChituPlan: ChituPlanCardDto = {
  id: "chitu-overdue-02",
  code: "CHITU-100K-C",
  name: "Royal Heritage 100K Cash Accumulator",
  branchId: "branch-colombo-01",
  branchName: "Colombo Flagship (CMB-01)",
  status: "Active",
  statusLabel: "Active",
  redemptionType: ChituRedemptionType.Cash,
  redemptionTypeLabel: "Cash",
  monthlyInstalmentAmount: 10000,
  chituBenefitValue: 100000,
  durationMonths: 10,
  totalSlotCapacity: 40,
  assignedSlotsCount: 40,
  availableSlotsCount: 0,
  currentRoundIndex: 3,
  currentRoundDueDate: "2026-11-10",
  currentRoundPaidSlotsCount: 38,
  currentRoundUnpaidSlotsCount: 2,
  winningSlotsCount: 2,
  closedSlotsCount: 0,
  hasOverdueRaffle: true,
  overdueRaffleRoundIndex: 3,
  startDate: "2026-09-01",
  endDate: "2027-06-30",
  isUpcoming: false,
  description: "High-yield cash savings plan",
  createdAtUtc: "2026-09-01T00:00:00Z",
};

// 2. Jewellery Plan Mock Records
const standardJewelleryPlan: JewelleryPlan = {
  id: "jewel-plan-01",
  branchId: "branch-colombo-01",
  branchName: "Colombo Flagship",
  branchCode: "CMB-01",
  currency: "LKR",
  code: "SJW-22K-NCK",
  name: "Royal Heritage 22K Gold Bridal Necklace",
  description: "Exquisite handcrafted 22 karat gold necklace with traditional filigree work and certified gemstone embellishments.",
  imageUrl: "https://images.unsplash.com/photo-1599643478518-a784e5dc4c8f?auto=format&fit=crop&w=800&q=80",
  categoryId: "cat-necklaces",
  categoryName: "Necklaces & Pendants",
  targetProductGoldWeightGrams: 24.5,
  karat: 22,
  premiumPercentage: 8,
  allowedDurationsMonths: [6, 12, 18],
  startDate: "2026-06-01",
  isActive: true,
  displayStatus: "Active",
  totalEnrolments: 28,
  activeEnrolments: 24,
  totalNetAccumulatedGrams: 342.8,
  totalTargetGramsAcrossEnrolments: 686.0,
  overallProgressPercentage: 58,
  createdAtUtc: "2026-01-15T00:00:00Z",
  updatedAtUtc: null,
  deactivatedAtUtc: null,
  reopenedAtUtc: null,
};

const fallbackImageJewelleryPlan: JewelleryPlan = {
  id: "jewel-plan-02",
  branchId: "branch-colombo-01",
  branchName: "Kandy Branch",
  branchCode: "KDY-01",
  currency: "LKR",
  code: "SJW-22K-BNG",
  name: "Temple Filigree 22K Traditional Bangle Set (Missing Image Test)",
  description: "Handcrafted traditional temple motifs set of two bangles.",
  imageUrl: "", // Intentionally empty to test luxury fallback
  categoryId: "cat-bangles",
  categoryName: "Bangles & Bracelets",
  targetProductGoldWeightGrams: 32.0,
  karat: 22,
  premiumPercentage: 10,
  allowedDurationsMonths: [12, 24],
  startDate: "2026-09-01",
  isActive: true,
  displayStatus: "Active",
  totalEnrolments: 14,
  activeEnrolments: 12,
  totalNetAccumulatedGrams: 160.0,
  totalTargetGramsAcrossEnrolments: 448.0,
  overallProgressPercentage: 35,
  createdAtUtc: "2026-02-01T00:00:00Z",
  updatedAtUtc: null,
  deactivatedAtUtc: null,
  reopenedAtUtc: null,
};

export default function PilotPreviewPage() {
  const [activeTab, setActiveTab] = useState<"all" | "cards" | "states">("all");
  const [simulateError, setSimulateError] = useState(false);
  const [simulateLoading, setSimulateLoading] = useState(false);

  return (
    <AdminLayout title="shadcn/ui Design Pilot Preview" previewUser={previewStaffUser}>
      <div className="space-y-8 max-w-7xl mx-auto pb-16">
        {/* Pilot Overview Header Banner */}
        <div className="p-6 sm:p-7 rounded-2xl bg-gradient-to-r from-[#0B3D0B] via-[#0E4A0E] to-[#0B3D0B] text-white shadow-xs border border-emerald-900/60 relative overflow-hidden">
          <div className="absolute top-0 right-0 w-80 h-80 bg-[#FFD700]/10 rounded-full blur-3xl pointer-events-none" />

          <div className="relative z-10 flex flex-col md:flex-row md:items-center justify-between gap-4">
            <div className="space-y-1.5">
              <div className="flex items-center gap-2">
                <Badge variant="gold" size="sm">
                  Design Pilot
                </Badge>
                <span className="text-xs text-emerald-200 flex items-center gap-1">
                  <Sparkles className="w-3.5 h-3.5 text-[#FFD700]" />
                  shadcn/ui + Tailwind v4 + SJewls Brand System
                </span>
              </div>
              <h2 className="text-2xl sm:text-3xl font-serif-luxury font-bold tracking-tight">
                Modernized Operations Interface
              </h2>
              <p className="text-xs sm:text-sm text-emerald-100/80 max-w-2xl">
                Verified pilot components adopting deep green (#0B3D0B), warm off-white (#FAF9F6), and gold (#FFD700) with accessible status indicators, fine borders, and restrained shadows.
              </p>
            </div>

            <div className="flex items-center gap-2">
              <Button
                variant={simulateLoading ? "gold" : "outline"}
                size="sm"
                onClick={() => setSimulateLoading(!simulateLoading)}
                className="bg-white/10 text-white border-white/20 hover:bg-white/20"
              >
                {simulateLoading ? "Normal State" : "Simulate Loading"}
              </Button>
              <Button
                variant={simulateError ? "destructive" : "outline"}
                size="sm"
                onClick={() => setSimulateError(!simulateError)}
                className="bg-white/10 text-white border-white/20 hover:bg-white/20"
              >
                {simulateError ? "Clear Error" : "Simulate Error"}
              </Button>
            </div>
          </div>
        </div>

        {/* ================= SECTION 1: DASHBOARD SUMMARY CARDS ================= */}
        <section className="space-y-3">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <div className="p-1.5 rounded-lg bg-emerald-100 text-[#0B3D0B]">
                <Sparkles className="w-4 h-4" />
              </div>
              <div>
                <h3 className="font-serif-luxury font-bold text-stone-900 text-lg">
                  1. Dashboard Summary Cards
                </h3>
                <p className="text-xs text-stone-500">
                  Focal point figure with short label, small icon, and compact footprint
                </p>
              </div>
            </div>
            <Badge variant="stone" size="sm">
              4 Variants + States
            </Badge>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            <SummaryCard
              label="Total Customers"
              value={142}
              icon={UserCheck}
              href="/customers"
              linkText="Open Directory"
              variant="default"
              isLoading={simulateLoading}
              error={simulateError ? "Failed to load metrics from API" : null}
              onRetry={() => setSimulateError(false)}
            />

            <SummaryCard
              label="Total Slots"
              value={380}
              icon={Layers}
              subtext="Chitu & Jewellery plan slots"
              variant="gold"
              isLoading={simulateLoading}
            />

            <SummaryCard
              label="Active Investment"
              value="LKR 14.8M"
              icon={TrendingUp}
              subtext="Currently contributing"
              variant="emerald"
              isLoading={simulateLoading}
            />

            <SummaryCard
              label="Platform Staff"
              value={18}
              icon={Users}
              subtext="Assigned across 4 branches"
              variant="blue"
              isLoading={simulateLoading}
            />
          </div>
        </section>

        {/* ================= SECTION 2: CHITU PLAN CARDS ================= */}
        <section className="space-y-3 pt-4 border-t border-stone-200">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <div className="p-1.5 rounded-lg bg-amber-100 text-amber-900">
                <Coins className="w-4 h-4" />
              </div>
              <div>
                <h3 className="font-serif-luxury font-bold text-stone-900 text-lg">
                  2. Chitu Plan Cards
                </h3>
                <p className="text-xs text-stone-500">
                  Lead with plan name and value, side-by-side paid/unpaid slot counts, and prominent View Plan action
                </p>
              </div>
            </div>
            <Badge variant="stone" size="sm">
              Standard & Overdue Raffle
            </Badge>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {/* Standard Plan */}
            <ChituPlanCard
              plan={standardChituPlan}
              onEdit={() => alert(`Edit ${standardChituPlan.name}`)}
              onAssign={() => alert(`Assign slots for ${standardChituPlan.name}`)}
              onPayment={() => alert(`Make payment for ${standardChituPlan.name}`)}
              onRaffle={() => alert(`Record raffle for ${standardChituPlan.name}`)}
            />

            {/* Overdue Raffle Plan */}
            <ChituPlanCard
              plan={overdueRaffleChituPlan}
              onEdit={() => alert(`Edit ${overdueRaffleChituPlan.name}`)}
              onAssign={() => alert(`Assign slots for ${overdueRaffleChituPlan.name}`)}
              onPayment={() => alert(`Make payment for ${overdueRaffleChituPlan.name}`)}
              onRaffle={() => alert(`Record draw for ${overdueRaffleChituPlan.name}`)}
            />

            {/* Empty State / New Plan Preview Placeholder */}
            <Card className="border-dashed border-stone-300 bg-stone-50/50 flex flex-col items-center justify-center p-8 text-center space-y-3">
              <div className="w-12 h-12 rounded-2xl bg-stone-100 text-stone-400 flex items-center justify-center">
                <Coins className="w-6 h-6" />
              </div>
              <h4 className="font-bold text-stone-800 text-sm">
                Add New Chitu Plan
              </h4>
              <p className="text-xs text-stone-500 max-w-xs">
                Configure monthly instalment amounts, total slot capacity, and redemption options.
              </p>
              <Button variant="default" size="sm">
                Launch Wizard
              </Button>
            </Card>
          </div>
        </section>

        {/* ================= SECTION 3: JEWELLERY PLAN CARDS ================= */}
        <section className="space-y-3 pt-4 border-t border-stone-200">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <div className="p-1.5 rounded-lg bg-emerald-100 text-[#0B3D0B]">
                <Gem className="w-4 h-4" />
              </div>
              <div>
                <h3 className="font-serif-luxury font-bold text-stone-900 text-lg">
                  3. Jewellery Plan Cards
                </h3>
                <p className="text-xs text-stone-500">
                  Generous product image, gold progress tracking, target grams, offered durations, and customer actions
                </p>
              </div>
            </div>
            <Badge variant="stone" size="sm">
              Image & Fallback Mode
            </Badge>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {/* Standard Plan with Image */}
            <JewelleryPlanCard
              plan={standardJewelleryPlan}
              onViewCustomers={() => alert(`Viewing customers for ${standardJewelleryPlan.name}`)}
              onEdit={() => alert(`Editing ${standardJewelleryPlan.name}`)}
              onDeactivate={() => alert(`Deactivating ${standardJewelleryPlan.name}`)}
              onReopen={() => alert(`Reopening ${standardJewelleryPlan.name}`)}
            />

            {/* Plan with Missing Image Fallback */}
            <JewelleryPlanCard
              plan={fallbackImageJewelleryPlan}
              onViewCustomers={() => alert(`Viewing customers for ${fallbackImageJewelleryPlan.name}`)}
              onEdit={() => alert(`Editing ${fallbackImageJewelleryPlan.name}`)}
              onDeactivate={() => alert(`Deactivating ${fallbackImageJewelleryPlan.name}`)}
              onReopen={() => alert(`Reopening ${fallbackImageJewelleryPlan.name}`)}
            />

            {/* Empty State / Add Plan */}
            <Card className="border-dashed border-stone-300 bg-stone-50/50 flex flex-col items-center justify-center p-8 text-center space-y-3">
              <div className="w-12 h-12 rounded-2xl bg-stone-100 text-stone-400 flex items-center justify-center">
                <Gem className="w-6 h-6" />
              </div>
              <h4 className="font-bold text-stone-800 text-sm">
                Create Jewellery Plan
              </h4>
              <p className="text-xs text-stone-500 max-w-xs">
                Link fine jewellery categories, set target gold weights in grams, and select duration options.
              </p>
              <Button variant="default" size="sm">
                Create Plan
              </Button>
            </Card>
          </div>
        </section>
      </div>
    </AdminLayout>
  );
}
