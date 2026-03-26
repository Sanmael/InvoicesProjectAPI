-- Migration: AddCategoryToDebtAndCardPurchase
-- Execute in Supabase SQL Editor

-- Add Category column to Debts
ALTER TABLE "Debts"
ADD COLUMN "Category" VARCHAR(50) NOT NULL DEFAULT 'Outros';

-- Add Category column to CardPurchases
ALTER TABLE "CardPurchases"
ADD COLUMN "Category" VARCHAR(50) NOT NULL DEFAULT 'Outros';
