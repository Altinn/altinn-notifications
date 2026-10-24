// The simulator GUI. /api/events says when something changed, and the page then fetches /api/state again.
"use strict";

const state = { pending: [], history: [], systems: [], autoReplies: {}, timeoutSeconds: 0, intercept: true };
const triggers = { apiBaseUrl: "", jobs: [] };
const templateCache = {};
let selectedId = null;
let selectedFrom = null; // "pending" or "history"

const $ = (selector, root = document) => root.querySelector(selector);

function el(tag, attributes = {}, ...children) {
  const node = document.createElement(tag);
  for (const [name, value] of Object.entries(attributes)) {
    if (name === "class") node.className = value;
    else if (name.startsWith("on")) node.addEventListener(name.slice(2), value);
    else node.setAttribute(name, value);
  }
  for (const child of children) {
    if (child !== null && child !== undefined) node.append(child);
  }
  return node;
}

async function refresh() {
  const [requests, jobs] = await Promise.all([fetch("api/state"), fetch("api/triggers")]);
  Object.assign(state, await requests.json());
  Object.assign(triggers, await jobs.json());
  render();
  renderTriggers();
}

async function templatesFor(system) {
  // Fetched once per system and request, since the templates get a new {{guid}} on every fetch.
  const response = await fetch(`api/templates?system=${encodeURIComponent(system)}`);
  return response.json();
}

async function allTemplates() {
  if (!templateCache.all) {
    templateCache.all = await (await fetch("api/templates")).json();
  }
  return templateCache.all;
}

function render() {
  $("#timeout").textContent = state.intercept ? `Requests time out after ${state.timeoutSeconds} s with 504.` : "";
  $("#intercept-off").hidden = state.intercept;
  $("#tab-pending-count").textContent = state.pending.length;
  renderAutoReplies();
  renderPending();
  renderHistory();
  updateDetailState();
}

async function renderAutoReplies() {
  const templates = await allTemplates();
  const bar = $("#auto-replies");
  bar.replaceChildren(el("strong", {}, "Auto-reply:"));
  for (const system of state.systems) {
    const current = state.autoReplies[system] ?? "";
    const select = el("select", { onchange: e => setAutoReply(system, e.target.value) }, el("option", { value: "" }, "hold"));
    for (const template of templates.filter(t => t.system === system || t.system === "generic")) {
      const option = el("option", { value: template.key }, template.system === "generic" ? `${template.name} (generic)` : template.name);
      option.selected = template.key === current;
      select.append(option);
    }
    bar.append(el("label", { class: current ? "on" : "" }, system, select));
  }
}

async function setAutoReply(system, template) {
  await fetch(`api/auto-replies/${encodeURIComponent(system)}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ template: template || null }),
  });
}

function renderPending() {
  $("#pending-count").textContent = state.pending.length;
  $("#pending-empty").hidden = state.pending.length > 0;
  $("#pending").replaceChildren(...state.pending.map(request => el("li",
    { class: isSelected(request.id, "pending") ? "selected" : "", onclick: () => select(request.id, "pending") },
    el("span", { class: "badge" }, request.system),
    el("span", { class: "badge method" }, request.method),
    el("code", { class: "path", title: request.path }, request.path),
    el("span", { class: "countdown", "data-expires": request.expiresAt }, countdown(request.expiresAt)))));
}

function renderHistory() {
  $("#history-count").textContent = state.history.length;
  $("#history").replaceChildren(...state.history.map(entry => el("li",
    { class: isSelected(entry.request.id, "history") ? "selected" : "", onclick: () => select(entry.request.id, "history") },
    el("span", { class: `badge ${entry.outcome}` }, entry.response ? String(entry.response.status) : "—"),
    el("span", { class: "badge" }, entry.request.system),
    el("code", { class: "path", title: `${entry.request.method} ${entry.request.path}` }, `${entry.request.method} ${entry.request.path}`),
    el("span", { class: "time" }, time(entry.handledAt)))));
}

function isSelected(id, from) {
  return selectedId === id && selectedFrom === from;
}

function select(id, from) {
  selectedId = id;
  selectedFrom = from;
  renderPending();
  renderHistory();
  renderDetail();
}

// Builds the detail view from scratch, so it is only called when the selection changes and never wipes an answer being edited.
async function renderDetail() {
  const detail = $("#detail");
  const pending = state.pending.find(r => r.id === selectedId);
  const entry = state.history.find(h => h.request.id === selectedId);
  const request = selectedFrom === "pending" ? pending : entry?.request;
  if (!request) {
    detail.replaceChildren(el("p", { class: "muted" }, "Select a request."));
    return;
  }

  const view = $("#detail-template").content.cloneNode(true);
  $(".system", view).textContent = request.system;
  $(".target", view).textContent = `${request.method} ${request.path}`;
  fillMeta($(".meta", view), [
    ["Received", time(request.receivedAt)],
    ["Times out", time(request.expiresAt)],
    ["Trace", traceLink(request)],
    ["Id", request.id],
  ]);
  fillHeaders($(".headers tbody", view), request.headers);
  $(".body", view).textContent = pretty(request.body);

  const form = $("form.answer", view);
  if (selectedFrom === "pending") {
    const templates = await templatesFor(request.system);
    const picker = form.elements.template;
    for (const template of templates) {
      picker.append(el("option", { value: template.key }, `${template.name}${template.system === "generic" ? " (generic)" : ""}`));
    }
    picker.addEventListener("change", () => {
      const template = templates.find(t => t.key === picker.value);
      if (template) applyTemplate(form, template);
    });
    form.addEventListener("submit", e => { e.preventDefault(); sendAnswer(form, request.id); });
  } else {
    form.remove();
    showSent($(".sent", view), entry);
  }

  detail.replaceChildren(view);
  updateDetailState();
}

function applyTemplate(form, template) {
  form.elements.status.value = template.status;
  form.elements.headers.value = Object.entries(template.headers).map(([name, value]) => `${name}: ${value}`).join("\n");
  form.elements.body.value = template.body;
}

async function sendAnswer(form, id) {
  const error = $(".error", form);
  error.textContent = "";

  const headers = {};
  for (const line of form.elements.headers.value.split("\n")) {
    const colon = line.indexOf(":");
    if (line.trim() === "") continue;
    if (colon <= 0) {
      error.textContent = `Not a header: ${line}`;
      return;
    }
    headers[line.slice(0, colon).trim()] = line.slice(colon + 1).trim();
  }

  const response = await fetch(`api/requests/${id}/answer`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ status: Number(form.elements.status.value), headers, body: form.elements.body.value }),
  });

  if (response.ok) {
    await refresh();
    select(id, "history");
  } else {
    const problem = await response.json().catch(() => ({}));
    error.textContent = problem.error ?? Object.values(problem.errors ?? {}).flat().join(" ") ?? `Failed with ${response.status}`;
  }
}

function showSent(section, entry) {
  section.hidden = false;
  const outcome = $(".outcome", section);
  outcome.textContent = { Answered: "by the operator", AutoReplied: "auto-reply", TimedOut: "timed out", Cancelled: "cancelled by the caller" }[entry.outcome];
  outcome.classList.add(entry.outcome);
  if (!entry.response) {
    fillMeta($(".response-meta", section), [["Handled", time(entry.handledAt)]]);
    $(".response-body", section).remove();
    return;
  }

  const status = el("span", { class: `status-${String(entry.response.status)[0]}` }, String(entry.response.status));
  fillMeta($(".response-meta", section), [["Status", status], ["Handled", time(entry.handledAt)]]);
  fillHeaders($(".response-headers tbody", section), entry.response.headers);
  $(".response-body", section).textContent = pretty(entry.response.body);
}

// Marks the open request when it timed out or was answered elsewhere while the operator looked at it.
function updateDetailState() {
  const stateLabel = $("#detail .state");
  if (!stateLabel || selectedFrom !== "pending") return;
  const stillPending = state.pending.some(r => r.id === selectedId);
  const entry = state.history.find(h => h.request.id === selectedId);
  if (!stillPending && entry) {
    stateLabel.replaceChildren(el("span", { class: "gone" }, `No longer waiting (${entry.outcome}). `),
      el("a", { href: "#", onclick: e => { e.preventDefault(); select(selectedId, "history"); } }, "Show in history"));
    const button = $("#detail button[type=submit]");
    if (button) button.disabled = true;
  } else if (stillPending) {
    const request = state.pending.find(r => r.id === selectedId);
    stateLabel.replaceChildren(el("span", { class: "countdown", "data-expires": request.expiresAt }, countdown(request.expiresAt)));
  }
}

function fillMeta(list, rows) {
  for (const [name, value] of rows) {
    if (value === null || value === undefined || value === "") continue;
    list.append(el("dt", {}, name), el("dd", {}, value));
  }
}

function fillHeaders(body, headers) {
  for (const [name, value] of Object.entries(headers ?? {}).sort(([a], [b]) => a.localeCompare(b))) {
    body.append(el("tr", {}, el("td", {}, name), el("td", {}, value)));
  }
}

function traceLink(request) {
  if (!request.traceId) return null;
  if (!request.traceUrl) return request.traceId;
  return el("a", { href: request.traceUrl, target: "_blank", rel: "noopener" }, `${request.traceId} (Aspire dashboard)`);
}

function pretty(body) {
  if (!body) return "";
  const trimmed = body.trim();
  if (trimmed.startsWith("{") || trimmed.startsWith("[")) {
    try { return JSON.stringify(JSON.parse(trimmed), null, 2); } catch { return body; }
  }
  if (trimmed.startsWith("<")) return prettyXml(trimmed);
  return body;
}

// Puts each element on its own line, indented by depth. Elements with only text, or nothing, stay on one line.
function prettyXml(xml) {
  const tokens = (xml.match(/<[^>]+>|[^<]+/g) ?? []).map(t => t.trim()).filter(t => t !== "");
  const isOpen = t => /^<[^!?/]/.test(t) && !t.endsWith("/>");
  const isClose = t => t.startsWith("</");
  const lines = [];
  let depth = 0;
  for (let i = 0; i < tokens.length; i++) {
    const token = tokens[i];
    if (isOpen(token) && isClose(tokens[i + 1] ?? "")) {
      lines.push("  ".repeat(depth) + token + tokens[++i]);
    } else if (isOpen(token) && !tokens[i + 1]?.startsWith("<") && isClose(tokens[i + 2] ?? "")) {
      lines.push("  ".repeat(depth) + token + tokens[i + 1] + tokens[i + 2]);
      i += 2;
    } else if (isClose(token)) {
      depth = Math.max(depth - 1, 0);
      lines.push("  ".repeat(depth) + token);
    } else {
      lines.push("  ".repeat(depth) + token);
      if (isOpen(token)) depth++;
    }
  }
  return lines.join("\n");
}

function countdown(expiresAt) {
  const seconds = Math.max(0, Math.round((new Date(expiresAt) - Date.now()) / 1000));
  return `${seconds} s`;
}

function time(value) {
  return new Date(value).toLocaleTimeString();
}

function tick() {
  for (const node of document.querySelectorAll(".next-run[data-at]")) {
    node.textContent = `next in ${countdown(node.dataset.at)}`;
  }
  for (const node of document.querySelectorAll(".countdown[data-expires]")) {
    node.textContent = countdown(node.dataset.expires);
    node.classList.toggle("soon", new Date(node.dataset.expires) - Date.now() < 15000);
  }
}

function connect() {
  const connection = $("#connection");
  const events = new EventSource("api/events");
  events.onopen = () => { connection.textContent = "live"; connection.className = "connection live"; };
  events.onmessage = () => refresh();
  events.onerror = () => { connection.textContent = "reconnecting…"; connection.className = "connection down"; };
}

// The Triggers tab. The cards are built once and then updated in place, so a field being edited or an open history survives
// the refresh after every run.
function renderTriggers() {
  $("#triggers-api").textContent = triggers.apiBaseUrl ? `API: ${triggers.apiBaseUrl}` : "";
  const on = triggers.jobs.filter(j => j.enabled).length;
  const failing = triggers.jobs.some(j => j.lastRun && !j.lastRun.succeeded);
  const count = $("#tab-triggers-count");
  count.textContent = `${on} on`;
  count.classList.toggle("failing", failing);
  count.title = failing ? "A trigger failed on its last run" : "";

  const list = $("#trigger-list");
  for (const job of triggers.jobs) {
    let card = list.querySelector(`[data-name="${job.name}"]`);
    if (!card) {
      card = buildTriggerCard(job);
      list.append(card);
    }
    updateTriggerCard(card, job);
  }
}

function buildTriggerCard(job) {
  const card = $("#trigger-template").content.firstElementChild.cloneNode(true);
  card.dataset.name = job.name;
  $(".name", card).textContent = job.path;
  $(".description", card).textContent = job.description;
  $(".cluster", card).textContent = job.clusterSchedule ? `Cluster: ${job.clusterSchedule}` : "Not scheduled in the cluster";

  const enabled = $("input[name=enabled]", card);
  const interval = $("input[name=interval]", card);
  const hours = $("input[name=hours]", card);
  enabled.addEventListener("change", () => updateTrigger(card, { enabled: enabled.checked }));
  interval.addEventListener("change", () => updateTrigger(card, { intervalSeconds: Number(interval.value) }));
  hours.addEventListener("change", () => updateTrigger(card, { hours: hours.value.trim() }));
  $("button.run", card).addEventListener("click", () => runTrigger(card));
  return card;
}

function updateTriggerCard(card, job) {
  const setValue = (input, value) => { if (document.activeElement !== input) input.value = value; };
  $("input[name=enabled]", card).checked = job.enabled;
  setValue($("input[name=interval]", card), job.intervalSeconds);
  setValue($("input[name=hours]", card), job.hours);
  card.classList.toggle("paused", !job.enabled);
  $("button.run", card).disabled = job.running;

  let status = "";
  if (job.running) status = el("span", { class: "badge running" }, "running…");
  else if (!job.enabled) status = el("span", { class: "badge" }, "paused");
  else if (job.outsideHours) status = el("span", { class: "badge" }, `waiting for ${job.hours} UTC`);
  else if (job.nextRunAt) status = el("span", { class: "next-run", "data-at": job.nextRunAt }, `next in ${countdown(job.nextRunAt)}`);
  $(".trigger-state", card).replaceChildren(status);

  const meta = $(".last-run", card);
  meta.replaceChildren();
  const last = job.lastRun;
  fillMeta(meta, last
    ? [
      ["Last run", `${time(last.startedAt)} (${last.source})`],
      ["Status", statusBadge(last)],
      ["Duration", `${last.durationMs} ms`],
      ["Response", last.response ? el("code", { class: "response" }, last.response) : null],
    ]
    : [["Last run", "not yet"]]);

  $(".run-count", card).textContent = job.history.length;
  $(".run-table tbody", card).replaceChildren(...job.history.map(run => el("tr", {},
    el("td", {}, time(run.startedAt)),
    el("td", {}, run.source),
    el("td", {}, statusBadge(run)),
    el("td", {}, `${run.durationMs} ms`),
    el("td", { class: "response", title: run.response }, run.response))));
}

function statusBadge(run) {
  return run.status === null
    ? el("span", { class: "status-5" }, "no response")
    : el("span", { class: `status-${String(run.status)[0]}` }, String(run.status));
}

async function updateTrigger(card, change) {
  const error = $(".error", card);
  error.textContent = "";
  const response = await fetch(`api/triggers/${encodeURIComponent(card.dataset.name)}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(change),
  });
  if (!response.ok) {
    const problem = await response.json().catch(() => ({}));
    error.textContent = Object.values(problem.errors ?? {}).flat().join(" ") || problem.error || `Failed with ${response.status}`;
    await refresh();
  }
}

async function runTrigger(card) {
  const error = $(".error", card);
  error.textContent = "";
  $("button.run", card).disabled = true;
  const response = await fetch(`api/triggers/${encodeURIComponent(card.dataset.name)}/run`, { method: "POST" });
  if (!response.ok) {
    const problem = await response.json().catch(() => ({}));
    error.textContent = problem.error ?? `Failed with ${response.status}`;
  }
  await refresh();
}

async function setAllTriggers(enabled) {
  await fetch("api/triggers", { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ enabled }) });
}

function showTab() {
  const tab = location.hash === "#triggers" ? "triggers" : "requests";
  for (const link of document.querySelectorAll(".tabs a")) {
    link.setAttribute("aria-selected", String(link.dataset.tab === tab));
  }
  $("#tab-requests").hidden = tab !== "requests";
  $("#tab-triggers").hidden = tab !== "triggers";
}

$("#pause-all").addEventListener("click", () => setAllTriggers(false));
$("#resume-all").addEventListener("click", () => setAllTriggers(true));
window.addEventListener("hashchange", showTab);
showTab();
setInterval(tick, 1000);
connect();
