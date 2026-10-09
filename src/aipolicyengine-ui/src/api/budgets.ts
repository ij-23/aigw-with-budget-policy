import { authFetch, parseErrorMessage } from "../api"

export interface BudgetPolicy { id: string; name: string; limitUsd: number; period: "Monthly" | "Weekly"; enabled: boolean }
export interface BudgetAssignment { policyId: string; subjectType: "User" | "Group" | "Application"; subjectId: string; groupMode: "PerMember" | "Shared" }
export interface BudgetModelRate { deploymentId: string; version: string; inputUsdPerMillion: number; outputUsdPerMillion: number; cachedInputUsdPerMillion: number; maxInputTokens: number; maxOutputTokens: number }
export interface BudgetConfiguration { revision: number; policies: BudgetPolicy[]; assignments: BudgetAssignment[]; models: BudgetModelRate[] }
export interface BudgetBalance { id: string; policyId: string; subject: string; periodStart: string; resetsAt: string; limitUsd: number; spentUsd: number; reservedUsd: number; remainingUsd: number }
export interface BudgetReservation { id: string; userId: string | null; clientAppId: string; deploymentId: string; reservedUsd: number; actualUsd: number | null; createdAt: string; settledAt: string | null; overrun: boolean }

export async function budgetRequest<T>(tenant: string, route: string, body?: unknown): Promise<T> {
  const response = await authFetch(`/api/budgets/${encodeURIComponent(tenant)}/${route}`, body === undefined ? {} : {
    method: route === "configuration" ? "PUT" : "POST", body: JSON.stringify(body),
  })
  if (!response.ok) throw new Error(await parseErrorMessage(response, "Budget operation failed"))
  return response.json() as Promise<T>
}
