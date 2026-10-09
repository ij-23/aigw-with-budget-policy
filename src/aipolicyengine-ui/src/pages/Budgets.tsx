import { useState } from "react"
import { Button } from "../components/ui/button"
import { Input } from "../components/ui/input"
import { budgetRequest, type BudgetAssignment, type BudgetBalance, type BudgetConfiguration, type BudgetModelRate, type BudgetPolicy, type BudgetReservation } from "../api/budgets"

const empty: BudgetConfiguration = { revision: 0, policies: [], assignments: [], models: [] }
const money = (amount: number) => amount.toLocaleString("en-US", { style: "currency", currency: "USD", maximumFractionDigits: 6 })
const selectClass = "rounded-md border bg-background p-2 text-sm"

export function Budgets() {
  const [tenantInput, setTenantInput] = useState("")
  const [tenant, setTenant] = useState("")
  const [config, setConfig] = useState<BudgetConfiguration>(empty)
  const [balances, setBalances] = useState<BudgetBalance[]>([])
  const [reservations, setReservations] = useState<BudgetReservation[]>([])
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState("")
  const [notice, setNotice] = useState("")
  const [dirty, setDirty] = useState(false)
  const [policy, setPolicy] = useState({ name: "Standard AI allowance", limitUsd: 50, period: "Monthly" as BudgetPolicy["period"] })
  const [assignment, setAssignment] = useState<BudgetAssignment>({ policyId: "", subjectType: "Group", subjectId: "", groupMode: "PerMember" })
  const [model, setModel] = useState<BudgetModelRate>({ deploymentId: "", version: "", inputUsdPerMillion: 0, outputUsdPerMillion: 0, cachedInputUsdPerMillion: 0, maxInputTokens: 0, maxOutputTokens: 0 })
  const [reconcile, setReconcile] = useState({ id: "", promptTokens: 0, completionTokens: 0, cachedInputTokens: 0, note: "" })

  async function perform(action: () => Promise<void>) {
    setBusy(true); setError(""); setNotice("")
    try { await action() } catch (e) { setError(e instanceof Error ? e.message : "Budget operation failed") }
    finally { setBusy(false) }
  }
  async function load(id: string) {
    const [configuration, ledger, requests] = await Promise.all([
      budgetRequest<BudgetConfiguration>(id, "configuration"),
      budgetRequest<BudgetBalance[]>(id, "balances"),
      budgetRequest<BudgetReservation[]>(id, "reservations"),
    ])
    setTenant(id); setConfig(configuration); setBalances(ledger); setReservations(requests); setDirty(false)
  }
  function edit(next: BudgetConfiguration) { setConfig(next); setDirty(true); setNotice("") }
  const current = balances.filter(b => new Date(b.resetsAt) > new Date())
  const pending = reservations.filter(r => !r.settledAt)

  return <div className="space-y-6">
    <div><h2 className="text-2xl font-semibold">AI budgets</h2>
      <p className="mt-1 text-sm text-muted-foreground">Assign a USD allowance across models. Monthly resets occur on the first; weekly resets occur on Monday, at 00:00 UTC.</p></div>
    <form className="flex flex-wrap items-end gap-3" onSubmit={e => { e.preventDefault(); if (!busy && !dirty) void perform(() => load(tenantInput.trim().toLowerCase())) }}>
      <label className="min-w-80 text-sm">Entra tenant ID<Input required value={tenantInput} onChange={e => setTenantInput(e.target.value)} placeholder="Tenant UUID" /></label>
      <Button disabled={busy || dirty} type="submit">Load budgets</Button>
      {tenant && <Button type="button" disabled={busy || !dirty} onClick={() => void perform(async () => {
        const saved = await budgetRequest<BudgetConfiguration>(tenant, "configuration", config)
        setConfig(saved); setDirty(false); setNotice("Budget configuration saved. Apply the Entra JWT — AI USD budget template to enforce it in APIM.")
      })}>Save changes</Button>}
      {dirty && <Button type="button" variant="outline" disabled={busy} onClick={() => void perform(() => load(tenant))}>Discard changes</Button>}
    </form>
    {error && <p role="alert" className="rounded-md border border-destructive p-3 text-destructive">{error}</p>}
    {notice && <p role="status" className="rounded-md border p-3">{notice}</p>}
    {dirty && <p className="text-sm text-amber-600">Unsaved changes. Save to activate these policies and assignments.</p>}
    {tenant && <fieldset disabled={busy} className="space-y-6">
      <section className="space-y-4 rounded-xl border bg-card p-5">
        <h3 className="font-semibold">Allowance policies</h3>
        <form className="flex flex-wrap items-end gap-3" onSubmit={e => { e.preventDefault(); edit({ ...config, policies: [...config.policies, { ...policy, id: crypto.randomUUID(), enabled: true }] }) }}>
          <label className="text-sm">Name<Input required maxLength={100} value={policy.name} onChange={e => setPolicy({ ...policy, name: e.target.value })} /></label>
          <label className="text-sm">Allowance (USD)<Input required type="number" min="0.01" max="1000000" step="0.01" value={policy.limitUsd} onChange={e => setPolicy({ ...policy, limitUsd: Number(e.target.value) })} /></label>
          <label className="flex flex-col text-sm">Reset period<select className={selectClass} value={policy.period} onChange={e => setPolicy({ ...policy, period: e.target.value as BudgetPolicy["period"] })}><option>Monthly</option><option>Weekly</option></select></label>
          <Button disabled={busy} type="submit">Add policy</Button>
        </form>
        {config.policies.map(p => <div key={p.id} className="flex flex-wrap items-center gap-3 border-t pt-3">
          <span className="grow text-sm">{p.name} · {p.period}</span>
          <label className="text-sm">USD limit<Input aria-label={`${p.name} limit`} type="number" min="0.01" step="0.01" value={p.limitUsd} onChange={e => edit({ ...config, policies: config.policies.map(x => x.id === p.id ? { ...x, limitUsd: Number(e.target.value) } : x) })} /></label>
          <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={p.enabled} onChange={e => edit({ ...config, policies: config.policies.map(x => x.id === p.id ? { ...x, enabled: e.target.checked } : x) })} />Enabled</label>
        </div>)}
      </section>
      <section className="space-y-4 rounded-xl border bg-card p-5">
        <h3 className="font-semibold">Assignments</h3>
        <p className="text-sm text-muted-foreground">Each matching policy must have funds. Per-member assignments give each user an independent allowance; shared assignments give the whole group one allowance. Disabled policies stop enforcing their limits.</p>
        <form className="flex flex-wrap items-end gap-3" onSubmit={e => { e.preventDefault(); edit({ ...config, assignments: [...config.assignments, { ...assignment, subjectId: assignment.subjectId.trim().toLowerCase() }] }); setAssignment({ ...assignment, subjectId: "" }) }}>
          <label className="flex flex-col text-sm">Policy<select required className={selectClass} value={assignment.policyId} onChange={e => setAssignment({ ...assignment, policyId: e.target.value })}><option value="">Choose a policy</option>{config.policies.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}</select></label>
          <label className="flex flex-col text-sm">Assign to<select className={selectClass} value={assignment.subjectType} onChange={e => setAssignment({ ...assignment, subjectType: e.target.value as BudgetAssignment["subjectType"] })}><option>User</option><option>Group</option><option>Application</option></select></label>
          <label className="min-w-72 text-sm">Object ID / application client ID<Input required value={assignment.subjectId} onChange={e => setAssignment({ ...assignment, subjectId: e.target.value })} placeholder="UUID" /></label>
          {assignment.subjectType === "Group" && <label className="flex flex-col text-sm">Group allowance<select className={selectClass} value={assignment.groupMode} onChange={e => setAssignment({ ...assignment, groupMode: e.target.value as BudgetAssignment["groupMode"] })}><option value="PerMember">Per member</option><option value="Shared">Shared by group</option></select></label>}
          <Button disabled={busy} type="submit">Add assignment</Button>
        </form>
        {config.assignments.map((a, index) => <div key={`${a.policyId}-${a.subjectId}-${index}`} className="flex flex-wrap items-center justify-between gap-2 border-t pt-3 text-sm">
          <span>{config.policies.find(p => p.id === a.policyId)?.name} · {a.subjectType}: {a.subjectId} {a.subjectType === "Group" && `· ${a.groupMode === "Shared" ? "shared" : "per member"}`}</span>
          <Button variant="outline" disabled={busy} onClick={() => edit({ ...config, assignments: config.assignments.filter((_, i) => i !== index) })}>Remove</Button>
        </div>)}
      </section>
      <section className="space-y-4 rounded-xl border bg-card p-5">
        <h3 className="font-semibold">Approved model prices</h3>
        <p className="text-sm text-muted-foreground">Enter verified USD prices per million tokens. The input limit must cover the backend model’s full input/context limit. It is reserved conservatively on every call. Unknown deployments are blocked. Supports non-streaming text Chat Completions with an explicit output cap.</p>
        <form className="grid gap-3 md:grid-cols-4" onSubmit={e => { e.preventDefault(); edit({ ...config, models: [...config.models.filter(m => m.deploymentId !== model.deploymentId.trim()), { ...model, deploymentId: model.deploymentId.trim() }] }) }}>
          <label className="text-sm">Deployment ID<Input required value={model.deploymentId} onChange={e => setModel({ ...model, deploymentId: e.target.value })} /></label>
          <label className="text-sm">Price version<Input required value={model.version} onChange={e => setModel({ ...model, version: e.target.value })} placeholder="Model version / effective date" /></label>
          {([
            ["inputUsdPerMillion", "Input USD / million", "0.000001"], ["outputUsdPerMillion", "Output USD / million", "0.000001"],
            ["cachedInputUsdPerMillion", "Cached input USD / million", "0.000001"], ["maxInputTokens", "Backend input/context limit", "1"], ["maxOutputTokens", "Maximum output tokens", "1"],
          ] as const).map(([key, label, step]) => <label key={key} className="text-sm">{label}<Input required type="number" min={key === "cachedInputUsdPerMillion" ? "0" : step} step={step} value={model[key]} onChange={e => setModel({ ...model, [key]: Number(e.target.value) })} /></label>)}
          <Button disabled={busy} className="self-end" type="submit">Add / replace price</Button>
        </form>
        {config.models.map(m => <div key={m.deploymentId} className="flex flex-wrap items-center justify-between gap-2 border-t pt-3 text-sm">
          <span>{m.deploymentId} · {m.version} · input {money(m.inputUsdPerMillion)} / output {money(m.outputUsdPerMillion)} per million · input cap {m.maxInputTokens.toLocaleString()}</span>
          <div className="flex gap-2"><Button variant="outline" onClick={() => setModel(m)}>Edit</Button><Button variant="outline" disabled={busy} onClick={() => edit({ ...config, models: config.models.filter(x => x.deploymentId !== m.deploymentId) })}>Remove</Button></div>
        </div>)}
      </section>
      <section className="space-y-4 rounded-xl border bg-card p-5">
        <h3 className="font-semibold">Current balances</h3>
        <p className="text-sm text-muted-foreground">Spend includes settled calls. Reserved funds cover calls in progress or awaiting reconciliation. Views are limited to 500 records; refresh to update.</p>
        <Button variant="outline" disabled={busy || dirty} onClick={() => void perform(() => load(tenant))}>Refresh usage</Button>
        <div className="overflow-x-auto"><table className="w-full text-left text-sm"><thead><tr><th>Policy / consumer</th><th>Spent</th><th>Reserved</th><th>Remaining</th><th>Reset</th></tr></thead>
          <tbody>{current.map(b => <tr key={b.id} className="border-t"><td className="py-3">{config.policies.find(p => p.id === b.policyId)?.name}<br />{b.subject}</td><td>{money(b.spentUsd)}</td><td>{money(b.reservedUsd)}</td><td>{money(Math.max(0, (config.policies.find(p => p.id === b.policyId)?.limitUsd ?? b.limitUsd) - b.spentUsd - b.reservedUsd))}</td><td>{new Date(b.resetsAt).toISOString()}</td></tr>)}</tbody></table></div>
        {current.length === 0 && <p className="text-sm text-muted-foreground">No current usage. Balances appear after the first authorized request.</p>}
      </section>
      <section className="space-y-4 rounded-xl border bg-card p-5">
        <h3 className="font-semibold">Pending reservations ({pending.length})</h3>
        <p className="text-sm text-muted-foreground">Timeouts and missing callbacks never automatically refund money. Reconcile only after verifying actual backend usage. Zero usage releases the reservation when you have confirmed no billable inference occurred.</p>
        {pending.map(r => <div key={r.id} className="flex flex-wrap justify-between gap-2 border-t pt-3 text-sm"><span>{r.id.replace("reservation:", "")} · {r.deploymentId} · {money(r.reservedUsd)} · {new Date(r.createdAt).toISOString()}</span><Button variant="outline" onClick={() => setReconcile({ id: r.id.replace("reservation:", ""), promptTokens: 0, completionTokens: 0, cachedInputTokens: 0, note: "" })}>Reconcile</Button></div>)}
        {reservations.some(r => r.overrun) && <p role="alert" className="text-destructive">A request exceeded its configured bound. Review backend limits and pricing before enabling further use.</p>}
        {reconcile.id && <form className="grid gap-3 md:grid-cols-3" onSubmit={e => { e.preventDefault(); void perform(async () => {
          await budgetRequest(tenant, `reservations/${encodeURIComponent(reconcile.id)}/reconcile`, { usage: { promptTokens: reconcile.promptTokens, completionTokens: reconcile.completionTokens, cachedInputTokens: reconcile.cachedInputTokens }, note: reconcile.note })
          setReconcile({ ...reconcile, id: "" }); await load(tenant); setNotice("Verified usage reconciled.")
        }) }}>
          <p className="md:col-span-3 text-sm">Request: {reconcile.id}</p>
          {(["promptTokens", "completionTokens", "cachedInputTokens"] as const).map(key => <label key={key} className="text-sm">{key}<Input required type="number" min="0" step="1" value={reconcile[key]} onChange={e => setReconcile({ ...reconcile, [key]: Number(e.target.value) })} /></label>)}
          <label className="md:col-span-3 text-sm">Evidence / reconciliation note<Input required maxLength={2000} value={reconcile.note} onChange={e => setReconcile({ ...reconcile, note: e.target.value })} /></label>
          <Button disabled={busy || dirty} type="submit">Record verified usage</Button>
        </form>}
      </section>
    </fieldset>}
  </div>
}
