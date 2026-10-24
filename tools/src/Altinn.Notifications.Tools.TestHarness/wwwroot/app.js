// The test harness control panel. /api/events says when something changed, and the page then fetches /api/state again.
"use strict";

const state = { pending: [], history: [], systems: [], autoReplies: {}, timeoutSeconds: 0, intercept: true };
const templateCache = {};
let selectedId = null;
let selectedFrom = null; // "pending" or "history"
const mirror = { sms: [], email: [] };
let selectedPhone = null;
let selectedEmail = null;
let emailView = "html";

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
  const [requests, sms, email] = await Promise.all([fetch("api/state"), fetch("api/mirror/sms"), fetch("api/mirror/email")]);
  Object.assign(state, await requests.json());
  mirror.sms = await sms.json();
  mirror.email = await email.json();
  render();
  renderPhone();
  renderInbox();
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
  $("#tab-pending-count").textContent = state.pending.length;
  $("#timeout").textContent = state.intercept ? `Requests time out after ${state.timeoutSeconds} s with 504.` : "";
  $("#intercept-off").hidden = state.intercept;
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

// The Phone tab: one conversation per recipient number, newest message last, like a phone's messages app.
function renderPhone() {
  const filter = $("#phone-filter").value;
  const shown = mirror.sms.filter(m => !filter || m.status === filter);
  $("#tab-phone-count").textContent = mirror.sms.length;

  const conversations = new Map();
  for (const message of shown) {
    const recipient = message.content.recipient;
    if (!conversations.has(recipient)) conversations.set(recipient, []);
    conversations.get(recipient).push(message);
  }

  $("#phone-empty").hidden = conversations.size > 0;
  $("#phone-empty").textContent = mirror.sms.length === 0
    ? "No SMS yet. Order one, and the copy wiremock-linkmobility sends here shows up."
    : "No delivered SMS yet. Show all to see the ones still on their way or failed.";
  if (!conversations.has(selectedPhone)) selectedPhone = conversations.keys().next().value ?? null;

  // The store is newest first, so the first message of each conversation is its latest.
  $("#conversations").replaceChildren(...[...conversations].map(([recipient, messages]) => el("li",
    { class: recipient === selectedPhone ? "selected" : "", onclick: () => { selectedPhone = recipient; renderPhone(); } },
    el("strong", { class: "number" }, recipient),
    el("span", { class: "count" }, String(messages.length)),
    el("span", { class: "preview muted" }, messages[0].content.text ?? ""),
    el("span", { class: "time" }, time(messages[0].receivedAt)))));

  $("#phone-number").textContent = selectedPhone ?? "No conversation";
  const list = $("#phone-messages");
  const atBottom = list.scrollHeight - list.scrollTop - list.clientHeight < 40;
  const messages = (conversations.get(selectedPhone) ?? []).slice().reverse();
  list.replaceChildren(...messages.map(message => el("div", { class: "sms" },
    el("div", { class: "sender" }, message.content.sender || "unknown sender"),
    el("div", { class: "bubble" }, message.content.text ?? ""),
    el("div", { class: "sms-meta" },
      el("span", { class: "time" }, time(message.receivedAt)),
      mirrorStatusBadge(message),
      el("span", { class: "muted", title: smsDetails(message) }, "ⓘ")))));
  if (atBottom) list.scrollTop = list.scrollHeight;
}

function mirrorStatusBadge(message) {
  const title = message.detail ? `${message.detail} at ${time(message.statusAt)}` : time(message.statusAt);
  return el("span", { class: `badge mirror-${message.status.toLowerCase()}`, title }, message.status);
}

function smsDetails(message) {
  const sms = message.content;
  return [
    `Reference: ${sms.reference || "none"}`,
    `Scenario: ${sms.scenario ?? "-"}`,
    `TTL: ${sms.ttl ?? "-"} min`,
    message.detail ? `Gateway state: ${message.detail}` : null,
  ].filter(Boolean).join("\n");
}

// The Inbox tab: newest first, and the selected email with its HTML body in an empty sandbox, so it can neither run scripts nor
// reach this page.
function renderInbox() {
  $("#tab-inbox-count").textContent = mirror.email.length;
  if (!mirror.email.some(m => m.id === selectedEmail)) selectedEmail = mirror.email[0]?.id ?? null;

  $("#emails").replaceChildren(...mirror.email.map(message => {
    const email = message.content;
    return el("li", { class: message.id === selectedEmail ? "selected" : "", onclick: () => selectEmail(message.id) },
      el("strong", { class: "subject" }, email.subject || "(no subject)"),
      mirrorStatusBadge(message),
      el("span", { class: "muted address" }, `${email.sender ?? "unknown sender"} → ${recipients(email)}`),
      el("span", { class: "time" }, time(message.receivedAt)));
  }));

  const selected = mirror.email.find(m => m.id === selectedEmail);
  const detail = $("#email-detail");
  if (!selected) {
    if (detail.dataset.id !== "") {
      detail.dataset.id = "";
      detail.replaceChildren(el("p", { class: "muted" }, mirror.email.length
        ? "Select an email."
        : "No email yet. Order one, and the copy mock-acs sends here shows up."));
    }
    return;
  }

  // Only rebuilt when the selection changes, so the body does not reload on every update. The status is updated in place.
  if (detail.dataset.id !== selected.id) {
    detail.dataset.id = selected.id;
    detail.replaceChildren(buildEmail(selected));
  }
  $(".status-slot", detail)?.replaceChildren(mirrorStatusBadge(selected));
}

function buildEmail(message) {
  const email = message.content;
  const view = $("#email-template").content.cloneNode(true);
  $(".subject", view).textContent = email.subject || "(no subject)";
  fillMeta($(".email-meta", view), [
    ["From", email.sender],
    ["To", email.to.join(", ")],
    ["Cc", email.cc.join(", ")],
    ["Bcc", email.bcc.join(", ")],
    ["Received", time(message.receivedAt)],
    ["Status", el("span", { class: "status-slot" })],
    ["Operation", email.operationId],
    ["Scenario", email.scenario],
  ]);

  const hasHtml = Boolean(email.html);
  const hasText = Boolean(email.plainText);
  const show = hasHtml && (emailView === "html" || !hasText) ? "html" : "text";
  const frame = $(".email-html", view);
  const text = $(".email-text", view);
  frame.hidden = show !== "html";
  text.hidden = show !== "text";
  if (hasHtml) frame.srcdoc = email.html;
  text.textContent = email.plainText || (hasHtml ? "" : "(no body)");

  for (const button of view.querySelectorAll(".body-switch button")) {
    const available = button.dataset.view === "html" ? hasHtml : hasText;
    button.disabled = !available;
    button.setAttribute("aria-pressed", String(button.dataset.view === show));
    button.addEventListener("click", () => {
      emailView = button.dataset.view;
      $("#email-detail").dataset.id = "";
      renderInbox();
    });
  }
  return view;
}

function selectEmail(id) {
  selectedEmail = id;
  renderInbox();
}

function recipients(email) {
  return [...email.to, ...email.cc, ...email.bcc].join(", ");
}

function showTab() {
  const tab = ["#phone", "#inbox"].includes(location.hash) ? location.hash.slice(1) : "requests";
  for (const link of document.querySelectorAll(".tabs a")) {
    link.setAttribute("aria-selected", String(link.dataset.tab === tab));
  }
  $("#tab-requests").hidden = tab !== "requests";
  $("#tab-phone").hidden = tab !== "phone";
  $("#tab-inbox").hidden = tab !== "inbox";
}

$("#phone-filter").addEventListener("change", renderPhone);
window.addEventListener("hashchange", showTab);
showTab();
setInterval(tick, 1000);
connect();
