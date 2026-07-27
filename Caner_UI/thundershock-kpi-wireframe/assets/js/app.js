/* =============================================================================
   THUNDERSHOCK KPI PORTALI — UYGULAMA KABUĞU, ROUTER VE OTURUM
   =============================================================================
   Hash tabanlı router, rol bazlı navigasyon, filtre durumu ve giriş ekranı.

   GÜVENLİK NOTU: Burada GERÇEK BİR KİMLİK DOĞRULAMA YOKTUR.
   Rol ayrımı yalnızca istemci tarafında temsil edilir ve bir güvenlik sınırı
   DEĞİLDİR. Üretimde sunucu tarafı yetkilendirme zorunludur (bkz. README).
============================================================================= */

(function () {
  'use strict';

  const U = window.TS_UI;
  const K = window.TS_KPI;
  const D = window.TS_DATA;
  const h = U.h;

  // ---------------------------------------------------------------------------
  // OTURUM (yalnızca bellekte — sayfa yenilenince sıfırlanır)
  // ---------------------------------------------------------------------------

  const app = {
    session: null,        // { id, name, role: 'employee' | 'manager' }
    navOpen: false,
    filters: null
  };

  // ---------------------------------------------------------------------------
  // FİLTRELER
  // ---------------------------------------------------------------------------

  function createFilters() {
    const state = {
      range: '30',        // gün; 'all' = tümü
      levelId: '',
      sequenceId: '',
      employeeId: '',
      mistakeType: ''
    };

    function bounds() {
      if (state.range === 'all') return { from: null, to: null };
      const days = Number(state.range);
      const to = new Date(D.TODAY.getTime() + 86400000);
      const from = new Date(D.TODAY.getTime() - days * 86400000);
      return { from: from.toISOString(), to: to.toISOString() };
    }

    return {
      state: state,
      bounds: bounds,
      /** Yönetici kapsamı — tüm filtreler geçerli. */
      managerScope: function () {
        const b = bounds();
        return {
          from: b.from, to: b.to,
          levelId: state.levelId || null,
          sequenceId: state.sequenceId || null,
          employeeId: state.employeeId || null,
          mistakeType: state.mistakeType || null
        };
      },
      /** Çalışan kapsamı — employeeId ZORLA oturum sahibine sabitlenir. */
      employeeScope: function () {
        const b = bounds();
        return { from: b.from, to: b.to, employeeId: app.session ? app.session.id : null };
      },
      label: function () {
        if (state.range === 'all') return 'Tüm zamanlar';
        return 'Son ' + state.range + ' gün';
      },
      reset: function () {
        state.range = '30'; state.levelId = ''; state.sequenceId = '';
        state.employeeId = ''; state.mistakeType = '';
      }
    };
  }

  app.filters = createFilters();

  // ---------------------------------------------------------------------------
  // MARKA İŞARETİ
  // ---------------------------------------------------------------------------
  // NOT: Projede web'e export edilmiş bir "Thundershock" logosu YOKTUR
  // (bedas_logo.glb yalnızca 3D model). Aşağıdaki işaret, oyunun adından
  // (yıldırım) türetilmiş bir prototip işaretidir — bkz. ASSET_GAPS.md.

  function brandMark(size) {
    const px = size || 34;
    return U.s('svg', {
      width: px, height: px, viewBox: '0 0 40 40', 'aria-hidden': 'true',
      class: 'brand__mark', focusable: 'false'
    }, [
      U.s('rect', { x: 1, y: 1, width: 38, height: 38, rx: 9,
        fill: 'var(--panel-2)', stroke: 'var(--accent)', 'stroke-width': 1.6 }),
      U.s('path', { d: 'M22.5 7 L13 21.5 h6.2 L17.5 33 L27 18.5 h-6.2 z',
        fill: 'var(--accent)' }),
      U.s('path', { d: 'M8 30.5 h5M27.5 9.5 h5', stroke: 'var(--warn)',
        'stroke-width': 1.6, 'stroke-linecap': 'round', opacity: .8 })
    ]);
  }

  function brand(sub) {
    return h('div', { class: 'brand' }, [
      brandMark(34),
      h('div', null, [
        h('div', { class: 'brand__name', text: 'THUNDERSHOCK' }),
        h('div', { class: 'brand__sub', text: sub || 'KPI & Eğitim Analitiği' })
      ])
    ]);
  }

  // ---------------------------------------------------------------------------
  // TEMSİLİ VERİ ŞERİDİ
  // ---------------------------------------------------------------------------

  function mockBanner() {
    return h('div', { class: 'mock-banner', role: 'note' }, [
      U.icon('warn', 14),
      h('span', { text:
        'Temsili veri — bu portaldaki tüm çalışan ve olay kayıtları prototip amaçlı ' +
        'üretilmiştir; gerçek kişisel veri içermez.' })
    ]);
  }

  // ---------------------------------------------------------------------------
  // GİRİŞ EKRANI
  // ---------------------------------------------------------------------------

  const DEMO = {
    employee: [
      { id: 'TEST001',  label: 'Zengin veri — 6 deneme, 3 senaryo' },
      { id: 'EMP-1049', label: 'Gelişen performans — 3 deneme' },
      { id: 'EMP-1047', label: 'Tek deneme — karşılaştırma yapılamaz' },
      { id: 'EMP-1048', label: 'Hiç verisi yok — boş durum' }
    ],
    manager: [
      { id: 'ADMIN_DEMO', label: 'Tüm kurum verisine erişim' }
    ]
  };

  function renderLogin() {
    const root = document.getElementById('app');
    U.clear(root);
    document.body.className = 'is-login';

    const tabState = { role: 'employee' };
    const idInput = h('input', {
      type: 'text', id: 'login-id', autocomplete: 'off', spellcheck: 'false',
      placeholder: 'TEST001', 'aria-describedby': 'login-id-hint'
    });
    const pwInput = h('input', {
      type: 'password', id: 'login-pw', autocomplete: 'off',
      placeholder: '••••••••', 'aria-describedby': 'login-pw-hint'
    });
    const status = h('div', { role: 'status', 'aria-live': 'polite', style: 'min-height:1.4rem' });

    function setRole(r) {
      tabState.role = r;
      empTab.setAttribute('aria-selected', r === 'employee' ? 'true' : 'false');
      mgrTab.setAttribute('aria-selected', r === 'manager' ? 'true' : 'false');
      idInput.placeholder = r === 'employee' ? 'TEST001' : 'ADMIN_DEMO';
      U.clear(demoWrap);
      demoWrap.appendChild(demoList(r));
      U.clear(status);
    }

    const empTab = h('button', {
      class: 'tab', type: 'button', role: 'tab', 'aria-selected': 'true',
      onClick: function () { setRole('employee'); }
    }, [U.icon('user', 15), 'Çalışan Girişi']);

    const mgrTab = h('button', {
      class: 'tab', type: 'button', role: 'tab', 'aria-selected': 'false',
      onClick: function () { setRole('manager'); }
    }, [U.icon('users', 15), 'Yönetici Girişi']);

    const demoWrap = h('div');

    function demoList(role) {
      return h('div', null, [
        h('div', { style: 'font-size:.78rem;font-weight:600;color:var(--ink-2);margin-bottom:8px' },
          'Demo hesapla giriş yap'),
        h('div', { class: 'demo-list' }, DEMO[role].map(function (d) {
          return h('button', {
            class: 'demo-btn', type: 'button',
            onClick: function () { doLogin(d.id, role); }
          }, [
            U.icon(role === 'employee' ? 'user' : 'users', 16),
            h('span', { style: 'flex:1' }, [
              h('code', { text: d.id }),
              h('small', { text: d.label })
            ]),
            U.icon('arrowRight', 14)
          ]);
        }))
      ]);
    }

    function doLogin(id, role) {
      const found = role === 'manager'
        ? D.managers.filter(function (m) { return m.id === id; })[0]
        : D.employees.filter(function (e) { return e.id === id; })[0];

      if (!found) {
        U.clear(status);
        status.appendChild(U.notice('bad',
          'Bu ID demo listesinde bulunamadı: <b>' + U.esc(id) + '</b>. ' +
          'Aşağıdaki demo hesaplardan birini seçebilirsiniz.'));
        return;
      }
      app.session = { id: found.id, name: found.name, role: role };
      app.filters.reset();
      location.hash = role === 'manager' ? '#/manager/dashboard' : '#/employee/dashboard';
    }

    function submit() {
      const id = idInput.value.trim();
      if (!id) {
        U.clear(status);
        status.appendChild(U.notice('warn', 'Lütfen bir çalışan ID girin veya demo hesap seçin.'));
        idInput.focus();
        return;
      }
      doLogin(id, tabState.role);
    }

    idInput.addEventListener('keydown', function (e) { if (e.key === 'Enter') submit(); });
    pwInput.addEventListener('keydown', function (e) { if (e.key === 'Enter') submit(); });

    const totalEvents = D.events.length;
    const totalEmployees = D.employees.length;

    root.appendChild(h('div', { class: 'login-wrap' }, [
      // Sol: marka / atmosfer
      h('div', { class: 'login-art' }, [
        brand('İş Güvenliği Eğitim Simülasyonu'),
        h('div', { class: 'login-tagline' }, [
          h('h2', { text: 'Saha eğitimini ölçülebilir hale getirin.' }),
          h('p', { text:
            'Thundershock, saha çalışanlarına elektrik tesisi operasyonlarını öğreten bir 3D ' +
            'iş güvenliği eğitim simülasyonudur. Bu portal, simülasyondan toplanan event ' +
            'verisini çalışan ve yönetici için anlaşılır KPI\'lara dönüştürür.' })
        ]),
        h('div', { class: 'login-facts' }, [
          h('div', { class: 'login-fact' }, [
            h('b', { text: String(D.content.levels.length) }), h('span', { text: 'Senaryo' })]),
          h('div', { class: 'login-fact' }, [
            h('b', { text: String(D.content.levels.reduce(function (a, l) {
              return a + l.sequences.length; }, 0)) }), h('span', { text: 'Görev Grubu' })]),
          h('div', { class: 'login-fact' }, [
            h('b', { text: String(totalEmployees) }), h('span', { text: 'Demo Çalışan' })]),
          h('div', { class: 'login-fact' }, [
            h('b', { text: String(totalEvents) }), h('span', { text: 'Temsili Event' })])
        ])
      ]),

      // Sağ: giriş
      h('div', { class: 'login-panel' }, [
        h('div', { class: 'login-card' }, [
          h('div', { style: 'margin-bottom:24px' }, [
            h('h1', { text: 'Portala Giriş' }),
            h('p', { style: 'color:var(--ink-2);margin-top:6px;font-size:.88rem',
              text: 'Rolünüzü seçin ve çalışan ID\'nizle giriş yapın.' })
          ]),
          h('div', { class: 'tabs', role: 'tablist',
                     'aria-label': 'Giriş rolü' }, [empTab, mgrTab]),
          h('div', { class: 'field' }, [
            h('label', { for: 'login-id', text: 'Çalışan ID' }),
            idInput,
            h('div', { class: 'hint', id: 'login-id-hint',
              text: 'Oyundaki giriş ekranı da yalnızca çalışan ID ister ' +
                    '(UILoginPanel.cs — PlayFab whitelist araması).' })
          ]),
          h('div', { class: 'field' }, [
            h('label', { for: 'login-pw', text: 'Şifre' }),
            pwInput,
            h('div', { class: 'hint', id: 'login-pw-hint',
              text: 'Prototipte kontrol edilmez — oyunun mevcut giriş akışında şifre alanı yoktur.' })
          ]),
          h('button', { class: 'btn btn--primary btn--block', type: 'button',
                        onClick: submit }, [U.icon('lock', 15), 'Giriş Yap']),
          status,
          h('div', { style: 'margin-top:20px' }, demoWrap),
          h('div', { class: 'login-note' }, [
            h('b', { text: 'Bu bir prototiptir. ' }),
            'Gerçek kimlik doğrulama yoktur; şifre alanı doğrulanmaz. ' +
            'Demo hesaplar (TEST001, ADMIN_DEMO) gerçek kullanıcı değildir ve tüm veriler temsilidir.'
          ]),
          h('div', { style: 'margin-top:14px;display:flex;gap:14px;font-size:.78rem' }, [
            h('a', { href: 'README.md', text: 'Yardım & dokümantasyon' }),
            h('span', { style: 'color:var(--ink-dis)', text: 'Destek: (placeholder)' })
          ])
        ])
      ])
    ]));

    setRole('employee');
    idInput.focus();
  }

  // ---------------------------------------------------------------------------
  // NAVİGASYON TANIMI
  // ---------------------------------------------------------------------------

  const NAV = {
    employee: [
      { hash: '#/employee/dashboard',   label: 'Genel Bakış',  icon: 'overview' },
      { hash: '#/employee/scenarios',   label: 'Senaryolarım', icon: 'scenarios' },
      { hash: '#/employee/performance', label: 'Performansım', icon: 'chart' },
      { hash: '#/employee/mistakes',    label: 'Hatalarım',    icon: 'alert' },
      { hash: '#/employee/progress',    label: 'Gelişimim',    icon: 'trophy' },
      { hash: '#/employee/profile',     label: 'Profil',       icon: 'user' }
    ],
    manager: [
      { hash: '#/manager/dashboard', label: 'Yönetim Özeti',      icon: 'overview' },
      { hash: '#/manager/employees', label: 'Çalışanlar',         icon: 'users' },
      { hash: '#/manager/scenarios', label: 'Senaryolar',         icon: 'scenarios' },
      { hash: '#/manager/risks',     label: 'Hata ve Risk Analizi', icon: 'alert' },
      { hash: '#/manager/trends',    label: 'Gelişim Trendleri',  icon: 'chart' },
      { hash: '#/manager/reports',   label: 'Raporlar',           icon: 'report' },
      { hash: '#/manager/settings',  label: 'Ayarlar',            icon: 'settings' }
    ]
  };

  // ---------------------------------------------------------------------------
  // KABUK
  // ---------------------------------------------------------------------------

  function renderShell(route) {
    const root = document.getElementById('app');
    U.clear(root);
    document.body.className = 'is-app';

    const role = app.session.role;
    const items = NAV[role];

    // -- Yan menü ------------------------------------------------------------
    const nav = h('nav', { class: 'sidenav' + (app.navOpen ? ' is-open' : ''),
                           id: 'sidenav', 'aria-label': 'Ana menü' }, [
      h('div', { class: 'sidenav__head' }, [
        brand(role === 'manager' ? 'Yönetici Portalı' : 'Çalışan Portalı'),
        h('span', { class: 'rolechip rolechip--' + role }, [
          U.icon(role === 'manager' ? 'users' : 'user', 12),
          role === 'manager' ? 'Yönetici' : 'Çalışan'
        ])
      ]),
      h('ul', { class: 'navlist' }, items.map(function (it) {
        const active = route.hash === it.hash ||
          (route.section === sectionOf(it.hash) && route.hash.indexOf(it.hash) === 0);
        return h('li', null, h('a', {
          class: 'navlink', href: it.hash,
          'aria-current': isActive(route, it) ? 'page' : null,
          onClick: function () { app.navOpen = false; }
        }, [U.icon(it.icon, 17), it.label]));
      })),
      h('div', { class: 'sidenav__foot' }, [
        h('button', { class: 'btn btn--ghost btn--block', type: 'button',
          onClick: confirmLogout }, [U.icon('logout', 15), 'Çıkış'])
      ])
    ]);

    // -- Üst bar -------------------------------------------------------------
    const topbar = h('header', { class: 'topbar' }, [
      h('button', {
        class: 'iconbtn navtoggle', type: 'button', 'aria-label': 'Menüyü aç/kapat',
        'aria-expanded': app.navOpen ? 'true' : 'false', 'aria-controls': 'sidenav',
        onClick: function () {
          app.navOpen = !app.navOpen;
          nav.classList.toggle('is-open', app.navOpen);
          this.setAttribute('aria-expanded', app.navOpen ? 'true' : 'false');
        }
      }, U.icon('menu', 18)),
      h('nav', { class: 'crumbs', 'aria-label': 'Sayfa yolu' }, crumbs(route)),
      h('div', { class: 'topbar__spacer' }),
      h('button', {
        class: 'iconbtn', type: 'button', 'aria-label': 'Bildirimler (3 okunmamış)',
        onClick: showNotifications
      }, [U.icon('bell', 17), h('span', { class: 'iconbtn__dot', 'aria-hidden': 'true' })]),
      h('div', { class: 'userchip' }, [
        h('div', { class: 'avatar', 'aria-hidden': 'true',
                   text: initials(app.session.name) }),
        h('div', null, [
          h('div', { class: 'userchip__name', text: app.session.name }),
          h('div', { class: 'userchip__id', text: app.session.id })
        ])
      ])
    ]);

    // -- İçerik --------------------------------------------------------------
    const main = h('main', { class: 'main', id: 'main', tabindex: '-1' });

    // Yönetici filtre çubuğu (çalışan portalında sadece tarih)
    main.appendChild(role === 'manager' ? managerFilterBar(route) : employeeFilterBar(route));
    main.appendChild(renderRoute(route));

    root.appendChild(mockBanner());
    root.appendChild(h('div', { class: 'shell' }, [nav, h('div', null, [topbar, main])]));
  }

  function isActive(route, item) {
    if (route.hash === item.hash) return true;
    // Detay sayfaları üst menü öğesini aktif tutar
    const map = {
      '#/employee/scenarios': ['#/employee/scenario/'],
      '#/manager/employees':  ['#/manager/employee/'],
      '#/manager/scenarios':  ['#/manager/scenario/']
    };
    const pre = map[item.hash];
    return pre ? pre.some(function (p) { return route.hash.indexOf(p) === 0; }) : false;
  }

  function sectionOf(hash) { return hash.split('/')[1]; }

  function initials(name) {
    return name.split(/\s+/).map(function (w) { return w[0]; }).slice(0, 2).join('').toUpperCase();
  }

  // -- Breadcrumb -------------------------------------------------------------

  function crumbs(route) {
    const role = app.session.role;
    const home = role === 'manager'
      ? { label: 'Yönetim Özeti', hash: '#/manager/dashboard' }
      : { label: 'Genel Bakış', hash: '#/employee/dashboard' };

    const parts = [home];

    const nav = NAV[role].filter(function (n) { return isActive(route, n) && n.hash !== home.hash; })[0];
    if (nav) parts.push({ label: nav.label, hash: nav.hash });

    if (route.detail) parts.push({ label: route.detailLabel || route.detail, hash: null });

    const out = [];
    parts.forEach(function (p, i) {
      if (i) out.push(h('span', { class: 'sep', 'aria-hidden': 'true', text: '›' }));
      out.push(p.hash && i < parts.length - 1
        ? h('a', { href: p.hash, text: p.label })
        : h('span', { 'aria-current': 'page', text: p.label }));
    });
    return out;
  }

  // -- Filtre çubukları -------------------------------------------------------

  function rangeField(id) {
    return h('div', { class: 'filterbar__field' }, [
      h('label', { for: id, text: 'Tarih aralığı' }),
      h('select', { id: id, onChange: function (e) {
        app.filters.state.range = e.target.value; rerender(); } }, [
        h('option', { value: '7',   selected: app.filters.state.range === '7',   text: 'Son 7 gün' }),
        h('option', { value: '30',  selected: app.filters.state.range === '30',  text: 'Son 30 gün' }),
        h('option', { value: '90',  selected: app.filters.state.range === '90',  text: 'Son 90 gün' }),
        h('option', { value: 'all', selected: app.filters.state.range === 'all', text: 'Tüm zamanlar' })
      ])
    ]);
  }

  function employeeFilterBar(route) {
    const b = app.filters.bounds();
    const evs = K.filterEvents(D.events, app.filters.employeeScope());
    const bar = h('div', { class: 'filterbar' }, [
      h('button', { class: 'filter-toggle', type: 'button',
        onClick: function () { bar.classList.toggle('is-open'); } },
        [h('span', null, [U.icon('filter', 15), ' Filtreler']), h('span', { text: '⌄' })]),
      h('div', { class: 'filterbar__row' }, [
        rangeField('emp-range'),
        h('div', { class: 'filterbar__field', style: 'flex:2' }, [
          h('label', { text: 'Kapsam' }),
          h('div', { style: 'font-size:.84rem;color:var(--ink-2);padding-top:7px' },
            'Yalnızca kendi kayıtlarınız gösterilir.')
        ])
      ]),
      h('div', { class: 'scope-line' }, [
        U.icon('info', 13),
        h('span', null, [
          'Aktif kapsam: ', h('b', { text: app.filters.label() }),
          ' · ', h('b', { text: evs.length + ' event' }),
          b.from ? ' · ' + K.fmtDate(b.from) + ' – ' + K.fmtDate(D.TODAY) : ''
        ])
      ])
    ]);
    return bar;
  }

  function managerFilterBar(route) {
    const f = app.filters.state;
    const b = app.filters.bounds();
    const evs = K.filterEvents(D.events, app.filters.managerScope());

    // Seçili level'a göre sequence listesi
    const lvl = f.levelId ? D.levelByEmittedId[f.levelId] : null;
    const seqOptions = lvl ? lvl.sequences
      : D.content.levels.reduce(function (a, l) { return a.concat(l.sequences); }, []);

    const bar = h('div', { class: 'filterbar' }, [
      h('button', { class: 'filter-toggle', type: 'button',
        onClick: function () { bar.classList.toggle('is-open'); } },
        [h('span', null, [U.icon('filter', 15), ' Filtreler']), h('span', { text: '⌄' })]),

      h('div', { class: 'filterbar__row' }, [
        rangeField('mgr-range'),

        h('div', { class: 'filterbar__field' }, [
          h('label', { for: 'mgr-level', text: 'Senaryo' }),
          h('select', { id: 'mgr-level', onChange: function (e) {
            f.levelId = e.target.value; f.sequenceId = ''; rerender(); } },
            [h('option', { value: '', text: 'Tümü' })].concat(D.content.levels.map(function (l) {
              return h('option', { value: l.emittedLevelId, selected: f.levelId === l.emittedLevelId,
                                   text: l.name });
            })))
        ]),

        h('div', { class: 'filterbar__field' }, [
          h('label', { for: 'mgr-seq', text: 'Görev grubu' }),
          h('select', { id: 'mgr-seq', onChange: function (e) {
            f.sequenceId = e.target.value; rerender(); } },
            [h('option', { value: '', text: 'Tümü' })].concat(seqOptions.map(function (s) {
              return h('option', { value: s.id, selected: f.sequenceId === s.id, text: s.name });
            })))
        ]),

        h('div', { class: 'filterbar__field' }, [
          h('label', { for: 'mgr-emp', text: 'Çalışan' }),
          h('select', { id: 'mgr-emp', onChange: function (e) {
            f.employeeId = e.target.value; rerender(); } },
            [h('option', { value: '', text: 'Tümü' })].concat(D.employees.map(function (em) {
              return h('option', { value: em.id, selected: f.employeeId === em.id,
                                   text: em.name + ' (' + em.id + ')' });
            })))
        ]),

        h('div', { class: 'filterbar__field' }, [
          h('label', { for: 'mgr-mtype', text: 'Hata türü' }),
          h('select', { id: 'mgr-mtype', onChange: function (e) {
            f.mistakeType = e.target.value; rerender(); } }, [
            h('option', { value: '', selected: !f.mistakeType, text: 'Tümü' }),
            h('option', { value: 'wrong_answer', selected: f.mistakeType === 'wrong_answer',
                          text: 'Yanlış cevap' }),
            h('option', { value: 'wrong_drop', selected: f.mistakeType === 'wrong_drop',
                          text: 'Yanlış yerleştirme' })
          ])
        ]),

        // --- Veri kaynağı olmayan boyutlar: DEVRE DIŞI, sahte seçenek yok ---
        h('div', { class: 'filterbar__field filterbar__field--off' }, [
          h('label', { for: 'mgr-sev', text: 'Önem (severity)' }),
          h('select', { id: 'mgr-sev', disabled: true, 'aria-describedby': 'sev-note' },
            [h('option', { text: 'Ölçek tanımsız' })]),
          h('div', { class: 'hint', id: 'sev-note', text: 'Kodda sabit 1' })
        ]),
        h('div', { class: 'filterbar__field filterbar__field--off' }, [
          h('label', { for: 'mgr-team', text: 'Ekip / Departman' }),
          h('select', { id: 'mgr-team', disabled: true, 'aria-describedby': 'team-note' },
            [h('option', { text: 'Veri kaynağı yok' })]),
          h('div', { class: 'hint', id: 'team-note', text: 'Gelecek veri entegrasyonu' })
        ]),
        h('div', { class: 'filterbar__field filterbar__field--off' }, [
          h('label', { for: 'mgr-loc', text: 'Lokasyon / Vardiya' }),
          h('select', { id: 'mgr-loc', disabled: true, 'aria-describedby': 'loc-note' },
            [h('option', { text: 'Veri kaynağı yok' })]),
          h('div', { class: 'hint', id: 'loc-note', text: 'Gelecek veri entegrasyonu' })
        ]),

        h('div', { class: 'filterbar__field', style: 'flex:0 0 auto;min-width:0' }, [
          h('label', { text: ' ' }),
          h('button', { class: 'btn btn--ghost', type: 'button', text: 'Sıfırla',
            onClick: function () { app.filters.reset(); rerender(); } })
        ])
      ]),

      h('div', { class: 'scope-line', role: 'status', 'aria-live': 'polite' }, [
        U.icon('info', 13),
        h('span', null, [
          'Aktif kapsam: ', h('b', { text: app.filters.label() }),
          f.levelId ? ' · senaryo: ' : '', f.levelId ? h('b', { text: levelLabel(f.levelId) }) : '',
          f.sequenceId ? ' · görev grubu: ' : '', f.sequenceId ? h('b', { text: f.sequenceId }) : '',
          f.employeeId ? ' · çalışan: ' : '', f.employeeId ? h('b', { text: f.employeeId }) : '',
          f.mistakeType ? ' · hata türü: ' : '',
          f.mistakeType ? h('b', { text: U.TYPE_LABEL[f.mistakeType] }) : '',
          ' · ', h('b', { text: evs.length + ' event' }),
          ' · ', h('b', { text: K.activeEmployees(evs) + ' aktif çalışan' })
        ])
      ])
    ]);
    return bar;
  }

  function levelLabel(id) {
    const l = D.levelByEmittedId[id];
    return l ? l.name : id;
  }

  // -- Bildirimler ------------------------------------------------------------

  function showNotifications() {
    U.openModal('Bildirimler', [
      h('div', { class: 'stack' }, [
        U.notice('warn', '<b>Severity ölçeği tanımsız.</b> Kritik hata sınıflandırması ' +
          'yapılamıyor — Ayarlar sayfasına bakın.'),
        U.notice('warn', '<b>Level 2 ve Level 3 levelId değerleri hatalı.</b> ' +
          '"lvl1" ve "NewLevel" olarak gönderiliyor.'),
        U.notice('info', '<b>Anket sonuçları telemetriye yazılmıyor.</b> ' +
          'SurveyResultTracker verileri yalnızca bellekte tutuyor.')
      ]),
      h('p', { class: 'card__desc mt-4', text:
        'Bildirimler bu prototipte sabittir; gerçek üründe veri kalitesi kontrollerinden üretilir.' })
    ]);
  }

  // -- Çıkış ------------------------------------------------------------------

  function confirmLogout() {
    U.openModal('Çıkış yapılsın mı?', [
      h('p', { text: 'Oturumunuz kapatılacak ve giriş ekranına döneceksiniz. ' +
                     'Filtre seçimleriniz sıfırlanır.' })
    ], [
      h('button', { class: 'btn btn--ghost', type: 'button', text: 'Vazgeç',
        onClick: U.closeModal }),
      h('button', { class: 'btn btn--primary', type: 'button', text: 'Çıkış Yap',
        onClick: function () {
          U.closeModal();
          app.session = null;
          app.filters.reset();
          location.hash = '#/login';
        } })
    ]);
  }

  // ---------------------------------------------------------------------------
  // ROUTER
  // ---------------------------------------------------------------------------

  function parseRoute() {
    const hash = location.hash || '#/login';
    const parts = hash.replace(/^#\/?/, '').split('/');
    return {
      hash: hash,
      section: parts[0] || 'login',      // login | employee | manager
      page: parts[1] || '',
      detail: parts[2] ? decodeURIComponent(parts[2]) : null,
      detailLabel: null
    };
  }

  function renderRoute(route) {
    const E = window.TS_EMPLOYEE, M = window.TS_MANAGER;

    try {
      if (route.section === 'employee') {
        switch (route.page) {
          case 'dashboard':   return E.dashboard(app);
          case 'scenarios':   return E.scenarios(app);
          case 'scenario':    return E.scenarioDetail(app, route.detail);
          case 'performance': return E.performance(app);
          case 'mistakes':    return E.mistakes(app);
          case 'progress':    return E.progress(app);
          case 'profile':     return E.profile(app);
          default:            return notFound(route);
        }
      }
      if (route.section === 'manager') {
        switch (route.page) {
          case 'dashboard': return M.dashboard(app);
          case 'employees': return M.employees(app);
          case 'employee':  return M.employeeDetail(app, route.detail);
          case 'scenarios': return M.scenarios(app);
          case 'scenario':  return M.scenarioDetail(app, route.detail);
          case 'risks':     return M.risks(app);
          case 'trends':    return M.trends(app);
          case 'reports':   return M.reports(app);
          case 'settings':  return M.settings(app);
          default:          return notFound(route);
        }
      }
    } catch (err) {
      // Beklenmeyen veri formatı / hesap hatası — kullanıcıya sessizce boş ekran gösterme
      if (window.console) console.error('[Thundershock KPI] Görünüm hatası:', err);
      return U.emptyState({
        icon: 'alert', tone: 'bad', title: 'Bu ekran yüklenemedi',
        what: 'Görünüm oluşturulurken beklenmeyen bir veri formatı hatası oluştu: ' +
              (err && err.message ? err.message : 'bilinmeyen hata'),
        why: 'Event kaydı beklenen şemaya uymuyor olabilir (eksik payload alanı ya da ' +
             'tanınmayan eventType).',
        action: 'Filtreleri sıfırlayıp tekrar deneyin; sorun sürerse tarayıcı konsolundaki ' +
                'hata kaydını geliştirme ekibine iletin.',
        cta: h('button', { class: 'btn btn--ghost', type: 'button', text: 'Filtreleri sıfırla',
          onClick: function () { app.filters.reset(); rerender(); } })
      });
    }
    return notFound(route);
  }

  function notFound(route) {
    const home = app.session.role === 'manager' ? '#/manager/dashboard' : '#/employee/dashboard';
    return U.emptyState({
      icon: 'search', title: 'Sayfa bulunamadı',
      what: '"' + route.hash + '" adresine karşılık gelen bir ekran yok.',
      action: 'Sol menüden bir sayfa seçin.',
      cta: h('a', { class: 'btn btn--primary', href: home, text: 'Ana sayfaya dön' })
    });
  }

  // Detay sayfaları için breadcrumb etiketini zenginleştir
  function decorateRoute(route) {
    if (route.page === 'scenario' && route.detail) {
      const l = D.levelByEmittedId[route.detail];
      route.detailLabel = l ? l.name : route.detail;
    }
    if (route.page === 'employee' && route.detail) {
      const e = D.employeeById[route.detail];
      route.detailLabel = e ? e.name : route.detail;
    }
    return route;
  }

  function render() {
    U.closeDrawer();
    U.hideTip();
    const route = decorateRoute(parseRoute());

    // Oturum yoksa daima giriş ekranı
    if (!app.session || route.section === 'login') {
      if (app.session) app.session = null;
      renderLogin();
      document.title = 'Giriş — Thundershock KPI Portalı';
      return;
    }

    // ROL SINIRI: çalışan yönetici rotalarına, yönetici çalışan rotalarına giremez.
    // (Prototip temsili — gerçek koruma sunucuda olmalıdır.)
    if (route.section !== app.session.role) {
      const home = app.session.role === 'manager' ? '#/manager/dashboard' : '#/employee/dashboard';
      location.replace(home);
      return;
    }

    renderShell(route);
    document.title = pageTitle(route) + ' — Thundershock KPI Portalı';
    window.scrollTo(0, 0);
  }

  function pageTitle(route) {
    const item = NAV[app.session.role].filter(function (n) { return isActive(route, n); })[0];
    return route.detailLabel || (item ? item.label : 'Portal');
  }

  function rerender() { render(); }

  // ---------------------------------------------------------------------------
  // BAŞLAT
  // ---------------------------------------------------------------------------

  window.addEventListener('hashchange', render);

  document.addEventListener('DOMContentLoaded', function () {
    if (!location.hash) location.hash = '#/login';
    else render();
  });

  // DOMContentLoaded zaten geçtiyse
  if (document.readyState !== 'loading') {
    if (!location.hash) location.hash = '#/login';
    else render();
  }

  window.TS_APP = app;
})();
