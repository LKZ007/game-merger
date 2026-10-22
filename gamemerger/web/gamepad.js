"use strict";
// Controller support (Xbox, PlayStation and most others): D-pad or left stick moves around,
// A picks, B goes back, LB / RB switch pages.
(() => {
  const VIEWS = () => [...document.querySelectorAll(".nav-item[data-view]")].map((b) => b.dataset.view);
  const FOCUSABLE = "button:not([disabled]), a[href], input:not([disabled]):not([type=hidden]), select:not([disabled]), textarea, [tabindex]:not([tabindex='-1'])";
  let prev = [], repeatAt = 0, lastDir = null, told = false;

  const visible = (el) => {
    if (!el.offsetParent && getComputedStyle(el).position !== "fixed") return false;
    const r = el.getBoundingClientRect();
    return r.width > 0 && r.height > 0 && r.bottom > 0 && r.top < innerHeight;
  };
  // the top layer decides what can be reached: account picker, merge animation, a dialog, else the app
  const layer = () => document.querySelector(".who") || document.querySelector(".fusion") ||
    (!$("#modal").classList.contains("hidden") ? $("#modal") : document.querySelector(".app"));

  function move(dir) {
    const root = layer();
    const items = [...root.querySelectorAll(FOCUSABLE)].filter(visible);
    if (!items.length) return;
    const cur = document.activeElement && root.contains(document.activeElement) && document.activeElement !== document.body ? document.activeElement : null;
    if (!cur) return focus(items[0]);
    if (cur.tagName === "SELECT" && (dir === "left" || dir === "right")) {
      cur.selectedIndex = Math.max(0, Math.min(cur.options.length - 1, cur.selectedIndex + (dir === "left" ? -1 : 1)));
      return cur.dispatchEvent(new Event("change", { bubbles: true }));
    }
    const a = cur.getBoundingClientRect();
    const ax = a.left + a.width / 2, ay = a.top + a.height / 2;
    let best = null, bestScore = Infinity;
    for (const el of items) {
      if (el === cur) continue;
      const b = el.getBoundingClientRect();
      const dx = b.left + b.width / 2 - ax, dy = b.top + b.height / 2 - ay;
      const main = { left: -dx, right: dx, up: -dy, down: dy }[dir];
      if (main <= 4) continue;
      const side = dir === "left" || dir === "right" ? Math.abs(dy) : Math.abs(dx);
      const score = main + side * 2.2;
      if (score < bestScore) { bestScore = score; best = el; }
    }
    if (best) focus(best);
  }

  function focus(el) {
    el.focus({ preventScroll: true });
    el.scrollIntoView({ block: "nearest", inline: "nearest", behavior: "smooth" });
  }

  function key(k) { (document.activeElement || document.body).dispatchEvent(new KeyboardEvent("keydown", { key: k, bubbles: true })); }

  function back() {
    if (document.querySelector(".fusion")) return document.querySelector(".fusion").click();
    if (document.querySelector(".who #whoBack")) return document.querySelector("#whoBack").click();
    if (!$("#modal").classList.contains("hidden")) {
      const cancel = [...$("#modalActions").children].find((b) => !b.classList.contains("primary"));
      return cancel ? cancel.click() : null;
    }
    if (state.view === "setup") return show("library");
  }

  function page(step) {
    const v = VIEWS(), i = Math.max(0, v.indexOf(state.view === "setup" ? "library" : state.view));
    show(v[(i + step + v.length) % v.length]);
    const nav = document.querySelector(`.nav-item[data-view="${state.view}"]`);
    if (nav) focus(nav);
  }

  function pressed(i, pad) { return pad.buttons[i] && pad.buttons[i].pressed && !prev[i]; }

  function tick() {
    const pad = [...(navigator.getGamepads ? navigator.getGamepads() : [])].find((p) => p && p.connected);
    if (pad) {
      if (!told && pad.buttons.some((b) => b.pressed)) { told = true; document.body.classList.add("pad"); call("controller_used").catch(() => {}); }
      const ax = pad.axes[0] || 0, ay = pad.axes[1] || 0;
      const dir = pad.buttons[12]?.pressed || ay < -0.6 ? "up" : pad.buttons[13]?.pressed || ay > 0.6 ? "down"
        : pad.buttons[14]?.pressed || ax < -0.6 ? "left" : pad.buttons[15]?.pressed || ax > 0.6 ? "right" : null;
      const now = performance.now();
      if (dir && (dir !== lastDir || now > repeatAt)) { move(dir); repeatAt = now + (dir === lastDir ? 130 : 380); }
      lastDir = dir;
      if (pressed(0, pad)) {
        const el = document.activeElement;
        if (el && el !== document.body) el.tagName === "INPUT" && el.type !== "checkbox" ? el.form?.requestSubmit() : el.click();
        else move("down");
      }
      if (pressed(1, pad)) back();
      if (pressed(4, pad)) page(-1);
      if (pressed(5, pad)) page(1);
      if (pressed(8, pad)) key("F11");
      prev = pad.buttons.map((b) => b.pressed);
    }
    requestAnimationFrame(tick);
  }
  window.addEventListener("gamepadconnected", () => toast("Controller connected. D-pad moves, A picks, B goes back, LB/RB switch pages."));
  document.addEventListener("mousemove", () => document.body.classList.remove("pad"));
  requestAnimationFrame(tick);
})();
