"use strict";

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
    $("#modalBody").querySelectorAll("canvas[data-brain]").forEach((cv) => window.FranklinBrain.mount(cv, { dense: 520, speed: 0.4 }));
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
  ({ library: renderLibrary, setup: renderSetup, merges: loadMerges, party: loadParty, quests: renderQuests, backups: loadBackups, settings: loadSettings }[view])();
  renderFranklinCard();
}
document.querySelectorAll(".nav-item[data-view]").forEach((b) => (b.onclick = () => show(b.dataset.view)));

// ---------- 3D cases ----------
function caseHtml(name, cover, extra = "") {
  const art = cover ? `style="background-image:url('${cover}')"` : "";
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
  renderFranklinCard();
  for (const g of state.games) {
    loadCover(g.key).then((c) => {
      if (!c) return;
      document.querySelectorAll(`[data-cover="${g.key}"]`).forEach((el) => {
        el.querySelectorAll(".face, .spine").forEach((f) => { f.style.backgroundImage = `url('${c}')`; f.classList.remove("placeholder"); });
        el.querySelector(".face").textContent = "";
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
      <div class="banner-content">
        <div class="eyebrow">Base game</div>
        <h1>${esc(g.name)}</h1>
        <p>${esc(t ? t.description : "")}</p>
        ${g.warning ? `<div class="note small">${esc(g.warning)}</div>` : ""}
        <div class="row">
          <label for="themePick" class="muted">Merge with</label>
          <select id="themePick" class="input">${opts}</select>
          <button class="btn primary big" id="mergeBtn">Merge</button>
        </div>
      </div></section>`;
  } else {
    banner = `<section class="banner"><div class="banner-content">
        <div class="eyebrow">Welcome</div><h1>Pick a base game</h1>
        <p>Game Merger supports Cyberpunk 2077, Schedule I and GTA V as base games. If yours isn't found below, add its install folder.</p>
        <div class="row"><button class="btn primary" id="addFolder2">Add game folder</button></div>
      </div></section>`;
  }
  const tiles = state.games.map((x) => `
      <button class="tile ${x.base ? "base" : ""} ${x.key === state.selected ? "selected" : ""}" data-key="${x.key}" data-cover="${x.key}" aria-pressed="${x.key === state.selected}">
        ${caseHtml(x.name, state.covers[x.key])}
        <div class="tile-name">${esc(x.name)}</div>
        <div class="tile-sub">${esc(x.source)} · ${x.base ? (x.key === state.selected ? "selected" : "can be base") : "style source"}</div>
      </button>`).join("");
  el.innerHTML = `${banner}
    <section class="shelf">
      <div class="shelf-head"><h2>Your games</h2>
        <div class="row"><span class="muted small">Glowing edge = can be a base game</span>
          <button class="btn ghost" id="rescan">Rescan</button><button class="btn" id="addFolder">Add game folder</button></div></div>
      ${state.games.length ? `<div class="grid">${tiles}</div>` : `<div class="empty">No games found yet.</div>`}
    </section>`;
  el.querySelectorAll(".tile").forEach((b) => (b.onclick = () => {
    const x = game(b.dataset.key);
    if (!x.base) return toast(`${x.name} can't be a base game yet, but it can be a style you merge in.`);
    state.selected = x.key;
    state.themeId = x.themes[0]?.id;
    renderLibrary();
    renderFranklinCard();
  }));
  $("#rescan").onclick = scan;
  const add = async () => {
    try { const r = await call("add_game_folder"); if (r) { state.games = r; await scan(); } } catch (e) { toast(errText(e)); }
  };
  $("#addFolder").onclick = add;
  if ($("#addFolder2")) $("#addFolder2").onclick = add;
  if ($("#themePick")) $("#themePick").onchange = (e) => { state.themeId = e.target.value; renderLibrary(); renderFranklinCard(); };
  if ($("#mergeBtn")) $("#mergeBtn").onclick = startSetup;
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
      mode: o.mode, name: o.default_name, destination: o.destination,
    };
    show("setup");
  } catch (e) { toast(errText(e)); }
}

const MODES = [
  ["hardlink", "Fast copy", "Separate folder that shares untouched files with the original. Almost no extra space. Same drive only."],
  ["copy", "Full copy", "Completely independent folder. Safest, but as big as the game."],
  ["inplace", "Mod the original", "Changes your real install. Every replaced file is backed up first."],
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
    return `<div class="slot"><div class="slot-head"><span class="slot-name">${esc(sl.name)}</span>
        <button class="btn ghost" data-find="${i}">Find mods</button><button class="btn" data-add="${i}">Add file</button></div>${files}</div>`;
  }).join("");
  const clashes = s.check.clashes.length
    ? `<div class="note"><strong>Heads up:</strong> these mods change the same files, so the one added last wins.<ul>${s.check.clashes.map((c) => `<li>${esc(c)}</li>`).join("")}</ul>Ask Franklin which to keep.</div>` : "";
  const modes = MODES.map(([id, name, text]) => `<button class="mode ${s.mode === id ? "on" : ""}" data-mode="${id}" aria-pressed="${s.mode === id}"><strong>${name}</strong><span>${text}</span></button>`).join("");

  $("#view-setup").innerHTML = `<div class="page">
    <div><button class="btn ghost" id="back">← Library</button></div>
    <div class="setup-head">
      <div data-cover="${g.key}">${caseHtml(g.name, state.covers[g.key])}</div>
      <div><div class="eyebrow">New merge</div><h1>${esc(g.name)} <span class="versus">×</span> ${esc(t.from_game)}</h1>
        <p class="lede" style="margin-top:6px">${esc(t.description)}</p></div>
    </div>
    ${g.warning ? `<div class="note">${esc(g.warning)}</div>` : ""}
    <div class="card"><div class="section-title"><span class="step">1</span><h2>Pick your mods</h2></div>
      <p class="muted" style="margin-top:-6px">Click <strong>Find mods</strong>, then download the ones you like from Nexus Mods. I'll spot each download and add it here for you. You can also skip this and add mods later.</p>
      ${slots}${clashes}</div>
    <div class="card"><div class="section-title"><span class="step">2</span><h2>Merge it</h2></div>
      <p class="muted" style="margin-top:-6px">Everything else is handled: mod loaders download automatically, the merge goes into its own safe copy, and it gets its own save games.</p>
      <div class="row" style="justify-content:flex-end"><button class="btn primary big" id="go">Merge it</button></div></div>
    <details class="card advanced" ${s.advancedOpen ? "open" : ""}>
      <summary><h2>Advanced options</h2><span class="muted small">Only if you want to change how it's done</span></summary>
      <div class="label" style="margin:14px 0 4px">Mod loaders</div>${fw}
      <div class="label" style="margin:16px 0 8px">Where to put it</div>
      <div class="modes">${modes}</div>
      <div class="two" style="margin-top:14px">
        <div class="field"><label for="mName">Name</label><input class="input" id="mName" value="${esc(s.name)}"></div>
        <div class="field"><label for="mDest">Folder</label><div class="row" style="flex-wrap:nowrap"><input class="input" id="mDest" value="${esc(s.mode === "inplace" ? g.path : s.destination)}" ${s.mode === "inplace" ? "disabled" : ""}>
          <button class="btn" id="mBrowse" ${s.mode === "inplace" ? "disabled" : ""}>Browse</button></div></div>
      </div></details>
  </div>`;

  const v = $("#view-setup");
  $("#back").onclick = () => show("library");
  v.querySelector(".advanced").ontoggle = (e) => (s.advancedOpen = e.target.open);
  v.querySelectorAll("[data-fw]").forEach((c) => (c.onchange = () => { c.checked ? s.frameworks.add(c.dataset.fw) : s.frameworks.delete(c.dataset.fw); }));
  v.querySelectorAll("[data-url]").forEach((b) => (b.onclick = () => call("open_url", b.dataset.url)));
  v.querySelectorAll("[data-manual]").forEach((b) => (b.onclick = async () => {
    const p = await call("pick_file"); if (p) { s.manual[b.dataset.manual] = p; renderSetup(); }
  }));
  v.querySelectorAll("[data-find]").forEach((b) => (b.onclick = () => {
    const slot = s.options.slots[+b.dataset.find];
    call("find_mods", s.key, slot.search);
    watchDownloads(slot.name);
  }));
  v.querySelectorAll("[data-add]").forEach((b) => (b.onclick = async () => {
    const files = await call("pick_mod_files");
    if (!files || !files.length) return;
    files.forEach((p) => s.mods.push({ slot: s.options.slots[+b.dataset.add].name, path: p }));
    renderSetup();
    checkMods();
  }));
  v.querySelectorAll("[data-remove]").forEach((b) => (b.onclick = () => { s.mods.splice(+b.dataset.remove, 1); renderSetup(); checkMods(); }));
  v.querySelectorAll("[data-mode]").forEach((b) => (b.onclick = () => { s.mode = b.dataset.mode; renderSetup(); }));
  $("#mName").oninput = (e) => (s.name = e.target.value);
  $("#mDest").oninput = (e) => { if (s.mode !== "inplace") s.destination = e.target.value; };
  $("#mBrowse").onclick = async () => { const p = await call("pick_folder"); if (p) { s.destination = p; renderSetup(); } };
  $("#go").onclick = runMerge;
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
  toast("Download any mod you like. I'll add it to “" + slotName + "” automatically.", 4500);
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
  renderFranklinCard();
}

async function runMerge() {
  const s = state.setup;
  const fw = s.options.frameworks.filter((f) => s.frameworks.has(f.id));
  if (s.mode === "inplace" && !(await confirmBox("Change the original install?", "Every replaced file is backed up and Undo puts it back. Continue?"))) return;
  if (!s.name.trim()) return toast("Give your merge a name.");
  let id;
  try {
    id = await call("start_merge", {
      key: s.key, theme_id: s.themeId, mode: s.mode, name: s.name, destination: s.destination,
      frameworks: fw.map((f) => f.id), manual_files: s.manual, mods: s.mods,
    });
  } catch (e) { return modal("Couldn't start", `<p>${esc(errText(e))}</p>`); }
  const res = await followJob(id, "Merging " + s.name);
  if (res && res.result) {
    const p = res.result.problems;
    const body = `<p>“${esc(res.result.name)}” is ready. Press Play in My merges.</p>` +
      (p.length ? `<div class="note">Some parts didn't install:<ul>${p.map((x) => `<li>${esc(x)}</li>`).join("")}</ul>Franklin can help with these.</div>` : "");
    const go = await modal("Merge complete", body, [{ label: "Stay here", value: false }, { label: "Go to My merges", value: true, primary: true }]);
    state.setup = null;
    show(go ? "merges" : "library");
  }
}

// Shows a progress window for a background job until it finishes.
async function followJob(id, title) {
  $("#modalTitle").textContent = title;
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
        <div class="muted">${esc(m.game)} × ${esc(m.theme)}</div>
        <div class="row small"><span class="tag">${esc(m.mode_label)}</span><span class="tag">${m.mods.length} installed</span>${m.has_saves ? `<span class="tag">Own saves</span>` : ""}${m.playing ? `<span class="tag live">Playing now</span>` : ""}${m.status === "restored" ? `<span class="tag">Mods removed</span>` : ""}</div>
        <div class="muted small" title="${esc(m.folder)}">${esc(m.folder)}</div>
      </div>
      <div class="merge-actions">
        <button class="btn primary" data-act="play">▶ Play</button>
        <button class="btn" data-act="add">Add mods</button>
        <button class="btn" data-act="share">Share</button>
        <button class="btn" data-act="crash">Why did it crash?</button>
        <button class="btn ghost" data-act="folder">Open folder</button>
        <button class="btn ghost" data-act="undo">Undo all mods</button>
        <button class="btn ghost danger" data-act="delete">Delete</button>
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
  el.querySelectorAll("[data-act]").forEach((b) => (b.onclick = () => mergeAction(b.closest("[data-id]").dataset.id, b.dataset.act)));
  $("#importBtn").onclick = async () => { const p = await call("pick_share_file"); if (p) importMerge(p); };
}

// ---------- updates ----------
async function offerUpdate(u, asked = false) {
  if (!u) { if (asked) toast("You're on the newest version."); return; }
  const go = await modal("Update ready, our kid", `
    <p>Game Merger <strong>${esc(u.version)}</strong> is out. It takes a few seconds: the app closes, updates and opens again. Your merges and saves aren't touched.</p>
    ${u.notes ? `<div class="log">${esc(u.notes)}</div>` : ""}`,
    [{ label: "Later", value: false }, { label: "Update now", value: true, primary: true }]);
  if (go) followJob(await call("install_update"), "Updating Game Merger");
}

// ---------- sharing ----------
const mb = (n) => (n / 1048576).toFixed(n > 104857600 ? 0 : 1) + " MB";

async function shareMerge(m) {
  const job = await call("share_merge", m.id);
  const r = (await followJob(job, "Packing " + m.name))?.result;
  if (!r) return;
  modal("Ready to share", `<p>Saved <strong>${esc(basename(r.path))}</strong> (${mb(r.size)}) in your Downloads folder.</p>
    <p class="muted">Send it to your friend (Discord, Google Drive, a USB stick). They double-click it and Game Merger builds the same merge from their own copy of the game. Only the mods are shared, never game files.</p>`);
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
  if (r) { toast("Sound, “" + r.name + "” is ready to play."); show("merges"); }
}

// Refresh My merges when the game closes and the normal saves are put back.
let playWatch = null;
function watchPlaying() {
  if (playWatch) return;
  playWatch = setInterval(async () => {
    const now = await call("playing").catch(() => null);
    if (!now) {
      clearInterval(playWatch); playWatch = null;
      toast("Game closed. Your merge's progress is saved and your normal saves are back.", 4500);
      if (state.view === "merges") loadMerges();
    }
  }, 5000);
}

async function mergeAction(id, act) {
  const m = state.merges.find((x) => x.id === id);
  try {
    if (act === "play") { await call("play", id); toast("Starting " + m.name + "… its own saves are loaded."); loadMerges(); watchPlaying(); }
    else if (act === "folder") await call("open_folder", id);
    else if (act === "share") await shareMerge(m);
    else if (act === "crash") { openFranklin(); askFranklin("My merged game crashed or a mod isn't working. What went wrong?", { merge: id }); }
    else if (act === "add") { const job = await call("add_mods", id); if (job) { await followJob(job, "Adding mods"); loadMerges(); } }
    else if (act === "undo") {
      if (await confirmBox("Undo all mods?", `This removes every mod from “${m.name}” and puts the original files back.`, "Undo all mods")) { await followJob(await call("undo", id), "Undoing"); loadMerges(); }
    } else if (act === "delete") {
      const text = m.mode === "inplace" ? "This undoes all mods in your original install and forgets this merge." : `This deletes the merged copy at ${m.folder}. Your original game is not touched.`;
      if (await confirmBox("Delete merge?", text, "Delete", true)) { await followJob(await call("delete", id), "Deleting"); loadMerges(); }
    }
  } catch (e) { modal("That didn't work", `<p>${esc(errText(e))}</p>`); }
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
      <button class="btn primary" id="partySetup">Set it up</button></div>`;
  } else if (!p.party) {
    main = `<div class="card"><h2>Host a party</h2><p class="muted">You get a code to send your friends. Your PC runs the party, so keep Game Merger open while you play.</p>
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
        <div class="row" style="justify-content:flex-end;margin-top:14px"><button class="btn ghost danger" id="partyLeave">${q.host ? "End party" : "Leave"}</button></div></div>`;
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
  on("partyCopy", async () => { await call("copy_text", p.party.code); toast("Code copied. Send it to your friends."); });
  el.querySelectorAll("[data-letin]").forEach((b) => (b.onclick = async () => { await call("party_let_in", b.dataset.letin, ""); loadParty(); }));
  el.querySelectorAll("[data-kick]").forEach((b) => (b.onclick = async () => { await call("party_kick", b.dataset.kick); loadParty(); }));
}

// ---------- questlines, backups, settings ----------
function renderQuests() {
  $("#view-quests").innerHTML = `<div class="page"><h1>Questlines</h1>
    <p class="lede">Coming next: Franklin will write new missions in the style of the game you merged in, and a small quest-loader mod will play them inside your base game.</p>
    <div class="card"><h2>How it will work</h2>
      <ul class="muted" style="line-height:1.8">
        <li>Pick a merge and a theme, like “street race series” or “take back the block”.</li>
        <li>Franklin writes the quests: objectives, places, enemies, rewards.</li>
        <li>Game Merger installs the quest loader for that game and the quests with it.</li>
      </ul>
      <div class="row small"><span class="tag">GTA V: script missions</span><span class="tag">Schedule I: MelonLoader</span><span class="tag">Cyberpunk: fixer-style jobs</span></div>
    </div></div>`;
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

async function loadSettings() {
  const el = $("#view-settings");
  try { state.settings = await call("get_settings"); } catch (e) { toast(errText(e)); return; }
  const s = state.settings;
  el.innerHTML = `<div class="page"><h1>Settings</h1>
    <div class="card"><h2>Franklin</h2>
      <p class="muted">Franklin runs on Claude using your own Anthropic API key. Get one at <a href="#" id="consoleLink">console.anthropic.com</a>. Each question usually costs a few cents. Franklin only sees game names, mod file names and log files, never your game files.</p>
      <div class="field"><label for="apiKey">Claude API key ${s.has_key ? `<span class="tag">Saved ${esc(s.key_hint)}</span>` : ""}</label>
        <div class="row" style="flex-wrap:nowrap"><input class="input" type="password" id="apiKey" placeholder="${s.has_key ? "Paste a new key to replace it" : "sk-ant-…"}" autocomplete="off">
          <button class="btn primary" id="saveKey">Save key</button>${s.has_key ? `<button class="btn ghost danger" id="clearKey">Remove</button>` : ""}</div></div>
      <p class="muted small">Saved only on this PC, in ${esc(s.data_folder)}.</p>
    </div>
    <div class="card"><h2>Franklin's voice</h2>
      <label class="switch"><input type="checkbox" id="voiceOn" ${s.voice_on ? "checked" : ""}> Franklin talks out loud</label>
      <div class="field" style="margin-top:12px"><label for="voiceStyle">Voice</label>
        <div class="row" style="flex-wrap:nowrap"><select class="input" id="voiceStyle">${(s.voice_styles || []).map(([id, name]) => `<option value="${id}" ${s.voice_style === id ? "selected" : ""}>${esc(name)}</option>`).join("")}</select>
          <button class="btn" id="testVoice">Hear Franklin</button></div></div>
      <p class="muted">These are free, natural-sounding voices, no key needed. If you're offline he falls back to the voice built into Windows. For an even more lifelike voice, paste an <a href="#" id="elevenLink">ElevenLabs</a> key (it has a free tier).</p>
      <div class="field"><label for="elevenKey">ElevenLabs key (optional) ${s.has_eleven ? `<span class="tag">Saved${s.voice_name ? " · " + esc(s.voice_name) : ""}</span>` : ""}</label>
        <div class="row" style="flex-wrap:nowrap"><input class="input" type="password" id="elevenKey" placeholder="${s.has_eleven ? "Paste a new key to replace it" : "Paste your ElevenLabs key"}" autocomplete="off">
          <button class="btn primary" id="saveEleven">Save</button>${s.has_eleven ? `<button class="btn ghost danger" id="clearEleven">Remove</button>` : ""}</div></div>
    </div>
    <div class="card"><h2>Updates</h2>
      <div class="row"><div class="grow muted">You're on Game Merger ${esc(s.version)}. It checks for updates every time it opens.</div><button class="btn" id="checkUpdate">Check now</button></div>
    </div>
    <div class="card"><h2>Merging</h2>
      <div class="field"><label for="defMode">Default type</label>
        <select class="input" id="defMode">${MODES.map(([id, name]) => `<option value="${id}" ${s.mode === id ? "selected" : ""}>${name}</option>`).join("")}</select></div>
      <div class="field" style="margin-top:12px"><label for="defDest">Default folder for copies</label>
        <div class="row" style="flex-wrap:nowrap"><input class="input" id="defDest" value="${esc(s.destination)}" placeholder="Same drive as the game, in a GameMerger folder"><button class="btn" id="defBrowse">Browse</button></div></div>
      <div class="row" style="margin-top:14px;justify-content:flex-end"><button class="btn primary" id="saveMerging">Save</button></div>
    </div></div>`;
  $("#consoleLink").onclick = (e) => { e.preventDefault(); call("open_url", "https://console.anthropic.com/"); };
  $("#saveKey").onclick = async () => {
    const v = $("#apiKey").value.trim();
    if (!v) return toast("Paste your API key first.");
    await call("save_settings", { claude_api_key: v }); toast("Saved. Franklin is ready."); loadSettings(); renderFranklinCard();
  };
  if ($("#clearKey")) $("#clearKey").onclick = async () => { await call("save_settings", { claude_api_key: "" }); loadSettings(); renderFranklinCard(); };
  $("#checkUpdate").onclick = async () => offerUpdate(await call("check_update").catch(() => null), true);
  $("#elevenLink").onclick = (e) => { e.preventDefault(); call("open_url", "https://elevenlabs.io/"); };
  $("#voiceStyle").onchange = async () => {
    state.settings = await call("save_settings", { voice_style: $("#voiceStyle").value });
    speak("Alright our kid, how's this one sound?", true);
  };
  $("#voiceOn").onchange = async () => { await setVoice($("#voiceOn").checked); };
  $("#saveEleven").onclick = async () => {
    const v = $("#elevenKey").value.trim();
    if (!v) return toast("Paste your ElevenLabs key first.");
    await call("save_settings", { elevenlabs_key: v }); toast("Saved. Finding Franklin a voice…"); await loadSettings();
    speak("Alright our kid, that's me sorted. Sound.");
  };
  if ($("#clearEleven")) $("#clearEleven").onclick = async () => { await call("save_settings", { elevenlabs_key: "" }); loadSettings(); };
  $("#testVoice").onclick = () => speak("Alright our kid, it's Franklin. Pick a game and let's get merging.", true);
  $("#defBrowse").onclick = async () => { const p = await call("pick_folder"); if (p) $("#defDest").value = p; };
  $("#saveMerging").onclick = async () => { await call("save_settings", { mode: $("#defMode").value, destination: $("#defDest").value.trim() }); toast("Saved."); };
}

// ---------- Franklin ----------
const app = $(".app");
function openFranklin() { app.classList.remove("no-franklin"); }
$("#franklinToggle").onclick = () => app.classList.toggle("no-franklin");
$("#franklinClose").onclick = () => app.classList.add("no-franklin");
$("#franklinMute").onclick = () => setVoice(!voiceOn());

$("#franklinNew").onclick = async () => { await call("franklin_reset"); $("#franklinMessages").innerHTML = ""; renderFranklinCard(); };

function franklinContext() {
  const ctx = { screen: { library: "Library", setup: "Merge setup", merges: "My merges", party: "Play together", quests: "Questlines", backups: "Backups", settings: "Settings" }[state.view] };
  ctx.installed_games = state.games.map((g) => `${g.name} (${g.source}${g.base ? ", can be base" : ""})`);
  const s = state.setup && state.view === "setup" ? state.setup : null;
  const g = game(s ? s.key : state.selected);
  if (g) {
    ctx.base_game = g.name;
    const t = g.themes.find((x) => x.id === (s ? s.themeId : state.themeId));
    if (t) { ctx.theme = t.name + " (from " + t.from_game + ")"; ctx.theme_description = t.description; }
  }
  if (s) {
    ctx.frameworks = s.options.frameworks.filter((f) => s.frameworks.has(f.id)).map((f) => f.name);
    ctx.mods = s.check.mods;
    ctx.clashes = s.check.clashes;
  }
  return ctx;
}

function renderFranklinCard() {
  const card = $("#franklinCard");
  const hasKey = state.settings ? state.settings.has_key : null;
  const g = game(state.setup && state.view === "setup" ? state.setup.key : state.selected);
  const t = g && g.themes.find((x) => x.id === (state.setup && state.view === "setup" ? state.setup.themeId : state.themeId));
  let html = g && t
    ? `<div class="eyebrow">This merge</div>${esc(g.name)} stays the game you play. ${esc(t.name)} gets layered on with mods. Your original install isn't touched unless you choose to mod it directly.`
    : `<div class="eyebrow">Hi, I'm Franklin</div>Pick a base game and I'll explain what a merge will do, check your mods, and help when something crashes.`;
  if (state.setup && state.view === "setup" && state.setup.check.clashes.length)
    html += `<div class="note small" style="margin-top:8px">${state.setup.check.clashes.length} mod clash${state.setup.check.clashes.length > 1 ? "es" : ""} found. Ask me which to keep.</div>`;
  if (hasKey === false) html += `<div class="note small" style="margin-top:8px">Add your Claude API key in <a href="#" id="toSettings">Settings</a> to chat with me.</div>`;
  card.innerHTML = html;
  if ($("#toSettings")) $("#toSettings").onclick = (e) => { e.preventDefault(); show("settings"); };
}

// Minimal, safe formatting for Franklin's replies: paragraphs, bullet lists and **bold**.
function formatReply(text) {
  const blocks = esc(text).split(/\n{2,}/);
  return blocks.map((b) => {
    const lines = b.split("\n");
    if (lines.every((l) => /^\s*([-*•]|\d+\.)\s+/.test(l)))
      return "<ul>" + lines.map((l) => "<li>" + l.replace(/^\s*([-*•]|\d+\.)\s+/, "") + "</li>").join("") + "</ul>";
    return "<p>" + lines.join("<br>") + "</p>";
  }).join("").replace(/\*\*(.+?)\*\*/g, "<strong>$1</strong>");
}

function addMsg(cls, html) {
  const d = document.createElement("div");
  d.className = "msg " + cls;
  d.innerHTML = html;
  $("#franklinMessages").appendChild(d);
  $("#franklinBody").scrollTop = $("#franklinBody").scrollHeight;
  return d;
}

let franklinBusy = false;
async function askFranklin(text, opts = {}) {
  if (franklinBusy || !text.trim()) return;
  franklinBusy = true;
  addMsg("me", esc(text));
  const out = addMsg("typing", "");
  let sid;
  let logsFor = opts.merge || null;
  if (opts.crashLogs && !logsFor) logsFor = state.selected != null ? "game:" + state.selected : null;
  try {
    sid = await call("franklin_send", text, franklinContext(), logsFor);
  } catch (e) {
    out.className = "msg err"; out.textContent = errText(e); franklinBusy = false; return;
  }
  let full = "", since = 0;
  window.FranklinBrain.talking(true);
  while (true) {
    let r;
    try { r = await call("franklin_poll", sid, since); } catch (e) { r = { text: "", done: true, error: errText(e) }; }
    since = r.next ?? since;
    if (r.text) { full += r.text; out.innerHTML = formatReply(full); $("#franklinBody").scrollTop = $("#franklinBody").scrollHeight; }
    if (r.done) {
      out.classList.remove("typing");
      if (r.error) {
        out.className = "msg err";
        out.innerHTML = esc(r.error) + (/Settings/.test(r.error) ? ` <a href="#" class="goSettings">Open Settings</a>` : "");
        out.querySelector(".goSettings")?.addEventListener("click", (e) => { e.preventDefault(); show("settings"); });
      }
      break;
    }
    await sleep(120);
  }
  window.FranklinBrain.talking(false);
  franklinBusy = false;
  if (full) speak(full);
}

const CHIPS = {
  explain: ["What will this merge actually do, and what won't it be able to do?", {}],
  suggest: ["Which kinds of mods should I look for to get the most out of this merge? Tell me what to search for.", {}],
  check: ["Check the mods I've added. Do any clash, need something that's missing, or look like they're for a different game?", {}],
  crash: ["My game crashed or a mod isn't working. Check the logs and tell me what went wrong.", { crashLogs: true }],
  online: ["Is it safe to play this merged game online?", {}],
};
document.querySelectorAll("#franklinChips [data-ask]").forEach((b) => (b.onclick = () => { const [t, o] = CHIPS[b.dataset.ask]; askFranklin(t, o); }));
$("#franklinForm").onsubmit = (e) => { e.preventDefault(); const v = $("#franklinText").value; $("#franklinText").value = ""; askFranklin(v); };
$("#modal").addEventListener("keydown", (e) => { if (e.key === "Escape" && $("#modalActions").children.length) closeModal(); });

// ---------- Franklin's voice ----------
// Your own cloned voice or ElevenLabs if set up, otherwise a free natural voice, and offline the Windows one.
const voiceOn = () => !state.settings || state.settings.voice_on !== false;
let speaking = null;

function stopSpeaking() {
  speechRun++;
  if (speaking) { speaking.pause(); speaking = null; }
  if (window.speechSynthesis) speechSynthesis.cancel();
  window.FranklinBrain.talking(false);
}

async function setVoice(on) {
  if (!on) stopSpeaking();
  try { state.settings = { ...(state.settings || {}), ...(await call("save_settings", { voice_on: on })) }; } catch (e) { toast(errText(e)); }
  renderMute();
  if ($("#voiceOn")) $("#voiceOn").checked = on;
  toast(on ? "Franklin's voice is on." : "Franklin's muted.");
}

function renderMute() {
  const b = $("#franklinMute"), on = voiceOn();
  b.classList.toggle("muted", !on);
  b.title = on ? "Mute Franklin's voice" : "Unmute Franklin's voice";
  b.setAttribute("aria-label", b.title);
}

function fallbackVoice() {
  const voices = window.speechSynthesis ? speechSynthesis.getVoices() : [];
  const en = voices.filter((v) => /en[-_]US/i.test(v.lang));
  const male = /Andrew|Brian|Christopher|Roger|Guy|Eric|Davis|Mark|David|Male/i;
  return en.find((v) => male.test(v.name) && /Natural|Online/i.test(v.name)) || en.find((v) => male.test(v.name)) || en[0] || null;
}
if (window.speechSynthesis) speechSynthesis.onvoiceschanged = () => {};

function speakable(text) {
  return text.replace(/\*\*/g, "").replace(/^\s*([-*•]|\d+\.)\s+/gm, "").replace(/https?:\/\/\S+/g, "the link").replace(/\s+/g, " ").trim().slice(0, 1200);
}

// Long replies are spoken a sentence or two at a time, making the next clip while one plays,
// so Franklin starts talking quickly even with the slower voices.
function chunks(text, max = 220) {
  const out = [];
  let cur = "";
  for (const s of text.match(/[^.!?]+[.!?]*\s*/g) || [text]) {
    if (cur && (cur + s).length > max) { out.push(cur.trim()); cur = ""; }
    cur += s;
  }
  if (cur.trim()) out.push(cur.trim());
  return out;
}

let speechRun = 0;
async function speak(text, force = false) {
  if ((!voiceOn() && !force) || !text) return;
  stopSpeaking();
  const run = ++speechRun;
  const parts = chunks(speakable(text));
  const fetchPart = (i) => (i < parts.length ? call("franklin_voice", parts[i], force).catch(() => null) : Promise.resolve(null));
  let next = fetchPart(0);
  window.FranklinBrain.talking(true);
  for (let i = 0; i < parts.length && run === speechRun; i++) {
    const uri = await next;
    if (run !== speechRun) return;
    next = fetchPart(i + 1);
    await (uri ? playUri(uri) : sayBuiltIn(parts[i]));
  }
  if (run === speechRun) window.FranklinBrain.talking(false);
}

function playUri(uri) {
  return new Promise((done) => {
    speaking = new Audio(uri);
    speaking.onended = speaking.onerror = done;
    speaking.onpause = done;
    speaking.play().catch(done);
  });
}

function sayBuiltIn(line) {
  return new Promise((done) => {
    if (!window.speechSynthesis) return done();
    const u = new SpeechSynthesisUtterance(line);
    const v = fallbackVoice();
    if (v) u.voice = v;
    u.lang = "en-US";
    u.pitch = 0.9;
    u.onend = u.onerror = done;
    speechSynthesis.speak(u);
  });
}

// ---------- first run: ask Windows for permission once ----------
async function firstRun() {
  let st;
  try { st = await call("setup_status"); } catch (e) { return; }
  if (st.done) return;
  if (!st.needs.length) { call("run_setup").catch(() => {}); return; }
  const list = st.needs.map((f) => `<li>${esc(f)}</li>`).join("");
  const go = await modal("One quick thing, our kid", `
    <canvas class="welcome-brain brain" data-brain="welcome" aria-hidden="true"></canvas>
    <p>Your games live in protected folders, so Windows needs to say it's OK for Game Merger to make copies and add mods there.</p>
    <p class="muted">Windows will pop up <strong>once</strong>. Press Yes and you'll never be asked again, even for games you install later in the same library.</p>
    <details class="advanced"><summary>Which folders?</summary><ul class="muted small">${list}</ul></details>`,
    [{ label: "Later", value: false }, { label: "Sort it", value: true, primary: true }]);
  const cv = document.querySelector('canvas[data-brain="welcome"]');
  if (cv) cv.remove();
  if (!go) return toast("No worries. I'll ask again when you first merge.");
  const ok = await call("run_setup").catch(() => false);
  if (ok) { toast("Sound, all sorted. You won't be asked again."); speak("Sound, all sorted."); }
  else modal("Windows said no", `<p class="muted">That's fine, nothing changed. I'll ask again when you start your first merge.</p>`);
}

// ---------- Franklin's brain avatar ----------
document.querySelectorAll("canvas[data-brain]").forEach((cv) => {
  const size = cv.dataset.brain;
  window.FranklinBrain.mount(cv, { dense: size === "big" ? 1400 : size === "small" ? 420 : 180, speed: size === "big" ? 0.45 : 0.3 });
});

// ---------- start ----------
const SPLASH_LINES = [
  "Alright our kid! Give us a sec, I'm finding your games…",
  "Checking Steam, Epic, GOG and the Xbox app…",
  "Nearly there, sorting the box art…",
];
(async () => {
  const started = Date.now();
  let i = 0;
  const rotate = setInterval(() => { i = (i + 1) % SPLASH_LINES.length; $("#splashLine").textContent = SPLASH_LINES[i]; }, 1600);
  renderLibrary();
  renderFranklinCard();
  try { state.settings = await call("get_settings"); } catch (e) { /* shown when needed */ }
  renderFranklinCard();
  renderMute();
  call("franklin_voice_warm", [...SPLASH_LINES, "Sound, all sorted.", "Alright our kid, how's this one sound?",
    "Alright our kid, it's Franklin. Pick a game and let's get merging."]).catch(() => {});
  await scan();
  await sleep(Math.max(0, 2200 - (Date.now() - started)));
  clearInterval(rotate);
  const n = state.games.length;
  $("#splashLine").textContent = n ? `Sound, found ${n} game${n === 1 ? "" : "s"}. Let's get merging.` : "Couldn't spot any games yet, but you can add one in a sec.";
  speak($("#splashLine").textContent);
  await sleep(1100);
  $("#splash").classList.add("gone");
  setTimeout(() => $("#splash").remove(), 700);
  if (await call("playing").catch(() => null)) watchPlaying();
  await sleep(700);
  await firstRun();
  const opened = await call("pending_import").catch(() => null);
  if (opened) await importMerge(opened);
  offerUpdate(await call("check_update").catch(() => null));
})();
