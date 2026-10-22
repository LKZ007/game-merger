"use strict";

// Frankenstein's logo: a blacked-out head in side profile with a slowly turning 3D brain of
// dots inside it. The dots pulse grey to white and sparks run across it like thoughts.
// Drawn on a <canvas>, no images needed.
(function () {
  const TAU = Math.PI * 2;

  function makePoints(count) {
    const pts = [];
    const golden = Math.PI * (3 - Math.sqrt(5));
    for (let i = 0; i < count; i++) {
      // even spread over a sphere (Fibonacci lattice)
      const y = 1 - (i / (count - 1)) * 2;
      const ring = Math.sqrt(1 - y * y);
      const th = golden * i;
      let x = Math.cos(th) * ring, z = Math.sin(th) * ring;
      // two hemispheres with a groove down the middle
      const side = x >= 0 ? 1 : -1;
      x = side * (0.16 + Math.abs(x) * 0.84);
      // folds (gyri) so it reads as a brain, not a ball
      const folds = 1 + 0.07 * Math.sin(9 * th + 3 * y) * Math.sin(11 * y + 2 * z) + 0.04 * Math.sin(17 * z + 5 * x);
      let px = x * 0.72 * folds, py = y * 0.68 * folds, pz = z * 1.05 * folds;
      if (py < -0.3) py = -0.3 + (py + 0.3) * 0.4; // flatter underside
      // temporal lobes: bulge low on the sides toward the front
      if (py < 0.05 && pz > -0.2) px *= 1 + 0.18 * Math.exp(-((py + 0.2) ** 2) / 0.04);
      // frontal lobe slightly fuller than the back
      if (pz > 0.4) py *= 1.06;
      pts.push({ x: px, y: py - 0.05, z: pz, kind: 0 });
    }
    // cerebellum: a small bundle low at the back
    for (let i = 0; i < count * 0.12; i++) {
      const u = Math.random() * TAU, v = Math.acos(2 * Math.random() - 1);
      pts.push({ x: 0.42 * Math.sin(v) * Math.cos(u), y: -0.42 + 0.17 * Math.cos(v), z: -0.62 + 0.26 * Math.sin(v) * Math.sin(u), kind: 1 });
    }
    // brain stem
    for (let i = 0; i < count * 0.04; i++) {
      const t = i / (count * 0.04);
      pts.push({ x: (Math.random() - 0.5) * 0.12, y: -0.45 - t * 0.38, z: -0.3 - t * 0.12 + (Math.random() - 0.5) * 0.1, kind: 2 });
    }
    return pts;
  }

  // Head facing right, in units where the head fills -1..1. y points down.
  function headPath(ctx, cx, cy, k) {
    const P = (x, y) => [cx + x * k, cy + y * k];
    const m = (x, y) => ctx.moveTo(...P(x, y));
    const l = (x, y) => ctx.lineTo(...P(x, y));
    const c = (a, b, c2, d, e, f) => ctx.bezierCurveTo(...P(a, b), ...P(c2, d), ...P(e, f));
    ctx.beginPath();
    m(-0.34, 1.0);
    c(-0.36, 0.86, -0.38, 0.7, -0.4, 0.56);        // back of the neck
    c(-0.76, 0.36, -0.84, -0.34, -0.47, -0.7);      // back of the skull
    c(-0.16, -0.98, 0.36, -0.96, 0.56, -0.6);       // crown
    c(0.66, -0.42, 0.66, -0.28, 0.65, -0.16);       // forehead
    c(0.66, -0.11, 0.68, -0.08, 0.68, -0.05);       // brow
    c(0.72, 0.03, 0.8, 0.1, 0.83, 0.15);            // nose
    c(0.84, 0.19, 0.78, 0.22, 0.7, 0.22);           // nose tip
    c(0.71, 0.26, 0.73, 0.29, 0.72, 0.31);          // top lip
    c(0.69, 0.34, 0.68, 0.35, 0.71, 0.38);
    c(0.72, 0.41, 0.69, 0.44, 0.66, 0.45);          // bottom lip
    c(0.68, 0.52, 0.66, 0.6, 0.56, 0.63);           // chin
    c(0.42, 0.66, 0.26, 0.62, 0.2, 0.68);           // jaw
    c(0.18, 0.8, 0.2, 0.9, 0.24, 1.0);              // throat
    ctx.closePath();
  }

  class Brain {
    constructor(canvas, opts = {}) {
      this.c = canvas;
      this.ctx = canvas.getContext("2d");
      this.dense = opts.dense ?? 900;
      this.pts = makePoints(this.dense);
      this.angle = opts.angle ?? 0.6;
      this.speed = opts.speed ?? 0.35;
      this.sparks = [];
      this.still = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
      this.talking = false;
      this.head = opts.head ?? true;
      // the startup logo: the brain faces forward, glows purple and keeps collapsing inward and swelling back out
      this.front = opts.front ?? false;
      this.purple = opts.purple ?? this.front;
      this.level = 0;  // 0..1: how hard it works (the loading bar)
      if (this.front) {
        // a network over the dots, like the logo's glowing brain: some dots are nodes, each joined to its nearest two
        this.nodes = this.pts.map((p, i) => i).filter((i) => i % 7 === 0 && this.pts[i].kind === 0);
        this.edges = [];
        for (const i of this.nodes) {
          const a = this.pts[i];
          const near = this.nodes.filter((j) => j !== i).map((j) => [j, (this.pts[j].x - a.x) ** 2 + (this.pts[j].y - a.y) ** 2 + (this.pts[j].z - a.z) ** 2])
            .sort((x, y) => x[1] - y[1]).slice(0, 2);
          near.forEach(([j]) => i < j && this.edges.push([i, j]));
        }
      }
      this.resize();
      this.last = performance.now();
      this.frame = this.frame.bind(this);
      requestAnimationFrame(this.frame);
    }

    resize() {
      const dpr = window.devicePixelRatio || 1;
      const r = this.c.getBoundingClientRect();
      this.w = Math.max(16, r.width || this.c.width);
      this.h = Math.max(16, r.height || this.c.height);
      this.c.width = this.w * dpr;
      this.c.height = this.h * dpr;
      this.ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    }

    frame(now) {
      if (!this.c.isConnected) { // canvas gone (splash or dialog closed): stop drawing
        window.LogoBrain.all = window.LogoBrain.all.filter((b) => b !== this);
        return;
      }
      const dt = Math.min(0.05, (now - this.last) / 1000);
      this.last = now;
      if (this.front) this.angle = this.still ? 0 : 0.28 * Math.sin(now / 1000 * 0.45);
      else if (!this.still) this.angle += dt * (this.talking ? this.speed * 2.2 : this.speed);
      if (!this.still && Math.random() < (this.talking ? 0.35 : 0.08 + this.level * 0.3)) {
        const p = this.pts[(Math.random() * this.dense) | 0];
        this.sparks.push({ p, life: 1 });
      }
      this.draw(now / 1000);
      requestAnimationFrame(this.frame);
    }

    draw(t) {
      const { ctx, w, h } = this;
      ctx.clearRect(0, 0, w, h);
      let s = Math.min(w, h) * 0.46;
      let cx = w / 2, cy = h / 2;
      if (this.head) {
        // the blacked-out head, with a faint violet rim so it stands out on the dark background
        const k = Math.min(w, h) * 0.47;
        const hx = w / 2 - k * 0.12, hy = h / 2;
        ctx.save();
        ctx.shadowColor = "rgba(167,139,250,0.35)";
        ctx.shadowBlur = Math.max(4, k * 0.12);
        headPath(ctx, hx, hy, k);
        ctx.fillStyle = "#050308";
        ctx.fill();
        ctx.restore();
        ctx.lineWidth = Math.max(1, k * 0.012);
        ctx.strokeStyle = "rgba(167,139,250,0.28)";
        ctx.stroke();
        ctx.save();
        ctx.clip(); // keep the brain inside the head
        s = k * 0.52;
        cx = hx - k * 0.05;
        cy = hy - k * 0.33;
      }
      const ca = Math.cos(this.angle), sa = Math.sin(this.angle);
      const tilt = this.front ? 0.05 : 0.12, ct = Math.cos(tilt), st = Math.sin(tilt);
      const pulse = 1 + (this.still ? 0 : 0.025 * Math.sin(t * (this.talking ? 9 : 2.2)));
      const beat = 1.7 + this.level * 2.2;  // the collapse speeds up as loading gets closer to done
      const proj = [];
      for (const p of this.pts) {
        // turn around the vertical axis, then tip forward a little
        let x = p.x * ca + p.z * sa, z = -p.x * sa + p.z * ca, y = -p.y; // model is y-up, canvas is y-down
        const y2 = y * ct - z * st; z = y * st + z * ct; y = y2;
        const persp = 2.6 / (2.6 - z);
        // collapse inward and back out: the outer dots lag the inner ones, so it ripples like it's breathing
        let k = pulse;
        if (this.front && !this.still) k *= 0.7 + 0.3 * (0.5 + 0.5 * Math.sin(t * beat - Math.hypot(p.x, p.y, p.z) * 2.4));
        proj.push({ x: cx + x * s * persp * k, y: cy + y * s * persp * k, z, p });
      }
      if (this.front && this.edges) {
        const at = proj.slice();  // still in the points' own order
        ctx.lineWidth = Math.max(0.6, s / 160);
        for (const [i, j] of this.edges) {
          const a = at[i], b = at[j], depth = ((a.z + b.z) / 2 + 1.1) / 2.2;
          ctx.strokeStyle = `rgba(196,150,255,${0.08 + depth * 0.28})`;
          ctx.beginPath(); ctx.moveTo(a.x, a.y); ctx.lineTo(b.x, b.y); ctx.stroke();
        }
        for (const i of this.nodes) {  // the nodes glow brighter than the rest
          const a = at[i];
          if (a.z < -0.2) continue;
          ctx.fillStyle = "rgba(240,220,255,0.85)";
          ctx.beginPath(); ctx.arc(a.x, a.y, Math.max(1.2, s / 75), 0, TAU); ctx.fill();
        }
      }
      proj.sort((a, b) => a.z - b.z);
      const dot = Math.max(this.head ? 0.5 : 0.7, s / 70);
      for (const q of proj) {
        const depth = (q.z + 1.1) / 2.2; // 0 far .. 1 near
        const a = 0.18 + depth * 0.82;
        const r = dot * (0.55 + depth * 0.75);
        // colour pulse: a slow wave rolls across the brain, each dot breathing between grey and white
        const wave = this.still ? 0.6 : 0.5 + 0.5 * Math.sin(t * (this.talking ? 6 : 2.4) - q.p.z * 3.2 - q.p.y * 2.0);
        const v = (120 + wave * 135) | 0;
        ctx.fillStyle = this.purple ? `rgba(${(140 + wave * 105) | 0},${(70 + wave * 150) | 0},255,${a})` : `rgba(${v},${v},${v},${a})`;
        ctx.beginPath();
        ctx.arc(q.x, q.y, r, 0, TAU);
        ctx.fill();
      }
      // sparks: bright flashes with a soft glow, like neurons firing
      this.sparks = this.sparks.filter((sp) => (sp.life -= 0.04) > 0);
      for (const sp of this.sparks) {
        const q = proj.find((v) => v.p === sp.p);
        if (!q) continue;
        const g = ctx.createRadialGradient(q.x, q.y, 0, q.x, q.y, dot * 9 * sp.life);
        g.addColorStop(0, `rgba(255,255,255,${0.9 * sp.life})`);
        g.addColorStop(0.3, this.purple ? `rgba(200,150,255,${0.6 * sp.life})` : `rgba(225,225,230,${0.5 * sp.life})`);
        g.addColorStop(1, this.purple ? "rgba(168,85,247,0)" : "rgba(200,200,210,0)");
        ctx.fillStyle = g;
        ctx.beginPath();
        ctx.arc(q.x, q.y, dot * 9 * sp.life, 0, TAU);
        ctx.fill();
      }
      if (this.head) ctx.restore();
    }
  }

  window.LogoBrain = {
    all: [],
    mount(canvas, opts) { const b = new Brain(canvas, opts); this.all.push(b); return b; },
    talking(on) { this.all.forEach((b) => (b.talking = on)); },
  };
})();
