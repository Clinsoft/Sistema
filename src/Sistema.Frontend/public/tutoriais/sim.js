// Motor de simulação (sem áudio): move um cursor pela tela mock, clica, digita e
// mostra legendas. Roda em loop. Uso: Sim.run(steps, { reset, badge }).
(function () {
  const cursorSvg = `<svg viewBox="0 0 24 24"><path d="M4 2l6 20 3-8 8-3z" fill="#0f172a" stroke="#fff" stroke-width="1.5" stroke-linejoin="round"/></svg>`;

  function el(cls, parent) { const d = document.createElement('div'); d.className = cls; (parent || document.body).appendChild(d); return d; }
  const sleep = (ms) => new Promise(r => setTimeout(r, ms));

  const Sim = {
    async run(steps, opts = {}) {
      const stage = document.querySelector('.sim-stage');
      const cursor = el('sim-cursor', stage); cursor.innerHTML = cursorSvg;
      const cap = el('sim-caption', stage);
      const prog = el('sim-progress', stage);
      if (opts.badge) { const b = el('sim-badge', stage); b.textContent = opts.badge; }
      cursor.style.transform = 'translate(60px,60px)';

      async function moveTo(sel) {
        const t = typeof sel === 'string' ? stage.querySelector(sel) : sel;
        if (!t) return null;
        const s = stage.getBoundingClientRect(), r = t.getBoundingClientRect();
        const x = r.left - s.left + r.width / 2, y = r.top - s.top + r.height / 2;
        cursor.style.transform = `translate(${x}px,${y}px)`;
        await sleep(650);
        return { t, x, y };
      }
      function ripple(x, y) {
        const rp = el('sim-ripple', stage);
        rp.style.left = x + 'px'; rp.style.top = y + 'px';
        requestAnimationFrame(() => rp.classList.add('go'));
        setTimeout(() => rp.remove(), 550);
      }
      async function type(t, text) {
        t.classList.add('typed-caret');
        for (let i = 0; i <= text.length; i++) { t.firstChild ? (t.childNodes[0].nodeValue = text.slice(0, i)) : (t.textContent = text.slice(0, i)); await sleep(55); }
        t.classList.remove('typed-caret');
      }

      const total = steps.length;
      // captura o HTML inicial da tela para o reset do loop
      const scene = stage.querySelector('.scene');
      const inicial = scene ? scene.innerHTML : null;

      /* loop infinito */
      while (true) {
        for (let i = 0; i < total; i++) {
          const st = steps[i];
          prog.style.width = ((i) / total * 100) + '%';
          cap.innerHTML = `<span class="n">${i + 1}</span><span>${st.cap || ''}</span>`;
          if (st.before) st.before(stage);
          let hit = null;
          if (st.sel) hit = await moveTo(st.sel);
          if (st.act === 'click' && hit) { ripple(hit.x, hit.y); hit.t.classList.add('hl'); await sleep(280); }
          if (st.act === 'type' && hit) { await type(hit.t, st.text || ''); }
          if (st.after) st.after(stage);
          await sleep(st.ms || 1100);
          if (hit && st.act === 'click') hit.t.classList.remove('hl');
        }
        prog.style.width = '100%';
        await sleep(1400);
        // reset para repetir
        if (opts.reset) opts.reset(stage);
        else if (scene && inicial != null) scene.innerHTML = inicial;
        cursor.style.transform = 'translate(60px,60px)';
        await sleep(600);
      }
    }
  };
  window.Sim = Sim;
})();
