/* =============================================================================
   THUNDERSHOCK KPI PORTALI — ORTAK BİLEŞENLER & SVG GRAFİKLER
   =============================================================================
   Harici bağımlılık YOK. Grafikler saf SVG ile çizilir.

   Renk kullanımı dataviz kurallarına göre:
   - Kategorik seriler --cat-1..5 (doğrulanmış palet, sabit sıra, döngüsüz)
   - Büyüklük (ısı haritası) --seq-1..5 tek hue sıralı rampa
   - Durum renkleri (--ok/--warn/--bad) REZERVE; her zaman ikon + metin ile
   - Tek eksen; farklı ölçekli metrikler asla aynı grafiğe konmaz
============================================================================= */

(function () {
  'use strict';

  // ---------------------------------------------------------------------------
  // DOM yardımcıları
  // ---------------------------------------------------------------------------

  function h(tag, attrs, children) {
    const el = document.createElement(tag);
    if (attrs) {
      Object.keys(attrs).forEach(function (k) {
        const v = attrs[k];
        if (v === null || v === undefined || v === false) return;
        if (k === 'class') el.className = v;
        else if (k === 'html') el.innerHTML = v;
        else if (k === 'text') el.textContent = v;
        else if (k.slice(0, 2) === 'on' && typeof v === 'function') {
          el.addEventListener(k.slice(2).toLowerCase(), v);
        } else if (k === 'dataset') {
          Object.keys(v).forEach(function (d) { el.dataset[d] = v[d]; });
        } else el.setAttribute(k, v === true ? '' : v);
      });
    }
    append(el, children);
    return el;
  }

  function append(parent, children) {
    if (children === null || children === undefined) return parent;
    (Array.isArray(children) ? children : [children]).forEach(function (c) {
      if (c === null || c === undefined || c === false) return;
      parent.appendChild(typeof c === 'string' || typeof c === 'number'
        ? document.createTextNode(String(c)) : c);
    });
    return parent;
  }

  const SVGNS = 'http://www.w3.org/2000/svg';
  function s(tag, attrs, children) {
    const el = document.createElementNS(SVGNS, tag);
    if (attrs) Object.keys(attrs).forEach(function (k) {
      const v = attrs[k];
      if (v === null || v === undefined || v === false) return;
      if (k === 'text') el.textContent = v;
      else if (k.slice(0, 2) === 'on' && typeof v === 'function') {
        el.addEventListener(k.slice(2).toLowerCase(), v);
      } else el.setAttribute(k, v);
    });
    if (children) (Array.isArray(children) ? children : [children]).forEach(function (c) {
      if (c) el.appendChild(c);
    });
    return el;
  }

  function clear(el) { while (el.firstChild) el.removeChild(el.firstChild); return el; }

  function css(name) {
    return getComputedStyle(document.documentElement).getPropertyValue(name).trim();
  }

  // ---------------------------------------------------------------------------
  // İkonlar — inline SVG (harici istek yok)
  // ---------------------------------------------------------------------------

  const PATHS = {
    overview:  'M3 12h6v9H3zM10.5 3h3v18h-3zM15 8h6v13h-6z',
    scenarios: 'M3 5h18v4H3zM3 11h18v4H3zM3 17h11v4H3z',
    chart:     'M4 20V10M10 20V4M16 20v-7M22 20H2',
    alert:     'M12 3l9.5 17H2.5zM12 9v5M12 17.2v.1',
    trophy:    'M6 4h12v4a6 6 0 01-12 0zM4 5h2v3a2 2 0 01-2-2zM20 5h-2v3a2 2 0 002-2zM9 20h6M12 14v6',
    user:      'M12 12a4 4 0 100-8 4 4 0 000 8zM4 21a8 8 0 0116 0',
    users:     'M9 12a4 4 0 100-8 4 4 0 000 8zM2 21a7 7 0 0114 0M17 11a3 3 0 100-6M18 21h4a5 5 0 00-4-4.9',
    report:    'M6 2h9l5 5v15H6zM15 2v5h5M9 12h8M9 16h8M9 8h3',
    settings:  'M12 15a3 3 0 100-6 3 3 0 000 6zM19.4 15a1.6 1.6 0 00.3 1.8l.1.1a2 2 0 11-2.8 2.8l-.1-.1a1.6 1.6 0 00-2.7 1.1v.2a2 2 0 11-4 0v-.1A1.6 1.6 0 005 19.4l-.1.1a2 2 0 11-2.8-2.8l.1-.1A1.6 1.6 0 002.6 14H2.4a2 2 0 110-4h.1A1.6 1.6 0 004.6 5L4.5 5a2 2 0 112.8-2.8l.1.1A1.6 1.6 0 0010 2.6V2.4a2 2 0 114 0v.1a1.6 1.6 0 002.7 1.1l.1-.1a2 2 0 112.8 2.8l-.1.1a1.6 1.6 0 001.1 2.7h.2a2 2 0 110 4h-.1a1.6 1.6 0 00-1.3.9z',
    logout:    'M16 17l5-5-5-5M21 12H9M12 3H5v18h7',
    bell:      'M18 8a6 6 0 10-12 0c0 7-3 8-3 8h18s-3-1-3-8M13.7 21a2 2 0 01-3.4 0',
    check:     'M20 6L9 17l-5-5',
    x:         'M18 6L6 18M6 6l12 12',
    warn:      'M12 3l9.5 17H2.5zM12 9v5M12 17.2v.1',
    info:      'M12 22a10 10 0 100-20 10 10 0 000 20zM12 16v-5M12 8.2v.1',
    clock:     'M12 22a10 10 0 100-20 10 10 0 000 20zM12 6v6l4 2',
    repeat:    'M17 2l4 4-4 4M3 11V9a4 4 0 014-4h14M7 22l-4-4 4-4M21 13v2a4 4 0 01-4 4H3',
    arrowUp:   'M12 19V5M5 12l7-7 7 7',
    arrowDown: 'M12 5v14M19 12l-7 7-7-7',
    arrowRight:'M5 12h14M12 5l7 7-7 7',
    minus:     'M5 12h14',
    search:    'M11 19a8 8 0 100-16 8 8 0 000 16zM21 21l-4.3-4.3',
    filter:    'M3 4h18l-7 8v7l-4 2v-9z',
    grid:      'M3 3h7v7H3zM14 3h7v7h-7zM3 14h7v7H3zM14 14h7v7h-7z',
    menu:      'M3 6h18M3 12h18M3 18h18',
    lock:      'M5 11h14v10H5zM8 11V7a4 4 0 118 0v4',
    bolt:      'M13 2L4 14h6l-1 8 9-12h-6z',
    empty:     'M3 7l9-4 9 4v10l-9 4-9-4zM3 7l9 4 9-4M12 11v10',
    download:  'M12 3v12M7 11l5 5 5-5M4 21h16'
  };

  function icon(name, size, opts) {
    opts = opts || {};
    const d = PATHS[name] || PATHS.info;
    const el = s('svg', {
      width: size || 16, height: size || 16, viewBox: '0 0 24 24',
      fill: opts.fill || 'none', stroke: opts.stroke || 'currentColor',
      'stroke-width': opts.weight || 1.9,
      'stroke-linecap': 'round', 'stroke-linejoin': 'round',
      'aria-hidden': 'true', focusable: 'false'
    }, [s('path', { d: d })]);
    return el;
  }

  // ---------------------------------------------------------------------------
  // Küçük bileşenler
  // ---------------------------------------------------------------------------

  function badge(kind, label, iconName) {
    return h('span', { class: 'badge badge--' + kind },
      [iconName ? icon(iconName, 12) : null, h('span', { text: label })]);
  }

  function tooltip(text) {
    const id = 'tip-' + Math.abs(hashStr(text)).toString(36);
    const bubble = h('span', { class: 'tip__bubble', role: 'tooltip', id: id, text: text });

    // Balon görünmeden hemen önce, ekran kenarını taşacaksa hizasını çevir.
    // (Balon display:none olduğu için idle durumda sayfaya hiç taşma katmaz.)
    function place() {
      bubble.className = 'tip__bubble';
      const host = bubble.parentNode.getBoundingClientRect();
      const half = Math.min(256, window.innerWidth - 32) / 2;
      if (host.left + host.width / 2 + half > window.innerWidth - 12) {
        bubble.className = 'tip__bubble tip__bubble--end';
      } else if (host.left + host.width / 2 - half < 12) {
        bubble.className = 'tip__bubble tip__bubble--start';
      }
    }

    const btn = h('button', {
      class: 'tip__btn', type: 'button', 'aria-describedby': id,
      'aria-label': 'Açıklama: ' + text.slice(0, 90),
      onMouseenter: place, onFocus: place
    }, '?');

    return h('span', { class: 'tip' }, [btn, bubble]);
  }

  function hashStr(str) {
    let x = 0;
    for (let i = 0; i < str.length; i++) { x = (x * 31 + str.charCodeAt(i)) | 0; }
    return x;
  }

  /**
   * KPI kartı. `ratio` nesnesi ok:false ise yüzde YERİNE durum etiketi basar.
   */
  function kpiCard(o) {
    const body = [];
    body.push(h('div', { class: 'kpi__label' }, [
      h('span', { text: o.label }),
      o.help ? tooltip(o.help) : null
    ]));

    if (o.unavailable) {
      body.push(h('div', { class: 'kpi__value kpi__value--na' }, [
        icon('info', 14), ' ', o.unavailable
      ]));
      if (o.reason) body.push(h('div', { class: 'kpi__foot', text: o.reason }));
    } else {
      body.push(h('div', { class: 'kpi__value', text: o.value }));
      const foot = [];
      if (o.delta) foot.push(o.delta);
      if (o.sub) foot.push(h('span', { text: o.sub }));
      if (foot.length) body.push(h('div', { class: 'kpi__foot' }, foot));
    }
    return h('div', { class: 'kpi' + (o.accent ? ' kpi--accent' : '') }, body);
  }

  /**
   * Değişim rozeti. Yön hem renk, hem ok ikonu, hem de metinle anlatılır.
   * @param invert  true ise artış KÖTÜdür (hata sayısı, süre gibi)
   */
  function deltaBadge(value, opts) {
    opts = opts || {};
    if (value === null || value === undefined || !isFinite(value)) {
      return h('span', { class: 'delta delta--flat' }, [icon('minus', 11), 'veri yok']);
    }
    const eps = opts.eps || 0.0001;
    const fmt = opts.fmt || function (v) { return (v > 0 ? '+' : '') + v.toFixed(1); };
    if (Math.abs(value) < eps) {
      return h('span', { class: 'delta delta--flat' }, [icon('minus', 11), 'değişim yok']);
    }
    const rising = value > 0;
    const good = opts.invert ? !rising : rising;
    return h('span', {
      class: 'delta delta--' + (good ? 'up' : 'down'),
      title: (good ? 'İyileşme' : 'Kötüleşme') + ': ' + fmt(value)
    }, [icon(rising ? 'arrowUp' : 'arrowDown', 11), fmt(value)]);
  }

  /** Boş / hatalı / sınırlı veri durumu — ne eksik, neden, ne yapılabilir.
   *  Başlık h2: hem sayfa düzeyinde (H1 → H2) hem kart içinde (H2 → H2 kardeş)
   *  atlama yaratmadan doğru sırayı korur. */
  function emptyState(o) {
    return h('div', { class: 'state' + (o.tone ? ' state--' + o.tone : '') + (o.inline ? ' state--inline' : '') }, [
      h('div', { class: 'state__icon' }, icon(o.icon || 'empty', 22)),
      h('h2', { text: o.title }),
      o.what ? h('p', { text: o.what }) : null,
      o.why ? h('p', { class: 'state__why', text: 'Olası neden: ' + o.why }) : null,
      o.action ? h('p', { class: 'state__why', text: 'Ne yapabilirsiniz: ' + o.action }) : null,
      o.cta || null
    ]);
  }

  function notice(tone, content, iconName) {
    return h('div', { class: 'notice notice--' + tone, role: tone === 'bad' ? 'alert' : 'note' }, [
      icon(iconName || (tone === 'warn' ? 'warn' : tone === 'bad' ? 'alert' : 'info'), 16),
      h('div', typeof content === 'string' ? { html: content } : null,
        typeof content === 'string' ? null : content)
    ]);
  }

  function card(title, opts, children) {
    opts = opts || {};
    const head = [];
    if (title) {
      // Kart başlığı h2: sayfa başlığı (h1) altındaki bölüm başlığıdır.
      head.push(h('div', { class: 'card__head' }, [
        h('div', { style: 'flex:1' }, [
          h('h2', null, [title, opts.help ? ' ' : null, opts.help ? tooltip(opts.help) : null]),
          opts.desc ? h('p', { class: 'card__desc', text: opts.desc }) : null
        ]),
        opts.aside || null
      ]));
    }
    return h('section', { class: 'card' }, head.concat(Array.isArray(children) ? children : [children]));
  }

  // ---------------------------------------------------------------------------
  // GRAFİK TOOLTIP (tek paylaşılan katman)
  // ---------------------------------------------------------------------------

  let tipEl = null;
  function chartTip() {
    if (!tipEl) {
      tipEl = h('div', { class: 'chart-tip', role: 'status', 'aria-live': 'polite' });
      document.body.appendChild(tipEl);
    }
    return tipEl;
  }
  function showTip(evt, html) {
    const t = chartTip();
    t.innerHTML = html;
    t.classList.add('is-on');
    const pad = 14;
    const r = t.getBoundingClientRect();
    let x = evt.clientX + pad, y = evt.clientY + pad;
    if (x + r.width > window.innerWidth - 8) x = evt.clientX - r.width - pad;
    if (y + r.height > window.innerHeight - 8) y = evt.clientY - r.height - pad;
    t.style.left = Math.max(8, x) + 'px';
    t.style.top = Math.max(8, y) + 'px';
  }
  function hideTip() { if (tipEl) tipEl.classList.remove('is-on'); }

  // ---------------------------------------------------------------------------
  // ÇİZGİ GRAFİĞİ — tek seri, tek eksen
  // ---------------------------------------------------------------------------
  /**
   * @param points [{label, value, ok, reason, meta}]
   * @param o      {color, unit, summary, yMax, height, fmt}
   */
  function lineChart(points, o) {
    o = o || {};
    const valid = points.filter(function (p) { return p.value !== null && isFinite(p.value); });

    if (valid.length === 0) {
      return emptyState({
        inline: true, icon: 'chart', title: 'Grafik için yeterli veri yok',
        what: 'Seçilen kapsamda bu metrik için hesaplanabilir değer bulunamadı.',
        why: points.length ? (points[0].reason || 'Metrik paydası oluşmadı.') : 'Hiç event yok.',
        action: 'Tarih aralığını genişletin veya filtreleri gevşetin.'
      });
    }
    if (valid.length === 1) {
      return h('div', null, [
        h('p', { class: 'chart__summary', text:
          'Tek ölçüm noktası var — trend çizilmedi. Değer: ' +
          (o.fmt ? o.fmt(valid[0].value) : valid[0].value) + ' (' + valid[0].label + ')' }),
        emptyState({
          inline: true, icon: 'info', title: 'Trend için en az iki nokta gerekir',
          what: 'Bu kapsamda yalnızca bir zaman kovası ölçüm içeriyor.',
          action: 'Daha geniş bir tarih aralığı seçin.'
        })
      ]);
    }

    const W = 720, H = o.height || 210;
    const M = { t: 14, r: 16, b: 30, l: 42 };
    const iw = W - M.l - M.r, ih = H - M.t - M.b;

    const vals = valid.map(function (p) { return p.value; });
    const maxFixed = o.yMax !== undefined;   // açıkça verilen sınıra dokunulmaz
    const minFixed = o.yMin !== undefined;
    let max = maxFixed ? o.yMax : Math.max.apply(null, vals);
    let min = minFixed ? o.yMin : Math.min.apply(null, vals);
    if (max === min) { max = max + 1; min = Math.max(0, min - 1); }
    // Nefes payı YALNIZCA otomatik hesaplanan uçlara eklenir. Aksi halde
    // yüzde grafiğinde yMax:100 iken eksen "112%" gibi anlamsız bir değere çıkardı.
    const pad = (max - min) * 0.12;
    if (!maxFixed) max += pad;
    if (!minFixed) min = Math.max(0, min - pad);

    const x = function (i) { return M.l + (points.length === 1 ? iw / 2 : (i / (points.length - 1)) * iw); };
    const y = function (v) { return M.t + ih - ((v - min) / (max - min)) * ih; };

    const color = o.color || css('--cat-1');
    const g = [];

    // Izgara + y ekseni (geri planda kalan ince çizgiler)
    for (let i = 0; i <= 3; i++) {
      const v = min + (max - min) * (i / 3);
      const yy = y(v);
      g.push(s('line', { x1: M.l, y1: yy, x2: W - M.r, y2: yy,
        stroke: 'rgba(255,255,255,.07)', 'stroke-width': 1 }));
      g.push(s('text', { x: M.l - 8, y: yy + 4, 'text-anchor': 'end',
        fill: css('--ink-3'), 'font-size': 10,
        text: o.fmtAxis ? o.fmtAxis(v) : Math.round(v) }));
    }

    // Alan + çizgi — sadece geçerli noktalardan geçen tek yol
    let dLine = '', dArea = '';
    let started = false;
    points.forEach(function (p, i) {
      if (p.value === null || !isFinite(p.value)) return;
      const cmd = started ? 'L' : 'M';
      dLine += cmd + x(i).toFixed(1) + ' ' + y(p.value).toFixed(1) + ' ';
      started = true;
    });
    const firstIdx = points.findIndex(function (p) { return p.value !== null && isFinite(p.value); });
    const lastIdx = points.length - 1 - points.slice().reverse()
      .findIndex(function (p) { return p.value !== null && isFinite(p.value); });
    dArea = dLine + 'L' + x(lastIdx).toFixed(1) + ' ' + (M.t + ih) + ' L' +
            x(firstIdx).toFixed(1) + ' ' + (M.t + ih) + ' Z';

    const gid = 'grad-' + Math.abs(hashStr(o.summary || '' + color)).toString(36);
    g.push(s('defs', null, [
      s('linearGradient', { id: gid, x1: 0, y1: 0, x2: 0, y2: 1 }, [
        s('stop', { offset: '0%', 'stop-color': color, 'stop-opacity': .28 }),
        s('stop', { offset: '100%', 'stop-color': color, 'stop-opacity': 0 })
      ])
    ]));
    g.push(s('path', { d: dArea, fill: 'url(#' + gid + ')' }));
    g.push(s('path', { d: dLine, fill: 'none', stroke: color, 'stroke-width': 2,
      'stroke-linejoin': 'round', 'stroke-linecap': 'round' }));

    // Noktalar + hover hedefleri (marktan büyük vuruş alanı)
    points.forEach(function (p, i) {
      if (p.value === null || !isFinite(p.value)) return;
      const cx = x(i), cy = y(p.value);
      g.push(s('circle', { cx: cx, cy: cy, r: 4, fill: color,
        stroke: css('--panel'), 'stroke-width': 2 }));
      const hit = s('rect', {
        x: cx - iw / (points.length * 2) - 6, y: M.t,
        width: iw / points.length + 12, height: ih,
        fill: 'transparent', style: 'cursor:pointer'
      });
      const label = p.label, val = o.fmt ? o.fmt(p.value) : String(p.value);
      const meta = p.eventCount !== undefined ? p.eventCount + ' event' : '';
      hit.addEventListener('mousemove', function (e) {
        showTip(e, '<b>' + esc(label) + '</b>' + esc(val) +
          (meta ? '<div class="muted">' + esc(meta) + '</div>' : ''));
      });
      hit.addEventListener('mouseleave', hideTip);
      g.push(hit);
    });

    // X ekseni etiketleri — kalabalık olmasın diye seyreltilir
    const step = Math.ceil(points.length / 6);
    points.forEach(function (p, i) {
      if (i % step !== 0 && i !== points.length - 1) return;
      g.push(s('text', { x: x(i), y: H - 9, 'text-anchor': 'middle',
        fill: css('--ink-3'), 'font-size': 10, text: p.label }));
    });

    const svg = s('svg', { viewBox: '0 0 ' + W + ' ' + H, role: 'img',
      'aria-label': o.summary || 'Zaman serisi grafiği' }, g);

    // Özet metni her zaman svg'nin aria-label'ında bulunur. Görünür paragraf
    // yalnızca kartın kendi açıklaması aynı bilgiyi vermiyorsa basılır.
    return h('div', { class: 'chart' }, [
      (o.summary && !o.hideSummary) ? h('p', { class: 'chart__summary', text: o.summary }) : null,
      svg
    ]);
  }

  // ---------------------------------------------------------------------------
  // YATAY BAR LİSTESİ — kategorik veya durum renkli
  // ---------------------------------------------------------------------------
  /**
   * @param rows [{label, value, display, color, sub, onClick}]
   */
  function barList(rows, o) {
    o = o || {};
    if (!rows.length) {
      return emptyState({
        inline: true, icon: 'empty', title: o.emptyTitle || 'Gösterilecek kayıt yok',
        what: o.emptyWhat || 'Seçilen filtrelerle eşleşen kayıt bulunamadı.',
        action: o.emptyAction || 'Filtreleri gevşetin.'
      });
    }
    const max = o.max !== undefined ? o.max
      : Math.max.apply(null, rows.map(function (r) { return r.value || 0; })) || 1;

    return h('div', { class: 'barlist' }, rows.map(function (r, i) {
      const w = max > 0 ? Math.max(2, (r.value / max) * 100) : 0;
      const color = r.color || css('--cat-' + ((i % 5) + 1));
      const label = r.onClick
        ? h('button', { class: 'rowlink barlist__label', type: 'button', onClick: r.onClick,
                        text: r.label })
        : h('span', { class: 'barlist__label', text: r.label });
      return h('div', { class: 'barlist__row' }, [
        label,
        h('span', { class: 'barlist__val', text: r.display !== undefined ? r.display : r.value }),
        h('div', { class: 'barlist__track', role: 'img',
                   'aria-label': r.label + ': ' + (r.display !== undefined ? r.display : r.value) }, [
          h('div', { class: 'barlist__fill',
                     style: 'width:' + w + '%;background:' + color })
        ]),
        r.sub ? h('div', { class: 'barlist__label', style: 'grid-column:1/-1;color:var(--ink-3);font-size:.72rem',
                           text: r.sub }) : null
      ]);
    }));
  }

  // ---------------------------------------------------------------------------
  // ISI HARİTASI — sıralı (ordinal) tek hue rampa, normalize orana göre
  // ---------------------------------------------------------------------------

  const SEQ = ['--seq-1', '--seq-2', '--seq-3', '--seq-4', '--seq-5'];

  function heatColor(rate, maxRate) {
    if (rate === null || !isFinite(rate)) return null;
    if (maxRate <= 0) return css(SEQ[0]);
    const t = Math.min(1, rate / maxRate);
    const idx = Math.min(SEQ.length - 1, Math.floor(t * SEQ.length));
    return css(SEQ[idx]);
  }

  const TYPE_LABEL = { wrong_answer: 'Yanlış cevap', wrong_drop: 'Yanlış yerleştirme' };

  /**
   * @param data { types:[], rows:[{actionName, sequenceName, levelName, denominator, cells:[]}] }
   */
  function heatmapTable(data, onCellClick) {
    if (!data.rows.length) {
      return emptyState({
        inline: true, icon: 'grid', title: 'Isı haritası için hata kaydı yok',
        what: 'Seçilen kapsamda hiç MistakeRecorded event\'i bulunamadı.',
        why: 'Filtre aralığında hata yapılmamış ya da bu dönemde hiç oynanmamış olabilir.',
        action: 'Tarih aralığını genişletin.'
      });
    }

    // Ölçek üst sınırı: en yüksek normalize oran
    let maxRate = 0;
    data.rows.forEach(function (r) {
      r.cells.forEach(function (c) { if (c.rate.ok) maxRate = Math.max(maxRate, c.rate.value); });
    });

    const head = s ? null : null;
    const thead = h('thead', null, h('tr', null, [h('th', { class: 'rowhead', text: 'Adım (Action)' })]
      .concat(data.types.map(function (t) {
        return h('th', { scope: 'col', text: TYPE_LABEL[t] || t });
      }))));

    const tbody = h('tbody', null, data.rows.map(function (r) {
      return h('tr', null, [
        h('th', { class: 'rowhead', scope: 'row' }, [
          h('span', { text: r.actionName }),
          h('small', { text: r.levelName + ' › ' + r.sequenceName +
            ' · payda: ' + r.denominator + ' deneme' })
        ])
      ].concat(r.cells.map(function (c) {
        if (!c.count) {
          return h('td', null, h('div', { class: 'heat-cell heat-cell--empty',
            'aria-label': 'Kayıt yok' }, '—'));
        }
        const bg = c.rate.ok ? heatColor(c.rate.value, maxRate) : 'rgba(255,255,255,.10)';
        // 1'i aşan oranlar yüzde yerine çarpan olarak gösterilir (bkz. K.rateLabel).
        const label = c.rate.ok
          ? (c.rate.value <= 1 ? (c.rate.value * 100).toFixed(0) + '%'
                               : c.rate.value.toFixed(1) + '×')
          : c.count + ' adet';
        const sub = c.rate.ok ? c.count + '/' + c.rate.den : 'payda yok';
        return h('td', null, h('button', {
          class: 'heat-cell', type: 'button',
          style: 'background:' + bg,
          'aria-label': r.actionName + ' — ' + (TYPE_LABEL[c.type] || c.type) + ': ' +
            label + ' (' + sub + '), ' + c.employeeCount + ' çalışan',
          onClick: function () { if (onCellClick) onCellClick(r, c); },
          onMousemove: function (e) {
            showTip(e, '<b>' + esc(r.actionName) + '</b>' +
              esc(TYPE_LABEL[c.type] || c.type) + ': <b>' + esc(label) + '</b>' +
              '<div class="muted">' + esc(sub) + ' · ' + c.employeeCount + ' çalışan</div>' +
              '<div class="muted">Detay için tıklayın</div>');
          },
          onMouseleave: hideTip
        }, [h('span', { text: label }), h('small', { text: sub })]));
      })));
    }));

    return h('div', null, [
      h('div', { class: 'heatmap' }, h('table', null, [thead, tbody])),
      h('div', { class: 'heat-scale' }, [
        h('span', { text: 'Düşük oran' }),
        h('i', { style: 'background:' + css('--seq-1') }),
        h('i', { style: 'background:' + css('--seq-2') }),
        h('i', { style: 'background:' + css('--seq-3') }),
        h('i', { style: 'background:' + css('--seq-4') }),
        h('i', { style: 'background:' + css('--seq-5') }),
        h('span', { text: 'Yüksek oran (en yüksek: ' +
          (maxRate <= 1 ? (maxRate * 100).toFixed(0) + '%' : maxRate.toFixed(1) + '×') + ')' }),
        data.truncated ? badge('neutral', '+' + data.truncated + ' satır gizlendi') : null
      ]),
      h('p', { class: 'card__desc', text:
        'Hücreler HAM SAYI değil, normalize orandır: MistakeRecorded / ActionCompleted. ' +
        'Böylece çok oynanan bir adım sırf hacmi yüzünden riskli görünmez. ' +
        'Bir adım tek seferde birden fazla kez yanlış denenebildiği için bu oran 1\'i ' +
        'aşabilir; o durumda yüzde yerine çarpan (ör. 3.0×) gösterilir.' })
    ]);
  }

  // ---------------------------------------------------------------------------
  // KÜÇÜK ÇOKLU (small multiples) sekmeli grafik kabı
  // ---------------------------------------------------------------------------
  /**
   * Farklı ölçekteki metrikleri AYNI eksene sıkıştırmak yerine sekmeye ayırır.
   * @param tabs [{id, label, render:() => Node}]
   */
  function tabbedChart(tabs, initialId) {
    const body = h('div');
    const btns = [];

    function select(id) {
      tabs.forEach(function (t, i) {
        const on = t.id === id;
        btns[i].setAttribute('aria-selected', on ? 'true' : 'false');
        btns[i].tabIndex = on ? 0 : -1;
      });
      clear(body);
      const t = tabs.filter(function (x) { return x.id === id; })[0];
      if (t) body.appendChild(t.render());
    }

    const bar = h('div', { class: 'chart-tabs', role: 'tablist' }, tabs.map(function (t, i) {
      const b = h('button', {
        class: 'chart-tab', type: 'button', role: 'tab', id: 'tab-' + t.id,
        'aria-selected': 'false', text: t.label,
        onClick: function () { select(t.id); },
        onKeydown: function (e) {
          if (e.key !== 'ArrowRight' && e.key !== 'ArrowLeft') return;
          e.preventDefault();
          const d = e.key === 'ArrowRight' ? 1 : -1;
          const n = (i + d + tabs.length) % tabs.length;
          btns[n].focus(); select(tabs[n].id);
        }
      });
      btns.push(b);
      return b;
    }));

    const wrap = h('div', null, [bar, h('div', { role: 'tabpanel' }, body)]);
    select(initialId || tabs[0].id);
    return wrap;
  }

  // ---------------------------------------------------------------------------
  // DRAWER
  // ---------------------------------------------------------------------------

  let drawerEl = null, drawerBackdrop = null, lastFocus = null;

  function ensureDrawer() {
    if (drawerEl) return;
    drawerBackdrop = h('div', { class: 'drawer-backdrop', onClick: closeDrawer });
    drawerEl = h('aside', {
      class: 'drawer', role: 'dialog', 'aria-modal': 'true',
      'aria-labelledby': 'drawer-title', hidden: true
    });
    document.body.appendChild(drawerBackdrop);
    document.body.appendChild(drawerEl);
    document.addEventListener('keydown', function (e) {
      if (e.key === 'Escape' && drawerEl.classList.contains('is-open')) closeDrawer();
      if (e.key === 'Tab' && drawerEl.classList.contains('is-open')) trapFocus(e, drawerEl);
    });
  }

  function trapFocus(e, root) {
    const f = root.querySelectorAll(
      'a[href],button:not([disabled]),input,select,textarea,summary,[tabindex]:not([tabindex="-1"])');
    if (!f.length) return;
    const first = f[0], last = f[f.length - 1];
    if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus(); }
    else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); }
  }

  function openDrawer(title, content, subtitle) {
    ensureDrawer();
    lastFocus = document.activeElement;
    clear(drawerEl);
    const closeBtn = h('button', {
      class: 'iconbtn', type: 'button', 'aria-label': 'Paneli kapat', onClick: closeDrawer
    }, icon('x', 16));
    drawerEl.appendChild(h('header', { class: 'drawer__head' }, [
      h('div', { style: 'flex:1' }, [
        h('h2', { id: 'drawer-title', text: title }),
        subtitle ? h('p', { class: 'card__desc', text: subtitle }) : null
      ]),
      closeBtn
    ]));
    drawerEl.appendChild(h('div', { class: 'drawer__body' }, content));
    drawerEl.hidden = false;
    requestAnimationFrame(function () {
      drawerEl.classList.add('is-open');
      drawerBackdrop.classList.add('is-open');
      closeBtn.focus();
    });
  }

  function closeDrawer() {
    if (!drawerEl) return;
    drawerEl.classList.remove('is-open');
    drawerBackdrop.classList.remove('is-open');
    setTimeout(function () { drawerEl.hidden = true; }, 200);
    if (lastFocus && lastFocus.focus) lastFocus.focus();
  }

  // ---------------------------------------------------------------------------
  // MODAL
  // ---------------------------------------------------------------------------

  let modalBackdrop = null;

  function openModal(title, bodyNodes, actions) {
    if (!modalBackdrop) {
      modalBackdrop = h('div', { class: 'modal-backdrop', onClick: function (e) {
        if (e.target === modalBackdrop) closeModal();
      } });
      document.body.appendChild(modalBackdrop);
      document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && modalBackdrop.classList.contains('is-open')) closeModal();
        if (e.key === 'Tab' && modalBackdrop.classList.contains('is-open')) trapFocus(e, modalBackdrop);
      });
    }
    clear(modalBackdrop);
    const m = h('div', { class: 'modal', role: 'dialog', 'aria-modal': 'true',
                         'aria-labelledby': 'modal-title' }, [
      h('h2', { id: 'modal-title', text: title }),
      h('div', null, bodyNodes),
      h('div', { class: 'modal__actions' }, actions || [
        h('button', { class: 'btn btn--primary', type: 'button', text: 'Tamam',
                      onClick: closeModal })
      ])
    ]);
    modalBackdrop.appendChild(m);
    modalBackdrop.classList.add('is-open');
    const f = m.querySelector('button');
    if (f) f.focus();
  }

  function closeModal() {
    if (modalBackdrop) modalBackdrop.classList.remove('is-open');
  }

  // ---------------------------------------------------------------------------
  // TABLO (arama + sıralama + sayfalama)
  // ---------------------------------------------------------------------------
  /**
   * @param o {columns:[{key,label,num,render,sortable,value}], rows:[], pageSize, caption,
   *           emptyTitle, emptyWhat, onRow}
   */
  function dataTable(o) {
    const state = { sort: o.defaultSort || null, dir: o.defaultDir || 'desc', page: 1,
                    q: '', rows: o.rows.slice() };
    const wrap = h('div');

    function apply() {
      let rows = o.rows.slice();
      if (state.q && o.searchKeys) {
        const q = state.q.toLocaleLowerCase('tr');
        rows = rows.filter(function (r) {
          return o.searchKeys.some(function (k) {
            return String(r[k] === undefined || r[k] === null ? '' : r[k])
              .toLocaleLowerCase('tr').indexOf(q) >= 0;
          });
        });
      }
      if (state.sort) {
        const col = o.columns.filter(function (c) { return c.key === state.sort; })[0];
        const val = (col && col.value) || function (r) { return r[state.sort]; };
        rows.sort(function (a, b) {
          const x = val(a), y = val(b);
          const xn = (x === null || x === undefined), yn = (y === null || y === undefined);
          if (xn && yn) return 0;
          if (xn) return 1;           // veri yok → daima sona
          if (yn) return -1;
          if (typeof x === 'number' && typeof y === 'number') {
            return state.dir === 'asc' ? x - y : y - x;
          }
          const c = String(x).localeCompare(String(y), 'tr');
          return state.dir === 'asc' ? c : -c;
        });
      }
      state.rows = rows;
      render();
    }

    function render() {
      clear(wrap);

      if (o.searchKeys) {
        wrap.appendChild(h('div', { class: 'field', style: 'max-width:20rem;margin-bottom:12px' }, [
          h('label', { for: 'tbl-search', text: o.searchLabel || 'Ara' }),
          h('input', {
            type: 'search', id: 'tbl-search', value: state.q,
            placeholder: o.searchPlaceholder || 'Yazmaya başlayın…',
            onInput: function (e) { state.q = e.target.value; state.page = 1; apply(); }
          })
        ]));
      }

      if (!state.rows.length) {
        wrap.appendChild(emptyState({
          inline: true, icon: 'search',
          title: state.q ? 'Aramanızla eşleşen kayıt yok' : (o.emptyTitle || 'Kayıt yok'),
          what: state.q
            ? '"' + state.q + '" için sonuç bulunamadı.'
            : (o.emptyWhat || 'Bu kapsamda gösterilecek satır yok.'),
          action: state.q ? 'Arama terimini kısaltın veya temizleyin.' : 'Filtreleri gevşetin.'
        }));
        return;
      }

      const pageSize = o.pageSize || 12;
      const pages = Math.max(1, Math.ceil(state.rows.length / pageSize));
      if (state.page > pages) state.page = pages;
      const slice = state.rows.slice((state.page - 1) * pageSize, state.page * pageSize);

      const thead = h('thead', null, h('tr', null, o.columns.map(function (c) {
        const isSorted = state.sort === c.key;
        const th = h('th', {
          scope: 'col', class: c.num ? 'num' : null,
          'aria-sort': isSorted ? (state.dir === 'asc' ? 'ascending' : 'descending') : 'none'
        });
        if (c.sortable === false) { th.textContent = c.label; return th; }
        th.appendChild(h('button', {
          class: 'th-sort' + (isSorted ? ' is-active' : ''), type: 'button',
          onClick: function () {
            if (state.sort === c.key) state.dir = state.dir === 'asc' ? 'desc' : 'asc';
            else { state.sort = c.key; state.dir = c.num ? 'desc' : 'asc'; }
            apply();
          }
        }, [
          h('span', { text: c.label }),
          h('span', { class: 'th-sort__ind', text: isSorted ? (state.dir === 'asc' ? '▲' : '▼') : '↕',
                      'aria-hidden': 'true' })
        ]));
        return th;
      })));

      const tbody = h('tbody', null, slice.map(function (r) {
        return h('tr', null, o.columns.map(function (c) {
          const td = h('td', { class: c.num ? 'num' : null });
          const v = c.render ? c.render(r) : r[c.key];
          if (v === null || v === undefined) td.textContent = '—';
          else if (typeof v === 'object') td.appendChild(v);
          else td.textContent = String(v);
          return td;
        }));
      }));

      wrap.appendChild(h('div', { class: 'tablewrap' }, h('table', { class: 'data' }, [
        o.caption ? h('caption', { text: o.caption }) : null, thead, tbody
      ])));

      wrap.appendChild(h('div', { class: 'pager' }, [
        h('span', { text: state.rows.length + ' kayıt · sayfa ' + state.page + '/' + pages }),
        h('span', { style: 'display:flex;gap:8px' }, [
          h('button', { class: 'btn btn--ghost btn--sm', type: 'button', text: '‹ Önceki',
            'aria-disabled': state.page <= 1 ? 'true' : null,
            onClick: function () { if (state.page > 1) { state.page--; render(); } } }),
          h('button', { class: 'btn btn--ghost btn--sm', type: 'button', text: 'Sonraki ›',
            'aria-disabled': state.page >= pages ? 'true' : null,
            onClick: function () { if (state.page < pages) { state.page++; render(); } } })
        ])
      ]));
    }

    apply();
    return wrap;
  }

  // ---------------------------------------------------------------------------
  // Yardımcılar
  // ---------------------------------------------------------------------------

  function esc(v) {
    return String(v === null || v === undefined ? '' : v)
      .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
  }

  /** Oyun ikonunu gösterir; yüklenemezse dosya adını yazan placeholder'a düşer. */
  function assetImg(name, alt, size) {
    const px = size || 62;
    const img = h('img', {
      src: 'assets/images/' + name + '.png', alt: alt || '',
      width: px, height: px, loading: 'lazy'
    });
    img.addEventListener('error', function () {
      const fb = h('div', { class: 'imgfallback', style: 'width:' + px + 'px;height:' + px + 'px',
        title: 'Görsel yüklenemedi: assets/images/' + name + '.png' },
        [icon('warn', 14), h('span', { text: name + '.png' })]);
      if (img.parentNode) img.parentNode.replaceChild(fb, img);
    });
    return img;
  }

  window.TS_UI = {
    h: h, s: s, clear: clear, css: css, esc: esc, append: append,
    icon: icon, badge: badge, tooltip: tooltip, kpiCard: kpiCard, deltaBadge: deltaBadge,
    emptyState: emptyState, notice: notice, card: card,
    lineChart: lineChart, barList: barList, heatmapTable: heatmapTable, tabbedChart: tabbedChart,
    openDrawer: openDrawer, closeDrawer: closeDrawer, openModal: openModal, closeModal: closeModal,
    dataTable: dataTable, assetImg: assetImg,
    showTip: showTip, hideTip: hideTip, TYPE_LABEL: TYPE_LABEL
  };
})();
