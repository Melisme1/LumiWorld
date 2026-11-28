// Trang quản trị LumiWorld: HTML/JS tĩnh do LumiServer phục vụ ở /admin, gọi /api/v1/admin/* bằng token admin.
// Token giữ trong sessionStorage (đóng tab là mất). Mọi chữ từ server đưa vào bằng textContent, không dùng innerHTML.
"use strict";

const TOKEN_KEY = "lumiAdminToken";
const ME_KEY = "lumiAdminMe";
const TIME_ZONE = "Asia/Ho_Chi_Minh";
const PAGE_SIZE = 25;

const $ = (id) => document.getElementById(id);
const fmt = new Intl.NumberFormat("en-US");

let me = null;
let range = 7;
let playerPage = 1;

// ---------------------------------------------------------------- API

class ApiError extends Error {
  constructor(status, code, message) {
    super(message);
    this.status = status;
    this.code = code;
  }
}

async function api(path, { method = "GET", body } = {}) {
  const headers = { Accept: "application/json" };
  const token = sessionStorage.getItem(TOKEN_KEY);
  if (token) headers.Authorization = "Bearer " + token;
  if (body !== undefined) headers["Content-Type"] = "application/json";

  let response;
  try {
    response = await fetch(path, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) });
  } catch {
    throw new ApiError(0, "NETWORK", "No connection to the server.");
  }

  const data = response.status === 204 ? null : await response.json().catch(() => null);
  if (!response.ok) {
    const code = data && data.code ? data.code : "HTTP_" + response.status;
    const message = (data && (data.detail || data.title)) || "The server refused the request.";
    // Hết phiên, hoặc tài khoản vừa bị khóa / mất quyền admin: về màn đăng nhập
    if (token && (response.status === 401 || code === "ACCOUNT_SUSPENDED" || code === "FORBIDDEN")) signOut(message);
    throw new ApiError(response.status, code, message);
  }
  return data;
}

// ---------------------------------------------------------------- Đăng nhập

function showLogin(message) {
  $("app").hidden = true;
  $("login").hidden = false;
  $("login-error").textContent = message || "";
  $("login-user").focus();
}

function signOut(message) {
  sessionStorage.removeItem(TOKEN_KEY);
  sessionStorage.removeItem(ME_KEY);
  me = null;
  showLogin(message);
}

async function signIn(event) {
  event.preventDefault();
  const button = $("login-button");
  button.disabled = true;
  $("login-error").textContent = "";
  try {
    const auth = await api("/api/v1/auth/login", {
      method: "POST",
      body: { username: $("login-user").value.trim(), password: $("login-pass").value }
    });
    sessionStorage.setItem(TOKEN_KEY, auth.accessToken);
    const profile = await api("/api/v1/auth/me");
    if (profile.role !== "admin") {
      signOut("This account is not an admin.");
      return;
    }
    $("login-pass").value = "";
    sessionStorage.setItem(ME_KEY, JSON.stringify(profile));
    startApp(profile);
  } catch (error) {
    sessionStorage.removeItem(TOKEN_KEY);
    $("login-error").textContent = error.message;
  } finally {
    button.disabled = false;
  }
}

function startApp(profile) {
  me = profile;
  $("who-name").textContent = profile.displayName + " (" + profile.username + ")";
  $("login").hidden = true;
  $("app").hidden = false;
  route();
}

// ---------------------------------------------------------------- Điều hướng

function route() {
  if (!me) return;
  const tab = (location.hash || "#overview").slice(1);
  const name = ["overview", "players", "config"].includes(tab) ? tab : "overview";
  for (const section of document.querySelectorAll(".tab")) section.hidden = section.id !== "tab-" + name;
  for (const link of document.querySelectorAll(".tabs a")) link.classList.toggle("active", link.dataset.tab === name);
  if (name === "overview") loadOverview();
  else if (name === "players") loadPlayers();
  else loadConfig();
}

// ---------------------------------------------------------------- Tổng quan

function todayInVietnam() {
  // en-CA cho dạng yyyy-mm-dd
  return new Intl.DateTimeFormat("en-CA", { timeZone: TIME_ZONE }).format(new Date());
}

function addDays(isoDay, delta) {
  const date = new Date(isoDay + "T00:00:00Z");
  date.setUTCDate(date.getUTCDate() + delta);
  return date.toISOString().slice(0, 10);
}

const sum = (rows, pick) => rows.reduce((total, row) => total + pick(row), 0);

async function loadOverview() {
  const status = $("overview-status");
  status.textContent = "Loading…";
  const to = todayInVietnam();
  const from = addDays(to, -(range - 1));
  try {
    const [daily, totals] = await Promise.all([
      api(`/api/v1/admin/stats/daily?from=${from}&to=${to}`),
      api("/api/v1/admin/stats?days=7")
    ]);
    const days = daily.days;
    const last7 = days.slice(-7);

    $("t-players").textContent = fmt.format(totals.players.total);
    $("t-active").textContent = fmt.format(days.length ? days[days.length - 1].players.active : 0);
    $("t-new").textContent = fmt.format(sum(last7, (d) => d.players.new));
    $("t-shared").textContent = fmt.format(daily.now.world.sharedWorlds);
    $("t-sales").textContent = fmt.format(sum(last7, (d) => d.market.sold));
    $("t-sales-coins").textContent = fmt.format(sum(last7, (d) => d.market.coinsTraded)) + " Coins";
    $("t-coins").textContent = fmt.format(daily.now.economy.totalCoins);

    drawChart(days);
    fillDailyTable(days);
    status.textContent = `${daily.from} to ${daily.to} (Vietnam time)`;
  } catch (error) {
    status.textContent = error.message;
  }
}

function svg(name, attributes = {}) {
  const element = document.createElementNS("http://www.w3.org/2000/svg", name);
  for (const [key, value] of Object.entries(attributes)) element.setAttribute(key, value);
  return element;
}

function drawChart(days) {
  const chart = $("chart");
  chart.replaceChildren();

  const width = 900, height = 220, left = 36, bottom = 24, top = 10;
  const max = Math.max(1, ...days.map((d) => d.players.active));
  const step = (width - left) / Math.max(1, days.length);
  const scale = (value) => (height - bottom - top) * value / max;

  const root = svg("svg", { viewBox: `0 0 ${width} ${height}`, preserveAspectRatio: "none" });
  root.append(svg("line", { class: "axis", x1: left, y1: height - bottom, x2: width, y2: height - bottom }));

  // Trục số: 0, giữa, đỉnh
  for (const value of [0, Math.round(max / 2), max]) {
    const label = svg("text", { x: left - 6, y: height - bottom - scale(value) + 4, "text-anchor": "end" });
    label.textContent = fmt.format(value);
    root.append(label);
  }

  const labelEvery = Math.ceil(days.length / 10);
  days.forEach((day, i) => {
    const h = scale(day.players.active);
    const x = left + i * step + step * 0.15;
    const bar = svg("rect", { class: "bar", x, y: height - bottom - h, width: Math.max(1, step * 0.7), height: h, rx: 2 });
    const tip = svg("title");
    tip.textContent = `${day.day}: ${day.players.active} active, ${day.players.new} new`;
    bar.append(tip);
    root.append(bar);

    if (i % labelEvery === 0 || i === days.length - 1) {
      const label = svg("text", { x: left + i * step + step / 2, y: height - 6, "text-anchor": "middle" });
      label.textContent = day.day.slice(5);
      root.append(label);
    }
  });

  chart.append(root);
}

function cell(text, className) {
  const td = document.createElement("td");
  td.textContent = text;
  if (className) td.className = className;
  return td;
}

function fillDailyTable(days) {
  const rows = $("daily-rows");
  rows.replaceChildren();
  for (const day of [...days].reverse()) {
    const tr = document.createElement("tr");
    tr.append(
      cell(day.day),
      cell(fmt.format(day.players.active), "num"),
      cell(fmt.format(day.players.new), "num"),
      cell(fmt.format(day.market.listed), "num"),
      cell(fmt.format(day.market.sold), "num"),
      cell(fmt.format(day.market.coinsTraded), "num"),
      cell(fmt.format(day.market.fees), "num"),
      cell(fmt.format(day.trades.offered), "num"),
      cell(fmt.format(day.trades.accepted), "num"),
      cell(fmt.format(day.world.shares), "num"),
      cell(fmt.format(day.world.visits), "num"),
      cell(fmt.format(day.world.obstacles), "num")
    );
    rows.append(tr);
  }
}

// ---------------------------------------------------------------- Người chơi

function shortDate(iso) {
  return iso ? new Date(iso).toLocaleString("en-GB", { dateStyle: "medium", timeStyle: "short", timeZone: TIME_ZONE }) : "–";
}

function badge(text, className) {
  const span = document.createElement("span");
  span.className = "badge " + (className || "");
  span.textContent = text;
  return span;
}

function actionButton(text, onClick, className) {
  const button = document.createElement("button");
  button.textContent = text;
  if (className) button.className = className;
  button.addEventListener("click", onClick);
  return button;
}

async function loadPlayers() {
  const status = $("players-status");
  status.textContent = "Loading…";
  const params = new URLSearchParams({ page: playerPage, pageSize: PAGE_SIZE });
  const query = $("player-query").value.trim();
  if (query) params.set("search", query);
  if ($("player-suspended").checked) params.set("suspended", "true");

  try {
    const result = await api("/api/v1/admin/players?" + params);
    renderPlayers(result.items);
    $("players-page").textContent = "Page " + result.page;
    $("players-prev").disabled = result.page <= 1;
    $("players-next").disabled = !result.hasMore;
    status.textContent = result.items.length ? "" : "No players found.";
  } catch (error) {
    status.textContent = error.message;
  }
}

function renderPlayers(players) {
  const rows = $("player-rows");
  rows.replaceChildren();
  for (const player of players) {
    const tr = document.createElement("tr");
    const self = player.playerId === me.playerId;

    const role = document.createElement("td");
    role.append(badge(player.role, player.role === "admin" ? "admin" : ""));
    const state = document.createElement("td");
    state.append(player.isSuspended ? badge("Suspended", "suspended") : badge("Active"));

    const actions = document.createElement("td");
    actions.className = "actions";
    if (self) {
      actions.append(badge("You"));
    } else {
      if (player.isSuspended) actions.append(actionButton("Unsuspend", () => unsuspend(player)));
      else if (player.role !== "admin") actions.append(actionButton("Suspend", () => suspend(player), "danger"));

      if (player.role === "admin") actions.append(actionButton("Remove admin", () => setRole(player, "player")));
      else if (!player.isSuspended) actions.append(actionButton("Make admin", () => setRole(player, "admin")));
    }

    tr.append(
      cell(player.username),
      cell(player.displayName),
      role,
      state,
      cell(shortDate(player.createdAt)),
      cell(shortDate(player.lastLoginAt)),
      cell(player.score == null ? "–" : fmt.format(player.score), "num"),
      actions
    );
    rows.append(tr);
  }
}

async function playerAction(path, body, done) {
  try {
    await api(path, { method: "POST", body });
    $("players-status").textContent = done;
    await loadPlayers();
    $("players-status").textContent = done;
  } catch (error) {
    $("players-status").textContent = error.message;
  }
}

function suspend(player) {
  const reason = prompt(`Suspend ${player.username}? They are signed out at once.\nReason (optional):`, "");
  if (reason === null) return;
  playerAction(`/api/v1/admin/players/${player.playerId}/suspend`, { reason }, `${player.username} is suspended.`);
}

function unsuspend(player) {
  if (!confirm(`Unsuspend ${player.username}?`)) return;
  playerAction(`/api/v1/admin/players/${player.playerId}/unsuspend`, {}, `${player.username} can play again.`);
}

function setRole(player, role) {
  const question = role === "admin"
    ? `Make ${player.username} an admin? They can change settings and suspend players.`
    : `Remove admin rights from ${player.username}?`;
  if (!confirm(question)) return;
  playerAction(`/api/v1/admin/players/${player.playerId}/role`, { role },
    role === "admin" ? `${player.username} is now an admin.` : `${player.username} is no longer an admin.`);
}

// ---------------------------------------------------------------- Tham số

async function loadConfig() {
  const status = $("config-status");
  status.textContent = "Loading…";
  try {
    const result = await api("/api/v1/admin/config");
    renderConfig(result.items);
    status.textContent = "";
  } catch (error) {
    status.textContent = error.message;
  }
}

function renderConfig(items) {
  const groups = new Map();
  for (const item of items) {
    const group = item.key.split(".")[0];
    if (!groups.has(group)) groups.set(group, []);
    groups.get(group).push(item);
  }

  const host = $("config-groups");
  host.replaceChildren();
  for (const [group, entries] of groups) {
    const card = document.createElement("div");
    card.className = "card config-group";
    const title = document.createElement("h3");
    title.textContent = group;
    const table = document.createElement("table");
    table.className = "data";
    const body = document.createElement("tbody");
    for (const entry of entries) body.append(configRow(entry));
    table.append(body);
    const wrap = document.createElement("div");
    wrap.className = "table-wrap";
    wrap.append(table);
    card.append(title, wrap);
    host.append(card);
  }
  applyConfigFilter();
}

function configRow(entry) {
  const tr = document.createElement("tr");
  tr.className = "config-row";
  tr.dataset.key = entry.key;

  const input = document.createElement("input");
  input.value = entry.value;
  input.setAttribute("aria-label", entry.key);
  const save = actionButton("Save", () => saveConfig(entry, input, save, message), "primary");
  save.disabled = true;
  const message = document.createElement("span");
  message.className = "msg";

  input.addEventListener("input", () => {
    save.disabled = input.value.trim() === entry.value;
    message.textContent = "";
  });
  input.addEventListener("keydown", (event) => {
    if (event.key === "Enter" && !save.disabled) save.click();
  });

  const valueCell = document.createElement("td");
  valueCell.append(input);
  const saveCell = document.createElement("td");
  saveCell.append(save);
  const messageCell = document.createElement("td");
  messageCell.append(message);

  tr.append(cell(entry.key), valueCell, saveCell, cell("Updated " + shortDate(entry.updatedAt), "muted"), messageCell);
  return tr;
}

async function saveConfig(entry, input, save, message) {
  save.disabled = true;
  message.className = "msg";
  message.textContent = "Saving…";
  try {
    const result = await api("/api/v1/admin/config", { method: "PUT", body: { key: entry.key, value: input.value.trim() } });
    entry.value = result.value;
    input.value = result.value;
    message.className = "msg ok";
    message.textContent = `Saved (was ${result.previousValue}).`;
  } catch (error) {
    message.className = "msg error";
    message.textContent = error.message;
    save.disabled = false;
  }
}

function applyConfigFilter() {
  const text = $("config-filter").value.trim().toLowerCase();
  for (const card of document.querySelectorAll(".config-group")) {
    let visible = 0;
    for (const row of card.querySelectorAll(".config-row")) {
      const show = !text || row.dataset.key.toLowerCase().includes(text);
      row.hidden = !show;
      if (show) visible++;
    }
    card.hidden = visible === 0;
  }
}

// ---------------------------------------------------------------- Khởi động

document.addEventListener("DOMContentLoaded", () => {
  $("login-form").addEventListener("submit", signIn);
  $("logout").addEventListener("click", () => signOut(""));
  window.addEventListener("hashchange", route);

  for (const button of document.querySelectorAll("[data-range]")) {
    button.addEventListener("click", () => {
      range = Number(button.dataset.range);
      for (const other of document.querySelectorAll("[data-range]")) other.classList.toggle("active", other === button);
      loadOverview();
    });
  }

  $("player-search").addEventListener("submit", (event) => {
    event.preventDefault();
    playerPage = 1;
    loadPlayers();
  });
  $("player-suspended").addEventListener("change", () => { playerPage = 1; loadPlayers(); });
  $("players-prev").addEventListener("click", () => { playerPage = Math.max(1, playerPage - 1); loadPlayers(); });
  $("players-next").addEventListener("click", () => { playerPage++; loadPlayers(); });
  $("config-filter").addEventListener("input", applyConfigFilter);

  // Còn phiên trong tab này: kiểm tra lại quyền rồi vào thẳng
  const token = sessionStorage.getItem(TOKEN_KEY);
  if (!token) {
    showLogin("");
    return;
  }
  api("/api/v1/auth/me")
    .then((profile) => (profile.role === "admin" ? startApp(profile) : signOut("This account is not an admin.")))
    .catch((error) => signOut(error.status === 401 ? "Your session expired. Sign in again." : error.message));
});
