"use strict";
window.APP_BUILD = "3.42.2";  // must match index.html: a mixed-up install is caught at start

// ---------- talking to the Python side ----------
const TOKEN = document.querySelector('meta[name="gm-token"]')?.content;
const ready = new Promise((resolve) => {
  if (TOKEN) return resolve();                                    // browser mode
  if (window.pywebview && window.pywebview.api) return resolve(); // pywebview, already up
  window.addEventListener("pywebviewready", () => resolve());
});

async function call(name, ...args) {
  await ready;
  if (!TOKEN) return window.pywebview.api[name](...args);
  const r = await fetch("/api/" + name, {
    method: "POST",
    headers: { "Content-Type": "application/json", "X-GM-Token": TOKEN },
    body: JSON.stringify(args),
  });
  const j = await r.json();
  if (j.error) throw new Error(j.error);
  return j.result;
}

// ---------- borderless window ----------
async function setFullscreen(on) {
  const w = await call("win_fullscreen", on);
  document.body.classList.toggle("fullscreen", w.fullscreen);
  $("#winbar").classList.toggle("pywebview-drag-region", !w.fullscreen); // nothing to drag when it fills the screen
  $("#winFull").title = w.fullscreen ? "Windowed (Esc or F11)" : "Full screen (F11)";
  $("#winFull").setAttribute("aria-label", w.fullscreen ? "Windowed" : "Full screen");
}

async function initWindow() {
  const w = await call("win_state");
  if (!w.framed) return; // browser mode: the browser has its own buttons
  document.body.classList.add("framed");
  document.body.classList.toggle("fullscreen", w.fullscreen);
  $("#winbar").classList.toggle("pywebview-drag-region", !w.fullscreen);
  $("#winbar").classList.remove("hidden");
  $("#winMin").onclick = () => call("win_minimize");
  $("#winClose").onclick = () => call("win_close");
  $("#winFull").onclick = () => setFullscreen(!document.body.classList.contains("fullscreen"));
  $("#winbar").ondblclick = (e) => { if (!e.target.closest("button")) setFullscreen(!document.body.classList.contains("fullscreen")); };
  document.addEventListener("keydown", (e) => {
    if (e.key === "F11") { e.preventDefault(); setFullscreen(!document.body.classList.contains("fullscreen")); }
    else if (e.key === "Escape" && document.body.classList.contains("fullscreen") && $("#modal").classList.contains("hidden")) setFullscreen(false);
  });
}
initWindow();

// ---------- small helpers ----------
const $ = (sel, el = document) => el.querySelector(sel);
const esc = (s) => String(s ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
const basename = (p) => String(p).split(/[\\/]/).pop();
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const errText = (e) => (e && (e.message || e.toString())) || "Something went wrong.";

function toast(text, ms = 3200) {
  const t = $("#toast");
  t.textContent = text;
  t.classList.remove("hidden");
  clearTimeout(toast.timer);
  toast.timer = setTimeout(() => t.classList.add("hidden"), ms);
}

function modal(title, bodyHtml, actions) {
  $("#modalTitle").textContent = title;
  $("#modalBody").innerHTML = bodyHtml;
  const box = $("#modalActions");
  box.innerHTML = "";
  return new Promise((resolve) => {
    (actions || [{ label: "OK", value: true, primary: true }]).forEach((a) => {
      const b = document.createElement("button");
      b.className = "btn" + (a.primary ? " primary" : "") + (a.danger ? " danger" : "");
      b.textContent = a.label;
      b.onclick = () => { closeModal(); resolve(a.value); };
      box.appendChild(b);
    });
    $("#modal").classList.remove("hidden");
    box.lastChild?.focus();
  });
}
function closeModal() { $("#modal").classList.add("hidden"); }
const confirmBox = (title, text, yes = "Continue", danger = false) =>
  modal(title, `<p class="muted">${esc(text)}</p>`, [{ label: "Cancel", value: false }, { label: yes, value: true, primary: !danger, danger }]);

// ---------- app state ----------
const state = {
  view: "library",
  games: [],
  selected: null,   // key of the chosen base game
  themeId: null,
  covers: {},       // key -> data uri | null
  setup: null,      // the merge being set up
  merges: [],
  settings: null,
};

function game(key) { return state.games.find((g) => g.key === key); }
function theme() { const g = game(state.selected); return g && g.themes.find((t) => t.id === state.themeId); }

// ---------- navigation ----------
function show(view) {
  state.view = view;
  document.querySelectorAll(".view").forEach((v) => v.classList.toggle("hidden", v.id !== "view-" + view));
  document.querySelectorAll(".nav-item[data-view]").forEach((b) =>
    b.classList.toggle("active", b.dataset.view === view || (view === "setup" && b.dataset.view === "library")));
  $("#main").scrollTop = 0;
  ({ library: renderLibrary, setup: renderSetup, merges: loadMerges, party: loadParty, gallery: loadGallery, quests: renderQuests, awards: loadAwards, backups: loadBackups, settings: loadSettings }[view])();
}
document.querySelectorAll(".nav-item[data-view]").forEach((b) => (b.onclick = () => show(b.dataset.view)));

// ---------- 3D cases ----------
// A cover made from the game's name when no real art can be found, so no cover is ever blank.
const _genCovers = {};
function genCover(name) {
  if (_genCovers[name]) return _genCovers[name];
  let h = 0;
  for (const ch of name || "?") h = (h * 31 + ch.charCodeAt(0)) >>> 0;
  const rnd = () => ((h = (h * 1103515245 + 12345) >>> 0) % 1000) / 1000;
  const a = Math.floor(rnd() * 360), b = (a + 40 + Math.floor(rnd() * 80)) % 360;
  let shapes = "";
  if (/minecraft/i.test(name)) {  // blocks
    for (let y = 0; y < 15; y++) for (let x = 0; x < 10; x++) {
      const g = y < 5 ? `hsl(${100 + rnd() * 20},55%,${30 + rnd() * 15}%)` : `hsl(${25 + rnd() * 10},45%,${22 + rnd() * 14}%)`;
      shapes += `<rect x="${x * 30}" y="${y * 30}" width="30" height="30" fill="${g}"/>`;
    }
  } else {
    for (let i = 0; i < 7; i++) shapes += `<circle cx="${rnd() * 300}" cy="${rnd() * 450}" r="${30 + rnd() * 110}" fill="hsla(${rnd() < .5 ? a : b},85%,60%,${.08 + rnd() * .18})"/>`;
    for (let i = 0; i < 5; i++) { const x = rnd() * 300; shapes += `<path d="M${x} 0 L${x + 120} 450" stroke="hsla(${b},90%,75%,.12)" stroke-width="${2 + rnd() * 10}"/>`; }
    for (let i = 0; i < 46; i++) shapes += `<circle cx="${150 + Math.cos(i * 2.4) * (8 + i * 1.6)}" cy="${150 + Math.sin(i * 2.4) * (8 + i * 1.6) * .8}" r="2.2" fill="rgba(255,255,255,${.15 + rnd() * .5})"/>`;
  }
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 300 450"><defs><linearGradient id="g" x1="0" y1="0" x2="1" y2="1">
    <stop offset="0" stop-color="hsl(${a},65%,32%)"/><stop offset="1" stop-color="hsl(${b},70%,12%)"/></linearGradient>
    <linearGradient id="s" x1="0" y1="0" x2="0" y2="1"><stop offset=".45" stop-color="rgba(0,0,0,0)"/><stop offset="1" stop-color="rgba(0,0,0,.75)"/></linearGradient></defs>
    <rect width="300" height="450" fill="url(#g)"/>${shapes}<rect width="300" height="450" fill="url(#s)"/></svg>`;
  return (_genCovers[name] = "data:image/svg+xml;base64," + btoa(unescape(encodeURIComponent(svg))));
}

function caseHtml(name, cover, extra = "") {
  const art = `style="background-image:url('${cover || genCover(name)}')"`;
  return `<div class="case ${extra}"><div class="case-inner">
      <div class="spine" ${art}></div>
      <div class="face ${cover ? "" : "placeholder"}" ${art}>${cover ? "" : esc(name)}</div>
    </div></div>`;
}

async function loadCover(key) {
  if (key in state.covers) return state.covers[key];
  state.covers[key] = null;
  try { state.covers[key] = await call("cover", key); } catch (e) { /* placeholder stays */ }
  return state.covers[key];
}

// ---------- library ----------
async function scan() {
  $("#navFoot").textContent = "Looking for games…";
  try {
    state.games = await call("scan");
  } catch (e) {
    toast("Couldn't scan for games: " + errText(e));
    state.games = [];
  }
  state.covers = {};
  const base = state.games.filter((g) => g.base);
  const sources = [...new Set(state.games.map((g) => g.source))].join(", ");
  $("#navFoot").textContent = `${state.games.length} games found${sources ? " · " + sources : ""}`;
  if (!game(state.selected) || !game(state.selected).base) state.selected = base[0]?.key ?? null;
  state.themeId = game(state.selected)?.themes[0]?.id ?? null;
  if (state.view === "library") renderLibrary();
  for (const g of state.games) {
    loadCover(g.key).then((c) => {
      if (!c) return;
      document.querySelectorAll(`[data-cover="${g.key}"]`).forEach((el) => {
        el.querySelectorAll(".face, .spine").forEach((f) => { f.style.backgroundImage = `url('${c}')`; f.classList.remove("placeholder"); });
        if (el.querySelector(".face")) el.querySelector(".face").textContent = "";
        if (el.classList.contains("pair-cover")) { el.style.backgroundImage = `url('${c}')`; el.classList.remove("placeholder"); el.textContent = ""; }
      });
      if (g.key === state.selected) $(".banner-art") && ($(".banner-art").style.backgroundImage = `url('${c}')`);
    });
  }
}

function renderLibrary() {
  const el = $("#view-library");
  const g = game(state.selected);
  const t = theme();
  let banner;
  if (g) {
    const opts = g.themes.map((x) => `<option value="${esc(x.id)}" ${x.id === state.themeId ? "selected" : ""}>${esc(x.name)}${x.found ? "  ·  found on this PC" : ""}</option>`).join("");
    banner = `<section class="banner">
      <div class="banner-art" style="${state.covers[g.key] ? `background-image:url('${state.covers[g.key]}')` : ""}"></div>
      <div class="banner-shade"></div>
      ${mergePairHtml(g, t)}
      <div class="banner-content">
        <div class="eyebrow">Host game${g.engine ? " · " + esc(g.engine) : ""}</div>
        <h1>${esc(g.name)}</h1>
        <p>${esc(t ? t.description : "")}</p>
        ${g.warning ? `<div class="note small">${esc(g.warning)}</div>` : ""}
        <div class="row">
          <label for="themePick" class="muted">Merge with</label>
          <select id="themePick" class="input">${opts}</select>
          <button class="btn big" id="previewBtn">Preview</button>
          <button class="btn primary big" id="mergeBtn">Merge</button>
        </div>
      </div></section>`;
  } else {
    banner = `<section class="banner"><div class="banner-content">
        <div class="eyebrow">Welcome</div><h1>Pick a Host game</h1>
        <p>Any game can be the Host game. Cyberpunk 2077, Schedule I and GTA V have hand-made recipes; for every other game Frankenstein works out its engine and mod loader. If yours isn't found below, add its install folder.</p>
        <div class="row"><button class="btn primary" id="addFolder2">Add game folder</button></div>
      </div></section>`;
  }
  const partner = t?.source_key ?? null;
  const tiles = state.games.map((x) => `
      <button class="tile ${x.base ? "base" : ""} ${x.key === state.selected ? "selected picked" : ""} ${x.key === partner ? "partner picked" : ""}" data-key="${x.key}" data-cover="${x.key}" aria-pressed="${x.key === state.selected}">
        ${x.key === state.selected ? `<span class="pick-badge host">Host</span>` : x.key === partner ? `<span class="pick-badge">Extension</span>` : ""}
        ${caseHtml(x.name, state.covers[x.key])}
        ${x.key === state.selected ? "" : x.base ? `<span class="make-host" data-host="${x.key}">Make host</span>` : `<span class="make-host off" title="Frankenstein couldn't read this game's folder">Can't host</span>`}
        <div class="tile-name">${esc(x.name)}</div>
        <div class="tile-sub">${esc(x.source)} · ${x.key === state.selected ? "Host game" : x.moddable === "no" ? "no mod support" : x.moddable === "limited" ? "limited mods" : "moddable"}${x.creative ? ` · <span class="creative-tag" title="Has a creative / sandbox mode">creative</span>` : ""}</div>
      </button>`).join("");
  el.innerHTML = `${banner}
    <section class="shelf">
      <div class="shelf-head"><h2>Your games</h2>
        <div class="row"><span class="muted small">Make host picks the Host game · the glowing pair is this merge</span>
          <button class="btn ghost" id="rescan">Rescan</button><button class="btn" id="addFolder">Add game folder</button></div></div>
      ${state.games.length ? `<div class="grid">${tiles}</div>` : `<div class="empty">No games found yet.</div>`}
    </section>
    <section class="shelf" id="ownedShelf"></section>`;
  renderOwnedShelf();
  el.querySelectorAll(".tile").forEach((b) => (b.onclick = () => {
    const x = game(b.dataset.key);
    // a game that one of the host's themes merges in: pick it as the second game
    const th = !x || x.key === state.selected ? null : g?.themes.find((y) => y.source_key === x.key);
    if (th && th.id !== state.themeId) { state.themeId = th.id; return renderLibrary(); }
    if (!x.base) return toast(`Frankenstein couldn't read ${x.name}'s folder. Try Rescan.`);
    state.selected = x.key;
    state.themeId = x.themes[0]?.id;
    renderLibrary();
  }));
  el.querySelectorAll(".make-host[data-host]").forEach((h) => (h.onclick = (e) => {
    e.stopPropagation();
    const x = game(h.dataset.host);
    state.selected = x.key;
    state.themeId = x.themes[0]?.id;
    renderLibrary();
  }));
  $("#rescan").onclick = scan;
  loadFun();
  loadPads();
  const add = async () => {
    try { const r = await call("add_game_folder"); if (r) { state.games = r; await scan(); } } catch (e) { toast(errText(e)); }
  };
  $("#addFolder").onclick = add;
  if ($("#addFolder2")) $("#addFolder2").onclick = add;
  if ($("#themePick")) $("#themePick").onchange = (e) => { state.themeId = e.target.value; renderLibrary(); };
  if ($("#mergeBtn")) $("#mergeBtn").onclick = startSetup;
  if ($("#previewBtn")) $("#previewBtn").onclick = () => previewMerge(state.selected, state.themeId);
}

// Top right of the banner: this merge at a glance, host on the left, the game merged in on the right.
function mergePairHtml(g, t) {
  if (!t) return "";
  const other = t.source_key != null ? game(t.source_key) : null;
  const card = (name, key, label) => `<div class="pair-card">
      <div class="pair-cover ${key != null && state.covers[key] ? "" : "placeholder"}" data-cover="${key ?? ""}" style="background-image:url('${(key != null && state.covers[key]) || genCover(name)}')">${key != null && state.covers[key] ? "" : esc(name)}</div>
      <div class="pair-label"><span class="muted small">${label}</span><strong>${esc(name)}</strong></div></div>`;
  return `<aside class="merge-pair" aria-label="This merge">
    <div class="eyebrow">This merge</div>
    <div class="pair-row">${card(g.name, g.key, "Host")}<div class="pair-fuse" aria-hidden="true">+</div>${card(other ? other.name : t.from_game || t.name, other ? other.key : null, "Extension")}</div></aside>`;
}

// ---------- merge setup ----------
async function startSetup() {
  const g = game(state.selected), t = theme();
  try {
    const o = await call("merge_options", g.key, t.id);
    state.setup = {
      key: g.key, themeId: t.id, options: o,
      frameworks: new Set(o.frameworks.map((f) => f.id)),
      manual: {}, mods: [], check: { mods: [], clashes: [] },
      mode: o.mode, name: o.default_name, destination: o.destination, tagline: "", names: [],
      loader: o.minecraft ? "fabric" : null, version: null,
    };
    show("setup");
    nameMerge(state.setup);
    if (o.minecraft) pickLoader(state.setup, "fabric");
  } catch (e) { toast(errText(e)); }
}

// Every merge is its own new game: the AI names it and gives it a tagline (the player can change it).
async function nameMerge(s) {
  const btn = $("#mReroll");
  if (btn) { btn.disabled = true; btn.textContent = "Thinking…"; }
  try {
    const id = await call("merge_identity", s.key, s.themeId, s.names);
    if (state.setup === s && !s.named) {
      s.name = id.title; s.tagline = id.tagline; s.names.push(id.title);
    }
  } catch (e) { /* keep the plain name */ }
  if (state.setup === s && state.view === "setup") renderSetup();
}

// Minecraft: the loader decides which Minecraft version (and which mods) the merge uses.
async function pickLoader(s, loader) {
  s.loader = loader; s.version = null;
  if (state.view === "setup") renderSetup();
  try { s.version = await call("mc_version", loader); } catch (e) { toast(errText(e)); }
  if (state.setup === s && state.view === "setup") renderSetup();
}

const MODES = [
  ["copy", "Full copy (recommended)", "A completely separate copy of the game that Frankenstein can change freely. Your original install is never touched."],
  ["hardlink", "Space saver", "A separate copy that shares only big, read-only game data with the original. Anything the game or a mod could write gets its own copy. Same drive only."],
];

function renderSetup() {
  const s = state.setup;
  if (!s) return show("library");
  const g = game(s.key), t = g.themes.find((x) => x.id === s.themeId);
  const checkFor = (path) => s.check.mods.find((m) => m.file === basename(path));
  const fw = s.options.frameworks.map((f) => `
      <div class="check">
        <input type="checkbox" id="fw-${esc(f.id)}" data-fw="${esc(f.id)}" ${s.frameworks.has(f.id) ? "checked" : ""}>
        <label for="fw-${esc(f.id)}" class="grow">${esc(f.name)}</label>
        ${f.manual_url ? `<button class="btn ghost" data-url="${esc(f.manual_url)}">Get it</button>
          <button class="btn" data-manual="${esc(f.id)}">${s.manual[f.id] ? esc(basename(s.manual[f.id])) : "Pick file"}</button>` : `<span class="muted small">Downloads automatically</span>`}
      </div>`).join("");
  const slots = s.options.slots.map((sl, i) => {
    const files = s.mods.map((m, j) => [m, j]).filter(([m]) => m.slot === sl.name).map(([m, j]) => {
      const c = checkFor(m.path);
      const info = !c ? "Checking…" : c.error ? c.error : `Installs ${c.paths.length} file${c.paths.length === 1 ? "" : "s"}${c.unknown.length ? `, ${c.unknown.length} not recognised` : ""}`;
      return `<div class="file"><span class="name">${esc(basename(m.path))}</span><span class="info ${c && (c.error || !c.paths.length) ? "bad" : ""}">${esc(info)}</span>
        <button class="icon-btn" data-remove="${j}" aria-label="Remove ${esc(basename(m.path))}"><svg viewBox="0 0 24 24"><path d="M18 6 6 18M6 6l12 12"/></svg></button></div>`;
    }).join("");
    return `<div class="slot ${sl.hero ? "hero" : ""}"><div class="slot-head"><span class="slot-name">${sl.hero ? `<span class="tag live" title="The Extension game's most iconic thing: brought over first">★ Hero</span> ` : ""}${esc(sl.name)}</span>
        ${files ? `<span class="tag">Your pick</span>` : `<span class="tag live" title="Frankenstein finds and installs the best mod for this when you press Merge it">Auto</span>`}
        <span class="muted small">or pick yourself:</span>
        <button class="btn ghost" data-find="${i}">Find mods</button><button class="btn ghost" data-add="${i}">Add file</button></div>${files}</div>`;
  }).join("");
  const clashes = s.check.clashes.length
    ? `<div class="note"><strong>Heads up:</strong> these mods change the same files, so the one added last wins.<ul>${s.check.clashes.map((c) => `<li>${esc(c)}</li>`).join("")}</ul>Switch one off later in Mods &amp; profiles if it causes trouble.</div>` : "";
  const modes = MODES.map(([id, name, text]) => `<button class="mode ${s.mode === id ? "on" : ""}" data-mode="${id}" aria-pressed="${s.mode === id}"><strong>${name}</strong><span>${text}</span></button>`).join("");

  $("#view-setup").innerHTML = `<div class="page">
    <div><button class="btn ghost" id="back">← Library</button></div>
    <div class="setup-head">
      <div data-cover="${g.key}">${caseHtml(g.name, state.covers[g.key])}</div>
      <div><div class="eyebrow">New merge</div><h1>${esc(g.name)} <span class="versus">×</span> ${esc(t.from_game)}</h1>
        <p class="lede" style="margin-top:6px">${esc(t.description)}</p></div>
    </div>
    ${g.warning ? `<div class="note">${esc(g.warning)}</div>` : ""}
    ${s.options.minecraft ? `<div class="card"><div class="section-title"><span class="step">0</span><h2>Mod loader</h2></div>
      <div class="row"><select class="input" id="mcLoader">${Object.entries(s.options.minecraft.loaders).map(([id, n]) => `<option value="${id}" ${s.loader === id ? "selected" : ""}>${esc(n)}</option>`).join("")}</select>
        <span class="muted">${s.version ? `Minecraft ${esc(s.version)}` : "Finding the newest Minecraft version…"}</span></div>
      <p class="muted small">Fabric is quickest to set up. Mods only show if they work with the loader and version you pick.</p></div>` : ""}
    <div class="card"><div class="section-title"><span class="step">1</span><h2>What it brings over</h2></div>
      <p class="muted" style="margin-top:-6px">You don't need to pick anything. When you press <strong>Merge it</strong>, Frankenstein searches ${esc((s.options.sources || ["Nexus Mods"]).join(", "))} and installs the best mod for each part below by itself, plus its own built-in mod where it has one. Afterwards "What it adds" shows what it got and anything it couldn't get on its own. Only want a specific mod? Use Find mods or Add file for that part.</p>
      ${slots}${clashes}</div>
    <div class="card" id="planCard"><div class="section-title"><span class="step">2</span><h2>Merge plan</h2></div>
      <p class="muted" style="margin-top:-6px">What ${esc(g.name)} becomes once ${esc((t.from_game || t.name).split(" /")[0])} is coded onto it, on ${esc(g.name)}'s own engine. Each line says how it's done.</p>
      <div id="planBody" class="muted">Writing the plan…</div></div>
    <div class="card"><div class="section-title"><span class="step">3</span><h2>Merge it</h2></div>
      <p class="muted" style="margin-top:-6px">Everything else is handled: mod loaders download automatically, the merge goes into its own safe copy, and it gets its own save games.</p>
      <div class="row" style="justify-content:flex-end"><button class="btn primary big" id="go">Merge it</button></div></div>
    <details class="card advanced" ${s.advancedOpen ? "open" : ""}>
      <summary><h2>Advanced options</h2><span class="muted small">Only if you want to change how it's done</span></summary>
      <div class="label" style="margin:14px 0 4px">Mod loaders</div>${fw}
      <div class="label" style="margin:16px 0 8px">Where to put it</div>
      <div class="modes">${modes}</div>
      <div class="two" style="margin-top:14px">
        <div class="field"><label for="mName">Your new game's name</label>
          <div class="row" style="flex-wrap:nowrap"><input class="input" id="mName" value="${esc(s.name)}"><button class="btn" id="mReroll" title="Make up another one">New name</button></div>
          ${s.tagline ? `<div class="muted small" style="margin-top:6px">“${esc(s.tagline)}”</div>` : ""}</div>
        <div class="field"><label for="mDest">Folder</label><div class="row" style="flex-wrap:nowrap"><input class="input" id="mDest" value="${esc(s.mode === "inplace" ? g.path : s.destination)}" ${s.mode === "inplace" ? "disabled" : ""}>
          <button class="btn" id="mBrowse" ${s.mode === "inplace" ? "disabled" : ""}>Browse</button></div></div>
      </div></details>
  </div>`;

  const v = $("#view-setup");
  if (s.planHtml) { $("#planBody").innerHTML = s.planHtml; $("#planBody").classList.remove("muted"); }
  else loadPlan(() => call("merge_plan", g.key, t.id), $("#planBody"), 0, (h) => (s.planHtml = h));
  $("#back").onclick = () => show("library");
  v.querySelector(".advanced").ontoggle = (e) => (s.advancedOpen = e.target.open);
  v.querySelectorAll("[data-fw]").forEach((c) => (c.onchange = () => { c.checked ? s.frameworks.add(c.dataset.fw) : s.frameworks.delete(c.dataset.fw); }));
  v.querySelectorAll("[data-url]").forEach((b) => (b.onclick = () => call("open_url", b.dataset.url)));
  v.querySelectorAll("[data-manual]").forEach((b) => (b.onclick = async () => {
    const p = await call("pick_file"); if (p) { s.manual[b.dataset.manual] = p; renderSetup(); }
  }));
  v.querySelectorAll("[data-find]").forEach((b) => (b.onclick = () => {
    const slot = s.options.slots[+b.dataset.find];
    watcher.slot = slot.name;
    modBrowser({ recipe: g.recipe_id, title: slot.name, search: slot.search, sources: s.options.sources, loader: s.loader, version: s.version,
      onFiles: (files) => files.forEach((f) => addToSetup(f.path, f.name)), onOpened: () => watchDownloads(slot.name) });
  }));
  if ($("#mcLoader")) $("#mcLoader").onchange = (e) => pickLoader(s, e.target.value);
  v.querySelectorAll("[data-add]").forEach((b) => (b.onclick = async () => {
    const files = await call("pick_mod_files");
    if (!files || !files.length) return;
    files.forEach((p) => s.mods.push({ slot: s.options.slots[+b.dataset.add].name, path: p }));
    renderSetup();
    checkMods();
  }));
  v.querySelectorAll("[data-remove]").forEach((b) => (b.onclick = () => { s.mods.splice(+b.dataset.remove, 1); renderSetup(); checkMods(); }));
  v.querySelectorAll("[data-mode]").forEach((b) => (b.onclick = () => { s.mode = b.dataset.mode; renderSetup(); }));
  $("#mName").oninput = (e) => { s.name = e.target.value; s.named = true; };
  $("#mReroll").onclick = () => { s.named = false; nameMerge(s); };
  $("#mDest").oninput = (e) => { if (s.mode !== "inplace") s.destination = e.target.value; };
  $("#mBrowse").onclick = async () => { const p = await call("pick_folder"); if (p) { s.destination = p; renderSetup(); } };
  $("#go").onclick = runMerge;
}

// ---------- Nexus Mods inside the app ----------
function addToSetup(path, name) {
  const s = state.setup;
  if (s && state.view === "setup") {
    const slot = watcher.slot || s.options.slots[0]?.name || "Mods";
    s.mods.push({ slot, path });
    renderSetup(); checkMods();
    toast(`Added ${name} to “${slot}”.`);
  } else {
    toast(`Downloaded ${name}. Open a merge setup or use Add mods to put it in.`, 5000);
  }
}

// One search across every mod site that has the game (Nexus Mods, CurseForge, Modrinth), plus any link.
// o: {recipe, title, search, sources, loader, version, onFiles(files), onOpened()}
async function modBrowser(o) {
  const sources = o.sources && o.sources.length ? o.sources : ["Nexus Mods"];
  let only = "all";
  const nx = await call("nexus_status").catch(() => ({}));
  const render = (list) => list.length ? list.map((m, i) => `<div class="row mod-hit">
      ${m.picture ? `<img src="${esc(m.picture)}" alt="" class="mod-pic">` : `<div class="mod-pic"></div>`}
      <div class="grow"><strong>${esc(m.name)}</strong> <span class="src-tag src-${esc(m.source.split(" ")[0].toLowerCase())}">${esc(m.source)}</span>
        <div class="muted small">by ${esc(m.author || "?")} · ${Number(m.downloads || 0).toLocaleString()} ${m.source === "Nexus Mods" ? "endorsements" : "downloads"}</div>
        <div class="muted small">${esc(m.summary)}</div></div>
      <button class="btn primary" data-get="${i}">Get</button></div>`).join("") : `<p class="muted">Nothing found. Try other words.</p>`;
  const done = modal("Find mods: " + o.title, `
    <div class="row" style="flex-wrap:nowrap"><input class="input" id="mq" value="${esc(o.search)}" autocomplete="off"><button class="btn" id="mgo" type="button">Search</button></div>
    ${sources.length > 1 ? `<div class="row chips" id="msrc"><button class="chip on" data-src="all">All</button>${sources.map((x) => `<button class="chip" data-src="${esc(x)}">${esc(x)}</button>`).join("")}</div>` : ""}
    <div id="mres" class="mod-results"><p class="muted">Searching…</p></div>
    <div class="row" style="flex-wrap:nowrap;margin-top:12px"><input class="input" id="mlink" placeholder="Or paste a download link (https://…)" autocomplete="off"><button class="btn" id="mlinkGo" type="button">Download</button><button class="btn ghost" id="mfile" type="button">Pick a file</button></div>
    ${sources.includes("Nexus Mods") && !nx.premium ? `<p class="muted small">Nexus Mods: Get opens the mod's Files page. ${nx.has_key ? "Press <strong>Mod Manager Download</strong> there and it lands here automatically." : "Download it there and Frankenstein adds it from your Downloads folder."}</p>` : ""}
    ${sources.includes("CurseForge") ? "" : `<p class="muted small">Add a CurseForge API key in Settings to search CurseForge too.</p>`}`, [{ label: "Done", value: true, primary: true }]);
  $("#modal .modal-box").classList.add("wide");
  done.then(() => $("#modal .modal-box").classList.remove("wide"));
  let list = [];
  const got = (r) => { if (r && r.files && r.files.length) { o.onFiles(r.files); toast(`Added ${r.files.map((f) => f.name).join(", ")}.`); } };
  const run = async () => {
    $("#mres").innerHTML = `<p class="muted">Searching…</p>`;
    let r;
    try { r = await call("mod_search", o.recipe, $("#mq").value, only, o.loader || null, o.version || null); } catch (e) { $("#mres").innerHTML = `<p class="note">${esc(errText(e))}</p>`; return; }
    list = r.results;
    $("#mres").innerHTML = (r.errors.length ? `<p class="note small">${r.errors.map(esc).join("<br>")}</p>` : "") + render(list);
    $("#mres").querySelectorAll("[data-get]").forEach((b) => (b.onclick = async () => {
      const m = list[+b.dataset.get];
      b.disabled = true;
      try {
        const job = await call("mod_get", o.recipe, m.source, m.id, o.loader || null, o.version || null);
        if (!job) { b.textContent = "Opened"; o.onOpened && o.onOpened(); return; }
        const res = (await followJob(job, "Downloading " + m.name))?.result;
        got(res);
      } catch (e) { toast(errText(e)); b.disabled = false; }
    }));
  };
  $("#mgo").onclick = run;
  $("#mq").onkeydown = (e) => { if (e.key === "Enter") { e.preventDefault(); run(); } };
  document.querySelectorAll("#msrc [data-src]").forEach((c) => (c.onclick = () => {
    only = c.dataset.src;
    document.querySelectorAll("#msrc .chip").forEach((x) => x.classList.toggle("on", x === c));
    run();
  }));
  $("#mlinkGo").onclick = async () => {
    try { got((await followJob(await call("mod_link", $("#mlink").value), "Downloading"))?.result); } catch (e) { toast(errText(e)); }
  };
  $("#mfile").onclick = async () => {
    const files = await call("pick_mod_files");
    if (files && files.length) got({ files: files.map((p) => ({ path: p, name: basename(p) })) });
  };
  run();
  return done;
}

// Files and links handed to the app (double-clicked .gmerge files, Nexus "Mod Manager Download").
// ---------- accounts ----------
const initials = (n) => (n || "?").trim().split(/\s+/).map((w) => w[0]).join("").slice(0, 2).toUpperCase();

// "Who's playing?" when there's more than one account or a PIN. Resolves once someone is in.
async function whoIsPlaying(force = false) {
  const st = await atMost(call("profiles_state"), 8000).catch(() => null);
  state.profiles = st;
  if (!st || (!st.locked && !force)) return renderMe();
  const el = document.createElement("div");
  el.className = "who";
  document.body.appendChild(el);
  await new Promise((resolve) => {
    const pick = (p, err = "") => {
      if (!p.has_pin) return go(p, "");
      let pin = "";
      el.innerHTML = `<div class="who-box"><div class="who-avatar big" style="--c:${esc(p.color)}">${esc(initials(p.name))}</div>
        <h2>${esc(p.name)}</h2><p class="muted">${err ? `<span class="bad">${esc(err)}</span>` : "Enter your PIN"}</p>
        <div class="pin-dots" aria-live="polite"></div>
        <div class="pin-pad">${[1, 2, 3, 4, 5, 6, 7, 8, 9, "←", 0, "OK"].map((k) => `<button class="pin-key" data-k="${k}">${k}</button>`).join("")}</div>
        <button class="btn ghost" id="whoBack">Back</button></div>`;
      const dots = () => (el.querySelector(".pin-dots").textContent = "●".repeat(pin.length) || " ");
      dots();
      const press = (k) => {
        if (k === "←") pin = pin.slice(0, -1);
        else if (k === "OK") return go(p, pin);
        else if (pin.length < 8) pin += k;
        dots();
      };
      el.querySelectorAll(".pin-key").forEach((b) => (b.onclick = () => press(b.dataset.k)));
      el.onkeydown = (e) => { if (/^\d$/.test(e.key)) press(e.key); else if (e.key === "Backspace") press("←"); else if (e.key === "Enter") press("OK"); };
      el.tabIndex = -1; el.focus();
      $("#whoBack").onclick = list;
    };
    const go = async (p, pin) => {
      try { state.profiles = await call("profile_switch", p.id, pin); } catch (e) { return pick(p, errText(e)); }
      el.remove(); resolve();
    };
    const list = () => {
      el.innerHTML = `<div class="who-box"><h2>Who's playing?</h2><div class="who-list">${st.profiles.map((p) => `
        <button class="who-pick" data-id="${esc(p.id)}"><div class="who-avatar" style="--c:${esc(p.color)}">${esc(initials(p.name))}</div>
        <span>${esc(p.name)}</span><span class="muted small">${p.admin ? "Admin" : ""}${p.has_pin ? " 🔒" : ""}</span></button>`).join("")}</div></div>`;
      el.querySelectorAll(".who-pick").forEach((b) => (b.onclick = () => pick(st.profiles.find((p) => p.id === b.dataset.id))));
      el.querySelector(".who-pick")?.focus();
    };
    list();
  });
  renderMe();
}

function renderMe() {
  const me = state.profiles?.current;
  const el = $("#meChip");
  if (!el || !me) return;
  el.innerHTML = `<div class="who-avatar tiny" style="--c:${esc(me.color)}">${esc(initials(me.name))}</div><span class="grow">${esc(me.name)}</span>
    <span class="muted small">${state.profiles.profiles.length > 1 || me.has_pin ? "Switch" : ""}</span>`;
  el.onclick = async () => { await call("profile_lock"); location.reload(); };
}

async function renderAccountsCard() {
  const el = $("#profilesCard");
  if (!el) return;
  const st = await call("profiles_state").catch(() => null);
  if (!st) return;
  state.profiles = st;
  const me = st.current;
  const perms = (p) => Object.entries(st.permissions).map(([k, label]) =>
    `<label class="switch small"><input type="checkbox" data-perm="${k}" ${p.perms[k] ? "checked" : ""} ${me.admin && !p.admin ? "" : "disabled"}> ${esc(label)}</label>`).join("");
  el.innerHTML = `<h2>Accounts</h2>
    <p class="muted">Everyone gets their own merges, saves, achievements and settings. ${me.admin ? "You're the admin: you add accounts, choose what each one is allowed to do, and manage the shared keys." : "The admin manages accounts and shared keys."} A PIN keeps your account to you on this PC.</p>
    ${st.profiles.map((p) => `<div class="acct" data-id="${esc(p.id)}">
      <div class="row" style="flex-wrap:nowrap"><div class="who-avatar tiny" style="--c:${esc(p.color)}">${esc(initials(p.name))}</div>
        <div class="grow"><strong>${esc(p.name)}</strong> <span class="muted small">${p.admin ? "Admin" : ""}${p.id === me.id ? " · you" : ""}${p.has_pin ? " · PIN set" : ""}</span></div>
        ${me.admin || p.id === me.id ? `<button class="btn ghost" data-act="rename">Rename</button><button class="btn ghost" data-act="pin">${p.has_pin ? "Change PIN" : "Set PIN"}</button>` : ""}
        ${me.admin && !p.admin ? `<button class="btn ghost danger" data-act="remove">Remove</button>` : ""}</div>
      ${p.admin ? "" : `<div class="acct-perms">${perms(p)}</div>`}</div>`).join("")}
    ${me.admin ? `<div class="row" style="margin-top:12px;flex-wrap:nowrap"><input class="input" id="newAcct" placeholder="New account name" maxlength="24"><button class="btn primary" id="addAcct">Add account</button></div>` : ""}
    <h3 style="margin-top:18px">Your account on your other PCs</h3>
    <p class="muted small">${st.remembered ? `This PC keeps you signed in as ${esc(me.name)}. Press your name and Switch to sign out.` : "Pick your account once and this PC keeps you signed in."}
      With cloud backup (OneDrive or Google Drive) on both PCs, your account follows you on its own. Otherwise get a link code here and paste it on the other PC.</p>
    <div class="row" style="flex-wrap:nowrap"><button class="btn ghost" id="linkGet">Get my link code</button>
      <input class="input" id="linkIn" placeholder="Paste a link code from your other PC" autocomplete="off"><button class="btn primary" id="linkGo">Link</button></div>
    <div id="devicesBox" style="margin-top:18px"></div>`;
  renderDevices();
  const ask = (title, label, type = "text") => {
    const done = modal(title, `<input class="input" id="askIn" type="${type}" inputmode="${type === "password" ? "numeric" : "text"}" placeholder="${esc(label)}" autocomplete="off">`,
      [{ label: "Cancel", value: null }, { label: "Save", value: true, primary: true }]);
    setTimeout(() => $("#askIn")?.focus(), 30);
    return done.then((ok) => (ok ? $("#askIn").value : null));
  };
  el.querySelectorAll(".acct").forEach((row) => {
    const id = row.dataset.id;
    row.querySelectorAll("[data-act]").forEach((b) => (b.onclick = async () => {
      try {
        if (b.dataset.act === "rename") { const n = await ask("Rename account", "Name"); if (n) await call("profile_update", id, n, null, null); }
        else if (b.dataset.act === "pin") { const pin = await ask("PIN", "4 to 8 numbers, or leave empty for no PIN", "password"); if (pin !== null) await call("profile_update", id, null, pin, null); }
        else if (b.dataset.act === "remove" && (await confirmBox("Remove this account?", "Its settings go. Merges it made stay on the PC.", "Remove", true))) await call("profile_remove", id);
      } catch (e) { toast(errText(e)); }
      renderAccountsCard(); renderMe();
    }));
    row.querySelectorAll("[data-perm]").forEach((c) => (c.onchange = async () => {
      const p = Object.fromEntries([...row.querySelectorAll("[data-perm]")].map((x) => [x.dataset.perm, x.checked]));
      try { await call("profile_update", id, null, null, p); toast("Saved."); } catch (e) { toast(errText(e)); renderAccountsCard(); }
    }));
  });
  $("#linkGet").onclick = async () => {
    try {
      const r = await call("profile_link_code");
      try { await navigator.clipboard.writeText(r.code); } catch (e) { /* the box below still shows it */ }
      modal("Your link code", `<p class="muted">Copied. On your other PC open Frankenstein, go to Settings > Accounts, paste it and press Link. It brings your account, admin rights, PIN and achievements.</p>
        <textarea class="input" readonly style="width:100%;height:110px;font-family:monospace;font-size:12px">${esc(r.code)}</textarea>`, [{ label: "Done", value: true, primary: true }]);
    } catch (e) { toast(errText(e)); }
  };
  $("#linkGo").onclick = async () => {
    try { state.profiles = await call("profile_link", $("#linkIn").value); toast("Linked. You're signed in on this PC now."); } catch (e) { return toast(errText(e)); }
    renderAccountsCard(); renderMe();
  };
  if ($("#addAcct")) $("#addAcct").onclick = async () => {
    try { await call("profile_add", $("#newAcct").value, "", null); toast("Account added. They pick it when Frankenstein opens."); } catch (e) { toast(errText(e)); }
    renderAccountsCard();
  };
}

// ---------- your devices: what's on your other PCs, and joining an account over Wi-Fi ----------
const ago = (t) => { const s = Date.now() / 1000 - t; return s < 90 ? "just now" : s < 3600 ? Math.round(s / 60) + " min ago" : s < 86400 ? Math.round(s / 3600) + " h ago" : Math.round(s / 86400) + " days ago"; };

async function renderDevices() {
  const el = $("#devicesBox");
  if (!el) return;
  const d = await call("devices_state").catch(() => null);
  if (!d) return;
  const merges = (list) => list.length ? list.map((m) => `<li>${esc(m.name)} <span class="muted small">${esc(m.host)}${m.extension ? " × " + esc(m.extension) : ""}${m.packed ? " · packed" : ""}</span></li>`).join("") : `<li class="muted">No merges yet</li>`;
  el.innerHTML = `<div class="row" style="flex-wrap:nowrap"><h3 class="grow">Your devices</h3><button class="btn ghost" id="devRefresh">Refresh</button></div>
    <p class="muted small">This PC is <strong>${esc(d.device)}</strong>. Every PC signed in to ${esc(d.account || "your account")} shows here with what's installed and merged on it. They find each other on the same Wi-Fi${d.cloud ? " and through your cloud folder" : ""}.${d.network ? "" : " " + esc(d.network_error || "The local network isn't on.")}</p>
    ${d.mine.length ? d.mine.map((x) => `<details class="device-card"><summary><strong>${esc(x.device)}</strong> <span class="muted small">· ${x.merges.length} merge${x.merges.length === 1 ? "" : "s"} · ${x.games.length}${x.games_cut ? "+" : ""} games · seen ${ago(x.time)} ${x.via === "network" ? "on this Wi-Fi" : "through the cloud folder"}${x.version ? " · Frankenstein " + esc(x.version) : ""}</span></summary>
      <div class="row" style="align-items:flex-start;gap:24px"><div><h4>Merges</h4><ul>${merges(x.merges)}</ul></div>
      <div><h4>Games installed</h4><ul class="small">${x.games.slice(0, 60).map((g) => `<li>${esc(g.name)} <span class="muted">${esc(g.source)}</span></li>`).join("")}${x.games.length > 60 ? `<li class="muted">and ${x.games.length - 60} more</li>` : ""}</ul></div></div></details>`).join("")
      : `<p class="muted">No other PC on ${esc(d.account || "your account")} yet. Open Frankenstein on your other PC, on the same Wi-Fi, and it shows up below to join.</p>`}
    <div class="device-card" style="margin-top:12px"><h4>Put another PC on this account</h4>
      <p class="muted small">Easiest way: press <strong>Show a pairing code</strong> on the PC that has your account, then type the code on the other PC and press <strong>Pair</strong>. Both PCs need Frankenstein open and the same Wi-Fi or router.</p>
      <div class="row" style="flex-wrap:wrap;gap:8px"><button class="btn primary" id="pairShow">Show a pairing code</button>
        <input class="input" id="pairIn" placeholder="Code from the other PC, like K7QM-3XWD" style="min-width:260px"><button class="btn" id="pairGo">Pair</button></div>
      ${d.heard ? "" : `<p class="muted small" style="margin-top:8px">This PC hasn't heard any other Frankenstein yet. Windows' firewall often blocks it on Wi-Fi marked Public. <a href="#" id="fwFix">Let Frankenstein through the firewall</a> (one Yes on Windows' prompt), on both PCs.</p>`}</div>
    ${d.nearby.length ? `<h4 style="margin-top:12px">Frankenstein on this Wi-Fi</h4>` + d.nearby.map((n) => `<div class="row" style="flex-wrap:wrap;gap:8px"><strong>${esc(n.device)}</strong>
        ${n.joinable.map((a) => `<button class="btn ghost" data-join="${esc(n.device)}" data-uid="${esc(a.uid)}">Use ${esc(a.name)}'s account on this PC</button>`).join("")}</div>`).join("") : ""}`;
  $("#devRefresh").onclick = renderDevices;
  el.querySelectorAll("[data-join]").forEach((b) => (b.onclick = () => joinDevice(b.dataset.join, b.dataset.uid, b.textContent)));
  $("#pairShow").onclick = showPairCode;
  $("#pairGo").onclick = () => pairWithCode($("#pairIn").value);
  if ($("#fwFix")) $("#fwFix").onclick = async (e) => {
    e.preventDefault();
    try { await call("device_firewall"); toast("Press Yes on Windows' prompt. Do the same on your other PC.", 8000); } catch (err) { toast(errText(err), 9000); }
  };
}

async function showPairCode() {
  let r;
  try { r = await call("device_pair_start"); } catch (e) { return toast(errText(e)); }
  await modal("Pairing code", `<p class="muted">On your other PC, open Frankenstein, go to Settings > Accounts and type this code under <strong>Put another PC on this account</strong>, then press Pair. It puts ${esc(r.account || "this account")} on that PC.</p>
    <p style="font-size:40px;letter-spacing:6px;font-family:monospace;text-align:center;margin:18px 0">${esc(r.code)}</p>
    <p class="muted small">Keep this open. The code works for ${r.minutes} minutes, on this Wi-Fi only.</p>`, [{ label: "Done", value: true, primary: true }]);
  call("device_pair_stop").catch(() => {});
}

async function pairWithCode(code) {
  try { await call("device_pair_join", code); } catch (e) { return toast(errText(e)); }
  toast("Looking for the other PC…", 4000);
  for (let i = 0; i < 30; i++) {
    await new Promise((r) => setTimeout(r, 2000));
    const r = await call("device_pair_result").catch(() => null);
    if (!r) continue;
    if (r.ok) {
      toast(`Paired with ${r.device}: this PC is on the same account now.`, 7000);
      state.profiles = r.profiles;
      renderAccountsCard(); renderMe();
      return;
    }
    if (r.why) return toast(r.why, 7000);
  }
  const r = await call("device_pair_result").catch(() => ({}));
  toast(r.heard ? "The other PC isn't showing that code. Check it, or press Show a pairing code again there."
    : "Couldn't hear the other PC. Press \"Let Frankenstein through the firewall\" below on both PCs, check they're on the same Wi-Fi, and try again.", 10000);
  renderDevices();
}

async function joinDevice(device, uid, label) {
  try { await call("device_join", device, uid); } catch (e) { return toast(errText(e)); }
  toast(`Asked ${device}. Press Yes on that PC.`, 8000);
  for (let i = 0; i < 60; i++) {
    await new Promise((r) => setTimeout(r, 2000));
    const r = await call("device_join_result", device).catch(() => null);
    if (!r || r.waiting) continue;
    if (!r.ok) return toast(r.why, 6000);
    toast("Done: this PC is on the same account now.", 6000);
    state.profiles = r.profiles;
    renderAccountsCard(); renderMe();
    return;
  }
  toast(`${device} didn't answer. Open Frankenstein there and try again.`, 6000);
}

// someone on this Wi-Fi asking to join one of this PC's accounts
const seenJoin = new Set();
async function watchJoinRequests() {
  const d = await call("devices_state").catch(() => null);
  for (const r of d?.requests || []) {
    if (seenJoin.has(r.id)) continue;
    seenJoin.add(r.id);
    const yes = await modal("Join your account?", `<p><strong>${esc(r.device)}</strong> on your Wi-Fi wants to use <strong>${esc(r.account)}</strong>'s account. Say yes only if it's your own PC: it gets this account's merges list, settings and achievements.</p>`,
      [{ label: "No", value: false }, { label: "Yes, it's mine", value: true, primary: true }]);
    try { await call("device_join_answer", r.id, yes); if (yes) toast(`${r.device} is on your account now.`); } catch (e) { toast(errText(e)); }
  }
}
setInterval(watchJoinRequests, 4000);

// ---------- Discord status ----------
async function renderDiscord() {
  const el = $("#discordCard");
  if (!el) return;
  const d = await call("discord_set", null, null).catch(() => null);
  if (!d) return;
  const admin = state.settings?.admin;
  el.innerHTML = `<h2>Discord status</h2>
    <label class="switch"><input type="checkbox" id="discordOn" ${d.on ? "checked" : ""}> Show the merge I'm playing on my Discord profile</label>
    ${d.has_id ? `<p class="muted small">Discord shows "Playing Frankenstein" with your merge's name while you play. Discord has to be open on this PC.</p>`
      : `<p class="muted">Discord needs an Application ID before it can show Frankenstein. ${admin ? `Make one in a minute: open <a href="#" id="discordDev">discord.com/developers/applications</a>, press <strong>New Application</strong>, call it Frankenstein, then copy its <strong>Application ID</strong> here.` : "Ask the admin to set it up in Settings."}</p>
         ${admin ? `<div class="row" style="flex-wrap:nowrap"><input class="input" id="discordId" placeholder="Application ID" inputmode="numeric"><button class="btn primary" id="discordSave">Save</button></div>` : ""}`}`;
  $("#discordOn").onchange = () => call("discord_set", $("#discordOn").checked, null);
  if ($("#discordDev")) $("#discordDev").onclick = (e) => { e.preventDefault(); call("open_url", "https://discord.com/developers/applications"); };
  if ($("#discordSave")) $("#discordSave").onclick = async () => {
    try { await call("discord_set", null, $("#discordId").value); toast("Discord's hooked up."); renderDiscord(); } catch (e) { toast(errText(e)); }
  };
}

// ---------- achievements ----------
async function loadAwards() {
  const el = $("#view-awards");
  const list = await call("achievements").catch(() => []);
  const got = list.filter((a) => a.unlocked).length;
  el.innerHTML = `<div class="page"><h1>Achievements</h1>
    <p class="muted">${got} of ${list.length} unlocked. They come from things you do in Frankenstein.</p>
    <div class="awards">${list.map((a) => `<div class="award ${a.unlocked ? "got" : ""}">
      <div class="award-icon" aria-hidden="true">${a.unlocked ? "★" : "☆"}</div>
      <div><strong>${esc(a.title)}</strong><div class="muted small">${esc(a.how)}</div>
      ${a.unlocked ? `<div class="small award-date">Unlocked ${new Date(a.unlocked * 1000).toLocaleDateString()}</div>` : ""}</div></div>`).join("")}</div></div>`;
}

async function checkAwards() {
  const fresh = await call("achievements_new").catch(() => []);
  for (const a of fresh) {
    toast(`🏆 Achievement unlocked: ${a.title}`, 5000);
    if (state.view === "awards") loadAwards();
  }
}

async function checkHandoffs() {
  checkAwards();
  const items = await call("handoffs").catch(() => []);
  for (const a of items) {
    if (/^nxm:\/\//i.test(a)) {
      try {
        const r = (await followJob(await call("nexus_link", a), "Downloading from Nexus"))?.result;
        if (r) addToSetup(r.path, r.name);
      } catch (e) { modal("That didn't work", `<p>${esc(errText(e))}</p>`); }
    } else await importMerge(a);
  }
}

// After "Find mods", watch the Downloads folder and add new mod downloads automatically.
const watcher = { timer: null, since: 0, slot: null, until: 0 };
async function watchDownloads(slotName) {
  watcher.slot = slotName;
  watcher.until = Date.now() + 20 * 60 * 1000;
  if (!watcher.timer) {
    watcher.since = await call("now").catch(() => Date.now() / 1000);
    watcher.timer = setInterval(checkDownloads, 3000);
  }
  toast("Download any mod you like. It's added to “" + slotName + "” automatically.", 4500);
}
async function checkDownloads() {
  const s = state.setup;
  if (!s || state.view !== "setup" || Date.now() > watcher.until) { clearInterval(watcher.timer); watcher.timer = null; return; }
  let found = [];
  try { found = await call("new_downloads", watcher.since); } catch (e) { return; }
  let added = 0;
  for (const d of found) {
    watcher.since = Math.max(watcher.since, d.time);
    if (s.mods.some((m) => m.path === d.path)) continue;
    s.mods.push({ slot: watcher.slot, path: d.path });
    toast(`Added ${d.name} to “${watcher.slot}”.`);
    added++;
  }
  if (added) { renderSetup(); checkMods(); }
}

async function checkMods() {
  const s = state.setup;
  const g = game(s.key);
  try { s.check = await call("check_mods", g.recipe_id, s.mods); } catch (e) { toast(errText(e)); }
  if (state.view === "setup") renderSetup();
}

async function runMerge() {
  const s = state.setup;
  const fw = s.options.frameworks.filter((f) => s.frameworks.has(f.id));
  if (s.mode === "inplace" && !(await confirmBox("Change the original install?", "Every replaced file is backed up and Undo puts it back. Continue?"))) return;
  if (!s.name.trim()) return toast("Give your merge a name.");
  let id;
  try {
    id = await call("start_merge", {
      key: s.key, theme_id: s.themeId, mode: s.mode, name: s.name, tagline: s.tagline, destination: s.destination,
      frameworks: fw.map((f) => f.id), manual_files: s.manual, mods: s.mods, mc_loader: s.loader, mc_version: s.version,
    });
  } catch (e) { return modal("Couldn't start", `<p>${esc(errText(e))}</p>`); }
  const res = await followJob(id, "Merging " + s.name);
  if (res && res.result) {
    await playFusion(res.result.merge_id);
    const p = res.result.problems;
    const adds = await call("merge_adds", res.result.merge_id).catch(() => null);
    const body = `<p>“${esc(res.result.name)}” is ready. Press Play in My merges.</p>` + (adds ? addsHtml(adds) : "") +
      (p.length ? `<div class="note">Some parts didn't install:<ul>${p.map((x) => `<li>${esc(x)}</li>`).join("")}</ul>Add them later with Add mods on the merge.</div>` : "");
    const go = await modal("Merge complete", body, [{ label: "Stay here", value: false }, { label: "Go to My merges", value: true, primary: true }]);
    state.setup = null;
    show(go ? "merges" : "library");
  }
}

// The two games spin in, fuse in a violet flash and come out as one box (about 3 seconds, skippable).
async function playFusion(mergeId) {
  let art;
  try { art = await call("fusion_art", mergeId); } catch (e) { return; }
  const box = (cover, name, cls) => `<div class="fz-box ${cls}">${caseHtml(name, cover)}</div>`;
  const el = document.createElement("div");
  el.className = "fusion";
  el.setAttribute("role", "dialog");
  el.setAttribute("aria-label", "Merging " + art.name);
  el.innerHTML = `${box(art.base, art.base_name, "fz-left")}${box(art.source, art.source_name, "fz-right")}
    <div class="fz-flash"></div><canvas class="fz-sparks"></canvas>
    ${box(art.merged, art.name, "fz-merged")}
    <div class="fz-title"><div class="eyebrow">It's alive</div><h1>${esc(art.name)}</h1>${art.tagline ? `<p class="muted">${esc(art.tagline)}</p>` : ""}</div>
    <button class="btn ghost fz-skip">Skip</button>`;
  document.body.appendChild(el);
  const reduce = matchMedia("(prefers-reduced-motion: reduce)").matches;
  if (reduce) el.classList.add("fz-instant");
  sparks(el.querySelector(".fz-sparks"), reduce ? 0 : 1500);
  await new Promise((resolve) => {
    let t;
    const end = () => { clearTimeout(t); document.removeEventListener("keydown", key, true); el.classList.add("fz-out"); setTimeout(() => el.remove(), 350); resolve(); };
    const key = (e) => { if (e.key === "Escape" || e.key === "Enter" || e.key === " ") { e.preventDefault(); e.stopPropagation(); end(); } };
    document.addEventListener("keydown", key, true);
    el.onclick = end;
    el.querySelector(".fz-skip").focus();
    t = setTimeout(end, reduce ? 1500 : 4200);
  });
}

// Violet sparks bursting out from the middle when the two boxes hit.
function sparks(cv, delay) {
  const w = (cv.width = innerWidth), h = (cv.height = innerHeight), ctx = cv.getContext("2d");
  const ps = Array.from({ length: 140 }, () => {
    const a = Math.random() * Math.PI * 2, v = 3 + Math.random() * 9;
    return { x: w / 2, y: h / 2, vx: Math.cos(a) * v, vy: Math.sin(a) * v, life: 1, grey: Math.random() < 0.35 };
  });
  setTimeout(() => {
    const step = () => {
      if (!cv.isConnected) return;
      ctx.clearRect(0, 0, w, h);
      let alive = 0;
      for (const p of ps) {
        if (p.life <= 0) continue;
        alive++;
        p.x += p.vx; p.y += p.vy; p.vx *= 0.965; p.vy *= 0.965; p.life -= 0.012;
        ctx.globalAlpha = Math.max(p.life, 0);
        ctx.fillStyle = p.grey ? "#ece8f5" : "#a78bfa";
        ctx.beginPath(); ctx.arc(p.x, p.y, 2.2, 0, Math.PI * 2); ctx.fill();
      }
      if (alive) requestAnimationFrame(step);
    };
    step();
  }, delay);
}

// Shows a progress window for a background job until it finishes.
async function followJob(id, title) {
  $("#modalTitle").textContent = title;
  // merges also get the little terminal window that streams the rebuild, file by file
  if (/^(Merging|Filling|Upgrading|Unpacking|Rebuilding)/.test(title) && state.settings?.merge_terminal !== false) call("job_terminal", id, title).catch(() => {});
  $("#modalBody").innerHTML = `<div class="bar indeterminate" id="jobBar"><div></div></div><div class="log" id="jobLog"></div>`;
  $("#modalActions").innerHTML = "";
  $("#modal").classList.remove("hidden");
  let since = 0;
  while (true) {
    let j;
    try { j = await call("job", id, since); } catch (e) { j = { log: [], done: true, error: errText(e) }; }
    since = j.next ?? since;
    const log = $("#jobLog");
    if (log && j.log.length) { log.textContent += j.log.join("\n") + "\n"; log.scrollTop = log.scrollHeight; }
    const bar = $("#jobBar");
    if (bar && j.progress) { bar.classList.remove("indeterminate"); bar.firstElementChild.style.width = (100 * j.progress[0] / Math.max(j.progress[1], 1)).toFixed(1) + "%"; }
    else if (bar) { bar.classList.add("indeterminate"); bar.firstElementChild.style.width = "30%"; }
    if (j.done) {
      closeModal();
      if (j.error) { await modal("That didn't work", `<p>${esc(j.error)}</p><div class="log">${esc($("#jobLog")?.textContent || "")}</div>`); return null; }
      return j;
    }
    await sleep(350);
  }
}

// ---------- Merge plan: the Host converted into the Extension, honest per engine ----------
const PLAN_LEVEL = { built: ["Built by Frankenstein", "built"], mod: ["Mod", "mod"], "stand-in": ["Stand-in", "stand"], "can't": ["Not possible yet", "cant"] };
function planHtml(p) {
  return `<p class="plan-sum">${esc(p.summary)}${p.by === "ai" ? ` <span class="muted small">(worded by AI)</span>` : ""}</p>
    ${p.sections.map((sec) => `<div class="plan-sec"><h3>${esc(sec.name)}</h3>${sec.rows.map((r) => `<div class="plan-row">
      <span class="plan-lv ${PLAN_LEVEL[r.level][1]}">${PLAN_LEVEL[r.level][0]}</span>
      <div><strong>${esc(r.part)}:</strong> ${esc(r.plan)}<div class="muted small">${esc(r.how)}</div></div></div>`).join("")}</div>`).join("")}`;
}
async function loadPlan(get, el, tries = 0, keep = null) {
  try {
    const r = await get();
    if (!el.isConnected) return;
    el.classList.remove("muted");
    el.innerHTML = planHtml(r.plan) + (r.ready ? "" : `<p class="muted small">The AI is writing a fuller plan; it shows up here in a moment.</p>`);
    if (r.ready && keep) keep(el.innerHTML);
    if (!r.ready && tries < 6) setTimeout(() => el.isConnected && loadPlan(get, el, tries + 1, keep), 5000);
  } catch (e) { el.textContent = errText(e); }
}

// what a merge brings over from the Extension game, and what it couldn't
function addsHtml(a) {
  const heroFirst = [...a.adds].sort((x, y) => (y.hero ? 1 : 0) - (x.hero ? 1 : 0));
  const real = heroFirst.filter((x) => x.ok && !x.standin), own = heroFirst.filter((x) => x.ok && x.standin), miss = heroFirst.filter((x) => !x.ok);
  const star = (x) => (x.hero ? `<span class="tag live">★ Hero</span> ` : "");
  const close = (x) => (x.close ? ` <span class="tag" title="How close Play as gets in this game">How close: ${esc(x.close)}</span>` : "");
  const picks = (x) => (x.picks || []).length ? `<div class="small muted">Want the real mod? On Nexus Mods (only Premium lets apps download): ${x.picks.map((k) => `<a href="#" data-ext="${esc(k.url)}">${esc(k.name)}</a>`).join(" · ")}. Press <strong>Mod Manager Download</strong> there and Frankenstein adds it.</div>` : "";
  const line = (x) => `<li>${star(x)}<strong>${esc(x.slot)}</strong>: ${esc(x.mod)} <span class="muted small">from ${esc(x.source)}${x.extra ? `, plus ${x.extra} mod${x.extra > 1 ? "s" : ""} it needs` : ""}${x.creative ? " · creative only" : ""}</span>${close(x)}</li>`;
  const ownLine = (x) => `<li>${star(x)}<strong>${esc(x.slot)}</strong>: ${esc(x.mod)} <span class="tag">${x.standin === "story" ? "story stand-in" : "built into Frankenstein"}</span>${x.creative ? ` <span class="muted small">creative only</span>` : ""}${close(x)}${picks(x)}</li>`;
  return `<div class="adds">
    ${real.length || a.builtin.length ? `<p class="muted">Mods brought over${a.source ? " from " + esc(a.source) : ""}:</p><ul>${real.map(line).join("")}${a.builtin.map((b) => `<li><strong>Frankenstein</strong>: ${esc(b)}</li>`).join("")}</ul>` : ""}
    ${own.length ? `<p class="muted">Made by Frankenstein itself (no mod for these could be downloaded, so Frankenstein's own mod or the merge's story covers them):</p><ul>${own.map(ownLine).join("")}</ul>` : ""}
    ${(a.arsenal || []).length ? `<p class="muted">${esc(a.source)}'s guns, in this game:</p><table class="small" style="width:100%">${a.arsenal.map((g) => `<tr><td><strong>${esc(g.gun)}</strong></td><td>${esc(g.as)}</td><td><span class="tag">${g.match === "close" ? "close match" : "rough stand-in"}</span></td></tr>`).join("")}</table><p class="muted small">They're in your starter kit (renamed to ${esc(a.source)}'s names), in the item menu, and for sale at Handy Hank's hardware store. Not checked in game yet.</p>` : ""}
    ${miss.length ? `<p class="muted">Still to fill:</p><ul>${miss.map((x) => `<li><strong>${esc(x.slot)}</strong>: ${esc(x.why)}.${picks(x)}</li>`).join("")}</ul>` : ""}
    ${!a.adds.length && !a.builtin.length ? `<p class="muted">Nothing added yet. Press Auto-fill on the merge.</p>` : ""}
    <p class="muted small">Only mods made for the Host game are installed. Nothing is copied out of the Extension game.</p></div>`;
}

// links to web pages anywhere in the app open in the normal browser
document.addEventListener("click", (e) => {
  const a = e.target.closest && e.target.closest("[data-ext]");
  if (a) { e.preventDefault(); call("open_url", a.dataset.ext); }
});

// the minimum standard (everything the Schedule I merge has), line by line with an honest note
function standardHtml(r) {
  const mark = { built: "✓", native: "✓", partial: "◐", planned: "○", "can't": "✕" };
  const word = { built: "Built by Frankenstein", native: "The game does it itself", partial: "Partly", planned: "Still to come", "can't": "Not possible in this game" };
  return `<p class="muted">Every merge gets everything the Schedule I merge has. ${esc(r.game)} meets ${r.met} of ${r.total}; Frankenstein builds and updates the lines it can by itself.</p>
    <ul class="standard">${r.rows.map((x) => `<li class="${x.met ? "good" : ""}"><strong>${mark[x.level] || "○"} ${esc(x.name)}</strong> <span class="tag">${esc(word[x.level] || x.level)}</span><div class="muted small">${esc(x.note || x.what)}</div></li>`).join("")}</ul>`;
}

// ---------- my merges ----------
async function loadMerges() {
  const el = $("#view-merges");
  el.innerHTML = `<div class="page"><h1>My merges</h1><p class="muted">Loading…</p></div>`;
  try { state.merges = await call("merges"); } catch (e) { state.merges = []; toast(errText(e)); }
  const items = state.merges.map((m) => `
    <div class="card merge" data-id="${esc(m.id)}">
      <div data-merge-cover="${esc(m.id)}">${caseHtml(m.game, null, "small")}</div>
      <div class="merge-info">
        <div class="merge-name">${esc(m.name)}</div>
        <div class="muted">${m.tagline ? `<em>${esc(m.tagline)}</em> · ` : ""}${esc(m.game)} × ${esc(m.theme)}</div>
        <div class="row small"><span class="tag">${esc(m.mode_label)}</span><span class="tag">${m.mods.length} installed</span><span class="tag" title="How deep this merge's story goes in this game">${esc(m.story || "")}</span>${m.has_saves ? `<span class="tag">Own saves</span>` : ""}${m.packed?.on ? `<span class="tag good" title="Only the files this merge changed and its saves are kept (${fmtGB(m.packed.kept)}). Play rebuilds the copy from your installed game first: about ${fmtSecs(m.packed.unpack_secs)}${m.packed.measured ? "" : " (estimate)"}.">📦 Packed · ${fmtGB(m.packed.saved)} saved</span>` : ""}${m.offline_lock ? `<span class="tag" title="Starts in offline mode so mods can't reach online play">Online locked</span>` : ""}${m.standard ? `<button class="tag tag-btn ${m.standard.met === m.standard.total ? "good" : ""}" data-act="standard" title="Frankenstein's minimum standard: everything the Schedule I merge has, checked for this game">${m.standard.met === m.standard.total ? "✓" : "◐"} Standard ${m.standard.met}/${m.standard.total}</button>` : ""}<button class="tag tag-btn" data-act="pad" title="${m.pad === "native" ? "This game supports controllers itself" : "Frankenstein's key mapper runs while you play. Click to change the buttons."}">🎮 ${m.pad === "native" ? "Controller" : "Controller (mapped)"}</button>${m.playing ? `<span class="tag live">Playing now</span>` : ""}${m.status === "restored" ? `<span class="tag">Mods removed</span>` : ""}${m.originals ? `<span class="tag ${/Verify|put your originals back/.test(m.originals) ? "bad" : ""}" title="${esc(m.originals)}">${/Verify|put your originals back/.test(m.originals) ? "⚠ Originals" : "✓ Originals untouched"}</span>` : ""}${m.save_fix ? `<span class="tag" title="A damaged save was swapped for its last good copy after you played">Save repaired</span>` : ""}</div>
        <div class="muted small" title="${esc(m.folder)}">${esc(m.folder)}</div>
      </div>
      <div class="merge-actions">
        <button class="btn primary play-big" data-act="play">▶ Play</button>
        <div class="merge-row">
          <button class="btn" data-act="adds" title="What this merge brings over from the Extension game">What it adds${m.adds ? ` (${m.adds})` : ""}</button>
          <button class="btn" data-act="share">Share</button>
          <div class="more"><button class="btn ghost" data-more aria-haspopup="true" aria-expanded="false">⋯ More</button>
            <div class="more-menu" role="menu" hidden>
              <button data-act="carry">Carry over my save</button>
              <button data-act="plan">Merge plan</button>
              <button data-act="rundown">Rundown</button>
              <button data-act="spawner" title="Send items and cars into the game while you play (works from a second screen)">Spawner</button>
              <button data-act="autofill" title="Frankenstein finds and installs the mods that bring the Extension game's features over">Auto-fill</button>
              <button data-act="mods">Mods &amp; profiles</button>
              <button data-act="add">Add mods</button>
              <button data-act="fix">Fix crash</button>
              ${m.needs_upgrade ? `<button data-act="upgrade" title="Give this merge everything new merges get. Mods and saves stay as they are.">Upgrade</button>` : ""}
              <button data-act="folder">Open folder</button>
              <hr>
              ${m.packed?.on ? `<button data-act="unpack">Unpack now</button>` : m.mode !== "inplace" && !m.playing ? `<button data-act="pack" title="Keep only what this merge changed and delete the copy. Play rebuilds it from your installed game.">Pack away to save space</button>` : ""}
              <button data-act="undo">Undo all mods</button>
              <button data-act="delete" class="danger">Delete</button>
            </div></div>
        </div>
        ${m.ingame ? `<div class="ingame small" title="Frankenstein's own mod adds these to the game itself">🎮 <strong>In the game:</strong> ${esc(m.ingame)}</div>` : `<div class="ingame small muted">Creative lives inside the game, never here. Frankenstein's own mod for this game isn't built yet, so this merge has no Creative yet.</div>`}
        ${m.mod_check && !m.mod_check.loaded ? `<div class="ingame small warn" role="status">⚠ <strong>Last time you played:</strong> ${esc(m.mod_check.why)}</div>` : m.mod_check?.version ? `<div class="ingame small muted" title="Read from MelonLoader's log. F6's title in the game shows the same number.">✓ Frankenstein mod ${esc(m.mod_check.version)} ran last time you played.${m.mod_check.guns ? ` ${esc(m.mod_check.guns)}.` : ""}</div>` : ""}
        ${m.creative_folded?.moved ? `<div class="ingame small muted" role="status">✦ Your ${m.creative_folded.moved} Creative save${m.creative_folded.moved > 1 ? "s are" : " is"} now with your other saves${m.creative_folded.kept ? `; ${m.creative_folded.kept} couldn't fit and ${m.creative_folded.kept > 1 ? "are" : "is"} kept safe` : ""}. Creative is a Game Mode on each save now.</div>` : ""}
        ${m.pack_error ? `<div class="ingame small muted" role="status">📦 ${esc(m.pack_error)}</div>` : ""}
        ${m.builtin_error ? `<div class="ingame small warn" role="status">⚠ Frankenstein's own mod couldn't be added: ${esc(m.builtin_error)} It's tried again when you press Play.</div>` : ""}
      </div>
    </div>`).join("");
  const now = state.merges.find((m) => m.playing);
  el.innerHTML = `<div class="page"><div class="row" style="justify-content:space-between"><h1>My merges</h1><button class="btn" id="importBtn">Open a merge from a friend</button></div>
    <p class="lede">Every merge can be played, added to, or rolled back, and each one keeps its own save files. Deleting a merge never touches your original game.</p>
    ${now ? `<div class="playing-banner" role="status"><span class="dot" aria-hidden="true"></span><div class="grow"><strong>${esc(now.name)} is running.</strong> <span class="muted">Its own saves are loaded. Your normal saves come back when you close the game.</span></div></div>` : ""}
    ${items || `<div class="empty">No merges yet. Pick a base game in the Library and press Merge.</div>`}</div>`;
  el.querySelectorAll("[data-merge-cover]").forEach(async (c) => {
    if (!c.dataset.mergeCover) return;
    const uri = await call("merge_cover", c.dataset.mergeCover).catch(() => null);
    if (uri) c.querySelectorAll(".face, .spine").forEach((f) => { f.style.backgroundImage = `url('${uri}')`; f.classList.remove("placeholder"); f.classList.contains("face") && (f.textContent = ""); });
  });
  el.querySelectorAll("[data-act]").forEach((b) => (b.onclick = () => { closeMore(); mergeAction(b.closest("[data-id]").dataset.id, b.dataset.act); }));
  el.querySelectorAll("[data-more]").forEach((b) => (b.onclick = (e) => {
    e.stopPropagation();
    const menu = b.nextElementSibling, open = menu.hidden;
    closeMore();
    menu.hidden = !open;
    b.setAttribute("aria-expanded", String(open));
    if (open) menu.querySelector("button")?.focus();
  }));
  $("#importBtn").onclick = async () => { const p = await call("pick_share_file"); if (p) importMerge(p); };
}

function closeMore() {
  document.querySelectorAll(".more-menu").forEach((m) => { m.hidden = true; m.previousElementSibling?.setAttribute("aria-expanded", "false"); });
}
document.addEventListener("click", (e) => { if (!(e.target.closest && e.target.closest(".more"))) closeMore(); });
document.addEventListener("keydown", (e) => { if (e.key === "Escape") closeMore(); });

// Carry over my save: the Extension's character (name, money, level, stats, abilities) rebuilt in the Host
async function carryDialog(m) {
  const d = await call("carry_read", m.id);
  const c = d.card;
  const lv = { built: "Built by Frankenstein", "stand-in": "Stand-in", "can't": "Not possible yet", mod: "Mod" };
  const body = `<p class="muted">${esc(d.found.how)}${d.found.save ? ` <span class="small">(${esc(d.found.save)}, only read, never changed)</span>` : ""}</p>
    <div class="two"><div class="field"><label for="cName">Name</label><input class="input" id="cName" value="${esc(c.name || "")}"></div>
      <div class="field"><label for="cLevel">Level</label><input class="input" id="cLevel" type="number" min="0" value="${+c.level || 0}"></div></div>
    <div class="field"><label for="cMoney">Money to bring (given once, in ${esc(d.host)}'s money)</label><input class="input" id="cMoney" type="number" min="0" value="${+c.money || 0}"></div>
    ${Object.keys(c.stats || {}).length ? `<p class="small">From the save: ${Object.entries(c.stats).map(([k, v]) => `<span class="tag">${esc(k)} ${esc(String(v))}</span>`).join(" ")}</p>` : ""}
    <h3 class="pv-h">Abilities, and what they become in ${esc(d.host)}</h3>
    ${d.abilities.map((a) => `<label class="check carry-ab"><input type="checkbox" data-ab="${esc(a.id)}" ${a.on ? "checked" : ""}>
      <span class="grow"><strong>${esc(a.name)}</strong><span class="muted small">${esc(a.how)}</span></span>
      <span class="plan-lv ${a.level === "built" ? "built" : a.level === "can't" ? "cant" : "stand"}">${esc(lv[a.level] || a.level)}</span></label>`).join("")}
    <p class="muted small">Your character arrives next time you press Play. Money and items come once; the powers stay on.</p>`;
  const go = await modal("Carry over my save: " + (c.from || ""), body, [{ label: "Cancel", value: false }, { label: "Bring them over", value: true, primary: true }]);
  if (!go) return;
  const card = Object.assign({}, c, {
    name: $("#cName").value.trim(), level: +$("#cLevel").value || 0, money: +$("#cMoney").value || 0,
    abilities: [...document.querySelectorAll("[data-ab]")].filter((x) => x.checked).map((x) => x.dataset.ab),
  });
  try { await call("carry_save", m.id, card); toast(`${card.name || "Your character"} comes over next time you play ${m.name}.`, 6000); }
  catch (e) { toast(errText(e)); }
}

// Frankenstein's merged loading screen: both games fused, while the Host game starts
async function loadingScreen(m, creative) {
  let art;
  try { art = await call("fusion_art", m.id); } catch (e) { return; }
  const el = document.createElement("div");
  el.className = "load-screen";
  el.innerHTML = `<div class="ls-art" style="background-image:url('${esc(art.merged || art.base || "")}')"></div><div class="pv-grade"></div>
    <div class="ls-text"><div class="ls-kicker">${esc(art.base_name)} × ${esc(art.source_name)}${creative ? " · Creative" : ""}</div>
    <div class="ls-title">${esc(art.name)}</div>${art.tagline ? `<div class="ls-tag">${esc(art.tagline)}</div>` : ""}
    <div class="ls-bar"><div></div></div><div class="ls-hint">Starting ${esc(art.base_name)} with ${esc(art.source_name)} coded onto it… click to close</div></div>`;
  document.body.appendChild(el);
  const close = () => { el.classList.add("out"); setTimeout(() => el.remove(), 600); };
  el.onclick = close;
  setTimeout(close, 9000);
}

// ---------- Spawner: items and cars straight into the running game ----------
const CAT_HUE = (c) => [...(c || "x")].reduce((h, ch) => (h * 31 + ch.charCodeAt(0)) % 360, 7);
async function openSpawner(m) {
  const c = await call("spawner_catalog", m.id);
  if (!c.available) return modal("Spawner", `<p>${esc(c.note)}</p>`, [{ label: "OK", value: true, primary: true }]);
  const cats = ["All", ...new Set(c.items.map((x) => x.cat))];
  let cat = "All", q = "";
  const body = `<p class="muted small">${c.live ? "Playing now: click a tile and it appears in the game within a second." : "Start the game, then click a tile: it appears in the game within a second. You can keep this open on a second screen."}
      Click: 1 · Right-click: 10. ${esc(c.note)}</p>
    <input class="input" id="spQ" placeholder="Search ${c.items.length} items and cars" autocomplete="off">
    <div class="sp-cats" id="spCats"></div><div class="sp-grid" id="spGrid"></div>`;
  const done = modal("Spawner · " + m.name, body, [{ label: "Close", value: true }]);
  const draw = () => {
    $("#spCats").innerHTML = cats.map((k) => `<button class="tag tag-btn ${k === cat ? "live" : ""}" data-cat="${esc(k)}">${esc(k)}</button>`).join("");
    $("#spCats").querySelectorAll("[data-cat]").forEach((b) => (b.onclick = () => { cat = b.dataset.cat; draw(); }));
    const words = q.toLowerCase().split(/\s+/).filter(Boolean);
    const shown = c.items.filter((x) => (cat === "All" || x.cat === cat) && words.every((w) => (x.name + " " + x.id + " " + x.cat).toLowerCase().includes(w))).slice(0, 300);
    $("#spGrid").innerHTML = shown.map((x, i) => `<button class="sp-tile" data-i="${i}" style="--h:${CAT_HUE(x.cat)}" title="${esc(x.id)}">
      <span class="sp-ico">${x.kind === "car" ? "🚗" : x.kind === "weapon" ? "🔫" : esc((x.name || "?")[0].toUpperCase())}</span>
      <span class="sp-name">${esc(x.name)}</span><span class="sp-cat">${esc(x.cat)}</span></button>`).join("") || `<p class="muted">Nothing matches.</p>`;
    $("#spGrid").querySelectorAll(".sp-tile").forEach((b) => {
      const x = shown[+b.dataset.i];
      const send = async (qty) => {
        try { await call("spawner_send", m.id, x.kind, x.id, qty, x.name); toast(`Sent ${qty} × ${x.name}`); } catch (e) { toast(errText(e)); }
      };
      b.onclick = () => send(1);
      b.oncontextmenu = (e) => { e.preventDefault(); send(x.kind === "car" ? 1 : 10); };
    });
  };
  $("#spQ").oninput = (e) => { q = e.target.value; draw(); };
  draw();
  setTimeout(() => $("#spQ")?.focus(), 30);
  await done;
}

// Covers tilt toward the mouse (CSS does the idle float and light sweep; both stop with reduced motion).
const calm = window.matchMedia ? window.matchMedia("(prefers-reduced-motion: reduce)") : { matches: false };
document.addEventListener("pointermove", (e) => {
  if (calm.matches) return;
  const c = e.target.closest && e.target.closest(".case");
  if (!c) return;
  const r = c.getBoundingClientRect();
  const x = (e.clientX - r.left) / r.width - 0.5, y = (e.clientY - r.top) / r.height - 0.5;
  c.style.setProperty("--ry", (x * 34).toFixed(1) + "deg");
  c.style.setProperty("--rx", (-y * 20).toFixed(1) + "deg");
  c.style.setProperty("--lx", ((x + 0.5) * 100).toFixed(0) + "%");
});

// ---------- living covers: each box shows the game's own cover art, moving ----------
// The original art, never swapped for trailer footage: it drifts and zooms slowly (like a GIF of the cover), a light
// sweeps across it and sparks rise in front. Only boxes on screen move, and nothing moves with reduced motion.
// clipVideo is still used by the merge preview slides, which do play trailers.
const CLIP_MAX = 6, clipCache = {}, clipLive = new Set();
function clipVideo(c, cls) {
  const v = document.createElement("video");
  v.className = "face-clip " + (cls || "");
  v.muted = true; v.loop = false; v.playsInline = true; v.preload = "metadata"; v.setAttribute("aria-hidden", "true");
  (c.sources || []).filter((x) => !x.type || v.canPlayType(x.type)).forEach((x) => {
    const s = document.createElement("source"); s.src = x.url; if (x.type) s.type = x.type; v.appendChild(s);
  });
  if (!v.children.length) return null;
  // trailers open on logos: skip a quarter in and loop eight seconds of gameplay, like a GIF
  v.addEventListener("loadedmetadata", () => { v.dataset.start = (v.duration > 30 ? v.duration * 0.25 : 0).toFixed(1); v.currentTime = +v.dataset.start; });
  v.addEventListener("timeupdate", () => { if (v.currentTime > +(v.dataset.start || 0) + 8) v.currentTime = +(v.dataset.start || 0); });
  v.addEventListener("ended", () => { v.currentTime = +(v.dataset.start || 0); v.play().catch(() => {}); });
  v.addEventListener("playing", () => v.classList.add("on"));
  v.addEventListener("error", () => v.remove(), true);
  return v;
}
function clipStart(box) {
  if (calm.matches) return;
  const face = box.querySelector(".face") || box;
  if (!face.querySelector(".live-fx")) {
    const fx = document.createElement("span");
    fx.className = "live-fx";
    fx.setAttribute("aria-hidden", "true");
    fx.innerHTML = "<i></i>".repeat(7);
    face.appendChild(fx);
  }
  face.classList.add("alive");
  clipLive.add(box);
}
function clipStop(box) {
  if (!box) return;
  clipLive.delete(box);
  const face = box.querySelector(".face");
  if (face) face.classList.remove("alive");
}
const clipSeen = "IntersectionObserver" in window ? new IntersectionObserver((list) => list.forEach((e) => {
  if (e.isIntersecting) setTimeout(() => e.target.isConnected && clipStart(e.target), 400 + Math.random() * 900);
  else clipStop(e.target);
}), { threshold: 0.6 }) : null;
function watchClips(root) {
  (root || document).querySelectorAll(".case:not([data-clip-watch])").forEach((b) => {
    b.dataset.clipWatch = "1";
    if (clipSeen) clipSeen.observe(b);
  });
}
document.addEventListener("pointerover", (e) => { const b = e.target.closest && e.target.closest(".case"); if (b) clipStart(b); });
new MutationObserver(() => watchClips()).observe(document.body, { childList: true, subtree: true });
(calm.addEventListener ? calm.addEventListener("change", () => calm.matches && [...clipLive].forEach(clipStop)) : null);

// ---------- updates ----------
async function offerUpdate(u, asked = false) {
  if (!u) { if (asked) toast("You're on the newest version."); return; }
  const go = await modal("Update available", `
    <p>Frankenstein <strong>${esc(u.version)}</strong> is out. It takes a few seconds: the app closes, updates and opens again. Your merges and saves aren't touched.</p>
    ${u.notes ? `<div class="log">${esc(u.notes)}</div>` : ""}`,
    [{ label: "Later", value: false }, { label: "Update now", value: true, primary: true }]);
  if (go) followJob(await call("install_update"), "Updating Frankenstein");
}

// The sidebar button, for when the automatic check hasn't caught a new release yet.
async function checkUpdateNow() {
  const b = $("#navUpdate"), label = b.querySelector("span");
  label.textContent = "Checking…"; b.disabled = true;
  const r = await call("check_update_now").catch((e) => ({ error: errText(e) }));
  b.disabled = false;
  label.textContent = "Check for updates";
  if (r.error) return toast(r.error, 5000);
  if (r.update) return offerUpdate(r.update, true);
  toast(`You're on the newest version (${r.current}).`);
}
if ($("#navUpdate")) $("#navUpdate").onclick = checkUpdateNow;

// After an update (or when something needed fixing): what the self-check found and fixed.
function selfCheckHtml(r) {
  const list = (xs) => `<ul>${xs.map((x) => `<li>${esc(x)}</li>`).join("")}</ul>`;
  return `${!r.fixed.length && !r.problems.length ? `<p><strong>Everything's up to date.</strong> Every feature, Frankenstein's own mods and your merges were checked for ${esc(r.version)}.</p>` : ""}
    ${r.fixed.length ? `<p><strong>Fixed ${r.fixed.length} thing${r.fixed.length === 1 ? "" : "s"}</strong> (each merge was backed up first):</p>${list(r.fixed)}` : ""}
    ${r.problems.length ? `<p><strong>Needs you:</strong></p>${list(r.problems)}` : ""}`;
}
async function showSelfCheck(r) {
  const go = await modal(r.after_update ? `Frankenstein ${r.version}: self-check` : "Self-check", selfCheckHtml(r),
    r.repair_install ? [{ label: "Later", value: false }, { label: "Repair", value: true, primary: true }] : [{ label: "OK", value: false, primary: true }]);
  if (go) followJob(await call("repair_install"), "Repairing Frankenstein");
}
async function watchSelfCheck(tries = 0) {
  const r = await call("self_check_result", false).catch(() => null);
  if (!r) return;
  if (r.pending) return tries < 60 && setTimeout(() => watchSelfCheck(tries + 1), 3000);
  if (r.after_update || r.fixed.length || r.problems.length) showSelfCheck(r);
}
setTimeout(watchSelfCheck, 4000);

// ---------- the rundown: what's new and what to do, while the game loads ----------
async function showRundown(m, creative, onPlay) {
  let d;
  try { d = await call("merge_rundown", m.id, creative); } catch (e) { return; }
  if (onPlay && !d.show) return;
  document.querySelector(".rundown")?.remove();
  const el = document.createElement("div");
  el.className = "rundown";
  el.setAttribute("role", "dialog");
  el.setAttribute("aria-label", "Rundown for " + m.name);
  const screens = await call("screens").catch(() => 0);
  const list = (title, xs) => xs && xs.length ? `<section><h3>${title}</h3><ul>${xs.map((x) => `<li>${esc(x)}</li>`).join("")}</ul></section>` : "";
  const draw = () => {
    el.innerHTML = `<div class="rd-box">
      <div class="eyebrow">${onPlay ? "Loading" : "Rundown"}${creative ? " · Creative" : ""}</div>
      <h1>${esc(d.headline)}</h1>${d.tagline ? `<p class="muted">${esc(d.tagline)}</p>` : ""}
      <div class="rd-grid">${list("What's new", d.new)}${list("Try first", d.try)}${list("Keys and buttons", d.keys)}${list("Your quest", d.quest ? [d.quest] : [])}${list("Tips", d.tips)}</div>
      ${d.ready ? "" : `<p class="muted small">Writing a fuller rundown in the background…</p>`}
      <div class="row rd-actions">
        <label class="switch small grow"><input type="checkbox" id="rdShow" ${d.show ? "checked" : ""}> Show this when I press Play</label>
        ${screens > 1 ? `<button class="btn" id="rdSecond">Show on my other screen</button>` : ""}
        ${screens ? `<button class="btn" id="rdTop">Keep on top</button>` : ""}
        <button class="btn primary" id="rdGo">${onPlay ? "Let's go" : "Close"}</button></div></div>`;
    $("#rdGo").onclick = close;
    $("#rdShow").onchange = (e) => call("rundown_pref", m.id, e.target.checked);
    if ($("#rdSecond")) $("#rdSecond").onclick = () => { call("rundown_window", m.id, "second", creative); close(); };
    if ($("#rdTop")) $("#rdTop").onclick = () => { call("rundown_window", m.id, "overlay", creative); close(); };
    $("#rdGo").focus();
  };
  const key = (e) => { if (e.key === "Escape") { e.preventDefault(); close(); } };
  const close = () => { document.removeEventListener("keydown", key, true); el.remove(); };
  document.addEventListener("keydown", key, true);
  document.body.appendChild(el);
  draw();
  for (let i = 0; i < 12 && !d.ready && el.isConnected; i++) {  // swap in the AI version when it's ready
    await sleep(2500);
    const n = await call("merge_rundown", m.id, creative).catch(() => null);
    if (n && n.ready && el.isConnected) { d = n; draw(); }
  }
}

// ---------- creative mode: sandbox tools, own saves ----------
// Returns true to go ahead and play in creative.
async function creativeSetup(m) {
  const c = await call("creative_info", m.id);
  const extra = [];
  const body = `<p>${esc(c.note || c.why_none)}</p>
    ${c.item_menu ? `<div class="note small"><strong>Every item:</strong> ${esc(c.item_menu)}</div>` : ""}
    ${c.native ? `<div class="note small"><strong>Built in:</strong> ${esc(c.native)}</div>` : ""}
    ${c.slots.length ? `<div class="row" style="margin:10px 0"><button class="btn primary" id="sbAuto" type="button">Add sandbox tools for me</button><span class="muted small grow">Frankenstein finds and installs the best one for each.</span></div>` : ""}
    ${c.adds.length ? `<ul class="small">${c.adds.map((x) => `<li>${esc(x.slot)}: ${x.ok ? esc(x.mod) + " ✔" : "not found (" + esc(x.why) + ")"}</li>`).join("")}</ul>` : ""}
    ${c.slots.length ? `<p class="muted">Or pick them yourself (they're only switched on in creative):</p>
      <div>${c.slots.map((x, i) => `<div class="row" style="padding:6px 0;border-top:1px solid var(--line)"><span class="grow">${esc(x.name)}</span><button class="btn" data-sb="${i}" type="button">Find</button></div>`).join("")}</div>` : ""}
    ${c.mods.length ? `<p class="muted small">Added: ${c.mods.map(esc).join(", ")}</p>` : ""}
    <p class="muted small">Creative uses this merge's saves, like Minecraft: make a new save in the game for a creative world, or load one you have. ${c.offline ? "It always starts offline, so none of this reaches online play." : "Don't take creative tools into online games."}</p>`;
  const done = modal("Creative mode: " + m.name, body, [{ label: "Cancel", value: false }, { label: "Play creative", value: true, primary: true }]);
  if ($("#sbAuto")) $("#sbAuto").onclick = async () => {
    closeModal();
    await followJob(await call("creative_auto", m.id), "Adding sandbox tools");
    mergeAction(m.id, "creative");
  };
  document.querySelectorAll("[data-sb]").forEach((b) => (b.onclick = () => {
    const slot = c.slots[+b.dataset.sb];
    closeModal();
    modBrowser({ recipe: c.recipe_id, title: slot.name, search: slot.search, sources: c.sources, loader: c.mc_loader, version: c.mc_version,
      onFiles: (files) => extra.push(...files.map((f) => f.path)) }).then(async () => {
      if (extra.length) await followJob(await call("creative_add", m.id, extra.splice(0)), "Adding sandbox mods");
      mergeAction(m.id, "creative");
    });
  }));
  return done;
}

// ---------- controller buttons for one merge ----------
async function padDialog(m) {
  const d = await call("pad_settings", m.id);
  const opt = (sel) => d.keys.map((k) => `<option ${k === sel ? "selected" : ""}>${esc(k)}</option>`).join("");
  const rows = Object.entries(d.names).map(([k, n]) => `<div class="pad-row"><label for="pad-${k}">${esc(n)}</label><select class="input" id="pad-${k}" data-k="${k}">${opt(d.map[k])}</select></div>`).join("");
  const status = { full: "This game supports controllers itself.", partial: "This game only partly supports controllers.", none: "This game doesn't support controllers.", unknown: "Frankenstein couldn't check this game's controller support." }[d.status];
  const body = `<p class="muted">${status} ${d.mapper_on ? "Frankenstein's key mapper turns your controller into keyboard and mouse while the game is in front." : "It uses its own controls."}</p>
    <div class="field"><label for="padMode">Key mapper</label><select class="input" id="padMode">
      <option value="auto" ${d.mode === "auto" ? "selected" : ""}>Automatic (on unless the game supports controllers fully)</option>
      <option value="on" ${d.mode === "on" ? "selected" : ""}>Always on</option><option value="off" ${d.mode === "off" ? "selected" : ""}>Off</option></select></div>
    <div class="pad-grid">${rows}
      <div class="pad-row"><label for="pad-ls2">Left stick</label><select class="input" id="pad-ls2" data-k="left_stick">${["wasd", "arrows", "none"].map((v) => `<option ${d.map.left_stick === v ? "selected" : ""}>${v}</option>`).join("")}</select></div>
      <div class="pad-row"><label for="pad-rs2">Right stick</label><select class="input" id="pad-rs2" data-k="right_stick">${["mouse", "none"].map((v) => `<option ${d.map.right_stick === v ? "selected" : ""}>${v}</option>`).join("")}</select></div>
      <div class="pad-row"><label for="pad-sens">Look speed</label><input class="input" type="number" min="2" max="60" id="pad-sens" data-k="sensitivity" value="${esc(d.map.sensitivity)}"></div></div>
    <p class="muted small">Xbox controllers work directly. For PlayStation controllers, use Steam Input or DS4Windows.</p>`;
  const go = await modal("Controller: " + m.name, body, [{ label: "Reset", value: "reset" }, { label: "Cancel", value: false }, { label: "Save", value: true, primary: true }]);
  if (!go) return;
  const map = {};
  if (go !== "reset") document.querySelectorAll("[data-k]").forEach((x) => (map[x.dataset.k] = x.dataset.k === "sensitivity" ? +x.value : x.value));
  // the modal is closed by now, so read the values before it's reused
  await call("pad_save", m.id, go === "reset" ? "auto" : $("#padMode")?.value || "auto", go === "reset" ? d.default : map);
  toast("Controller settings saved.");
  loadMerges();
}

// ---------- mod profiles ----------
async function modsDialog(m) {
  const d = await call("merge_mods", m.id);
  const profiles = Object.keys(d.profiles);
  const body = `
    ${profiles.length ? `<div class="field"><label for="profilePick">Profile</label><div class="row" style="flex-wrap:nowrap">
      <select class="input" id="profilePick"><option value="">Custom</option>${profiles.map((p) => `<option ${p === d.profile ? "selected" : ""}>${esc(p)}</option>`).join("")}</select>
      <button class="btn ghost danger" id="profileDel" type="button">Delete</button></div></div>` : ""}
    <div id="modChecks" style="max-height:300px;overflow:auto;margin:10px 0">
      ${d.mods.map((x, i) => `<label class="switch" style="padding:6px 0"><input type="checkbox" data-i="${i}" ${x.on ? "checked" : ""} ${x.kept ? "" : "disabled"}> ${esc(x.label)}${x.kept ? "" : ` <span class="muted small">(re-add to switch)</span>`}</label>`).join("") || `<p class="muted">No mods yet.</p>`}
    </div>
    <div class="field"><label for="profileName">Save these as a profile (optional)</label>
      <input class="input" id="profileName" placeholder="e.g. Chill or Chaos" value="${esc(d.profile)}" autocomplete="off"></div>`;
  const pick = () => {
    const sel = d.profiles[$("#profilePick").value];
    if (!sel) return;
    document.querySelectorAll("#modChecks input").forEach((c) => (c.checked = sel.includes(d.mods[+c.dataset.i].label)));
    $("#profileName").value = $("#profilePick").value;
  };
  setTimeout(() => {
    if ($("#profilePick")) $("#profilePick").onchange = pick;
    if ($("#profileDel")) $("#profileDel").onclick = async () => { closeModal(); await call("delete_profile", m.id, $("#profilePick").value); modsDialog(m); };
  });
  const go = await modal("Mods in “" + m.name + "”", body, [{ label: "Cancel", value: false }, { label: "Apply", value: true, primary: true }]);
  if (!go) return;
  const enabled = [...document.querySelectorAll("#modChecks input:checked")].map((c) => d.mods[+c.dataset.i].label);
  const name = ($("#profileName")?.value || "").trim();
  const r = await followJob(await call("set_mods", m.id, enabled, name), "Switching mods");
  if (r) { toast(name ? `Profile “${name}” is on.` : "Mods updated."); loadMerges(); }
}

// ---------- sharing ----------
const mb = (n) => (n / 1048576).toFixed(n > 104857600 ? 0 : 1) + " MB";

async function shareMerge(m) {
  const job = await call("share_merge", m.id);
  const r = (await followJob(job, "Packing " + m.name))?.result;
  if (!r) return;
  modal("Ready to share", `<p>Saved <strong>${esc(basename(r.path))}</strong> (${mb(r.size)}) in your Downloads folder.</p>
    <p class="muted">Send it to your friend (Discord, Google Drive, a USB stick). They double-click it and Frankenstein builds the same merge from their own copy of the game. Only the mods are shared, never game files.</p>`);
}

async function importMerge(path) {
  let info;
  try { info = await call("import_info", path); } catch (e) { return modal("Can't open that", `<p>${esc(errText(e))}</p>`); }
  if (!info.have_game) return modal("You need the base game", `<p>“${esc(info.name)}” is a ${esc(info.game_name)} merge. Install ${esc(info.game_name)}, then open the file again.</p>`);
  const go = await modal("Open “" + info.name + "”?", `
    <p>${esc(info.game_name)} × ${esc(info.theme_name)}, with ${info.mods.length} mod${info.mods.length === 1 ? "" : "s"} (${mb(info.size)}).</p>
    <ul class="muted small">${info.mods.slice(0, 8).map((x) => `<li>${esc(x)}</li>`).join("")}${info.mods.length > 8 ? `<li>…and ${info.mods.length - 8} more</li>` : ""}</ul>
    ${info.version_note ? `<div class="note small">${esc(info.version_note)}</div>` : ""}
    <p class="muted small">It goes into its own copy with its own saves, so your game stays as it is.</p>`,
    [{ label: "Cancel", value: false }, { label: "Add it", value: true, primary: true }]);
  if (!go) return;
  const r = (await followJob(await call("import_merge", path), "Adding " + info.name))?.result;
  if (r) { toast(`“${r.name}” is ready.`); show("merges"); }
}

// Refresh My merges when the game closes and the normal saves are put back.
let playWatch = null;
// While a merged game runs the app stays still so it never costs the game frames: animations and videos
// pause (the backend also drops the app to below-normal priority), and pick up again when the game closes.
let pausedVideos = [];
function gameOn(on) {
  document.body.classList.toggle("game-on", on);
  if (on) {
    pausedVideos = [...document.querySelectorAll("video")].filter((v) => !v.paused);
    pausedVideos.forEach((v) => v.pause());
  } else {
    pausedVideos.forEach((v) => v.isConnected && v.play().catch(() => {}));
    pausedVideos = [];
  }
}
function watchPlaying() {
  if (playWatch) return;
  gameOn(true);
  playWatch = setInterval(async () => {
    const now = await call("playing").catch(() => null);
    if (!now) {
      clearInterval(playWatch); playWatch = null;
      gameOn(false);
      toast("Game closed. Your merge's progress is saved and your normal saves are back.", 4500);
      if (state.view === "merges") loadMerges();
    }
  }, 5000);
}

// A packed merge is rebuilt from your own installed game before anything that needs its files
const NEEDS_FILES = ["play", "creative", "add", "undo", "autofill", "fix", "upgrade", "spawner"];
const fmtGB = (b) => (b < 1e9 ? Math.max(1, Math.round(b / 1e6)) + " MB" : (b / 1e9 >= 10 ? (b / 1e9).toFixed(0) : (b / 1e9).toFixed(1)) + " GB");
const fmtSecs = (s) => (s < 90 ? Math.max(5, Math.round(s)) + " seconds" : Math.round(s / 60) + " minutes");
// Before rebuilding a packed merge: if its drive is too full, say how much to free, or rebuild it on another drive
async function roomToUnpack(m) {
  for (;;) {
    const r = await call("unpack_room", m.id).catch(() => ({ short: 0 }));
    if (!r.short) return { ok: true };
    const actions = [{ label: "Cancel", value: null }, { label: "I've freed space, try again", value: "again" }];
    (r.drives || []).forEach((d) => actions.push({ label: `Rebuild it on ${d.path} (${fmtGB(d.free)} free)`, value: d.folder, primary: true }));
    actions.push({ label: "Pick a folder…", value: "pick" });
    const v = await modal("Not enough room to rebuild " + m.name, `
      <p>Rebuilding the copy needs <strong>${fmtGB(r.need)}</strong>, and drive ${esc(r.drive || "")} has <strong>${fmtGB(r.free)}</strong> free.
      Free up <strong>${fmtGB(r.short + 50e6)}</strong> and try again: empty the Recycle Bin, or run Disk Cleanup (Start menu, type “Disk Cleanup”).</p>
      ${(r.drives || []).length ? `<p class="muted">Or rebuild it on another drive that has room. Your merge and its saves come along; the original game isn't touched.</p>` : `<p class="muted">No other drive has enough room. You can also pick a folder on a USB or external drive.</p>`}
      <p class="muted small">Nothing is lost while it's packed: the merge's own files and saves are kept safe.</p>`, actions);
    if (v === null) return { ok: false };
    if (v === "again") continue;
    if (v === "pick") {
      const f = await call("pick_folder").catch(() => null);
      if (!f) continue;
      return { ok: true, folder: f };
    }
    return { ok: true, folder: v };
  }
}

async function ensureUnpacked(m) {
  if (!m.packed?.on) return true;
  const room = await roomToUnpack(m);
  if (!room.ok) return false;
  const j = await followJob(await call("unpack_merge", m.id, room.folder || null),
    `Rebuilding ${m.name} from your installed game (about ${fmtSecs(m.packed.unpack_secs)})`);
  if (j) m.packed = { on: false };
  return !!j;
}

async function mergeAction(id, act) {
  const m = state.merges.find((x) => x.id === id);
  try {
    if (NEEDS_FILES.includes(act) && !(await ensureUnpacked(m))) return;
    if (act === "spawner") return openSpawner(m);
    if (act === "pack") {
      const j = await followJob(await call("pack_merge", id), "Packing " + m.name);
      if (j?.result?.on) toast(`Packed. ${fmtGB(j.result.saved)} freed. Play brings it back in about ${fmtSecs(j.result.unpack_secs)}.`, 6000);
      return loadMerges();
    }
    if (act === "unpack") { if (await ensureUnpacked(m)) toast(`${m.name} is unpacked and ready.`); return loadMerges(); }
    if (act === "play" || act === "creative") {
      const creative = act === "creative";
      if (creative && !(await creativeSetup(m))) return;
      toast(creative ? "Starting creative mode…" : "Starting " + m.name + "…");
      loadingScreen(m, creative);
      const r = await call("play", id, creative);
      if (r && r.note) toast(r.note, 6000); else watchPlaying();
      loadMerges();
      showRundown(m, creative, true);
    }
    else if (act === "rundown") showRundown(m, false, false);
    else if (act === "carry") await carryDialog(m);
    else if (act === "plan") {
      const done = modal("Merge plan: " + m.name, `<div id="planM" class="muted">Writing the plan…</div>`);
      loadPlan(() => call("merge_plan_of", id), $("#planM"));
      await done;
    }
    else if (act === "standard") await modal("Minimum standard: " + m.name, standardHtml(await call("merge_standard", id)));
    else if (act === "adds") await modal("What " + m.name + " adds", addsHtml(await call("merge_adds", id)));
    else if (act === "autofill") {
      const r = await followJob(await call("merge_autofill", id), "Filling in " + m.name);
      if (r) await modal("What " + m.name + " adds", addsHtml(await call("merge_adds", id)));
      loadMerges();
    }
    else if (act === "folder") await call("open_folder", id);
    else if (act === "pad") await padDialog(m);
    else if (act === "share") await shareMerge(m);
    else if (act === "mods") await modsDialog(m);
    else if (act === "fix") {
      const r = (await followJob(await call("fix_crash", id), "Fixing " + m.name))?.result;
      if (r) {
        modal(r.fixed ? "Fixed it" : "Couldn't pin it down", `<p>${esc(r.why)}</p>${r.fixed ? `<p class="muted"><strong>${esc(r.culprit)}</strong> is switched off and the game is starting again. Switch it back on any time in Mods &amp; profiles.</p>` : `<p class="muted">Try switching mods off one at a time in Mods &amp; profiles.</p>`}`);
        loadMerges();
      }
    }
    else if (act === "upgrade") { await call("merge_upgrade", id); toast(`“${m.name}” is upgraded.`); loadMerges(); }
    else if (act === "add") {
      const picked = [];
      await modBrowser({ recipe: m.recipe_id, title: m.name, search: "", sources: m.sources, loader: m.mc_loader, version: m.mc_version,
        onFiles: (files) => picked.push(...files.map((f) => f.path)), onOpened: () => toast("When it's downloaded, use Add mods > Pick a file.", 6000) });
      if (picked.length) { await followJob(await call("install_files", id, picked), "Adding mods"); loadMerges(); }
    }
    else if (act === "undo") {
      if (await confirmBox("Undo all mods?", `This removes every mod from “${m.name}” and puts the original files back.`, "Undo all mods")) { await followJob(await call("undo", id), "Undoing"); loadMerges(); }
    } else if (act === "delete") {
      const text = m.mode === "inplace" ? "This undoes all mods in your original install and forgets this merge." : `This deletes the merged copy at ${m.folder}. Your original game is not touched.`;
      if (await confirmBox("Delete merge?", text, "Delete", true)) { await followJob(await call("delete", id), "Deleting"); loadMerges(); }
    }
  } catch (e) { modal("That didn't work", `<p>${esc(errText(e))}</p>`); }
}

// ---------- gallery ----------
async function loadGallery() {
  const el = $("#view-gallery");
  let g;
  try { g = await call("gallery"); } catch (e) { toast(errText(e)); return; }
  const groups = {};
  g.items.forEach((x) => (groups[x.merge] = groups[x.merge] || []).push(x));
  el.innerHTML = `<div class="page"><h1>Gallery</h1>
    <p class="lede">Press <strong>${esc(g.hotkey)}</strong> while you play to grab a screenshot. For video clips use Windows Game Bar (<strong>Win+Alt+R</strong>); they show up here too.</p>
    ${Object.keys(groups).length ? Object.entries(groups).map(([name, list]) => `<div class="card"><h2>${esc(name)}</h2>
      <div class="shots">${list.map((x) => `<button class="shot" data-path="${esc(x.path)}" title="${esc(basename(x.path))}">${x.kind === "clip" ? `<span class="clip">▶ ${esc(basename(x.path))}</span>` : `<img alt="" data-thumb="${esc(x.path)}">`}</button>`).join("")}</div></div>`).join("")
      : `<div class="empty">No screenshots yet. Play a merge and press ${esc(g.hotkey)}.</div>`}</div>`;
  el.querySelectorAll("[data-path]").forEach((b) => (b.onclick = () => call("open_path", b.dataset.path)));
  for (const img of el.querySelectorAll("img[data-thumb]")) {
    call("gallery_thumb", img.dataset.thumb).then((u) => { if (u) img.src = u; }).catch(() => {});
  }
}

// ---------- play together ----------
let partyTimer = null;
async function loadParty() {
  clearTimeout(partyTimer);
  const el = $("#view-party");
  let p;
  try { p = await call("party"); } catch (e) { el.innerHTML = `<div class="page"><h1>Play together</h1><p class="note">${esc(errText(e))}</p></div>`; return; }
  const games = p.games.map((g) => `<div style="padding:10px 0;border-top:1px solid var(--line)"><strong>${esc(g.name)}</strong><div class="muted small">${esc(g.how)}</div></div>`).join("");
  let main;
  if (!p.ready) {
    main = `<div class="card"><h2>Set up your private network</h2>
      <p class="muted">Like Hamachi: your PC and your friends' PCs act like they're on the same home network, so LAN games and private servers can see each other. It uses ZeroTier, which is free and needs no account.</p>
      <p class="muted">Windows will ask once. After that it's one click to host or join.</p>
      <div class="row"><button class="btn primary" id="partySetup">Set it up</button>
        <a href="#" class="small" data-ext="https://www.zerotier.com/download/">Setup keeps failing? Install ZeroTier yourself, then press Set it up</a></div></div>`;
  } else if (!p.party) {
    main = `<div class="card"><h2>Host a party</h2><p class="muted">You get a code to send your friends. Your PC runs the party, so keep Frankenstein open while you play.</p>
        <button class="btn primary" id="partyHost">Host a party</button></div>
      <div class="card"><h2>Join a friend</h2><p class="muted">Type the code your friend sent you.</p>
        <div class="row" style="flex-wrap:nowrap"><input class="input" id="partyCode" placeholder="16-character party code" autocomplete="off" spellcheck="false"><button class="btn primary" id="partyJoin">Join</button></div></div>`;
  } else {
    const q = p.party;
    const badge = q.connected ? `<span class="tag live">Connected</span>` : q.waiting ? `<span class="tag">Waiting for the host to let you in</span>` : `<span class="tag">Connecting…</span>`;
    const members = q.host ? q.members.map((m) => `<div class="row" style="padding:10px 0;border-top:1px solid var(--line)">
        <div class="grow"><strong>${m.you ? "You (host)" : esc(m.name || "Friend " + m.id.slice(0, 4))}</strong> <span class="muted small">${esc(m.ip || "")}</span></div>
        ${m.you ? "" : m.authorized ? `<button class="btn ghost" data-kick="${esc(m.id)}">Remove</button>` : `<button class="btn primary" data-letin="${esc(m.id)}">Let in</button>`}</div>`).join("") : "";
    main = `<div class="card"><div class="row"><h2 class="grow" style="margin:0">${q.host ? "Your party" : "You're in a party"}</h2>${badge}</div>
        <div class="field" style="margin-top:12px"><label>Party code</label><div class="row" style="flex-wrap:nowrap"><input class="input" readonly value="${esc(q.code)}" style="font-family:monospace;font-size:18px;letter-spacing:2px"><button class="btn" id="partyCopy">Copy</button></div></div>
        <p class="muted small">Your party IP: <strong>${esc(q.my_ip || "getting one…")}</strong>. In games that ask for an IP, use the host's party IP.</p>
        ${q.host ? `<h2 style="margin-top:18px">Who's here</h2>${members || `<p class="muted">Nobody yet. Send your friends the code.</p>`}` : ""}
        <h2 style="margin-top:18px">Same mods for everyone</h2>
        ${q.host ? `<p class="muted">Pick the merge you'll play. Everyone in the party can then download it from you in one click.</p>
          <div class="row" style="flex-wrap:nowrap"><select class="input" id="partyMerge">${p.merges.map((m) => `<option value="${esc(m.id)}">${esc(m.name)}</option>`).join("")}</select><button class="btn primary" id="partyShare">Share with the party</button></div>
          ${p.sharing ? `<p class="small"><span class="tag live">Sharing</span> ${esc(p.sharing)}</p>` : ""}`
        : `<p class="muted">Get the merge the host is playing, with exactly the same mods.</p><button class="btn primary" id="partyFetch">Get the host's merge</button>`}
        <div class="note small" style="margin-top:12px"><strong>Schedule I:</strong> once you both have the same merge, the host starts it, presses Esc, then Invite friends (Steam). The friend accepts the Steam invite. The party network is the backup if Steam can't connect.</div>
        <div class="row" style="justify-content:space-between;margin-top:14px"><button class="btn ghost" id="partyFix" title="Fixes the firewall and restarts the network service. Windows asks once.">Friends can't connect? Fix it</button>
        <button class="btn ghost danger" id="partyLeave">${q.host ? "End party" : "Leave"}</button></div></div>`;
    partyTimer = setTimeout(() => state.view === "party" && loadParty(), 5000);
  }
  el.innerHTML = `<div class="page"><h1>Play together</h1>
    <p class="lede">Share a merge so your friends have the same mods, then play over your own private network.</p>
    ${p.error ? `<div class="note">${esc(p.error)}</div>` : ""}${main}
    <div class="card"><h2>What works with friends</h2>${games}</div></div>`;
  const on = (id, fn) => { const b = $("#" + id); if (b) b.onclick = async () => { try { await fn(); } catch (e) { modal("That didn't work", `<p>${esc(errText(e))}</p>`); } loadParty(); }; };
  on("partySetup", async () => { await followJob(await call("party_setup"), "Setting up Play together"); });
  on("partyHost", () => call("party_host"));
  on("partyJoin", () => call("party_join", $("#partyCode").value));
  on("partyLeave", () => call("party_leave"));
  on("partyFix", async () => { await call("party_fix"); toast("Fixed. Ask your friend to try again."); });
  on("partyShare", async () => { await followJob(await call("party_share", $("#partyMerge").value), "Sharing with the party"); });
  on("partyFetch", async () => {
    const r = await followJob(await call("party_fetch"), "Getting the host's merge");
    if (r && r.result) importMerge(r.result.path);
  });
  on("partyCopy", async () => { await call("copy_text", p.party.code); toast("Code copied. Send it to your friends."); });
  el.querySelectorAll("[data-letin]").forEach((b) => (b.onclick = async () => { await call("party_let_in", b.dataset.letin, ""); loadParty(); }));
  el.querySelectorAll("[data-kick]").forEach((b) => (b.onclick = async () => { await call("party_kick", b.dataset.kick); loadParty(); }));
}

// ---------- questlines, backups, settings ----------
async function renderQuests() {
  const el = $("#view-quests");
  const merges = await call("merges").catch(() => []);
  if (!merges.length) {
    el.innerHTML = `<div class="page"><h1>Questlines</h1><p class="lede">Make a merge first, then missions for it get written here.</p></div>`;
    return;
  }
  if (!merges.some((m) => m.id === state.questMerge)) state.questMerge = merges[0].id;
  const m = merges.find((x) => x.id === state.questMerge);
  const lines = await call("questlines", m.id).catch(() => []);
  const next = (q) => {
    for (let i = 0; i < q.quests.length; i++) { const j = q.done[i].indexOf(false); if (j >= 0) return [i, j]; }
    return null;
  };
  const active = lines.find((q) => !q.finished);
  if (active) {
    const n = next(active);
    state.questNow = n ? `${active.title}, quest "${active.quests[n[0]].title}", next objective: ${active.quests[n[0]].objectives[n[1]]}` : null;
  } else state.questNow = null;
  el.innerHTML = `<div class="page"><h1>Questlines</h1>
    <p class="lede">Missions written for your merged game, using what's really in it. Play them and tick off objectives as you go.</p>
    <div class="card">
      <div class="row"><label for="qMerge" class="muted">Merge</label><select class="input" id="qMerge">${merges.map((x) => `<option value="${esc(x.id)}" ${x.id === m.id ? "selected" : ""}>${esc(x.name)}</option>`).join("")}</select></div>
      <div class="row" style="flex-wrap:nowrap;margin-top:10px"><input class="input" id="qIdea" placeholder="Optional: what kind of story? e.g. a street-racing rivalry, take over the block"><button class="btn primary" id="qNew">Write a questline</button></div>
      <p class="muted small">In-game quest markers need a small quest-loader mod per game. Schedule I is first on the list; until then quests are tracked here.</p>
    </div>
    ${lines.map((q) => `<div class="card quest ${q.finished ? "done" : ""}" data-q="${esc(q.id)}">
      <div class="row" style="flex-wrap:nowrap"><div class="grow"><div class="eyebrow">${q.finished ? "Complete" : "Questline"}</div><h2 style="margin:4px 0">${esc(q.title)}</h2></div>
        <button class="btn ghost danger" data-del>Delete</button></div>
      <p class="muted">${esc(q.intro)}</p>
      ${q.depth ? `<p class="small"><span class="tag">${esc(q.depth)}</span></p>` : ""}
      ${(q.characters || []).filter((c) => c.bio).length ? `<div class="small" style="margin:8px 0"><strong>Characters</strong><ul>${q.characters.filter((c) => c.bio).map((c) => `<li><strong>${esc((c.first + " " + c.last).trim())}</strong>, ${esc(c.role)}: ${esc(c.bio)}${c.shop?.length ? ` <span class="muted">Sells: ${c.shop.map((x) => esc(x.item) + " $" + x.price).join(", ")}</span>` : ""}</li>`).join("")}</ul></div>` : ""}
      ${q.quests.map((x, i) => `<div class="q-step"><strong>${i + 1}. ${esc(x.title)}</strong><div class="muted small">${esc(x.briefing)}</div>
        ${x.objectives.map((o, j) => `<label class="switch small"><input type="checkbox" data-i="${i}" data-j="${j}" ${q.done[i][j] ? "checked" : ""}> ${esc(o)}</label>`).join("")}
        ${x.reward ? `<div class="small q-reward">Reward: ${esc(x.reward)}</div>` : ""}</div>`).join("")}
    </div>`).join("")}</div>`;
  $("#qMerge").onchange = (e) => { state.questMerge = e.target.value; renderQuests(); };
  $("#qNew").onclick = async () => {
    try {
      const r = await followJob(await call("questline_new", m.id, $("#qIdea").value), "Writing a questline");
      if (r && r.result) renderQuests();
    } catch (e) { toast(errText(e)); }
  };
  el.querySelectorAll(".quest").forEach((card) => {
    const q = lines.find((x) => x.id === card.dataset.q);
    card.querySelectorAll("input[data-i]").forEach((c) => (c.onchange = async () => {
      try {
        const upd = await call("questline_tick", m.id, q.id, +c.dataset.i, +c.dataset.j, c.checked);
        if (upd.finished && !q.finished) toast(`Questline complete: ${q.title}`);
        renderQuests();
      } catch (e) { toast(errText(e)); }
    }));
    card.querySelector("[data-del]").onclick = async () => {
      if (await confirmBox("Delete this questline?", "Its progress goes too.", "Delete", true)) { await call("questline_delete", m.id, q.id); renderQuests(); }
    };
  });
}

async function loadBackups() {
  const el = $("#view-backups");
  let merges = [];
  try { merges = await call("merges"); } catch (e) { toast(errText(e)); }
  const backed = merges.filter((m) => m.mode === "inplace" && m.replaced > 0);
  const copies = merges.filter((m) => m.mode !== "inplace");
  el.innerHTML = `<div class="page"><h1>Backups</h1>
    <p class="lede">Copies keep your original game untouched. When you mod an original install, every file that gets replaced is backed up first.</p>
    <div class="card"><h2>Original installs with backups</h2>
      ${backed.length ? backed.map((m) => `<div class="row" style="padding:10px 0;border-top:1px solid var(--line)"><div class="grow"><strong>${esc(m.game)}</strong><div class="muted small">${m.replaced} original file(s) backed up · ${esc(m.name)}</div></div>
        <button class="btn" data-restore="${esc(m.id)}">Restore originals</button></div>`).join("") : `<p class="muted">None. All your merges are separate copies.</p>`}
    </div>
    <div class="card"><h2>Separate copies</h2><p class="muted">${copies.length} merge${copies.length === 1 ? "" : "s"} run from their own folder, so the original games are safe.</p></div>
  </div>`;
  el.querySelectorAll("[data-restore]").forEach((b) => (b.onclick = async () => {
    if (await confirmBox("Restore originals?", "This removes the mods from that install and puts every backed-up file back.", "Restore")) {
      await followJob(await call("undo", b.dataset.restore), "Restoring"); loadBackups();
    }
  }));
}

// ---------- how fun each pairing would be (a colour on every game) ----------
const FUN_WORDS = { green: "Great pairing", yellow: "OK pairing", red: "Doable, not as great" };
const ALONE_WORDS = { green: "Fully moddable as a Host game", yellow: "Some mod support", red: "No real mod support" };
const SELF_WORDS = { green: "Host game, fully moddable", yellow: "Host game, limited mod support", red: "Host game, no real mod support" };

// Ratings are made in the background; this keeps asking until every game has one.
async function loadFun() {
  const base = game(state.selected);
  const all = [...state.games.map((g) => g.name), ...(state.owned?.games || []).map((g) => g.name)];
  const key = base ? base.name : "";  // no Host game picked yet: each game's own colour
  if (state.fun?.base !== key) state.fun = { base: key, scores: {}, self: null, pending: true };
  const fun = state.fun;
  applyFun();
  clearTimeout(loadFun.timer);
  let r;
  try {
    r = base ? await call("fun_scores", base.key, all.filter((n) => n !== base.name)) : await call("game_ratings", all);
  } catch (e) { return; }
  if (state.fun !== fun) return;
  Object.assign(fun.scores, r.scores);
  fun.self = r.self || null;
  fun.pending = r.pending;
  applyFun();
  if (r.pending) loadFun.timer = setTimeout(loadFun, 4000);
}

function applyFun() {
  const style = state.settings?.fun_style || "dot";
  document.body.classList.toggle("fun-halo", style === "halo");
  const base = game(state.selected);
  const fun = state.fun && (base ? base.name : "") === state.fun.base ? state.fun : null;
  document.querySelectorAll(".tile").forEach((t) => {
    const name = t.querySelector(".tile-name")?.textContent;
    const mine = t.classList.contains("selected");
    const f = style === "off" || !fun ? null : mine ? fun.self : fun.scores[name];
    if (style === "off") {
      delete t.dataset.fun;
      t.removeAttribute("title");
    } else if (f) {
      t.dataset.fun = f.rating;
      t.title = `${mine ? SELF_WORDS[f.rating] : base ? FUN_WORDS[f.rating] : ALONE_WORDS[f.rating]}: ${f.why}`;
    } else {
      t.dataset.fun = "pending";  // every game gets a colour; this one is still being rated
      t.title = "Rating…";
    }
  });
}

// ---------- a controller badge on every game ----------
async function loadPads() {
  const names = [...state.games.map((g) => g.name), ...(state.owned?.games || []).map((g) => g.name)];
  clearTimeout(loadPads.timer);
  let r;
  try { r = await call("controller_info", names); } catch (e) { return; }
  state.pads = r.games;
  applyPads();
  if (r.pending) loadPads.timer = setTimeout(loadPads, 5000);
}

function applyPads() {
  document.querySelectorAll(".tile").forEach((t) => {
    const p = state.pads?.[t.querySelector(".tile-name")?.textContent];
    let b = t.querySelector(".pad-badge");
    if (!p) return b && b.remove();
    if (!b) { b = document.createElement("span"); b.className = "pad-badge"; t.querySelector(".case").appendChild(b); }
    b.dataset.kind = p.badge;
    b.textContent = p.badge === "native" ? "🎮" : "🎮+";
    b.title = p.why;
  });
}

// ---------- preview: what a merge could look like, before downloading anything ----------
async function previewMerge(key, themeId) {
  const g = game(key);
  const t = g?.themes.find((x) => x.id === themeId);
  if (!g || !t) return;
  const box = $("#modal .modal-box");
  box.classList.add("wide");
  const done = modal(`Preview: ${g.name} × ${t.from_game}`, `<div class="preview-loading"><div class="bar indeterminate"><div></div></div>
      <p class="muted">Pulling up some clips…</p></div>`,
    [{ label: "Close", value: false }, { label: "Merge it", value: true, primary: true }]);
  let p;
  try { p = await call("merge_preview", key, themeId); } catch (e) { $("#modalBody").innerHTML = `<p class="muted">${esc(errText(e))}</p>`; }
  if (p && !$("#modal").classList.contains("hidden")) {
    const clip = (c) => `<figure class="pv-clip">
        ${c.url ? `<video src="${esc(c.url)}" poster="${esc(c.poster)}" muted loop autoplay playsinline preload="metadata" controls></video>`
          : `<a href="#" data-url="${esc(c.page)}"><img src="${esc(c.poster)}" alt=""></a>`}
        <figcaption>${c.label === g.name ? "Host game" : "Extension game"}: <strong>${esc(c.label)}</strong></figcaption></figure>`;
    const other = t.source_key != null ? game(t.source_key) : null;
    const left = p.base_art || state.covers[key] || genCover(g.name);
    const right = p.source_art || (other && state.covers[other.key]) || genCover(t.from_game);
    // slide 0: both games fused into one picture; then one slide per thing you can do in the merge
    // Every slide is ONE picture of the merged game, never two side by side:
    // - a screenshot from a mod for the Host game (the two games really merged), shown as a moving gallery, or
    // - a single blended frame: the Host game's trailer as the world, the Extension game's trailer melted into its
    //   middle with soft edges, both under one colour grade and light sweep.
    const src = t.from_game.split(" /")[0];
    const hero = (p.slides || []).find((x) => x.real && x.tag === "★ Hero") || (p.slides || []).find((x) => x.real);
    const slides = [{ fused: true, caption: `${g.name} × ${src}`, real: !!hero, gallery: hero ? hero.gallery : [], at: 0.35 }, ...(p.slides || [])];
    const items = (sl) => (sl.items || []).length ? `<div class="pv-items">${sl.items.map((x, k) => `<span style="--d:${(0.5 + k * 0.45).toFixed(2)}s">${esc(x)}</span>`).join("")}</div>` : "";
    const art = (sl) => sl.real
      ? (sl.gallery && sl.gallery.length ? sl.gallery : [sl.image]).map((u, k, all) => `<div class="pv-img pv-g ${all.length > 1 ? "" : "solo"}" style="background-image:url('${esc(u)}');--n:${all.length};--k:${k}"></div>`).join("")
      : `<div class="pv-img pv-host" style="background-image:url('${esc(sl.host_shot || left)}')"></div>
         <div class="pv-img pv-mixin" style="background-image:url('${esc(sl.source_shot || right)}')"></div><div class="pv-grade"></div>`;
    const tag = (sl) => sl.real ? `<span class="tag live" title="A picture from a mod for ${esc(g.name)}: the two games really merged">${esc(sl.fused ? "Real mod screenshot" : (sl.tag ? sl.tag + " · " : "") + "Mod screenshot")}</span> ` : sl.tag ? `<span class="tag live">${esc(sl.tag)}</span> ` : "";
    const slideHtml = (sl, i) => `<div class="pv-slide ${sl.real ? "pv-real" : "pv-comp"} ${sl.fused ? "pv-fused" : ""} ${i ? "" : "on"}">${art(sl)}${sl.fused ? "" : items(sl)}
        ${sl.fused ? `<div class="pv-cap big">${tag(sl)}<span>${esc(g.name)}</span><b>×</b><span>${esc(src)}</span></div>`
          : `<div class="pv-cap">${tag(sl)}${esc(sl.caption)}</div>`}</div>`;
    $("#modalBody").innerHTML = `
      <div class="pv-label">Preview · trailers and mod pictures, not your merged game</div>
      <div class="pv-pitch"><p>${esc(p.summary)}</p></div>
      <div class="pv-show" id="pvShow"><div class="pv-bg" id="pvBg"></div><div class="pv-mix" id="pvMix"></div>${slides.map(slideHtml).join("")}
        <button class="pv-nav prev" aria-label="Previous">‹</button><button class="pv-nav next" aria-label="Next">›</button>
        <div class="pv-dots">${slides.map((_, i) => `<button class="${i ? "" : "on"}" data-i="${i}" aria-label="Slide ${i + 1}"></button>`).join("")}</div></div>
      ${p.clips.length ? `<h3 class="pv-h">Trailers</h3><div class="pv-clips">${p.clips.map(clip).join("")}</div>` : ""}
      ${p.pictures.length ? `<h3 class="pv-h">What the mods look like</h3><div class="pv-pics">${p.pictures.map((x) => `<figure><img src="${esc(x.url)}" alt="" loading="lazy"><figcaption><strong>${esc(x.slot)}</strong><span>${esc(x.name)}</span></figcaption></figure>`).join("")}</div>` : ""}
      <p class="muted small">Clips stream straight from Steam and pictures from Nexus Mods at low quality, so nothing is saved to your PC. Your merge will be ${esc(g.name)} with these mods layered on. <a href="#" id="pvVideos">Watch mod showcase videos</a></p>`;
    $("#pvVideos").onclick = (e) => { e.preventDefault(); call("open_url", p.videos_url); };
    let cur = 0;
    // the blended frames move: both trailers play under the slide, each at a different moment per slide
    const mv = p.moving || {};
    const bg = !calm.matches && mv.base ? clipVideo(mv.base, "") : null;
    const mix = !calm.matches && mv.source ? clipVideo(mv.source, "") : null;
    if (bg) { $("#pvBg").appendChild(bg); bg.addEventListener("playing", () => $("#pvShow")?.classList.add("live")); }
    if (mix) { $("#pvMix").appendChild(mix); mix.addEventListener("playing", () => $("#pvShow")?.classList.add("live-mix")); }
    const seek = (v, at) => {
      if (!v) return;
      const go = () => { v.dataset.start = (v.duration * at).toFixed(1); v.currentTime = +v.dataset.start; v.play().catch(() => {}); };
      if (v.readyState >= 1 && v.duration) go(); else v.addEventListener("loadedmetadata", go, { once: true });
    };
    const show = (i) => {
      cur = (i + slides.length) % slides.length;
      $("#pvShow").querySelectorAll(".pv-slide").forEach((el, j) => el.classList.toggle("on", j === cur));
      $("#pvShow").querySelectorAll(".pv-dots button").forEach((el, j) => el.classList.toggle("on", j === cur));
      const sl = slides[cur];
      if (sl.real) { bg && bg.pause(); mix && mix.pause(); return; }  // a real merged screenshot needs nothing behind it
      seek(bg, sl.at || 0.3);
      seek(mix, 1 - (sl.at || 0.3) * 0.8);
    };
    show(0);
    let timer = null;
    const tick = () => { clearInterval(timer); timer = setInterval(() => { if (!$("#pvShow")) return clearInterval(timer); show(cur + 1); }, 4200); };
    tick();
    done.then(() => clearInterval(timer));
    const pick = (i) => { show(i); tick(); };  // a click gives that slide its full time
    $("#pvShow .prev").onclick = () => pick(cur - 1);
    $("#pvShow .next").onclick = () => pick(cur + 1);
    $("#pvShow").querySelectorAll(".pv-dots button").forEach((b) => (b.onclick = () => pick(+b.dataset.i)));
    $("#modalBody").querySelectorAll("[data-url]").forEach((a) => (a.onclick = (e) => { e.preventDefault(); call("open_url", a.dataset.url); }));
  }
  const go = await done;
  box.classList.remove("wide");
  $("#modalBody").querySelectorAll("video").forEach((v) => { v.pause(); v.removeAttribute("src"); v.querySelectorAll("source").forEach((x) => x.remove()); v.load(); }); // stop streaming
  if (go) startSetup();
}

// ---------- every game you own ----------
async function loadOwned(refresh = false, quiet = false) {
  let r;
  try { r = await call("library_owned", refresh); } catch (e) { if (!quiet) toast(errText(e)); return; }
  state.owned = r;
  if (!quiet) for (const err of r.errors || []) toast(err, 7000);
  if (r.styles_changed) {  // owned games are styles for every Host game too
    try { state.games = await call("library"); } catch (e) { /* keep the old list */ }
    if (state.view === "library") renderLibrary();
  }
  if (state.view === "library") renderOwnedShelf();
  for (const g of r.games) {
    if (g.key in state.covers) continue;
    state.covers[g.key] = null;
    call("owned_cover", g.key).then((c) => {
      if (!c) return;
      state.covers[g.key] = c;
      document.querySelectorAll(`[data-cover="${CSS.escape(g.key)}"]`).forEach((el) => {
        el.querySelectorAll(".face, .spine").forEach((f) => { f.style.backgroundImage = `url('${c}')`; f.classList.remove("placeholder"); });
        if (el.querySelector(".face")) el.querySelector(".face").textContent = "";
        if (el.classList.contains("pair-cover")) { el.style.backgroundImage = `url('${c}')`; el.classList.remove("placeholder"); el.textContent = ""; }
      });
    }).catch(() => {});
  }
}

function renderOwnedShelf() {
  const el = $("#ownedShelf");
  if (!el) return;
  const r = state.owned;
  if (!r || !r.sources.length) {
    el.innerHTML = `<div class="card row"><div class="grow"><strong>See every game you own</strong>
        <div class="muted small">Connect Steam, Epic or GOG and games that aren't on this PC show up here too, even ones on your other PC.</div></div>
        <button class="btn" id="ownedConnect">Connect launchers</button></div>`;
    $("#ownedConnect").onclick = () => show("settings");
    return;
  }
  const tiles = r.games.map((g) => `
      <button class="tile owned" data-owned="${esc(g.key)}" data-cover="${esc(g.key)}">
        ${caseHtml(g.name, state.covers[g.key])}
        <div class="tile-name">${esc(g.name)}</div>
        <div class="tile-sub">${esc(g.source)} · ${g.elsewhere ? "on " + esc(g.elsewhere) : "not installed"}</div>
      </button>`).join("");
  el.innerHTML = `<div class="shelf-head"><h2>Not on this PC <span class="muted small">${r.games.length}</span></h2>
      <div class="row"><span class="muted small">From ${esc(r.sources.join(", "))}</span><button class="btn ghost" id="ownedRefresh">Refresh</button></div></div>
    ${r.games.length ? `<div class="grid">${tiles}</div>` : `<div class="empty">Everything you own is installed here.</div>`}`;
  $("#ownedRefresh").onclick = async () => { $("#ownedRefresh").disabled = true; await loadOwned(true); };
  loadFun();
  loadPads();
  el.querySelectorAll("[data-owned]").forEach((b) => (b.onclick = async () => {
    const g = r.games.find((x) => x.key === b.dataset.owned);
    const go = await modal(g.name, `<p class="muted">${g.elsewhere ? `Installed on <strong>${esc(g.elsewhere)}</strong>, not on this PC.` : "You own this but it isn't installed here."}
      Install it with ${esc(g.source)}, then press Rescan and it can join your merges.</p>`,
      [{ label: "Close", value: false }, ...(g.install ? [{ label: `Install with ${g.source}`, value: true, primary: true }] : [])]);
    if (go) { try { await call("owned_install", g.key); } catch (e) { toast(errText(e)); } }
  }));
}

async function renderAccounts() {
  const el = $("#accountsCard");
  if (!el) return;
  let st;
  try { st = await call("library_status"); } catch (e) { return; }
  const row = (name, status, actions) => `<div class="row" style="padding:10px 0;border-top:1px solid var(--line)"><div class="grow"><strong>${name}</strong><div class="muted small">${status}</div></div>${actions}</div>`;
  const steamStatus = st.steam.on ? (st.steam.error ? esc(st.steam.error) : `Connected${st.steam.name ? " as " + esc(st.steam.name) : ""}${st.steam.has_key ? " · with your Web API key" : ""}`)
    : st.steam.id ? `Found ${st.steam.name ? esc(st.steam.name) : "your account"} in Steam on this PC.` : "Steam isn't signed in on this PC. Paste your profile link to connect.";
  el.innerHTML = `<h2>Your game accounts</h2>
    <p class="muted">Connect your launchers and the Library shows every game you own, not just what's installed here. With cloud backup on, it also shows which games are on your other PC.</p>
    ${row("Steam", steamStatus, st.steam.on ? `<button class="btn" data-acc="steamKey">Add Web API key</button><button class="btn ghost danger" data-off="Steam">Disconnect</button>`
      : `${st.steam.id ? "" : `<input class="input" id="steamProfile" placeholder="steamcommunity.com/profiles/7656…" style="width:260px">`}<button class="btn primary" data-acc="steam">Connect</button>`)}
    ${row("Epic Games", st.epic.on ? `Signed in${st.epic.name ? " as " + esc(st.epic.name) : ""}` : "Sign in once in a small Epic window.",
      st.epic.on ? `<button class="btn ghost danger" data-off="Epic">Sign out</button>` : `<button class="btn primary" data-acc="epic">Sign in</button>`)}
    ${row("GOG", st.gog.on ? "Connected through GOG Galaxy" : st.gog.available ? "GOG Galaxy is on this PC, so your whole GOG library can show." : "Install GOG Galaxy and sign in there to show your GOG library.",
      st.gog.on ? `<button class="btn ghost danger" data-off="GOG">Disconnect</button>` : st.gog.available ? `<button class="btn primary" data-acc="gog">Connect</button>` : "")}
    ${row("Xbox / Microsoft Store", "Microsoft doesn't let other apps list the games you own, so only games installed here show.", "")}
    <p class="muted small">Sign-ins are kept in Windows Credential Manager on this PC.${st.devices.length ? " Other PCs seen: " + esc(st.devices.join(", ")) + "." : ""}</p>`;
  const busy = (b, on) => { b.disabled = on; };
  el.querySelectorAll("[data-acc]").forEach((b) => (b.onclick = async () => {
    busy(b, true);
    try {
      if (b.dataset.acc === "steam") await call("steam_connect", $("#steamProfile")?.value || null, null);
      else if (b.dataset.acc === "steamKey") {
        const asked = modal("Steam Web API key", `<p class="muted">Only needed if your Steam game list is private. Get a free key at <a href="#" id="steamKeyLink">steamcommunity.com/dev/apikey</a> (put anything as the domain), then paste it here.</p>
          <input class="input" id="steamKeyIn" type="password" placeholder="Steam Web API key" autocomplete="off">`, [{ label: "Cancel", value: null }, { label: "Save", value: "save", primary: true }]);
        $("#steamKeyLink").onclick = (e) => { e.preventDefault(); call("open_url", "https://steamcommunity.com/dev/apikey"); };
        if (await asked) await call("steam_connect", null, $("#steamKeyIn").value);
      } else if (b.dataset.acc === "epic") {
        toast("Sign in to Epic in the window that opens.");
        const r = await call("epic_connect");
        if (r && r.manual) {
          call("open_url", r.url);
          const ok = await modal("Sign in to Epic", `<p class="muted">Sign in on the Epic page that opened. It ends on a page of text: copy all of it and paste it here.</p><textarea class="input" id="epicCode" rows="4"></textarea>`,
            [{ label: "Cancel", value: false }, { label: "Done", value: true, primary: true }]);
          if (ok) await call("epic_paste", $("#epicCode").value);
        }
      } else if (b.dataset.acc === "gog") await call("gog_connect");
      toast("Connected. Your games are in the Library.");
      loadOwned(false);
    } catch (e) { toast(errText(e), 7000); }
    renderAccounts();
  }));
  el.querySelectorAll("[data-off]").forEach((b) => (b.onclick = async () => {
    await call("library_disconnect", b.dataset.off);
    loadOwned(false);
    renderAccounts();
  }));
}

async function loadSettings() {
  const el = $("#view-settings");
  try { state.settings = await call("get_settings"); } catch (e) { toast(errText(e)); return; }
  const s = state.settings;
  el.innerHTML = `<div class="page"><h1>Settings</h1>
    <div class="card" id="profilesCard"></div>
    <div class="card"><h2>AI features</h2>
      <p class="muted">An optional Claude API key from <a href="#" id="consoleLink">console.anthropic.com</a> switches on the AI that works in the background: fun ratings for every pairing, mod searches for games without a built-in recipe, merge names and taglines, one-click crash repair and questlines. It usually costs a few cents a day. It only ever sees game names, mod file names and log files, never your game files. Without a key everything still works, with simpler guesses.</p>
      <div class="field"><label for="apiKey">Claude API key ${s.has_key ? `<span class="tag">Saved ${esc(s.key_hint)}</span>` : ""}</label>
        <div class="row" style="flex-wrap:nowrap"><input class="input" type="password" id="apiKey" placeholder="${s.has_key ? "Paste a new key to replace it" : "sk-ant-…"}" autocomplete="off">
          <button class="btn primary" id="saveKey">Save key</button>${s.has_key ? `<button class="btn ghost danger" id="clearKey">Remove</button>` : ""}</div></div>
      <p class="muted small">Saved only on this PC, in ${esc(s.data_folder)}.</p>
    </div>
    <div class="card"><h2>Nexus Mods</h2>
      <p class="muted">Add your Nexus Mods API key to find and download mods right here, no browsing. Get it at <a href="#" id="nexusLink">nexusmods.com → Settings → API keys</a>. Premium accounts download in one click; free accounts click "Mod Manager Download" once and the file comes straight back.</p>
      <div class="field"><label for="nexusKey">API key <span id="nexusTag"></span></label>
        <div class="row" style="flex-wrap:nowrap"><input class="input" type="password" id="nexusKey" placeholder="Paste your Nexus API key" autocomplete="off"><button class="btn primary" id="saveNexus">Save</button></div></div>
    </div>
    <div class="card"><h2>CurseForge</h2>
      <p class="muted">CurseForge has mods for Minecraft, The Sims 4, Stardew Valley, Valheim and lots more. Its API needs a free key: sign in at <a href="#" id="cfLink">console.curseforge.com</a>, open <strong>API keys</strong> and copy yours here. Modrinth needs no key.</p>
      <div class="field"><label for="cfKey">CurseForge API key ${s.has_curseforge ? `<span class="tag">Saved</span>` : ""}</label>
        <div class="row" style="flex-wrap:nowrap"><input class="input" type="password" id="cfKey" placeholder="${s.has_curseforge ? "Paste a new key to replace it" : "Paste your CurseForge API key"}" autocomplete="off"><button class="btn primary" id="saveCf">Save</button>${s.has_curseforge ? `<button class="btn ghost danger" id="clearCf">Remove</button>` : ""}</div></div>
    </div>
    <div class="card"><h2>Library</h2>
      <p class="muted">Every game gets a colour for how fun it would be to merge into your Host game: green is a great pairing, yellow is OK, red is doable but not as great. Hover a game to see why.</p>
      <div class="field"><label for="funStyle">Show it as</label>
        <select class="input" id="funStyle">${[["dot", "A dot in the corner"], ["halo", "A glow around the box"], ["off", "Off"]].map(([v, n]) => `<option value="${v}" ${(s.fun_style || "dot") === v ? "selected" : ""}>${n}</option>`).join("")}</select></div>
    </div>
    <div class="card"><h2>Item menu</h2>
      <p class="muted">Every item in a merge, mods' items included, from a menu in the game (Schedule I: F6 or Back + Y; Cyberpunk: the ~ overlay). It's always on in Creative.</p>
      <label class="row"><input type="checkbox" id="itemMenuNormal" ${s.item_menu_normal ? "checked" : ""}> Also in normal play</label>
    </div>
    <div class="card"><h2>Performance</h2>
      <p class="muted">Merged games shouldn't lag. Frankenstein's own mod in the game keeps to a budget, checks the game's FPS and turns its own extras down (crews, gangs, car and gun checks) when the game drops frames, then back up when it's smooth again. While a game runs, this app stops its animations and steps back so the game gets the PC.</p>
      <div class="field"><label for="perfLevel">In-game extras</label>
        <select class="input" id="perfLevel">${[["auto", "Automatic (recommended)"], ["quality", "Always full"], ["balanced", "Lighter"], ["performance", "Lightest, for slower PCs"]].map(([v, n]) => `<option value="${v}" ${(s.performance || "auto") === v ? "selected" : ""}>${n}</option>`).join("")}</select></div>
    </div>
    <div class="card"><h2>Save space</h2>
      <p class="muted">After you close a merged game, Frankenstein packs it away. It keeps only the files the merge changed (mods, their settings) and zips its saves, then deletes the copy. When you press Play it rebuilds the copy from your own installed game first. That takes about as long as copying the game: seconds for small games, several minutes for big ones like Cyberpunk. Your original game is only ever read.</p>
      <label class="row"><input type="checkbox" id="packAfterPlay" ${s.pack_after_play !== false ? "checked" : ""}> Pack merges away after playing</label>
    </div>
    <div class="card" id="accountsCard"></div>
    <div class="card" id="discordCard"></div>
    <div class="card"><h2>Cloud backup of saves</h2>
      <p class="muted">Every merge's saves get copied to your cloud drive after you play, so your PC and laptop stay in step. Pick the same drive on both.</p>
      <select class="input" id="cloudPick"><option value="">Off</option>${(s.cloud_options || []).map(([n, p]) => `<option value="${esc(p)}" ${s.cloud_folder === p ? "selected" : ""}>${esc(n)} (${esc(p)})</option>`).join("")}</select>
      ${(s.cloud_options || []).length ? "" : `<p class="muted small">No OneDrive or Google Drive folder found on this PC. Install one of them and it shows up here.</p>`}
    </div>
    <div class="card"><h2>Updates</h2>
      <div class="row"><div class="grow muted">You're on Frankenstein ${esc(s.version)}. It checks for updates every time it opens.</div><button class="btn" id="checkUpdate">Check now</button><button class="btn ghost" id="selfCheck" title="Check every feature, Frankenstein's own mods and your merges, and fix anything missing">Check everything</button></div>
    </div>
    <div class="card"><h2>Merging</h2>
      <div class="field"><label for="defMode">Default type</label>
        <select class="input" id="defMode">${MODES.map(([id, name]) => `<option value="${id}" ${s.mode === id ? "selected" : ""}>${name}</option>`).join("")}</select></div>
      <div class="field" style="margin-top:12px"><label for="defDest">Default folder for copies</label>
        <div class="row" style="flex-wrap:nowrap"><input class="input" id="defDest" value="${esc(s.destination)}" placeholder="Same drive as the game, in a Frankenstein folder"><button class="btn" id="defBrowse">Browse</button></div></div>
      <div class="row" style="margin-top:14px;justify-content:flex-end"><button class="btn primary" id="saveMerging">Save</button></div>
    </div></div>`;
  $("#consoleLink").onclick = (e) => { e.preventDefault(); call("open_url", "https://console.anthropic.com/"); };
  $("#saveKey").onclick = async () => {
    const v = $("#apiKey").value.trim();
    if (!v) return toast("Paste your API key first.");
    await call("save_settings", { claude_api_key: v }); toast("Saved. AI features are on."); state.fun = null; loadSettings();
  };
  if ($("#clearKey")) $("#clearKey").onclick = async () => { await call("save_settings", { claude_api_key: "" }); loadSettings(); };
  $("#nexusLink").onclick = (e) => { e.preventDefault(); call("open_url", "https://next.nexusmods.com/settings/api-keys"); };
  $("#cfLink").onclick = (e) => { e.preventDefault(); call("open_url", "https://console.curseforge.com/"); };
  $("#saveCf").onclick = async () => {
    try { await call("curseforge_set_key", $("#cfKey").value); toast("CurseForge is hooked up."); loadSettings(); } catch (e) { modal("That didn't work", `<p>${esc(errText(e))}</p>`); }
  };
  if ($("#clearCf")) $("#clearCf").onclick = async () => { await call("curseforge_set_key", ""); loadSettings(); };
  renderAccounts();
  renderAccountsCard();
  renderDiscord();
  if (!s.admin) {
    ["#apiKey", "#saveKey", "#clearKey", "#nexusKey", "#saveNexus", "#cfKey", "#saveCf", "#clearCf", "#defDest", "#defBrowse"]
      .forEach((q) => { const x = $(q); if (x) { x.disabled = true; x.title = "Set by the admin account"; } });
    if ($("#defMode")) $("#defMode").disabled = true;
  }
  call("nexus_status").then((n) => { if (n.has_key) $("#nexusTag").innerHTML = `<span class="tag">Saved · ${esc(n.name)}${n.premium ? " · Premium" : ""}</span>`; }).catch(() => {});
  $("#saveNexus").onclick = async () => {
    try { const n = await call("nexus_set_key", $("#nexusKey").value); toast(n.has_key ? "Nexus is hooked up." : "Nexus key removed."); loadSettings(); }
    catch (e) { modal("That didn't work", `<p>${esc(errText(e))}</p>`); }
  };
  $("#cloudPick").onchange = async () => { state.settings = await call("save_settings", { cloud_folder: $("#cloudPick").value }); toast($("#cloudPick").value ? "Cloud backup is on." : "Cloud backup is off."); };
  $("#checkUpdate").onclick = async () => offerUpdate(await call("check_update").catch(() => null), true);
  $("#selfCheck").onclick = async () => { toast("Checking everything…"); try { showSelfCheck(await call("self_check_result", true)); } catch (e) { toast(errText(e)); } };
  $("#perfLevel").onchange = async () => { state.settings = await call("save_settings", { performance: $("#perfLevel").value }); };
  $("#packAfterPlay").onchange = async () => { state.settings = await call("save_settings", { pack_after_play: $("#packAfterPlay").checked }); };
  $("#itemMenuNormal").onchange = async () => { state.settings = await call("save_settings", { item_menu_normal: $("#itemMenuNormal").checked }); };
  $("#funStyle").onchange = async () => { state.settings = await call("save_settings", { fun_style: $("#funStyle").value }); applyFun(); };
  $("#defBrowse").onclick = async () => { const p = await call("pick_folder"); if (p) $("#defDest").value = p; };
  $("#saveMerging").onclick = async () => { await call("save_settings", { mode: $("#defMode").value, destination: $("#defDest").value.trim() }); toast("Saved."); };
}

$("#modal").addEventListener("keydown", (e) => { if (e.key === "Escape" && $("#modalActions").children.length) { e.stopPropagation(); closeModal(); } });

// ---------- first run: ask Windows for permission once ----------
async function firstRun() {
  let st;
  try { st = await call("setup_status"); } catch (e) { return; }
  if (st.done) return;
  if (!st.needs.length) { call("run_setup").catch(() => {}); return; }
  const list = st.needs.map((f) => `<li>${esc(f)}</li>`).join("");
  const go = await modal("One quick thing first", `
    <p>Your games live in protected folders, so Windows needs to say it's OK for Frankenstein to make copies and add mods there.</p>
    <p class="muted">Windows will pop up <strong>once</strong>. Press Yes and you'll never be asked again, even for games you install later in the same library.</p>
    <details class="advanced"><summary>Which folders?</summary><ul class="muted small">${list}</ul></details>`,
    [{ label: "Later", value: false }, { label: "Sort it", value: true, primary: true }]);
  if (!go) return toast("No problem. Frankenstein asks again when you first merge.");
  const ok = await call("run_setup").catch(() => false);
  if (ok) toast("All set. You won't be asked again.");
  else modal("Windows said no", `<p class="muted">That's fine, nothing changed. Frankenstein asks again when you start your first merge.</p>`);
}

// ---------- the logo: a head with a brain of dots ----------
let splashBrain = null;
document.querySelectorAll("canvas[data-brain]").forEach((cv) => {
  const size = cv.dataset.brain;
  try {
    if (size === "front") { splashBrain = window.LogoBrain.mount(cv, { dense: 700, head: false, front: true }); return; }
    window.LogoBrain.mount(cv, { dense: size === "big" ? 1400 : size === "small" ? 420 : 180, speed: size === "big" ? 0.45 : 0.3 });
  } catch (e) { console.error("brain", e); }  // no brain is better than no app
});

// ---------- startup animation ----------
// The hooded figure holds a game over each palm, spinning opposite ways. As the loading bar fills they spin
// faster and the hands draw in; at 100% the hands slap together, the games merge, and a purple dotted burst
// opens the app. Reduced motion: no spinning, no burst, just the bar.
const splash = { p: 0, target: 0, spinL: 0, spinR: 0, last: 0, clap: -1, done: null };
function splashProgress(v) { splash.target = Math.max(splash.target, Math.min(1, v)); }
function splashCovers(a, b) {
  const set = (id, art) => { const c = $(id + " .sp-card"); if (c && art) c.style.backgroundImage = `url('${art}')`; };
  set("#spCoverL", a); set("#spCoverR", b);
}
splashCovers(genCover("Host game"), genCover("Extension game"));
function splashFrame(now) {
  const stage = $("#splashStage");
  if (!stage || !stage.isConnected) return;
  try { splashDraw(now); } catch (e) { console.error("splash", e); }
  requestAnimationFrame(splashFrame);
}
function splashDraw(now) {
  const dt = Math.min(0.05, (now - (splash.last || now)) / 1000);
  splash.last = now;
  splash.p += (splash.target - splash.p) * Math.min(1, dt * 3.2);
  const p = splash.p, still = calm.matches;
  const fill = $("#splashFill");
  if (fill) { fill.style.width = (p * 100).toFixed(1) + "%"; fill.parentElement.setAttribute("aria-valuenow", Math.round(p * 100)); }
  if (splashBrain) splashBrain.level = p;
  // the clap: 0..1 over half a second once loading is done
  const c = splash.clap < 0 ? 0 : Math.min(1, (now - splash.clap) / 480);
  const ease = c * c * (3 - 2 * c);
  if (!still) {
    const speed = 160 + p * 900 + ease * 1400;  // degrees a second
    splash.spinL += speed * dt; splash.spinR -= speed * dt;
  }
  // hands: in a little as it loads, then all the way to meet in the middle
  const hand = 5 * p + (34.6 - 5 * p) * ease;  // % of their own width
  const hl = $("#spHandL"), hr = $("#spHandR"), cl = $("#spCoverL"), cr = $("#spCoverR");
  if (!hl || !hr || !cl || !cr) return;
  hl.style.transform = `translateX(${hand}%) rotate(${ease * 8}deg)`;
  hr.style.transform = `translateX(${-hand}%) rotate(${-ease * 8}deg)`;
  // covers: closer together as it loads, then into one in the middle
  const bob = still ? 0 : Math.sin(now / 420) * 2;
  const cx = 70 * p + (175 - 70 * p) * ease;  // % of a cover's width
  const shrink = 1 - ease * 0.35;
  cl.style.transform = `translate(${cx}%, ${bob}%) scale(${shrink})`;
  cr.style.transform = `translate(${-cx}%, ${-bob}%) scale(${shrink})`;
  cl.firstElementChild.style.transform = still ? "" : `rotateY(${splash.spinL % 360}deg)`;
  cr.firstElementChild.style.transform = still ? "" : `rotateY(${splash.spinR % 360}deg)`;
  if (c >= 1 && splash.done) { const d = splash.done; splash.done = null; d(); }
}
requestAnimationFrame(splashFrame);
function splashBurst() {
  // a purple explosion of dots from the slap that fills the whole screen, corner to corner
  const cv = $("#splashBurst"), st = $("#splashStage");
  if (!cv || !st || calm.matches) { if (cv) cv.remove(); return Promise.resolve(); }
  const r = st.getBoundingClientRect(), dpr = window.devicePixelRatio || 1;
  cv.width = innerWidth * dpr; cv.height = innerHeight * dpr;
  const ctx = cv.getContext("2d");
  ctx.scale(dpr, dpr);
  const ox = r.left + r.width * 0.5, oy = r.top + r.height * 0.62;
  const far = Math.hypot(Math.max(ox, innerWidth - ox), Math.max(oy, innerHeight - oy));  // to the furthest corner
  const DUR = 1.7;
  const dots = Array.from({ length: 2400 }, () => {
    const a = Math.random() * Math.PI * 2, reach = far * (0.35 + Math.random() * 0.95);
    return { a, reach, r: 1.2 + Math.random() * 4.2, h: 255 + Math.random() * 50, l: 62 + Math.random() * 28 };
  });
  return new Promise((done) => {
    const start = performance.now();
    const step = (now) => {
      const t = (now - start) / 1000, k = Math.min(1, t / DUR), out = 1 - Math.pow(1 - k, 3);
      ctx.clearRect(0, 0, innerWidth, innerHeight);
      // the flash: a white-violet glow that floods the screen, then fades
      const fl = Math.max(0, 1 - t / 0.5);
      if (fl > 0) {
        const g = ctx.createRadialGradient(ox, oy, 0, ox, oy, far * (0.4 + out));
        g.addColorStop(0, `rgba(248,236,255,${0.95 * fl})`); g.addColorStop(0.35, `rgba(192,132,252,${0.6 * fl})`); g.addColorStop(1, "rgba(88,28,135,0)");
        ctx.fillStyle = g; ctx.fillRect(0, 0, innerWidth, innerHeight);
      }
      // the shockwave ring
      ctx.lineWidth = 6 + 30 * (1 - k);
      ctx.strokeStyle = `rgba(216,180,254,${0.7 * (1 - k)})`;
      ctx.beginPath(); ctx.arc(ox, oy, far * 1.15 * out, 0, Math.PI * 2); ctx.stroke();
      const fade = Math.max(0, 1 - Math.max(0, k - 0.55) / 0.45);
      for (const d of dots) {
        const dist = d.reach * out, x = ox + Math.cos(d.a) * dist, y = oy + Math.sin(d.a) * dist;
        ctx.fillStyle = `hsla(${d.h},95%,${d.l}%,${fade})`;
        ctx.beginPath(); ctx.arc(x, y, d.r * (1 + out * 0.8), 0, Math.PI * 2); ctx.fill();
      }
      if (k < 1) requestAnimationFrame(step); else { cv.remove(); done(); }
    };
    requestAnimationFrame(step);
  });
}
function splashClap() {
  return new Promise((done) => {
    splash.target = 1;
    const el = $("#splash");
    if (!el || el.classList.contains("gone") || calm.matches || document.hidden) return done();  // skipped: nothing to clap
    el.classList.add("clap");
    setTimeout(done, 1500);  // never waits on the animation for long
    splash.done = done;
    splash.clap = performance.now();
  });
}

// ---------- start ----------
const SPLASH = ["It's alive! Finding your games…", "Checking Steam, Epic, GOG and the Xbox app…", "Grabbing your box art…", "Working out how each game is modded…"];

// waits for a step, but never longer than `ms` (the step carries on in the background)
const atMost = (p, ms) => Promise.race([p, sleep(ms)]);
function splashText(t) { const l = $("#splashLine"); if (l && $("#splash")) l.textContent = t; }

(async () => {
  splashProgress(0.08);
  const started = Date.now();
  let i = 0;
  const rotate = setInterval(() => splashText(SPLASH[++i % SPLASH.length]), 1600);
  const creep = setInterval(() => splashProgress(splash.target + (0.78 - splash.target) * 0.12), 250);
  // two random games from last time over the palms straight away, real covers once the scan is done
  try {
    const last = JSON.parse(localStorage.getItem("splashGames") || "[]").sort(() => Math.random() - 0.5);
    if (last.length) splashCovers(genCover(last[0]), genCover(last[1] || last[0]));
  } catch (e) { /* first start */ }
  await whoIsPlaying();
  splashProgress(0.15);
  renderLibrary();
  try { state.settings = await atMost(call("get_settings"), 8000); } catch (e) { /* shown when needed */ }
  splashProgress(0.25);
  await atMost(scan(), 45000);
  clearInterval(creep);
  splashProgress(0.82);
  // the two games over the palms: two random games from your library, with their real covers
  const pick = [...(state.games || [])].sort(() => Math.random() - 0.5).slice(0, 6);
  try { localStorage.setItem("splashGames", JSON.stringify((state.games || []).map((g) => g.name).slice(0, 40))); } catch (e) { /* private mode */ }
  const arts = (await atMost(Promise.all(pick.map((g) => loadCover(g.key).catch(() => null))), 6000) || []).filter(Boolean);
  splashCovers(arts[0] || genCover(pick[0]?.name || "Host game"), arts[1] || genCover(pick[1]?.name || "Extension game"));
  splashProgress(0.92);
  loadOwned(false, true).then(() => state.owned?.sources.length && loadOwned(true, true));
  await sleep(Math.max(0, 2400 - (Date.now() - started)));
  clearInterval(rotate);
  const n = state.games.length;
  splashText(n > 1 ? `Found ${n} games.` : n ? "Found 1 game." : "No games found yet. Add a game folder in the Library.");
  splashProgress(1);
  if ($("#splash") && !$("#splash").classList.contains("gone")) {
    await sleep(900);
    await splashClap();
    splashBurst();
    await sleep(250);
  } else $("#splashBurst")?.remove();
  window.skipSplash ? window.skipSplash() : $("#splash")?.remove();
  if (await call("playing").catch(() => null)) watchPlaying();
  await sleep(700);
  await firstRun();
  await checkHandoffs();
  setInterval(checkHandoffs, 2000);
  offerUpdate(await call("check_update").catch(() => null));
})();
