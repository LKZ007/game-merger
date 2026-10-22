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
        <div class="eyebrow">Main game${g.engine ? " · " + esc(g.engine) : ""}</div>
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
        <div class="eyebrow">Welcome</div><h1>Pick a main game</h1>
        <p>Any game can be the main game. Cyberpunk 2077, Schedule I and GTA V have hand-made recipes; for every other game Frankenstein works out its engine and mod loader. If yours isn't found below, add its install folder.</p>
        <div class="row"><button class="btn primary" id="addFolder2">Add game folder</button></div>
      </div></section>`;
  }
  const tiles = state.games.map((x) => `
      <button class="tile ${x.base ? "base" : ""} ${x.key === state.selected ? "selected" : ""}" data-key="${x.key}" data-cover="${x.key}" aria-pressed="${x.key === state.selected}">
        ${caseHtml(x.name, state.covers[x.key])}
        <div class="tile-name">${esc(x.name)}</div>
        <div class="tile-sub">${esc(x.source)} · ${x.key === state.selected ? "main game" : x.moddable === "no" ? "no mod support" : x.moddable === "limited" ? "limited mods" : "moddable"}${x.creative ? ` · <span class="creative-tag" title="Has a creative / sandbox mode">creative</span>` : ""}</div>
      </button>`).join("");
  el.innerHTML = `${banner}
    <section class="shelf">
      <div class="shelf-head"><h2>Your games</h2>
        <div class="row"><span class="muted small">Click any game to make it the main game</span>
          <button class="btn ghost" id="rescan">Rescan</button><button class="btn" id="addFolder">Add game folder</button></div></div>
      ${state.games.length ? `<div class="grid">${tiles}</div>` : `<div class="empty">No games found yet.</div>`}
    </section>
    <section class="shelf" id="ownedShelf"></section>`;
  renderOwnedShelf();
  el.querySelectorAll(".tile").forEach((b) => (b.onclick = () => {
    const x = game(b.dataset.key);
    if (!x.base) return toast(`Frankenstein couldn't read ${x.name}'s folder. Try Rescan.`);
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
    <div class="card"><div class="section-title"><span class="step">1</span><h2>Pick your mods</h2></div>
      <p class="muted" style="margin-top:-6px">Click <strong>Find mods</strong> to search ${esc((s.options.sources || ["Nexus Mods"]).join(", "))} at once, or paste a download link. Mods it needs come along automatically. You can also skip this and add mods later.</p>
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
        <div class="field"><label for="mName">Your new game's name</label>
          <div class="row" style="flex-wrap:nowrap"><input class="input" id="mName" value="${esc(s.name)}"><button class="btn" id="mReroll" title="Make up another one">New name</button></div>
          ${s.tagline ? `<div class="muted small" style="margin-top:6px">“${esc(s.tagline)}”</div>` : ""}</div>
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
  const st = await call("profiles_state").catch(() => null);
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
    ${me.admin ? `<div class="row" style="margin-top:12px;flex-wrap:nowrap"><input class="input" id="newAcct" placeholder="New account name" maxlength="24"><button class="btn primary" id="addAcct">Add account</button></div>` : ""}`;
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
  if ($("#addAcct")) $("#addAcct").onclick = async () => {
    try { await call("profile_add", $("#newAcct").value, "", null); toast("Account added. They pick it when Frankenstein opens."); } catch (e) { toast(errText(e)); }
    renderAccountsCard();
  };
}

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
    const body = `<p>“${esc(res.result.name)}” is ready. Press Play in My merges.</p>` +
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
        <div class="muted">${m.tagline ? `<em>${esc(m.tagline)}</em> · ` : ""}${esc(m.game)} × ${esc(m.theme)}</div>
        <div class="row small"><span class="tag">${esc(m.mode_label)}</span><span class="tag">${m.mods.length} installed</span>${m.has_saves ? `<span class="tag">Own saves</span>` : ""}${m.offline_lock ? `<span class="tag" title="Starts in offline mode so mods can't reach online play">Online locked</span>` : ""}<button class="tag tag-btn" data-act="pad" title="${m.pad === "native" ? "This game supports controllers itself" : "Frankenstein's key mapper runs while you play. Click to change the buttons."}">🎮 ${m.pad === "native" ? "Controller" : "Controller (mapped)"}</button>${m.playing ? `<span class="tag live">Playing now</span>` : ""}${m.status === "restored" ? `<span class="tag">Mods removed</span>` : ""}</div>
        <div class="muted small" title="${esc(m.folder)}">${esc(m.folder)}</div>
      </div>
      <div class="merge-actions">
        <button class="btn primary" data-act="play">▶ Play</button>
        ${m.creative ? `<button class="btn creative" data-act="creative" title="Sandbox: spawners, god mode, free camera. Own saves, offline.">✦ Creative</button>` : ""}
        <button class="btn ghost" data-act="rundown" title="What's in this merge and what to do">Rundown</button>
        <button class="btn" data-act="mods">Mods &amp; profiles</button>
        <button class="btn" data-act="add">Add mods</button>
        <button class="btn" data-act="share">Share</button>
        <button class="btn" data-act="fix">Fix crash</button>
        ${m.needs_upgrade ? `<button class="btn" data-act="upgrade" title="Give this merge everything new merges get. Mods and saves stay as they are.">Upgrade</button>` : ""}
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
  const go = await modal("Update available", `
    <p>Frankenstein <strong>${esc(u.version)}</strong> is out. It takes a few seconds: the app closes, updates and opens again. Your merges and saves aren't touched.</p>
    ${u.notes ? `<div class="log">${esc(u.notes)}</div>` : ""}`,
    [{ label: "Later", value: false }, { label: "Update now", value: true, primary: true }]);
  if (go) followJob(await call("install_update"), "Updating Frankenstein");
}

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
  const body = `<p>${esc(c.note)}</p>
    ${c.native ? `<div class="note small"><strong>Built in:</strong> ${esc(c.native)}</div>` : ""}
    ${c.slots.length ? `<p class="muted">Sandbox mods you can add (they're only switched on in creative):</p>
      <div>${c.slots.map((x, i) => `<div class="row" style="padding:6px 0;border-top:1px solid var(--line)"><span class="grow">${esc(x.name)}</span><button class="btn" data-sb="${i}" type="button">Find</button></div>`).join("")}</div>` : ""}
    ${c.mods.length ? `<p class="muted small">Added: ${c.mods.map(esc).join(", ")}</p>` : ""}
    <p class="muted small">${c.own_saves ? "Creative has its own saves (a copy of this merge's saves to start), so your normal progress stays clean. " : "This game's saves can't be told apart, so creative uses the same saves; make a new save slot in the game. "}${c.offline ? "It always starts offline, so none of this reaches online play." : "Don't take creative tools into online games."}</p>`;
  const done = modal("Creative mode: " + m.name, body, [{ label: "Cancel", value: false }, { label: "Play creative", value: true, primary: true }]);
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
    if (act === "play" || act === "creative") {
      const creative = act === "creative";
      if (creative && !(await creativeSetup(m))) return;
      toast(creative ? "Starting creative mode…" : "Starting " + m.name + "…");
      const r = await call("play", id, creative);
      if (r && r.note) toast(r.note, 6000); else watchPlaying();
      loadMerges();
      showRundown(m, creative, true);
    }
    else if (act === "rundown") showRundown(m, false, false);
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
      <button class="btn primary" id="partySetup">Set it up</button></div>`;
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
const SELF_WORDS = { green: "Main game, fully moddable", yellow: "Main game, limited mod support", red: "Main game, no real mod support" };

// Ratings are made in the background; this keeps asking until every game has one.
async function loadFun() {
  const base = game(state.selected);
  if (!base) return;
  const names = [...state.games.filter((g) => g.key !== base.key).map((g) => g.name), ...(state.owned?.games || []).map((g) => g.name)];
  if (state.fun?.base !== base.name) state.fun = { base: base.name, scores: {}, self: null };
  const fun = state.fun;
  clearTimeout(loadFun.timer);
  let r;
  try { r = await call("fun_scores", base.key, names); } catch (e) { return; }
  if (state.fun !== fun) return;
  Object.assign(fun.scores, r.scores);
  fun.self = r.self;
  applyFun();
  if (r.pending) loadFun.timer = setTimeout(loadFun, 4000);
}

function applyFun() {
  const style = state.settings?.fun_style || "dot";
  document.body.classList.toggle("fun-halo", style === "halo");
  const fun = state.fun && game(state.selected)?.name === state.fun.base ? state.fun : null;
  document.querySelectorAll(".tile").forEach((t) => {
    const name = t.querySelector(".tile-name")?.textContent;
    const mine = t.classList.contains("selected");
    const f = style === "off" || !fun ? null : mine ? fun.self : fun.scores[name];
    if (f) {
      t.dataset.fun = f.rating;
      t.title = `${mine ? SELF_WORDS[f.rating] : FUN_WORDS[f.rating]}: ${f.why}`;
    } else {
      delete t.dataset.fun;
      t.removeAttribute("title");
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
        <figcaption>${c.label === g.name ? "Your game" : "Bringing in"}: <strong>${esc(c.label)}</strong></figcaption></figure>`;
    $("#modalBody").innerHTML = `
      <div class="pv-label">Preview · trailers and mod pictures, not your merged game</div>
      <div class="pv-pitch"><p>${esc(p.summary)}</p></div>
      ${p.clips.length ? `<div class="pv-clips">${p.clips.map(clip).join("")}</div>` : ""}
      ${p.pictures.length ? `<h3 class="pv-h">What the mods look like</h3><div class="pv-pics">${p.pictures.map((x) => `<figure><img src="${esc(x.url)}" alt="" loading="lazy"><figcaption><strong>${esc(x.slot)}</strong><span>${esc(x.name)}</span></figcaption></figure>`).join("")}</div>` : ""}
      <p class="muted small">Clips stream straight from Steam and pictures from Nexus Mods at low quality, so nothing is saved to your PC. Your merge will be ${esc(g.name)} with these mods layered on. <a href="#" id="pvVideos">Watch mod showcase videos</a></p>`;
    $("#pvVideos").onclick = (e) => { e.preventDefault(); call("open_url", p.videos_url); };
    $("#modalBody").querySelectorAll("[data-url]").forEach((a) => (a.onclick = (e) => { e.preventDefault(); call("open_url", a.dataset.url); }));
  }
  const go = await done;
  box.classList.remove("wide");
  $("#modalBody").querySelectorAll("video").forEach((v) => { v.pause(); v.removeAttribute("src"); v.load(); }); // stop streaming
  if (go) startSetup();
}

// ---------- every game you own ----------
async function loadOwned(refresh = false, quiet = false) {
  let r;
  try { r = await call("library_owned", refresh); } catch (e) { if (!quiet) toast(errText(e)); return; }
  state.owned = r;
  if (!quiet) for (const err of r.errors || []) toast(err, 7000);
  if (r.styles_changed) {  // owned games are styles for every main game too
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
        el.querySelector(".face").textContent = "";
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
      <p class="muted">Every game gets a colour for how fun it would be to merge with your main game by how fun they'd be to merge in: green is a great pairing, yellow is OK, red is doable but not as great. Hover a game to see why.</p>
      <div class="field"><label for="funStyle">Show it as</label>
        <select class="input" id="funStyle">${[["dot", "A dot in the corner"], ["halo", "A glow around the box"], ["off", "Off"]].map(([v, n]) => `<option value="${v}" ${(s.fun_style || "dot") === v ? "selected" : ""}>${n}</option>`).join("")}</select></div>
    </div>
    <div class="card" id="accountsCard"></div>
    <div class="card" id="discordCard"></div>
    <div class="card"><h2>Cloud backup of saves</h2>
      <p class="muted">Every merge's saves get copied to your cloud drive after you play, so your PC and laptop stay in step. Pick the same drive on both.</p>
      <select class="input" id="cloudPick"><option value="">Off</option>${(s.cloud_options || []).map(([n, p]) => `<option value="${esc(p)}" ${s.cloud_folder === p ? "selected" : ""}>${esc(n)} (${esc(p)})</option>`).join("")}</select>
      ${(s.cloud_options || []).length ? "" : `<p class="muted small">No OneDrive or Google Drive folder found on this PC. Install one of them and it shows up here.</p>`}
    </div>
    <div class="card"><h2>Updates</h2>
      <div class="row"><div class="grow muted">You're on Frankenstein ${esc(s.version)}. It checks for updates every time it opens.</div><button class="btn" id="checkUpdate">Check now</button></div>
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
document.querySelectorAll("canvas[data-brain]").forEach((cv) => {
  const size = cv.dataset.brain;
  window.LogoBrain.mount(cv, { dense: size === "big" ? 1400 : size === "small" ? 420 : 180, speed: size === "big" ? 0.45 : 0.3 });
});

// ---------- start ----------
const SPLASH = ["It's alive! Finding your games…", "Checking Steam, Epic, GOG and the Xbox app…", "Grabbing your box art…", "Working out how each game is modded…"];

(async () => {
  await whoIsPlaying();
  const started = Date.now();
  let i = 0;
  const rotate = setInterval(() => { $("#splashLine").textContent = SPLASH[++i % SPLASH.length]; }, 1600);
  renderLibrary();
  try { state.settings = await call("get_settings"); } catch (e) { /* shown when needed */ }
  await scan();
  loadOwned(false, true).then(() => state.owned?.sources.length && loadOwned(true, true));
  await sleep(Math.max(0, 2200 - (Date.now() - started)));
  clearInterval(rotate);
  const n = state.games.length;
  $("#splashLine").textContent = n > 1 ? `Found ${n} games.` : n ? "Found 1 game." : "No games found yet. Add a game folder in the Library.";
  await sleep(1100);
  $("#splash").classList.add("gone");
  setTimeout(() => $("#splash").remove(), 700);
  if (await call("playing").catch(() => null)) watchPlaying();
  await sleep(700);
  await firstRun();
  await checkHandoffs();
  setInterval(checkHandoffs, 2000);
  offerUpdate(await call("check_update").catch(() => null));
})();
