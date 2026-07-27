/* =============================================================================
   THUNDERSHOCK KPI PORTALI — YÖNETİCİ (ADMIN) PORTALI EKRANLARI
   =============================================================================
   Yönetici ekranları çalışan ekranlarının kopyası DEĞİLDİR. Çalışan portalı
   "ben ne yaptım?" sorusuna, yönetici portalı "kim/ne riskli, nerede
   yoğunlaşmalıyım?" sorusuna cevap verir: karşılaştırma, normalize risk,
   kurum içi konum ve event'e kadar inen drill-down.
============================================================================= */

(function () {
  'use strict';

  const U = window.TS_UI;
  const K = window.TS_KPI;
  const D = window.TS_DATA;
  const h = U.h;

  function scoped(app) { return K.filterEvents(D.events, app.filters.managerScope()); }

  /** Bir önceki eşit uzunluktaki dönem — "önceki döneme göre" karşılaştırması için. */
  function previousPeriod(app) {
    const f = app.filters.managerScope();
    if (!f.from || !f.to) return null;
    const from = new Date(f.from), to = new Date(f.to);
    const span = to - from;
    return K.filterEvents(D.events, Object.assign({}, f, {
      from: new Date(from.getTime() - span).toISOString(),
      to: from.toISOString()
    }));
  }

  function levelName(id) {
    const l = D.levelByEmittedId[id];
    return l ? l.name : '(bilinmiyor: ' + id + ')';
  }

  // ---------------------------------------------------------------------------
  // 1) YÖNETİM ÖZETİ
  // ---------------------------------------------------------------------------

  function dashboard(app) {
    const evs = scoped(app);
    const prev = previousPeriod(app);
    const root = h('div');

    root.appendChild(h('div', { class: 'page-head' }, [
      h('div', { class: 'page-head__text' }, [
        h('h1', { text: 'Yönetim Özeti' }),
        h('p', { text: 'Thundershock iş güvenliği eğitimi — kurum geneli durum. ' +
                       'Tüm kartlar aktif filtre kapsamına göre hesaplanır.' })
      ]),
      h('div', { class: 'page-head__actions' }, [
        h('a', { class: 'btn btn--ghost', href: '#/manager/risks', text: 'Risk Analizi' }),
        h('a', { class: 'btn btn--primary', href: '#/manager/employees', text: 'Çalışanlar' })
      ])
    ]));

    if (!evs.length) {
      root.appendChild(U.emptyState({
        icon: 'search', title: 'Seçilen kapsamda hiç kayıt yok',
        what: 'Aktif tarih aralığı ve filtrelerle eşleşen event bulunamadı.',
        why: 'Aralık çok dar olabilir ya da seçilen senaryo bu dönemde hiç oynanmamış olabilir.',
        action: 'Tarih aralığını genişletin veya senaryo filtresini "Tümü" yapın.'
      }));
      return root;
    }

    const acc = K.accuracy(evs);
    const prevAcc = prev ? K.accuracy(prev) : null;
    const active = K.activeEmployees(evs);
    const prevActive = prev ? K.activeEmployees(prev) : null;
    const runs = K.deriveRuns(evs);
    const mist = K.mistakeCount(evs);
    const prevMist = prev ? K.mistakeCount(prev) : null;
    const mRate = K.mistakeRate(evs);
    const avgT = K.avgQuizTime(evs);

    // -- Üst KPI'lar: en önemli 6 --------------------------------------------
    root.appendChild(h('div', { class: 'grid grid--kpi' }, [
      U.kpiCard({
        label: 'Aktif Çalışan', accent: true, value: String(active),
        help: 'Seçilen aralıkta en az bir geçerli event gönderen benzersiz employeeId sayısı. ' +
              '"Katılım oranı" DEĞİLDİR — atanmış çalışan (roster) verisi projede yok.',
        sub: 'benzersiz employeeId',
        delta: prevActive !== null
          ? U.deltaBadge(active - prevActive, { fmt: function (v) { return (v > 0 ? '+' : '') + v; } })
          : null
      }),
      U.kpiCard({
        label: 'Senaryo Denemesi', value: String(runs.length),
        help: 'LevelStarted event sayısı. Denemeler LevelStarted → LevelCompleted çiftinden ' +
              'türetilir; şemada sessionId yoktur.',
        sub: runs.filter(function (r) { return r.completed; }).length + ' tamamlandı'
      }),
      acc.ok ? U.kpiCard({
        label: 'Genel Doğruluk', value: K.pct(acc, 0),
        help: 'Doğru QuizAnswered / toplam QuizAnswered. Yalnızca bilgi soruları.',
        sub: acc.num + '/' + acc.den + ' soru',
        delta: prevAcc && prevAcc.ok
          ? U.deltaBadge((acc.value - prevAcc.value) * 100,
              { fmt: function (v) { return (v > 0 ? '+' : '') + v.toFixed(0) + ' puan'; } })
          : null
      }) : U.kpiCard({ label: 'Genel Doğruluk', unavailable: 'Hesaplanamıyor', reason: acc.reason }),
      avgT.ok ? U.kpiCard({
        label: 'Ortalama Soru Süresi', value: K.fmtDuration(avgT.value),
        help: 'QuizAnswered.timeSpent ortalaması; eksik değerler hesaba katılmaz.',
        sub: avgT.n + ' geçerli' + (avgT.dropped ? ' · ' + avgT.dropped + ' eksik' : '')
      }) : U.kpiCard({ label: 'Ortalama Soru Süresi', unavailable: 'Veri yok', reason: avgT.reason }),
      U.kpiCard({
        label: 'Toplam Hata', value: String(mist),
        help: 'MistakeRecorded adedi. Oran değil, sayıdır.',
        sub: mRate.ok ? 'oran: ' + K.pct(mRate, 1) + ' (hata/adım)' : mRate.reason,
        delta: prevMist !== null
          ? U.deltaBadge(mist - prevMist, { invert: true,
              fmt: function (v) { return (v > 0 ? '+' : '') + v; } })
          : null
      }),
      U.kpiCard({
        label: 'Kritik Hata Oranı', unavailable: 'Sınıflandırılamıyor',
        reason: 'Severity ölçeği tanımsız',
        help: K.criticalMistakeRate().reason
      })
    ]));

    // -- "Hızlı cevaplar" bloğu ----------------------------------------------
    const factors = K.actionFactors(evs);
    const withRate = factors.filter(function (f) { return f.mistakeRate.ok && f.mistakeRate.num > 0; });
    withRate.sort(function (a, b) { return b.mistakeRate.value - a.mistakeRate.value; });

    const perLevel = D.content.levels.map(function (lvl) {
      const le = evs.filter(function (e) { return K.levelIdOf(e) === lvl.emittedLevelId; });
      return { lvl: lvl, events: le, mistakes: K.mistakeCount(le),
               acc: K.accuracy(le), rate: K.mistakeRate(le),
               runs: le.filter(function (e) { return e.eventType === 'LevelStarted'; }).length };
    }).filter(function (r) { return r.events.length > 0; });

    const worstLevel = perLevel.slice().sort(function (a, b) {
      const ar = a.rate.ok ? a.rate.value : -1, br = b.rate.ok ? b.rate.value : -1;
      return br - ar;
    })[0];

    root.appendChild(h('div', { class: 'grid grid--main mt-5' }, [
      h('div', { class: 'stack' }, [
        U.card('Performans Trendleri', {
          desc: 'Farklı ölçekteki metrikler ayrı sekmelerde — tek eksende karıştırılmaz.',
          help: 'Kovalar 7 günlük. Değer hesaplanamadığında nokta çizilmez.'
        }, U.tabbedChart([
          { id: 'acc', label: 'Doğruluk', render: function () {
            return U.lineChart(K.timeSeries(evs, 'accuracy', 7), {
              color: U.css('--cat-1'), yMin: 0, yMax: 100,
              fmt: function (v) { return v.toFixed(0) + '% doğruluk'; },
              fmtAxis: function (v) { return Math.round(v) + '%'; },
              summary: 'Haftalık doğru cevap oranı (%). Kaynak: QuizAnswered.isCorrect.'
            });
          } },
          { id: 'users', label: 'Aktif Çalışan', render: function () {
            return U.lineChart(K.timeSeries(evs, 'activeEmployees', 7), {
              color: U.css('--cat-5'), yMin: 0,
              fmt: function (v) { return v + ' çalışan'; },
              summary: 'Haftada en az bir event gönderen benzersiz çalışan sayısı.'
            });
          } },
          { id: 'runs', label: 'Deneme', render: function () {
            return U.lineChart(K.timeSeries(evs, 'runs', 7), {
              color: U.css('--cat-3'), yMin: 0,
              fmt: function (v) { return v + ' deneme'; },
              summary: 'Haftalık LevelStarted sayısı.'
            });
          } },
          { id: 'time', label: 'Ortalama Süre', render: function () {
            return U.lineChart(K.timeSeries(evs, 'avgTime', 7), {
              color: U.css('--cat-2'), yMin: 0,
              fmt: function (v) { return K.fmtDuration(v); },
              fmtAxis: function (v) { return Math.round(v) + 's'; },
              summary: 'Soru başına ortalama süre (saniye).'
            });
          } },
          { id: 'mrate', label: 'Hata Oranı', render: function () {
            return U.lineChart(K.timeSeries(evs, 'mistakeRate', 7), {
              color: U.css('--cat-4'), yMin: 0,
              fmt: function (v) { return v.toFixed(1) + '% (hata/adım)'; },
              fmtAxis: function (v) { return v.toFixed(0) + '%'; },
              summary: 'Normalize hata oranı: MistakeRecorded / ActionCompleted.'
            });
          } }
        ])),
        U.card('En Riskli Adımlar', {
          desc: 'Normalize orana göre sıralanır (hata / adım denemesi) — ham sayıya göre değil.',
          aside: h('a', { class: 'btn btn--ghost btn--sm', href: '#/manager/risks',
                          text: 'Tümünü gör' })
        }, U.barList(withRate.slice(0, 6).map(function (f) {
          return {
            label: f.actionName,
            value: f.mistakeRate.value * 100,
            display: K.rateLabel(f.mistakeRate, 0),
            color: U.css('--cat-4'),
            sub: f.levelName + ' › ' + f.sequenceName + ' · ' +
                 f.mistakeRate.num + '/' + f.mistakeRate.den + ' · ' +
                 f.employeeCount + ' çalışan',
            onClick: function () { openActionDrill(app, f.actionId); }
          };
        }), { emptyTitle: 'Normalize edilebilir riskli adım yok',
              emptyWhat: 'Hata kaydı var ama payda (ActionCompleted) oluşmadığı için oran üretilemedi.' }))
      ]),

      h('div', { class: 'stack' }, [
        U.card('Hızlı Cevaplar', { desc: 'Kapsam içindeki durumun özeti.' }, [
          h('dl', { class: 'deflist' }, [
            h('dt', { text: 'En çok hata üreten senaryo' }),
            h('dd', null, worstLevel
              ? h('button', { class: 'rowlink', type: 'button', text: worstLevel.lvl.name,
                  onClick: function () {
                    location.hash = '#/manager/scenario/' + encodeURIComponent(worstLevel.lvl.emittedLevelId); } })
              : h('span', { text: '—' })),
            h('dt', { text: 'En riskli adım' }),
            h('dd', null, withRate.length
              ? h('button', { class: 'rowlink', type: 'button', text: withRate[0].actionName,
                  onClick: function () { openActionDrill(app, withRate[0].actionId); } })
              : h('span', { text: '—' })),
            h('dt', { text: 'Tamamlanmayan oturum' }),
            h('dd', { text: runs.filter(function (r) { return !r.completed; }).length + ' / ' + runs.length }),
            h('dt', { text: 'Hata türleri' }),
            h('dd', { text: (function () {
              const t = K.mistakesByType(evs);
              return Object.keys(t).map(function (k) {
                return (U.TYPE_LABEL[k] || k) + ': ' + t[k]; }).join(' · ') || '—';
            })() })
          ])
        ]),
        reviewQueueCard(app, evs),
        dataQualityCard(evs)
      ])
    ]));

    // -- Senaryo karşılaştırması ---------------------------------------------
    root.appendChild(h('div', { class: 'mt-5' },
      U.card('Senaryo Karşılaştırması', {
        desc: 'Her senaryo için doğruluk ve normalize hata oranı yan yana. ' +
              'Tek bir "zorluk skoru" üretilmez — ağırlıklar bir iş kuralıdır ve tanımlı değildir.'
      }, [
        h('div', { class: 'tablewrap' }, h('table', { class: 'data' }, [
          h('thead', null, h('tr', null, [
            h('th', { text: 'Senaryo' }), h('th', { class: 'num', text: 'Deneme' }),
            h('th', { class: 'num', text: 'Doğruluk' }), h('th', { class: 'num', text: 'Hata' }),
            h('th', { class: 'num', text: 'Hata oranı' }), h('th', { text: '' })
          ])),
          h('tbody', null, perLevel.map(function (r) {
            return h('tr', null, [
              h('td', null, h('button', { class: 'rowlink', type: 'button', text: r.lvl.name,
                onClick: function () {
                  location.hash = '#/manager/scenario/' + encodeURIComponent(r.lvl.emittedLevelId); } })),
              h('td', { class: 'num', text: String(r.runs) }),
              h('td', { class: 'num', text: r.acc.ok ? K.pct(r.acc, 0) : '—' }),
              h('td', { class: 'num', text: String(r.mistakes) }),
              h('td', { class: 'num', text: r.rate.ok ? K.pct(r.rate, 1) : 'payda yok' }),
              h('td', null, r.lvl.dataWarning
                ? U.badge('warn', 'levelId sorunlu', 'warn') : U.badge('ok', 'Sağlıklı', 'check'))
            ]);
          }))
        ]))
      ])));

    return root;
  }

  // -- İncelenmesi gerekenler -------------------------------------------------

  function reviewQueueCard(app, evs) {
    const byEmp = {};
    evs.forEach(function (e) {
      byEmp[e.employeeId] = byEmp[e.employeeId] || [];
      byEmp[e.employeeId].push(e);
    });

    const flags = [];
    Object.keys(byEmp).forEach(function (id) {
      const own = byEmp[id];
      const acc = K.accuracy(own);
      const runs = K.deriveRuns(own, id);
      const mist = K.mistakeCount(own);
      const emp = D.employeeById[id];
      const reasons = [];

      if (acc.ok && acc.den >= 4 && acc.value < 0.6) {
        reasons.push('Doğruluk %' + (acc.value * 100).toFixed(0) + ' (' + acc.den + ' soruda)');
      }
      const incomplete = runs.filter(function (r) { return !r.completed; }).length;
      if (incomplete > 0) reasons.push(incomplete + ' oturum tamamlanmadı');

      // Aynı hatayı tekrar edenler
      const rep = {};
      own.filter(function (e) { return e.eventType === 'MistakeRecorded'; })
        .forEach(function (e) {
          const k = e.payload.actionId + '::' + e.payload.mistakeType;
          rep[k] = (rep[k] || 0) + 1;
        });
      const repeated = Object.keys(rep).filter(function (k) { return rep[k] >= 3; });
      if (repeated.length) reasons.push(repeated.length + ' adımda 3+ kez aynı hata');

      if (reasons.length) {
        flags.push({ id: id, name: emp ? emp.name : id, reasons: reasons, mistakes: mist });
      }
    });

    flags.sort(function (a, b) { return b.reasons.length - a.reasons.length || b.mistakes - a.mistakes; });

    if (!flags.length) {
      return U.card('İncelenmesi Gerekenler', {}, U.emptyState({
        inline: true, icon: 'check', title: 'İşaretlenen çalışan yok',
        what: 'Bu kapsamda kural eşiklerini aşan çalışan bulunamadı.',
        action: 'Tarih aralığını genişleterek geçmişe bakabilirsiniz.'
      }));
    }

    return U.card('İncelenmesi Gerekenler', {
      desc: 'Eşikler prototip önerisidir; onaylanmış iş kuralı değildir.',
      aside: U.badge('warn', flags.length + ' çalışan', 'warn')
    }, h('div', { class: 'stack' }, flags.slice(0, 5).map(function (f) {
      return h('div', { style: 'padding:10px;background:rgba(0,0,0,.2);border-radius:8px' }, [
        h('button', { class: 'rowlink', type: 'button', text: f.name,
          onClick: function () {
            location.hash = '#/manager/employee/' + encodeURIComponent(f.id); } }),
        h('ul', { style: 'margin:6px 0 0;padding-left:1.1rem;font-size:.76rem;color:var(--ink-2)' },
          f.reasons.map(function (r) { return h('li', { text: r }); }))
      ]);
    })));
  }

  // -- Veri kalitesi kartı — auditten doğan gerçek bir ürün özelliği ----------

  function dataQualityCard(evs) {
    const issues = [];

    // 1) levelId tutarsızlıkları
    const seen = {};
    evs.forEach(function (e) {
      const l = e.payload && e.payload.levelId;
      if (l) seen[l] = (seen[l] || 0) + 1;
    });
    D.content.levels.forEach(function (l) {
      if (l.dataWarning && seen[l.emittedLevelId]) {
        issues.push({ tone: 'warn', text: l.name + ': ' + l.dataWarning });
      }
    });

    // 2) Katalogda bulunmayan actionId
    const unknown = {};
    evs.forEach(function (e) {
      const a = e.payload && e.payload.actionId;
      if (a && !D.actionIndex[a]) unknown[a] = (unknown[a] || 0) + 1;
    });
    const unknownCount = Object.keys(unknown).length;
    if (unknownCount) {
      issues.push({ tone: 'warn', text: unknownCount + ' farklı actionId içerik kataloğunda ' +
        'bulunamadı — bu hatalar senaryodaki yerine bağlanamıyor.' });
    }

    // 3) Eksik timeSpent
    const q = evs.filter(function (e) { return e.eventType === 'QuizAnswered'; });
    const missingT = q.filter(function (e) { return !K.isValidDuration(e.payload.timeSpent); }).length;
    if (missingT) {
      issues.push({ tone: 'warn', text: missingT + ' / ' + q.length +
        ' QuizAnswered kaydında timeSpent eksik veya geçersiz — süre ortalamalarından çıkarıldı.' });
    }

    // 4) Severity
    const sev = K.mistakesBySeverity(evs);
    const sevKeys = Object.keys(sev);
    if (sevKeys.length) {
      issues.push({ tone: 'info', text: 'severity yalnızca [' + sevKeys.join(', ') +
        '] değerini alıyor — ölçek tanımlanmadan kritik sınıflandırma yapılamaz.' });
    }

    // 5) Survey verisi
    issues.push({ tone: 'info', text: 'Anket ve fotoğraf sonuçları hiçbir event ile gönderilmiyor ' +
      '(SurveyResultTracker.cs verileri yalnızca bellekte tutuyor) — Survey adımları için ' +
      'yalnızca ActionCompleted görülebiliyor.' });

    return U.card('Veri Kalitesi', {
      desc: 'Telemetri şemasında portalın güvenilirliğini etkileyen noktalar.',
      aside: U.badge(issues.some(function (i) { return i.tone === 'warn'; }) ? 'warn' : 'info',
        issues.length + ' not', 'info')
    }, h('div', { class: 'stack' }, issues.map(function (i) {
      return U.notice(i.tone, U.esc(i.text));
    })));
  }

  // ---------------------------------------------------------------------------
  // 2) ÇALIŞANLAR
  // ---------------------------------------------------------------------------

  function employees(app) {
    const evs = scoped(app);
    const prev = previousPeriod(app);
    const root = h('div');

    root.appendChild(h('div', { class: 'page-head' }, [
      h('div', { class: 'page-head__text' }, [
        h('h1', { text: 'Çalışanlar' }),
        h('p', { text: 'Filtre kapsamındaki tüm çalışanlar. Satıra tıklayarak detaya inebilirsiniz.' })
      ]),
      h('div', { class: 'page-head__actions' }, [
        h('button', {
          class: 'btn btn--ghost', type: 'button',
          onClick: function () {
            U.openModal('CSV Dışa Aktarma', [
              h('p', { text: 'Bu prototipte dışa aktarma uygulanmadı.' }),
              U.notice('warn',
                'Gerçek üründe CSV/XLSX üretimi <b>backend</b> tarafında yapılmalı ve ' +
                'dışa aktarma yetkisi (export authorization) ile audit log kaydı ' +
                'zorunlu olmalıdır. Kişisel veri içeren dışa aktarımlarda maskeleme ' +
                'politikası uygulanmalıdır.')
            ]);
          }
        }, [U.icon('download', 15), 'CSV İndir', U.badge('proto', 'Prototip')])
      ])
    ]));

    const rows = D.employees.map(function (emp) {
      const own = evs.filter(function (e) { return e.employeeId === emp.id; });
      const ownPrev = prev ? prev.filter(function (e) { return e.employeeId === emp.id; }) : [];
      const runs = K.deriveRuns(own, emp.id);
      const acc = K.accuracy(own);
      const accPrev = ownPrev.length ? K.accuracy(ownPrev) : null;
      const t = K.avgQuizTime(own);
      const levels = {};
      runs.forEach(function (r) { levels[r.levelId] = true; });

      return {
        id: emp.id, name: emp.name, role: emp.role,
        _last: own.length ? own[own.length - 1].clientTimestamp : null,
        scenarios: Object.keys(levels).length,
        _acc: acc.ok ? acc.value : null,
        accOk: acc.ok, accDen: acc.den,
        _time: t.ok ? t.value : null,
        mistakes: K.mistakeCount(own),
        _delta: (acc.ok && accPrev && accPrev.ok) ? (acc.value - accPrev.value) * 100 : null,
        eventCount: own.length,
        incomplete: runs.filter(function (r) { return !r.completed; }).length
      };
    });

    root.appendChild(U.card(null, {}, U.dataTable({
      caption: 'Temsili veri — gerçek çalışan adı veya kişisel veri içermez.',
      searchKeys: ['name', 'id', 'role'],
      searchLabel: 'Çalışan ara (ad, ID veya rol)',
      searchPlaceholder: 'ör. 1044 veya inspector',
      columns: [
        { key: 'id', label: 'Employee ID',
          render: function (r) {
            return h('button', { class: 'rowlink', type: 'button', text: r.id,
              onClick: function () {
                location.hash = '#/manager/employee/' + encodeURIComponent(r.id); } });
          } },
        { key: 'name', label: 'Çalışan' },
        { key: 'role', label: 'Rol',
          render: function (r) { return U.badge('neutral', r.role); } },
        { key: 'last', label: 'Son aktivite', value: function (r) { return r._last; },
          render: function (r) { return r._last ? K.relativeDays(r._last) : '—'; } },
        { key: 'scenarios', label: 'Senaryo', num: true },
        { key: 'accuracy', label: 'Doğruluk', num: true, value: function (r) { return r._acc; },
          render: function (r) {
            if (!r.accOk) return h('span', { style: 'color:var(--ink-3)', text: 'oran yok' });
            return (r._acc * 100).toFixed(0) + '%';
          } },
        { key: 'time', label: 'Ort. süre', num: true, value: function (r) { return r._time; },
          render: function (r) { return r._time !== null ? K.fmtDuration(r._time) : '—'; } },
        { key: 'mistakes', label: 'Hata', num: true },
        { key: 'delta', label: 'Değişim', num: true, value: function (r) { return r._delta; },
          render: function (r) {
            return U.deltaBadge(r._delta, {
              fmt: function (v) { return (v > 0 ? '+' : '') + v.toFixed(0) + ' p'; } });
          } },
        { key: 'status', label: 'Durum', sortable: false,
          render: function (r) {
            if (r.eventCount === 0) return U.badge('neutral', 'Veri yok', 'empty');
            if (r.incomplete > 0) return U.badge('warn', r.incomplete + ' yarım oturum', 'warn');
            if (r.accOk && r._acc < 0.6) return U.badge('bad', 'İnceleme öner.', 'alert');
            return U.badge('ok', 'Normal', 'check');
          } },
        { key: 'detail', label: '', sortable: false,
          render: function (r) {
            return h('button', { class: 'btn btn--ghost btn--sm', type: 'button', text: 'Detay',
              onClick: function () {
                location.hash = '#/manager/employee/' + encodeURIComponent(r.id); } });
          } }
      ],
      rows: rows, defaultSort: 'mistakes', defaultDir: 'desc', pageSize: 10,
      emptyTitle: 'Çalışan kaydı yok'
    })));

    root.appendChild(h('div', { class: 'mt-4' }, U.notice('info',
      '<b>Ekip / departman / lokasyon / vardiya sütunları yok.</b> Bu boyutlar ne event ' +
      'şemasında ne de PlayFab whitelist kaydında bulunuyor. Kolon uydurmak yerine ' +
      'gösterilmiyor — bkz. DATA_MAPPING.md "Ek veri kaynağı gerekir".')));

    return root;
  }

  // ---------------------------------------------------------------------------
  // 3) YÖNETİCİ — ÇALIŞAN DETAYI (çalışan portalının kopyası DEĞİL)
  // ---------------------------------------------------------------------------

  function employeeDetail(app, empId) {
    const emp = D.employeeById[empId];
    const root = h('div');

    if (!emp) {
      return U.emptyState({
        icon: 'warn', tone: 'warn', title: 'Çalışan bulunamadı',
        what: '"' + empId + '" kimliğine sahip kayıt yok.',
        action: 'Çalışanlar listesine dönün.',
        cta: h('a', { class: 'btn btn--ghost', href: '#/manager/employees', text: 'Çalışanlar' })
      });
    }

    const evs = scoped(app);
    const own = evs.filter(function (e) { return e.employeeId === empId; });
    const runs = K.deriveRuns(own, empId);
    const acc = K.accuracy(own);

    // Kurum ortalaması — YETERLİ VERİ YOKSA GÖSTERİLMEZ
    const others = evs.filter(function (e) { return e.employeeId !== empId; });
    const otherEmployees = K.activeEmployees(others);
    const orgAcc = K.accuracy(others);
    const orgShown = otherEmployees >= 3 && orgAcc.ok && orgAcc.den >= 20;

    root.appendChild(h('div', { class: 'page-head' }, [
      h('div', { class: 'page-head__text' }, [
        h('h1', { text: emp.name }),
        h('p', null, [
          h('code', { text: emp.id }), ' · rol: ' + emp.role + ' · ' +
          own.length + ' event · son aktivite: ' +
          (own.length ? K.relativeDays(own[own.length - 1].clientTimestamp) : '—')
        ])
      ]),
      h('div', { class: 'page-head__actions' }, [
        h('a', { class: 'btn btn--ghost', href: '#/manager/employees', text: '‹ Çalışanlar' })
      ])
    ]));

    if (!own.length) {
      root.appendChild(U.emptyState({
        icon: 'empty', title: 'Bu çalışanın kapsam içinde verisi yok',
        what: 'Seçilen tarih aralığı ve filtrelerde bu çalışana ait event bulunamadı.',
        why: 'Çalışan bu dönemde hiç oynamamış olabilir ya da hiç başlamamış olabilir.',
        action: 'Tarih aralığını genişletin.'
      }));
      return root;
    }

    // -- Konumlandırma: kurum içindeki yeri --------------------------------
    root.appendChild(h('div', { class: 'grid grid--kpi' }, [
      acc.ok ? U.kpiCard({
        label: 'Doğruluk', accent: true, value: K.pct(acc, 0),
        sub: acc.num + '/' + acc.den + ' soru',
        help: 'Bu çalışanın QuizAnswered doğruluk oranı.'
      }) : U.kpiCard({ label: 'Doğruluk', unavailable: 'Hesaplanamıyor', reason: acc.reason }),
      orgShown ? U.kpiCard({
        label: 'Kurum Ortalamasına Göre',
        value: ((acc.value - orgAcc.value) * 100 > 0 ? '+' : '') +
               ((acc.value - orgAcc.value) * 100).toFixed(0) + ' puan',
        sub: 'kurum: ' + K.pct(orgAcc, 0) + ' (' + otherEmployees + ' çalışan)',
        help: 'Karşılaştırma yalnızca en az 3 diğer çalışan ve 20 soru varsa gösterilir.'
      }) : U.kpiCard({
        label: 'Kurum Ortalamasına Göre', unavailable: 'Yeterli veri yok',
        reason: otherEmployees + ' diğer çalışan · ' + (orgAcc.ok ? orgAcc.den : 0) + ' soru',
        help: 'Anlamlı bir kurum ortalaması için en az 3 diğer çalışan ve 20 QuizAnswered kaydı gerekir.'
      }),
      U.kpiCard({ label: 'Deneme', value: String(runs.length),
        sub: runs.filter(function (r) { return r.completed; }).length + ' tamamlandı' }),
      U.kpiCard({ label: 'Toplam Hata', value: String(K.mistakeCount(own)),
        sub: Object.keys(K.mistakesByType(own)).map(function (t) {
          return (U.TYPE_LABEL[t] || t) + ': ' + K.mistakesByType(own)[t]; }).join(' · ') || '—' }),
      U.kpiCard({ label: 'Yarım Kalan Oturum',
        value: String(runs.filter(function (r) { return !r.completed; }).length),
        sub: 'LevelCompleted gelmedi',
        help: 'Oturumun tamamlandığı yalnızca LevelCompleted event\'i ile bilinir.' })
    ]));

    // -- Level bazlı KPI + action bazlı hata --------------------------------
    const perLevel = D.content.levels.map(function (lvl) {
      const le = own.filter(function (e) { return K.levelIdOf(e) === lvl.emittedLevelId; });
      if (!le.length) return null;
      const a = K.accuracy(le);
      const lr = runs.filter(function (r) { return r.levelId === lvl.emittedLevelId; });
      return { lvl: lvl, acc: a, mistakes: K.mistakeCount(le), runs: lr,
               rate: K.mistakeRate(le) };
    }).filter(Boolean);

    root.appendChild(h('div', { class: 'grid grid--main mt-5' }, [
      h('div', { class: 'stack' }, [
        U.card('Level Bazlı KPI', { desc: 'Yalnızca oynanan senaryolar.' },
          h('div', { class: 'tablewrap' }, h('table', { class: 'data' }, [
            h('thead', null, h('tr', null, [
              h('th', { text: 'Senaryo' }), h('th', { class: 'num', text: 'Deneme' }),
              h('th', { class: 'num', text: 'Doğruluk' }), h('th', { class: 'num', text: 'Hata' }),
              h('th', { class: 'num', text: 'Hata oranı' }), h('th', { text: 'Durum' })
            ])),
            h('tbody', null, perLevel.map(function (r) {
              const st = K.scenarioStatus(runs, r.lvl.emittedLevelId);
              return h('tr', null, [
                h('td', null, h('button', { class: 'rowlink', type: 'button', text: r.lvl.name,
                  onClick: function () {
                    location.hash = '#/manager/scenario/' + encodeURIComponent(r.lvl.emittedLevelId); } })),
                h('td', { class: 'num', text: String(r.runs.length) }),
                h('td', { class: 'num', text: r.acc.ok ? K.pct(r.acc, 0) : '—' }),
                h('td', { class: 'num', text: String(r.mistakes) }),
                h('td', { class: 'num', text: r.rate.ok ? K.pct(r.rate, 1) : 'payda yok' }),
                h('td', null, window.TS_EMPLOYEE.statusBadge(st))
              ]);
            }))
          ]))),
        actionMistakeCard(app, own),
        runHistoryCard(app, runs)
      ]),
      h('div', { class: 'stack' }, [
        repeatedMistakeCard(own),
        recommendationCard(own, runs, acc, orgShown ? orgAcc : null)
      ])
    ]));

    return root;
  }

  function actionMistakeCard(app, own) {
    const factors = K.actionFactors(own).filter(function (f) { return f.mistakes > 0; });
    factors.sort(function (a, b) { return b.mistakes - a.mistakes; });

    return U.card('Adım Bazlı Hata', {
      desc: 'Bu çalışanın hata yaptığı adımlar. Tıklayınca event detayı açılır.'
    }, U.barList(factors.slice(0, 8).map(function (f) {
      return {
        label: f.actionName, value: f.mistakes, display: f.mistakes + ' hata',
        color: U.css('--cat-4'),
        sub: f.levelName + ' › ' + f.sequenceName +
             (f.mistakeRate.ok ? ' · ' + K.rateLabel(f.mistakeRate, 0) : ' · payda yok'),
        onClick: function () { openActionDrill(app, f.actionId, own); }
      };
    }), { emptyTitle: 'Bu çalışanın hata kaydı yok',
          emptyWhat: 'Kapsam içinde MistakeRecorded event\'i bulunmuyor.' }));
  }

  function repeatedMistakeCard(own) {
    const rep = {};
    own.filter(function (e) { return e.eventType === 'MistakeRecorded'; })
      .forEach(function (e) {
        const k = e.payload.actionId + '::' + e.payload.mistakeType;
        rep[k] = rep[k] || { count: 0, last: null };
        rep[k].count += 1;
        if (!rep[k].last || e.clientTimestamp > rep[k].last) rep[k].last = e.clientTimestamp;
      });
    const rows = Object.keys(rep).filter(function (k) { return rep[k].count >= 2; })
      .map(function (k) { return { key: k, count: rep[k].count, last: rep[k].last }; })
      .sort(function (a, b) { return b.count - a.count; });

    if (!rows.length) {
      return U.card('Tekrar Eden Hatalar', {}, U.emptyState({
        inline: true, icon: 'check', title: 'Tekrar eden hata yok',
        what: 'Aynı adım + hata türü kombinasyonu iki veya daha fazla kez görülmedi.'
      }));
    }

    return U.card('Tekrar Eden Hatalar', {
      desc: 'Aynı adımda aynı hata türünün 2+ kez tekrarlanması.',
      aside: U.badge('warn', rows.length + ' adım', 'repeat')
    }, h('div', { class: 'stack' }, rows.map(function (r) {
      const parts = r.key.split('::');
      const idx = D.actionIndex[parts[0]];
      return h('div', { style: 'padding:10px;background:var(--warn-bg);border:1px solid rgba(232,163,61,.3);border-radius:8px' }, [
        h('div', { style: 'display:flex;gap:8px;align-items:center;margin-bottom:4px' }, [
          U.badge('warn', r.count + ' kez', 'repeat'),
          h('span', { style: 'font-size:.72rem;color:var(--ink-2);margin-left:auto',
                      text: K.relativeDays(r.last) })
        ]),
        h('div', { style: 'font-size:.83rem;font-weight:600',
                   text: idx ? idx.action.name : parts[0] }),
        h('div', { style: 'font-size:.72rem;color:var(--ink-2)',
                   text: (U.TYPE_LABEL[parts[1]] || parts[1]) +
                         (idx ? ' · ' + idx.level.name + ' › ' + idx.sequence.name : '') })
      ]);
    })));
  }

  function recommendationCard(own, runs, acc, orgAcc) {
    const recs = [];

    const factors = K.actionFactors(own).filter(function (f) { return f.mistakes >= 2; });
    factors.sort(function (a, b) { return b.mistakes - a.mistakes; });
    if (factors.length) {
      recs.push('"' + factors[0].sequenceName + '" görev grubu tekrar oynatılabilir — ' +
        '"' + factors[0].actionName + '" adımında ' + factors[0].mistakes + ' hata kaydı var.');
    }

    const incomplete = runs.filter(function (r) { return !r.completed; });
    if (incomplete.length) {
      recs.push(incomplete.length + ' oturum LevelCompleted almadan sonlanmış — ' +
        'cihaz/bağlantı sorunu mu yoksa eğitimin terk edilmesi mi olduğu kontrol edilmeli.');
    }

    if (acc.ok && orgAcc && orgAcc.ok && acc.value < orgAcc.value - 0.15) {
      recs.push('Doğruluk kurum ortalamasının ' +
        ((orgAcc.value - acc.value) * 100).toFixed(0) + ' puan altında — birebir tekrar önerilir.');
    }

    const slow = K.avgQuizTime(own);
    if (slow.dropped > 0) {
      recs.push(slow.dropped + ' kayıtta timeSpent eksik; süre değerlendirmesi eksik veriye dayanıyor.');
    }

    return U.card('Önerilen İnceleme Alanları', {
      desc: 'Yalnızca veriden çıkarılabilen gözlemler — neden üretilmez.'
    }, recs.length
      ? h('ul', { style: 'margin:0;padding-left:1.15rem;font-size:.84rem;color:var(--ink-2);line-height:1.75' },
          recs.map(function (r) { return h('li', { text: r }); }))
      : U.emptyState({ inline: true, icon: 'check', title: 'Öne çıkan inceleme alanı yok',
          what: 'Bu çalışan için eşikleri aşan bir gözlem bulunmuyor.' }));
  }

  function runHistoryCard(app, runs) {
    if (!runs.length) return U.card('Deneme Geçmişi', {}, U.emptyState({
      inline: true, icon: 'empty', title: 'Deneme kaydı yok' }));

    return U.card('Deneme Geçmişi', {
      desc: 'Türetilmiş denemeler (LevelStarted → LevelCompleted).'
    }, U.dataTable({
      columns: [
        { key: 'date', label: 'Tarih', value: function (r) { return r._ts; },
          render: function (r) { return K.fmtDateTime(r._ts); } },
        { key: 'level', label: 'Senaryo' },
        { key: 'attempt', label: 'Deneme', num: true },
        { key: 'accuracy', label: 'Doğruluk', num: true, value: function (r) { return r._acc; },
          render: function (r) { return r.accuracy; } },
        { key: 'mistakes', label: 'Hata', num: true },
        { key: 'time', label: 'Süre', num: true, value: function (r) { return r._time; },
          render: function (r) { return K.fmtDuration(r._time); } },
        { key: 'score', label: 'Skor', num: true },
        { key: 'status', label: 'Durum', sortable: false,
          render: function (r) {
            return r.completed ? U.badge('ok', 'Tamamlandı', 'check')
                               : U.badge('warn', 'Yarım', 'warn'); } }
      ],
      rows: runs.slice().reverse().map(function (r) {
        const m = K.runMetrics(r);
        return {
          _ts: r.startedAt, level: levelName(r.levelId), attempt: r.attemptNo,
          _acc: m.accuracy.ok ? m.accuracy.value : null,
          accuracy: m.accuracy.ok ? K.pct(m.accuracy, 0) : '—',
          mistakes: m.mistakes, _time: m.totalTime,
          score: r.score !== null ? r.score : null, completed: r.completed
        };
      }),
      defaultSort: 'date', defaultDir: 'desc', pageSize: 8
    }));
  }

  // ---------------------------------------------------------------------------
  // 4) SENARYOLAR (yönetici)
  // ---------------------------------------------------------------------------

  function scenarios(app) {
    const evs = scoped(app);
    const root = h('div');

    root.appendChild(h('div', { class: 'page-head' }, [
      h('div', { class: 'page-head__text' }, [
        h('h1', { text: 'Senaryolar' }),
        h('p', { text: 'Senaryo ve adım zorluğu — tek bir skor yerine faktörler yan yana.' })
      ])
    ]));

    root.appendChild(U.notice('info',
      '<b>Neden "Zorluk Skoru" yok?</b> Bir zorluk skoru; yanlış cevap oranı, deneme sayısı, ' +
      'süre ve hata oranını ağırlıklandırmayı gerektirir. Bu ağırlıklar bir iş kuralıdır ve ' +
      'proje belgelerinde tanımlı değildir. Tek skor, düşük hacimli ama kritik bir adımı ' +
      'yüksek hacimli zararsız bir adımın içinde gizleyebilir — bu yüzden faktörler ayrı ayrı gösterilir.'));

    const factors = K.actionFactors(evs);

    root.appendChild(h('div', { class: 'mt-4' }, U.card('Adım Zorluk Faktörleri', {
      desc: 'Her sütun bağımsız bir faktördür. Sıralamayı değiştirerek farklı risk ' +
            'tanımlarını deneyebilirsiniz.'
    }, U.dataTable({
      searchKeys: ['actionName', 'sequenceName', 'levelName', 'actionId'],
      searchLabel: 'Adım ara',
      searchPlaceholder: 'ör. klemens veya SCADA',
      columns: [
        { key: 'actionName', label: 'Adım',
          render: function (r) {
            return h('button', { class: 'rowlink', type: 'button', text: r.actionName,
              onClick: function () { openActionDrill(app, r.actionId); } });
          } },
        { key: 'levelName', label: 'Senaryo' },
        { key: 'sequenceName', label: 'Görev grubu' },
        { key: 'type', label: 'Tip', render: function (r) {
            return r.type ? U.badge('neutral', r.type) : '—'; } },
        { key: 'wrongRate', label: 'Yanlış cevap', num: true,
          value: function (r) { return r._wrong; },
          render: function (r) { return r._wrong !== null
            ? (r._wrong * 100).toFixed(0) + '%'
            : h('span', { style: 'color:var(--ink-3)', text: 'quiz değil' }); } },
        { key: 'avgAttempts', label: 'Ort. deneme', num: true,
          value: function (r) { return r._att; },
          render: function (r) { return r._att !== null ? r._att.toFixed(2) : '—'; } },
        { key: 'avgTime', label: 'Ort. süre', num: true, value: function (r) { return r._time; },
          render: function (r) { return r._time !== null ? K.fmtDuration(r._time) : '—'; } },
        { key: 'mistakeRate', label: 'Hata oranı', num: true, value: function (r) { return r._rate; },
          render: function (r) { return r._rate !== null
            ? K.rateLabel({ ok: true, value: r._rate }, 0)
            : h('span', { style: 'color:var(--ink-3)', text: 'payda yok' }); } },
        { key: 'mistakes', label: 'Hata', num: true },
        { key: 'employeeCount', label: 'Çalışan', num: true }
      ],
      rows: factors.map(function (f) {
        return {
          actionId: f.actionId, actionName: f.actionName, levelName: f.levelName,
          sequenceName: f.sequenceName, type: f.type,
          _wrong: f.wrongRate.ok ? f.wrongRate.value : null,
          _att: f.avgAttempts.ok ? f.avgAttempts.value : null,
          _time: f.avgTime.ok ? f.avgTime.value : null,
          _rate: f.mistakeRate.ok ? f.mistakeRate.value : null,
          mistakes: f.mistakes, employeeCount: f.employeeCount
        };
      }),
      defaultSort: 'mistakeRate', defaultDir: 'desc', pageSize: 12,
      emptyTitle: 'Kapsam içinde adım kaydı yok',
      emptyWhat: 'Seçilen filtrelerle eşleşen ActionCompleted / QuizAnswered event\'i bulunamadı.'
    }))));

    return root;
  }

  // -- Senaryo detayı (yönetici) ---------------------------------------------

  function scenarioDetail(app, levelId) {
    const lvl = D.levelByEmittedId[levelId];
    const root = h('div');

    if (!lvl) {
      return U.emptyState({
        icon: 'warn', tone: 'warn', title: 'Senaryo bulunamadı',
        what: '"' + levelId + '" içerik kataloğunda yok.',
        action: 'Senaryolar sayfasına dönün.',
        cta: h('a', { class: 'btn btn--ghost', href: '#/manager/scenarios', text: 'Senaryolar' })
      });
    }

    const evs = K.filterEvents(scoped(app), { levelId: levelId });
    const runs = K.deriveRuns(evs);
    const acc = K.accuracy(evs);

    root.appendChild(h('div', { class: 'page-head' }, [
      h('div', { class: 'page-head__text' }, [
        h('h1', { text: lvl.name }),
        h('p', { text: lvl.subtitle })
      ]),
      h('div', { class: 'page-head__actions' }, [
        h('a', { class: 'btn btn--ghost', href: '#/manager/scenarios', text: '‹ Senaryolar' })
      ])
    ]));

    if (lvl.dataWarning) {
      root.appendChild(h('div', { style: 'margin-bottom:16px' },
        U.notice('warn', '<b>Veri kalitesi:</b> ' + U.esc(lvl.dataWarning))));
    }
    if (lvl.criticalNote) {
      root.appendChild(h('div', { style: 'margin-bottom:16px' },
        U.notice('info', '<b>Tasarım notu (GDD):</b> ' + U.esc(lvl.criticalNote))));
    }

    if (!evs.length) {
      root.appendChild(U.emptyState({
        icon: 'empty', title: 'Bu senaryo kapsam içinde hiç oynanmamış',
        what: 'Seçilen tarih aralığında bu senaryoya ait event yok.',
        action: 'Tarih aralığını genişletin.'
      }));
      return root;
    }

    root.appendChild(h('div', { class: 'grid grid--kpi' }, [
      U.kpiCard({ label: 'Deneme', value: String(runs.length), accent: true,
        sub: runs.filter(function (r) { return r.completed; }).length + ' tamamlandı' }),
      U.kpiCard({ label: 'Çalışan', value: String(K.activeEmployees(evs)),
        sub: 'benzersiz employeeId' }),
      acc.ok ? U.kpiCard({ label: 'Doğruluk', value: K.pct(acc, 0),
        sub: acc.num + '/' + acc.den + ' soru' })
             : U.kpiCard({ label: 'Doğruluk', unavailable: 'Soru yok', reason: acc.reason }),
      U.kpiCard({ label: 'Hata', value: String(K.mistakeCount(evs)),
        sub: (function () { const r = K.mistakeRate(evs);
          return r.ok ? 'oran ' + K.pct(r, 1) : r.reason; })() })
    ]));

    // Görev grubu kırılımı
    const seqRows = lvl.sequences.map(function (seq) {
      const se = K.filterEvents(evs, { sequenceId: seq.id });
      return {
        seq: seq, events: se, acc: K.accuracy(se),
        mistakes: K.mistakeCount(se), rate: K.mistakeRate(se),
        completed: se.filter(function (e) { return e.eventType === 'SequenceCompleted'; }).length
      };
    });

    root.appendChild(h('div', { class: 'mt-5' },
      U.card('Görev Grubu (Sequence) Kırılımı', {
        desc: 'Drill-down: Senaryo › Görev Grubu › Adım › Event'
      }, h('div', { class: 'tablewrap' }, h('table', { class: 'data' }, [
        h('thead', null, h('tr', null, [
          h('th', { text: 'Görev grubu' }), h('th', { text: 'sequenceID' }),
          h('th', { class: 'num', text: 'Adım' }), h('th', { class: 'num', text: 'Tamamlanma' }),
          h('th', { class: 'num', text: 'Doğruluk' }), h('th', { class: 'num', text: 'Hata' }),
          h('th', { class: 'num', text: 'Hata oranı' })
        ])),
        h('tbody', null, seqRows.map(function (r) {
          return h('tr', null, [
            h('td', { text: r.seq.name }),
            h('td', null, h('code', { style: 'font-size:.74rem', text: r.seq.id })),
            h('td', { class: 'num', text: String(r.seq.actions.length) }),
            h('td', { class: 'num', text: String(r.completed) }),
            h('td', { class: 'num', text: r.acc.ok ? K.pct(r.acc, 0) : '—' }),
            h('td', { class: 'num', text: String(r.mistakes) }),
            h('td', { class: 'num', text: r.rate.ok ? K.pct(r.rate, 1) : 'payda yok' })
          ]);
        }))
      ])))));

    // Adım listesi
    const factors = K.actionFactors(evs);
    factors.sort(function (a, b) {
      const x = a.mistakeRate.ok ? a.mistakeRate.value : -1;
      const y = b.mistakeRate.ok ? b.mistakeRate.value : -1;
      return y - x;
    });

    root.appendChild(h('div', { class: 'mt-5' },
      U.card('Adım Bazlı Faktörler', { desc: 'Tıklayarak event detayına inin.' },
        U.barList(factors.slice(0, 12).map(function (f) {
          return {
            label: f.actionName,
            value: f.mistakeRate.ok ? f.mistakeRate.value * 100 : 0,
            display: f.mistakeRate.ok ? K.rateLabel(f.mistakeRate, 0) : f.mistakes + ' hata (oran yok)',
            color: U.css('--cat-4'),
            sub: f.sequenceName + ' · ort. deneme ' +
                 (f.avgAttempts.ok ? f.avgAttempts.value.toFixed(2) : '—') +
                 ' · ort. süre ' + (f.avgTime.ok ? K.fmtDuration(f.avgTime.value) : '—') +
                 ' · ' + f.employeeCount + ' çalışan',
            onClick: function () { openActionDrill(app, f.actionId); }
          };
        }), { emptyTitle: 'Adım kaydı yok' }))));

    return root;
  }

  // ---------------------------------------------------------------------------
  // 5) HATA & RİSK ANALİZİ
  // ---------------------------------------------------------------------------

  function risks(app) {
    const evs = scoped(app);
    const root = h('div');

    root.appendChild(h('div', { class: 'page-head' }, [
      h('div', { class: 'page-head__text' }, [
        h('h1', { text: 'Hata ve Risk Analizi' }),
        h('p', { text: 'Normalize edilmiş hata yoğunluğu, tekrar eden hatalar ve severity durumu.' })
      ])
    ]));

    // SEVERITY UYARISI — en üstte, çünkü tüm "kritik" kavramını etkiliyor
    root.appendChild(U.notice('warn',
      '<b>Kritik hata sınıflandırması yapılmıyor.</b> Oyun kodunda <code>MistakeRecorded</code> ' +
      'yalnızca iki yerden çağrılıyor ve ikisinde de <code>severity</code> sabit <code>1</code> ' +
      'gönderiliyor (<code>SequenceManager.cs:671</code>, <code>UIDropZone.cs:179</code>). ' +
      'Projede tanımlı bir önem ölçeği bulunmadığı için severity değerleri aşağıda ' +
      '<b>ham kategori</b> olarak sayılır; hiçbir değer otomatik "kritik" sayılmaz.'));

    const mist = evs.filter(function (e) { return e.eventType === 'MistakeRecorded'; });

    if (!mist.length) {
      root.appendChild(h('div', { class: 'mt-4' }, U.emptyState({
        icon: 'check', title: 'Kapsam içinde hata kaydı yok',
        what: 'Seçilen tarih aralığı ve filtrelerde hiç MistakeRecorded event\'i bulunamadı.',
        why: 'Aralık dar olabilir ya da bu dönemde hata yapılmamış olabilir.',
        action: 'Tarih aralığını genişletin.'
      })));
      return root;
    }

    const bySeverity = K.mistakesBySeverity(evs);
    const byType = K.mistakesByType(evs);

    root.appendChild(h('div', { class: 'grid grid--kpi' }, [
      U.kpiCard({ label: 'Toplam Hata', value: String(mist.length), accent: true,
        sub: 'MistakeRecorded adedi' }),
      U.kpiCard({ label: 'Hata Oranı',
        value: (function () { const r = K.mistakeRate(evs); return r.ok ? K.pct(r, 1) : null; })() || '—',
        sub: 'MistakeRecorded / ActionCompleted',
        help: 'Normalize oran. Payda oluşmazsa gösterilmez.' }),
      U.kpiCard({ label: 'Etkilenen Çalışan',
        value: String(K.activeEmployees(mist)), sub: 'hata kaydı olan benzersiz çalışan' }),
      U.kpiCard({ label: 'Severity Kategorisi',
        value: Object.keys(bySeverity).join(', '),
        sub: 'ham değerler — ölçek tanımsız',
        help: 'Yalnızca gözlemlenen severity değerleri listelenir; yorumlanmaz.' })
    ]));

    root.appendChild(h('div', { class: 'grid grid--main mt-5' }, [
      h('div', { class: 'stack' }, [
        U.card('Hata Isı Haritası — Adım × Hata Türü', {
          desc: 'Hücrelere tıklayınca ilgili çalışanlar ve event dökümü açılır.',
          help: 'Hücre değeri normalize orandır: MistakeRecorded / ActionCompleted.'
        }, U.heatmapTable(K.heatmap(evs, { limit: 14 }), function (row, cell) {
          openHeatCellDrill(app, row, cell, evs);
        })),
        U.card('Kritik Hata Trendi', {
          desc: '"Kritik" sınıflandırması yapılamadığı için TOPLAM hata trendi gösterilir.'
        }, U.lineChart(K.timeSeries(evs, 'mistakes', 7), {
          color: U.css('--cat-4'), yMin: 0,
          fmt: function (v) { return v + ' hata'; },
          summary: 'Haftalık MistakeRecorded adedi. Severity ayrımı yapılamadığı için ' +
                   'tek seri gösteriliyor.'
        }))
      ]),
      h('div', { class: 'stack' }, [
        U.card('Hata Türü Dağılımı', {
          desc: 'Repoda yalnızca iki mistakeType üretiliyor.'
        }, U.barList(Object.keys(byType).map(function (t, i) {
          return { label: U.TYPE_LABEL[t] || t, value: byType[t], display: byType[t] + ' adet',
                   color: U.css('--cat-' + ((i % 5) + 1)),
                   sub: 'mistakeType: ' + t };
        }), { emptyTitle: 'Hata türü kaydı yok' })),
        U.card('Severity Dağılımı', {
          desc: 'Ham kategori sayımı — hiçbir değer "kritik" olarak yorumlanmadı.'
        }, [
          U.barList(Object.keys(bySeverity).map(function (sv) {
            return { label: 'severity = ' + sv, value: bySeverity[sv],
                     display: bySeverity[sv] + ' adet', color: U.css('--cat-2'),
                     sub: 'yorumlanmadı' };
          }), { emptyTitle: 'Severity kaydı yok' }),
          h('div', { class: 'mt-4' }, U.notice('info',
            'Severity ölçeği tanımlandığında (ör. 1=düşük, 2=orta, 3=kritik) bu kart ' +
            'otomatik olarak kritik hata oranını hesaplayabilir hale gelir. ' +
            'Gerekli kod değişikliği ASSET_GAPS.md ve DATA_MAPPING.md içinde açıklanmıştır.'))
        ]),
        repeatedAcrossEmployeesCard(evs)
      ])
    ]));

    // En riskli senaryo/adım listeleri
    const factors = K.actionFactors(evs).filter(function (f) { return f.mistakes > 0; });

    root.appendChild(h('div', { class: 'grid grid--2 mt-5' }, [
      U.card('En Riskli Senaryolar', { desc: 'Normalize hata oranına göre.' },
        U.barList(D.content.levels.map(function (lvl) {
          const le = K.filterEvents(evs, { levelId: lvl.emittedLevelId });
          const r = K.mistakeRate(le);
          return { lvl: lvl, rate: r, mistakes: K.mistakeCount(le) };
        }).filter(function (r) { return r.mistakes > 0; })
          .sort(function (a, b) {
            return (b.rate.ok ? b.rate.value : -1) - (a.rate.ok ? a.rate.value : -1); })
          .map(function (r) {
            return {
              label: r.lvl.name,
              value: r.rate.ok ? r.rate.value * 100 : 0,
              display: r.rate.ok ? K.rateLabel(r.rate, 1) : r.mistakes + ' hata (oran yok)',
              color: U.css('--cat-4'),
              sub: r.rate.ok ? r.rate.num + ' hata / ' + r.rate.den + ' adım' : 'payda oluşmadı',
              onClick: function () {
                location.hash = '#/manager/scenario/' + encodeURIComponent(r.lvl.emittedLevelId); }
            };
          }), { emptyTitle: 'Hata kaydı olan senaryo yok' })),

      U.card('En Riskli Adımlar', { desc: 'Normalize hata oranına göre ilk 8.' },
        U.barList(factors.filter(function (f) { return f.mistakeRate.ok; })
          .sort(function (a, b) { return b.mistakeRate.value - a.mistakeRate.value; })
          .slice(0, 8).map(function (f) {
            return {
              label: f.actionName, value: f.mistakeRate.value * 100,
              display: K.rateLabel(f.mistakeRate, 0), color: U.css('--cat-4'),
              sub: f.levelName + ' › ' + f.sequenceName + ' · ' +
                   f.mistakeRate.num + '/' + f.mistakeRate.den + ' · ' +
                   f.employeeCount + ' çalışan',
              onClick: function () { openActionDrill(app, f.actionId); }
            };
          }), { emptyTitle: 'Normalize edilebilir adım yok',
                emptyWhat: 'Hata var ama ActionCompleted paydası oluşmadığı için oran üretilemedi.' }))
    ]));

    return root;
  }

  function repeatedAcrossEmployeesCard(evs) {
    const map = {};
    evs.filter(function (e) { return e.eventType === 'MistakeRecorded'; })
      .forEach(function (e) {
        const k = e.payload.actionId + '::' + e.payload.mistakeType;
        map[k] = map[k] || { emps: {}, count: 0 };
        map[k].count += 1;
        map[k].emps[e.employeeId] = (map[k].emps[e.employeeId] || 0) + 1;
      });

    const rows = Object.keys(map).map(function (k) {
      const repeaters = Object.keys(map[k].emps).filter(function (id) { return map[k].emps[id] >= 2; });
      return { key: k, count: map[k].count, repeaters: repeaters.length,
               total: Object.keys(map[k].emps).length };
    }).filter(function (r) { return r.repeaters > 0; })
      .sort(function (a, b) { return b.repeaters - a.repeaters; });

    if (!rows.length) {
      return U.card('Aynı Hatayı Tekrar Edenler', {}, U.emptyState({
        inline: true, icon: 'check', title: 'Tekrar eden hata kalıbı yok',
        what: 'Hiçbir çalışan aynı adımda aynı hatayı iki veya daha fazla kez yapmamış.'
      }));
    }

    return U.card('Aynı Hatayı Tekrar Edenler', {
      desc: 'Aynı adım + hata türünü 2+ kez yapan çalışan sayısı.',
      aside: U.badge('warn', rows.length + ' kalıp', 'repeat')
    }, U.barList(rows.slice(0, 6).map(function (r) {
      const parts = r.key.split('::');
      const idx = D.actionIndex[parts[0]];
      return {
        label: idx ? idx.action.name : parts[0],
        value: r.repeaters, display: r.repeaters + ' çalışan',
        color: U.css('--cat-2'),
        sub: (U.TYPE_LABEL[parts[1]] || parts[1]) + ' · toplam ' + r.count +
             ' hata · ' + r.total + ' çalışan etkilendi'
      };
    })));
  }

  // ---------------------------------------------------------------------------
  // DRILL-DOWN: Adım → Event
  // ---------------------------------------------------------------------------

  function openActionDrill(app, actionId, scopeEvents) {
    const evs = (scopeEvents || scoped(app))
      .filter(function (e) { return e.payload && e.payload.actionId === actionId; });
    const idx = D.actionIndex[actionId];
    const body = [];

    body.push(h('dl', { class: 'deflist' }, [
      h('dt', { text: 'actionId' }), h('dd', null, h('code', { text: actionId })),
      h('dt', { text: 'Senaryo' }), h('dd', { text: idx ? idx.level.name : '(katalogda yok)' }),
      h('dt', { text: 'Görev grubu' }), h('dd', { text: idx ? idx.sequence.name : '—' }),
      h('dt', { text: 'Adım tipi' }), h('dd', { text: idx ? idx.action.rawType : '—' }),
      h('dt', { text: 'Toplam event' }), h('dd', { text: String(evs.length) })
    ]));

    if (!idx) {
      body.push(h('div', { class: 'mt-4' }, U.notice('warn',
        '<b>Bu actionId içerik kataloğunda bulunamadı.</b> ' +
        'MistakeRecorded event\'i levelId/sequenceId taşımadığı için bu hata ' +
        'senaryodaki yerine bağlanamıyor.')));
    }

    const acc = K.accuracy(evs);
    const mist = K.mistakeCount(evs);
    const rate = K.mistakeRate(evs);

    body.push(h('div', { class: 'grid grid--3 mt-4' }, [
      h('div', { class: 'kpi' }, [
        h('div', { class: 'kpi__label', text: 'Yanlış cevap' }),
        h('div', { class: 'kpi__value', style: 'font-size:1.3rem',
                   text: acc.ok ? ((1 - acc.value) * 100).toFixed(0) + '%' : '—' })
      ]),
      h('div', { class: 'kpi' }, [
        h('div', { class: 'kpi__label', text: 'Hata' }),
        h('div', { class: 'kpi__value', style: 'font-size:1.3rem', text: String(mist) })
      ]),
      h('div', { class: 'kpi' }, [
        h('div', { class: 'kpi__label', text: 'Hata oranı' }),
        h('div', { class: 'kpi__value', style: 'font-size:1.3rem',
                   text: rate.ok ? K.pct(rate, 0) : 'payda yok' })
      ])
    ]));

    // Etkilenen çalışanlar
    const byEmp = {};
    evs.forEach(function (e) {
      byEmp[e.employeeId] = byEmp[e.employeeId] || { total: 0, mistakes: 0, wrong: 0 };
      byEmp[e.employeeId].total += 1;
      if (e.eventType === 'MistakeRecorded') byEmp[e.employeeId].mistakes += 1;
      if (e.eventType === 'QuizAnswered' && !e.payload.isCorrect) byEmp[e.employeeId].wrong += 1;
    });

    body.push(h('h3', { class: 'mt-4', style: 'margin-bottom:8px', text: 'Etkilenen çalışanlar' }));
    body.push(h('div', { class: 'tablewrap' }, h('table', { class: 'data', style: 'min-width:0' }, [
      h('thead', null, h('tr', null, [
        h('th', { text: 'Çalışan' }), h('th', { class: 'num', text: 'Event' }),
        h('th', { class: 'num', text: 'Yanlış' }), h('th', { class: 'num', text: 'Hata' })
      ])),
      h('tbody', null, Object.keys(byEmp)
        .sort(function (a, b) { return byEmp[b].mistakes - byEmp[a].mistakes; })
        .map(function (id) {
          const emp = D.employeeById[id];
          return h('tr', null, [
            h('td', null, h('button', { class: 'rowlink', type: 'button',
              text: emp ? emp.name : id,
              onClick: function () {
                U.closeDrawer();
                location.hash = '#/manager/employee/' + encodeURIComponent(id); } })),
            h('td', { class: 'num', text: String(byEmp[id].total) }),
            h('td', { class: 'num', text: String(byEmp[id].wrong) }),
            h('td', { class: 'num', text: String(byEmp[id].mistakes) })
          ]);
        }))
    ])));

    // Quiz soru metni
    const bank = D.quizBank[actionId];
    if (bank) {
      body.push(h('h3', { class: 'mt-4', style: 'margin-bottom:8px', text: 'Soru' }));
      body.push(h('p', { style: 'font-size:.86rem', text: bank.q }));
      body.push(h('div', { class: 'answer-row' }, bank.options.map(function (o, i) {
        const picks = evs.filter(function (e) {
          return e.eventType === 'QuizAnswered' &&
                 e.payload.selectedAnswer === ['A) ', 'B) ', 'C) ', 'D) '][i] + o;
        }).length;
        return h('div', { class: 'answer' + (i === bank.correctIndex ? ' answer--correct' : '') }, [
          U.icon(i === bank.correctIndex ? 'check' : 'minus', 14),
          h('div', { style: 'flex:1' }, [
            h('span', { text: ['A', 'B', 'C', 'D'][i] + ') ' + o }),
            h('span', { class: 'answer__label', style: 'margin-top:3px',
                        text: picks + ' kez seçildi' })
          ])
        ]);
      })));
      body.push(h('p', { class: 'card__desc', text: 'Kaynak: ' + bank.asset }));
    }

    body.push(h('details', { class: 'tech' }, [
      h('summary', { text: 'Teknik Detay (ham event kaydı) — ' + evs.length + ' event' }),
      h('div', { class: 'tech__body' },
        h('pre', { class: 'json', text: JSON.stringify(evs.slice(0, 25), null, 2) }))
    ]));

    U.openDrawer(idx ? idx.action.name : actionId, body,
      idx ? (idx.level.name + ' › ' + idx.sequence.name) : 'Katalog dışı adım');
  }

  function openHeatCellDrill(app, row, cell, evs) {
    const body = [];

    body.push(h('dl', { class: 'deflist' }, [
      h('dt', { text: 'Adım' }), h('dd', { text: row.actionName }),
      h('dt', { text: 'Senaryo' }), h('dd', { text: row.levelName }),
      h('dt', { text: 'Görev grubu' }), h('dd', { text: row.sequenceName }),
      h('dt', { text: 'Hata türü' }), h('dd', { text: U.TYPE_LABEL[cell.type] || cell.type }),
      h('dt', { text: 'Event sayısı' }), h('dd', { text: String(cell.count) }),
      h('dt', { text: 'Toplam adım denemesi' }), h('dd', { text: String(row.denominator) }),
      h('dt', { text: 'Normalize oran' }),
      h('dd', { text: cell.rate.ok
        ? (cell.rate.value * 100).toFixed(1) + '%  (' + cell.rate.num + '/' + cell.rate.den + ')'
        : cell.rate.reason }),
      h('dt', { text: 'Son görülme' }), h('dd', { text: K.fmtDateTime(cell.lastSeen) }),
      h('dt', { text: 'Etkilenen çalışan' }), h('dd', { text: String(cell.employeeCount) })
    ]));

    body.push(h('h3', { class: 'mt-4', style: 'margin-bottom:8px', text: 'Çalışanlar' }));
    body.push(h('div', { class: 'stack' }, cell.employees.map(function (id) {
      const emp = D.employeeById[id];
      const n = evs.filter(function (e) {
        return e.eventType === 'MistakeRecorded' && e.employeeId === id &&
               e.payload.actionId === row.actionId && e.payload.mistakeType === cell.type;
      }).length;
      return h('div', { style: 'display:flex;gap:8px;align-items:center;padding:8px;background:rgba(0,0,0,.2);border-radius:6px' }, [
        h('button', { class: 'rowlink', type: 'button', style: 'flex:1',
          text: emp ? emp.name : id,
          onClick: function () {
            U.closeDrawer();
            location.hash = '#/manager/employee/' + encodeURIComponent(id); } }),
        U.badge(n >= 2 ? 'warn' : 'neutral', n + ' kez', n >= 2 ? 'repeat' : null)
      ]);
    })));

    body.push(h('div', { class: 'mt-4' },
      h('button', { class: 'btn btn--ghost btn--block', type: 'button',
        text: 'Adımın tüm event detayını aç',
        onClick: function () { openActionDrill(app, row.actionId); } })));

    U.openDrawer(row.actionName, body, (U.TYPE_LABEL[cell.type] || cell.type) + ' — hücre detayı');
  }

  // ---------------------------------------------------------------------------
  // 6) GELİŞİM TRENDLERİ
  // ---------------------------------------------------------------------------

  function trends(app) {
    const evs = scoped(app);
    const root = h('div');

    root.appendChild(h('div', { class: 'page-head' }, [
      h('div', { class: 'page-head__text' }, [
        h('h1', { text: 'Gelişim Trendleri' }),
        h('p', { text: 'Zaman içindeki değişim. Farklı ölçekteki metrikler ayrı grafiklerde ' +
                       'gösterilir — çift eksen kullanılmaz.' })
      ])
    ]));

    if (!evs.length) {
      root.appendChild(U.emptyState({
        icon: 'chart', title: 'Kapsam içinde veri yok',
        what: 'Seçilen aralıkta hiç event bulunamadı.',
        action: 'Tarih aralığını genişletin.'
      }));
      return root;
    }

    const charts = [
      { title: 'Doğruluk Oranı', metric: 'accuracy', color: '--cat-1', yMin: 0, yMax: 100,
        fmt: function (v) { return v.toFixed(0) + '%'; },
        desc: 'Doğru QuizAnswered / toplam QuizAnswered (%)' },
      { title: 'Aktif Çalışan', metric: 'activeEmployees', color: '--cat-5', yMin: 0,
        fmt: function (v) { return v + ' çalışan'; },
        desc: 'Haftada en az bir event gönderen benzersiz employeeId' },
      { title: 'Senaryo Denemesi', metric: 'runs', color: '--cat-3', yMin: 0,
        fmt: function (v) { return v + ' deneme'; },
        desc: 'Haftalık LevelStarted sayısı' },
      { title: 'Ortalama Soru Süresi', metric: 'avgTime', color: '--cat-2', yMin: 0,
        fmt: function (v) { return K.fmtDuration(v); },
        desc: 'QuizAnswered.timeSpent ortalaması (saniye)' },
      { title: 'Hata Sayısı', metric: 'mistakes', color: '--cat-4', yMin: 0,
        fmt: function (v) { return v + ' hata'; },
        desc: 'Haftalık MistakeRecorded adedi (oran değil)' },
      { title: 'Hata Oranı', metric: 'mistakeRate', color: '--cat-4', yMin: 0,
        fmt: function (v) { return v.toFixed(1) + '%'; },
        desc: 'MistakeRecorded / ActionCompleted (%)' }
    ];

    // grid--wide: iki sütun. Üç sütunda grafikler o kadar küçülüyor ki
    // eksen etiketleri okunamaz hale geliyordu.
    root.appendChild(h('div', { class: 'grid grid--wide' }, charts.map(function (c) {
      return U.card(c.title, { desc: c.desc },
        U.lineChart(K.timeSeries(evs, c.metric, 7), {
          color: U.css(c.color), yMin: c.yMin, yMax: c.yMax, height: 200,
          fmt: c.fmt,
          fmtAxis: c.metric === 'accuracy' || c.metric === 'mistakeRate'
            ? function (v) { return Math.round(v) + '%'; }
            : function (v) { return Math.round(v); },
          // Kart açıklaması aynı bilgiyi verdiği için görünür özet gizlenir;
          // metin yine de svg'nin aria-label'ında kalır.
          summary: c.desc + ' — haftalık kovalar.', hideSummary: true
        }));
    })));

    root.appendChild(h('div', { class: 'mt-4' }, U.notice('info',
      '<b>Kritik hata trendi neden yok?</b> Severity ölçeği tanımlanmadığı için ' +
      '"kritik" alt kümesi ayrıştırılamıyor. Ölçek tanımlandığında bu sayfaya ' +
      'ayrı bir kritik hata grafiği eklenebilir.')));

    return root;
  }

  // ---------------------------------------------------------------------------
  // 7) RAPORLAR (placeholder — backend gerekiyor)
  // ---------------------------------------------------------------------------

  function reports(app) {
    const root = h('div');

    root.appendChild(h('div', { class: 'page-head' }, [
      h('div', { class: 'page-head__text' }, [
        h('h1', { text: 'Raporlar' }),
        h('p', { text: 'Planlanan rapor çıktıları. Bu prototipte hiçbiri üretilmez.' })
      ])
    ]));

    root.appendChild(U.notice('warn',
      '<b>Backend bağlantısı gerekli.</b> Rapor üretimi, zamanlama ve dışa aktarma ' +
      'sunucu tarafı iş yüküdür. Bu sayfa yalnızca bilgi mimarisini ve gerekli veri ' +
      'sözleşmesini gösterir.'));

    const items = [
      { t: 'Haftalık Kurum Özeti', d: 'Aktif çalışan, deneme, doğruluk ve hata sayısının ' +
        'haftalık özeti. Kaynak: tüm event türleri.',
        need: 'Zamanlanmış görev + e-posta gönderimi' },
      { t: 'Çalışan Performans Raporu', d: 'Tek çalışanın senaryo/level/adım kırılımı ve ' +
        'deneme karşılaştırması.',
        need: 'PDF üretimi + kişisel veri maskeleme politikası' },
      { t: 'Senaryo Zorluk Raporu', d: 'Adım bazlı faktör tablosunun dışa aktarımı.',
        need: 'CSV/XLSX üretimi' },
      { t: 'Risk Raporu', d: 'Normalize hata yoğunluğu ve tekrar eden hata kalıpları.',
        need: 'Severity ölçeği tanımı (aksi halde "kritik" bölümü boş kalır)' },
      { t: 'Ham Event Dışa Aktarımı', d: 'Seçilen kapsamdaki PlayFab event kayıtları.',
        need: 'Export yetkilendirmesi + audit logging' }
    ];

    root.appendChild(h('div', { class: 'grid grid--2 mt-4' }, items.map(function (it) {
      return U.card(it.t, { desc: it.d }, [
        h('div', { style: 'display:flex;gap:8px;flex-wrap:wrap;margin-bottom:12px' }, [
          U.badge('proto', 'Prototip', 'info'),
          U.badge('warn', 'Backend gerekli', 'warn')
        ]),
        h('p', { class: 'card__desc', text: 'Gereken: ' + it.need }),
        h('div', { style: 'display:flex;gap:8px;margin-top:12px' }, [
          h('button', { class: 'btn btn--ghost btn--sm', type: 'button',
            onClick: function () {
              U.openModal(it.t, [
                h('p', { text: it.d }),
                U.notice('warn', 'Bu rapor bu prototipte üretilmiyor. Gereken: ' + U.esc(it.need))
              ]);
            } }, [U.icon('download', 14), 'Önizleme']),
          h('button', { class: 'btn btn--ghost btn--sm', type: 'button', 'aria-disabled': 'true',
            onClick: function () {
              U.openModal('Zamanlama', [
                h('p', { text: 'Rapor zamanlama backend entegrasyonu gerektirir.' })
              ]);
            }, text: 'Zamanla' })
        ])
      ]);
    })));

    return root;
  }

  // ---------------------------------------------------------------------------
  // 8) AYARLAR
  // ---------------------------------------------------------------------------

  function settings(app) {
    const root = h('div');

    root.appendChild(h('div', { class: 'page-head' }, [
      h('div', { class: 'page-head__text' }, [
        h('h1', { text: 'Ayarlar' }),
        h('p', { text: 'Portalın davranışını belirleyecek iş kuralları — ' +
                       'hepsi tanımlanmayı bekliyor.' })
      ])
    ]));

    root.appendChild(U.notice('warn',
      '<b>Bu sayfadaki ayarlar kaydedilmez.</b> Her biri, portalın doğru çalışması için ' +
      'ürün ekibinin karara bağlaması gereken bir iş kuralını temsil eder.'));

    const groups = [
      {
        title: 'Severity Ölçeği', badge: 'Karar bekliyor',
        desc: 'Şu an oyun her hataya severity=1 yazıyor. Ölçek tanımlanana kadar ' +
              '"kritik hata" kavramı portalda kullanılamaz.',
        rows: [
          ['Mevcut durum', 'Sabit 1 (SequenceManager.cs:671, UIDropZone.cs:179)'],
          ['Gereken', 'En az 3 kademe + hangi mistakeType hangi kademe'],
          ['Etkilenen ekranlar', 'Yönetim Özeti, Risk Analizi, Gelişim Trendleri, Çalışan detay']
        ]
      },
      {
        title: 'Tamamlama Kuralı', badge: 'Kısmen tanımlı',
        desc: 'LevelCompleted event\'i var ve kullanılıyor. Ancak yarım kalan oturumların ' +
              'nasıl sayılacağı tanımlı değil.',
        rows: [
          ['Mevcut durum', 'LevelCompleted { completed: true } — güvenilir'],
          ['Belirsiz', 'SessionEnded gelip LevelCompleted gelmeyen oturum "terk" mi, "hata" mı?'],
          ['Gereken', 'Zaman aşımı süresi ve terk tanımı']
        ]
      },
      {
        title: 'Oturum / Deneme Kimliği', badge: 'Eksik alan',
        desc: 'Şemada sessionId veya attemptId yok. Denemeler LevelStarted → LevelCompleted ' +
              'çiftinden türetiliyor; bu türetme çökme ve çok cihaz durumunda bozulur.',
        rows: [
          ['Mevcut durum', 'Türetilmiş (kırılgan)'],
          ['Gereken', 'LogLevelStarted içinde üretilen bir sessionId ve tüm payload\'lara eklenmesi'],
          ['Etki', 'Deneme karşılaştırması, gelişim trendi, tamamlama oranı']
        ]
      },
      {
        title: 'Organizasyon Boyutları', badge: 'Ek veri kaynağı',
        desc: 'Ekip, departman, lokasyon ve vardiya bilgisi ne event\'te ne de whitelist\'te var.',
        rows: [
          ['Mevcut durum', 'Yok'],
          ['Gereken', 'İK sisteminden employeeId eşleşmeli bir organizasyon tablosu'],
          ['Kilitlenen özellikler', 'Ekip karşılaştırması, lokasyon bazlı risk, vardiya analizi']
        ]
      },
      {
        title: 'Eğitim Ataması (Roster)', badge: 'Ek veri kaynağı',
        desc: 'Kimin hangi eğitimi tamamlaması gerektiği bilinmiyor, bu yüzden ' +
              '"katılım oranı" hesaplanamıyor.',
        rows: [
          ['Mevcut durum', 'PlayFab whitelist = erişim listesi, atama listesi değil'],
          ['Gereken', 'employeeId × levelId atama tablosu + son tarih'],
          ['Kilitlenen özellikler', 'Katılım oranı, gecikmiş eğitim uyarısı']
        ]
      },
      {
        title: 'Anket & Fotoğraf Telemetrisi', badge: 'Eksik event',
        desc: 'GDD "veriler analitik sistemine aktarılır" diyor ancak SurveyResultTracker ' +
              'hiçbir event göndermiyor; sonuçlar yalnızca bellekte.',
        rows: [
          ['Mevcut durum', 'Yalnızca ActionCompleted { type: "survey" } görülüyor'],
          ['Gereken', 'SurveyCompleted event\'i: cevaplar, doğruluk, fotoğraf hizalama skoru'],
          ['Kilitlenen özellikler', 'Saha anketi analizi, fotoğraf kalite raporu']
        ]
      }
    ];

    root.appendChild(h('div', { class: 'grid grid--2 mt-4' }, groups.map(function (g) {
      return U.card(g.title, { desc: g.desc, aside: U.badge('warn', g.badge, 'warn') },
        h('dl', { class: 'deflist' }, g.rows.reduce(function (acc, r) {
          acc.push(h('dt', { text: r[0] }));
          acc.push(h('dd', { text: r[1] }));
          return acc;
        }, [])));
    })));

    return root;
  }

  // ---------------------------------------------------------------------------
  window.TS_MANAGER = {
    dashboard: dashboard,
    employees: employees,
    employeeDetail: employeeDetail,
    scenarios: scenarios,
    scenarioDetail: scenarioDetail,
    risks: risks,
    trends: trends,
    reports: reports,
    settings: settings
  };
})();
