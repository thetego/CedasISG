/* =============================================================================
   THUNDERSHOCK KPI PORTALI — ÇALIŞAN (PLAYER) PORTALI EKRANLARI
   =============================================================================
   Gizlilik sınırı: Bu dosyadaki her sorgu, oturum açan çalışanın KENDİ
   employeeId'si ile filtrelenir. Çalışan başka bir çalışanın verisine
   erişemez. (Prototipte istemci tarafı; gerçek üründe sunucu yetkilendirmesi
   zorunludur — bkz. README "Gerçek Ürün Gereksinimleri".)
============================================================================= */

(function () {
  'use strict';

  const U = window.TS_UI;
  const K = window.TS_KPI;
  const D = window.TS_DATA;
  const h = U.h;

  // ---------------------------------------------------------------------------
  // Ortak: çalışanın kendi verisi
  // ---------------------------------------------------------------------------

  function myEvents(app) {
    return D.events.filter(function (e) { return e.employeeId === app.session.id; });
  }

  function mySummary(app) {
    return K.employeeSummary(D.events, app.session.id, app.filters.employeeScope());
  }

  function levelOf(levelId) { return D.levelByEmittedId[levelId] || null; }

  function levelName(levelId) {
    const l = levelOf(levelId);
    return l ? l.name : '(katalogda yok: ' + levelId + ')';
  }

  // ---------------------------------------------------------------------------
  // 1) GENEL BAKIŞ
  // ---------------------------------------------------------------------------

  function dashboard(app) {
    const sum = mySummary(app);
    const root = h('div');

    // -- Hiç veri yok --------------------------------------------------------
    if (sum.eventCount === 0) {
      root.appendChild(h('div', { class: 'page-head' }, [
        h('div', { class: 'page-head__text' }, [
          h('h1', { text: 'Merhaba, ' + sum.employee.name }),
          h('p', { text: 'Henüz bir eğitim kaydınız görünmüyor.' })
        ])
      ]));
      root.appendChild(U.emptyState({
        icon: 'empty', title: 'Henüz hiç senaryo oynamamışsınız',
        what: 'Adınıza kayıtlı hiçbir eğitim event\'i bulunamadı; bu yüzden performans, ' +
              'hata ve gelişim kartları hesaplanamıyor.',
        why: 'Eğitimi henüz başlatmamış olabilirsiniz ya da oynadığınız oturumda ' +
             'cihaz çevrimdışıyken kayıtlar sunucuya ulaşmamış olabilir.',
        action: 'Thundershock uygulamasını açıp bir senaryoyu tamamlayın; ' +
                'veriler oturum sonunda bu ekrana yansır.',
        cta: h('a', { class: 'btn btn--ghost', href: '#/employee/scenarios',
                      text: 'Senaryo listesini görüntüle' })
      }));
      return root;
    }

    const last = sum.lastRun;
    const lastLevel = last ? levelOf(last.levelId) : null;
    const lastM = last ? K.runMetrics(last) : null;
    const cmp = sum.comparison;

    // -- Başlık --------------------------------------------------------------
    root.appendChild(h('div', { class: 'page-head' }, [
      h('div', { class: 'page-head__text' }, [
        h('h1', { text: 'Merhaba, ' + sum.employee.name }),
        h('p', { text:
          'Son aktivite: ' + K.fmtDateTime(sum.lastActivity) + ' (' + K.relativeDays(sum.lastActivity) + ')' +
          (lastLevel ? ' · Son senaryo: ' + lastLevel.name : '') })
      ]),
      h('div', { class: 'page-head__actions' }, [
        last ? h('a', {
          class: 'btn btn--primary',
          href: '#/employee/scenario/' + encodeURIComponent(last.levelId),
          text: 'Son Performansı İncele'
        }) : null,
        h('a', { class: 'btn btn--ghost', href: '#/employee/mistakes', text: 'Hatalarım' })
      ])
    ]));

    // -- Ana KPI'lar (en fazla 5) -------------------------------------------
    const kpis = h('div', { class: 'grid grid--kpi' });

    kpis.appendChild(sum.accuracy.ok
      ? U.kpiCard({
          label: 'Son Başarı Oranı', accent: true,
          value: K.pct(sum.accuracy, 0),
          help: 'Doğru cevaplanan QuizAnswered event sayısı / toplam QuizAnswered event sayısı. ' +
                'Yalnızca bilgi soruları sayılır; sürükle-bırak adımları bu orana girmez.',
          sub: sum.accuracy.num + '/' + sum.accuracy.den + ' soru',
          delta: cmp.ok && cmp.deltas.accuracy !== null
            ? U.deltaBadge(cmp.deltas.accuracy * 100, { fmt: function (v) {
                return (v > 0 ? '+' : '') + v.toFixed(0) + ' puan'; } })
            : null
        })
      : U.kpiCard({
          label: 'Son Başarı Oranı', unavailable: 'Hesaplanamıyor',
          reason: sum.accuracy.reason,
          help: 'Payda (toplam QuizAnswered) sıfır olduğu için oran gösterilmiyor.'
        }));

    kpis.appendChild(sum.avgTime.ok
      ? U.kpiCard({
          label: 'Ortalama Soru Süresi',
          value: K.fmtDuration(sum.avgTime.value),
          help: 'QuizAnswered.timeSpent alanlarının ortalaması. Eksik veya geçersiz ' +
                'değerler sıfır sayılmaz, hesap dışında bırakılır.',
          sub: sum.avgTime.n + ' geçerli kayıt' +
               (sum.avgTime.dropped ? ' · ' + sum.avgTime.dropped + ' kayıt eksik veri' : ''),
          delta: cmp.ok && cmp.deltas.totalTime !== null
            ? U.deltaBadge(-cmp.deltas.totalTime, { invert: false, fmt: function (v) {
                return (v > 0 ? '−' : '+') + K.fmtDuration(Math.abs(v)); } })
            : null
        })
      : U.kpiCard({ label: 'Ortalama Soru Süresi', unavailable: 'Veri yok',
                    reason: sum.avgTime.reason }));

    kpis.appendChild(U.kpiCard({
      label: 'Toplam Hata',
      value: String(sum.mistakes),
      help: 'MistakeRecorded event sayısı. Bu bir ORAN DEĞİL, adettir — ' +
            'projede güvenilir bir payda tanımlı olmadığı için yüzde gösterilmez.',
      sub: Object.keys(sum.mistakesByType).map(function (t) {
        return (U.TYPE_LABEL[t] || t) + ': ' + sum.mistakesByType[t];
      }).join(' · ') || 'kırılım yok',
      delta: cmp.ok ? U.deltaBadge(cmp.deltas.mistakes, {
        invert: true, fmt: function (v) { return (v > 0 ? '+' : '') + v + ' hata'; } }) : null
    }));

    kpis.appendChild(U.kpiCard({
      label: 'Kritik Hata',
      unavailable: 'Sınıflandırma yok',
      reason: 'Severity ölçeği tanımsız',
      help: K.criticalMistakeRate().reason
    }));

    kpis.appendChild(sum.avgAttempts.ok
      ? U.kpiCard({
          label: 'Ortalama Deneme',
          value: sum.avgAttempts.value.toFixed(2),
          help: 'Her soru için kaydedilen en yüksek attempts değerinin ortalaması. ' +
                'QuizAnswered.attempts kümülatiftir (UIQuizPanel.cs:157), bu yüzden ' +
                'aynı sorunun tekrarları toplanmaz.',
          sub: sum.avgAttempts.n + ' soru',
          delta: cmp.ok && cmp.deltas.avgAttempts !== null
            ? U.deltaBadge(cmp.deltas.avgAttempts, { invert: true, eps: 0.01,
                fmt: function (v) { return (v > 0 ? '+' : '') + v.toFixed(2); } })
            : null
        })
      : U.kpiCard({ label: 'Ortalama Deneme', unavailable: 'Veri yok',
                    reason: sum.avgAttempts.reason }));

    root.appendChild(kpis);

    // -- Son performans özeti + gelişim --------------------------------------
    const twoCol = h('div', { class: 'grid grid--main mt-5' });

    twoCol.appendChild(lastRunCard(app, last, lastM, lastLevel));

    const side = h('div', { class: 'stack' });
    side.appendChild(scenarioProgressCard(app, sum));
    side.appendChild(recentMistakesCard(app, sum));
    twoCol.appendChild(side);

    root.appendChild(twoCol);
    root.appendChild(h('div', { class: 'mt-5' }, trendCard(app, sum)));

    return root;
  }

  // -- Son deneme kartı -------------------------------------------------------

  function lastRunCard(app, run, m, level) {
    if (!run) {
      return U.card('Son Performans', {}, U.emptyState({
        inline: true, icon: 'clock', title: 'Tamamlanmış deneme yok',
        what: 'Kayıtlarınızda LevelStarted var ama tamamlanmış bir oturum bulunamadı.',
        action: 'Bir senaryoyu sonuna kadar oynayın.'
      }));
    }

    const evs = run.events;
    const quiz = evs.filter(function (e) { return e.eventType === 'QuizAnswered'; });
    const correct = quiz.filter(function (e) { return e.payload.isCorrect; }).length;
    const retried = {};
    quiz.forEach(function (e) { if (e.payload.attempts > 1) retried[e.payload.actionId] = true; });
    const sev = K.mistakesBySeverity(evs);

    const rows = [
      ['Senaryo', level ? level.name : run.levelId],
      ['Telemetri levelId', h('code', { text: run.levelId })],
      ['Deneme', 'Deneme #' + run.attemptNo + ' / ' + run.attemptTotal + ' (türetilmiş)'],
      ['Başlangıç', K.fmtDateTime(run.startedAt)],
      ['Bitiş', run.completed ? K.fmtDateTime(run.endedAt) : '—'],
      ['Yanıtlanan adım', String(evs.filter(function (e) { return e.eventType === 'ActionCompleted'; }).length)],
      ['Doğru yanıt', String(correct)],
      ['Yanlış yanıt', String(quiz.length - correct)],
      ['Toplam süre', K.fmtDuration(m.totalTime)],
      ['Tekrar denenen adım', String(Object.keys(retried).length)],
      ['MistakeRecorded', String(m.mistakes)],
      ['Severity dağılımı', Object.keys(sev).map(function (k) {
        return 'severity ' + k + ': ' + sev[k];
      }).join(' · ') || '—']
    ];

    const dl = h('dl', { class: 'deflist' });
    rows.forEach(function (r) {
      dl.appendChild(h('dt', { text: r[0] }));
      const dd = h('dd');
      if (typeof r[1] === 'string') dd.textContent = r[1]; else dd.appendChild(r[1]);
      dl.appendChild(dd);
    });

    const statusBadge = run.completed
      ? U.badge('ok', 'Tamamlandı', 'check')
      : U.badge('warn', 'Tamamlanmadı', 'warn');

    return U.card('Son Performans Özeti', {
      desc: 'En son oynanan senaryo denemesi.',
      aside: statusBadge
    }, [
      dl,
      !run.completed ? U.notice('warn',
        '<b>LevelCompleted event\'i alınmadı.</b> Bu oturum yarıda kalmış olabilir ' +
        '(uygulama kapatılmış veya bağlantı kopmuş). Tamamlanma durumu tahmin edilmiyor.') : null,
      Object.keys(sev).length ? U.notice('info',
        '<b>Severity yorumlanmıyor.</b> Oyun kodunda severity her zaman <code>1</code> ' +
        'gönderiliyor (SequenceManager.cs:671, UIDropZone.cs:179). Bu yüzden hiçbir ' +
        'hata otomatik olarak "kritik" sayılmadı.') : null,
      h('div', { class: 'mt-4', style: 'display:flex;gap:8px;flex-wrap:wrap' }, [
        h('a', { class: 'btn btn--primary',
                 href: '#/employee/scenario/' + encodeURIComponent(run.levelId),
                 text: 'Senaryo detayına git' }),
        h('a', { class: 'btn btn--ghost', href: '#/employee/performance',
                 text: 'Deneme karşılaştırması' })
      ])
    ]);
  }

  // -- Senaryo ilerleme kartı -------------------------------------------------

  function scenarioProgressCard(app, sum) {
    const rows = D.content.levels.map(function (lvl) {
      const st = K.scenarioStatus(sum.runs, lvl.emittedLevelId);
      const runs = sum.runs.filter(function (r) { return r.levelId === lvl.emittedLevelId; });
      const seqDone = {};
      runs.forEach(function (r) {
        r.events.filter(function (e) { return e.eventType === 'SequenceCompleted'; })
          .forEach(function (e) { seqDone[e.payload.sequenceId] = true; });
      });
      return {
        lvl: lvl, status: st,
        done: Object.keys(seqDone).length,
        total: lvl.sequences.length,
        runs: runs.length
      };
    });

    return U.card('Senaryo İlerlemem', {
      desc: 'Tamamlanan görev grubu sayısı SequenceCompleted event\'lerinden sayılır.'
    }, [
      h('div', { class: 'stack' }, rows.map(function (r) {
        const pctv = r.total ? (r.done / r.total) * 100 : 0;
        return h('div', null, [
          h('div', { style: 'display:flex;gap:8px;align-items:center;margin-bottom:6px' }, [
            h('a', { href: '#/employee/scenario/' + encodeURIComponent(r.lvl.emittedLevelId),
                     style: 'flex:1;font-weight:600;font-size:.85rem;text-decoration:none;color:var(--ink)',
                     text: r.lvl.name }),
            statusBadge(r.status)
          ]),
          h('div', { class: 'progress', role: 'img',
                     'aria-label': r.lvl.name + ': ' + r.done + '/' + r.total + ' görev grubu' }, [
            h('div', { class: 'progress__fill' + (r.done === r.total && r.total ? ' progress__fill--ok' : ''),
                       style: 'width:' + pctv + '%' })
          ]),
          h('div', { style: 'font-size:.72rem;color:var(--ink-3);margin-top:4px',
                     text: r.done + '/' + r.total + ' görev grubu · ' + r.runs + ' deneme' })
        ]);
      })),
      h('a', { class: 'btn btn--ghost btn--sm mt-4', href: '#/employee/scenarios',
               text: 'Tüm senaryolar' })
    ]);
  }

  function statusBadge(st) {
    const map = {
      completed:    ['ok', 'check'],
      in_progress:  ['info', 'clock'],
      retry:        ['warn', 'repeat'],
      not_started:  ['neutral', 'empty'],
      insufficient: ['neutral', 'info']
    };
    const m = map[st.code] || ['neutral', 'info'];
    const b = U.badge(m[0], st.label, m[1]);
    if (st.note) b.title = st.note;
    return b;
  }

  // -- Son hatalar ------------------------------------------------------------

  function recentMistakesCard(app, sum) {
    const mist = sum.events
      .filter(function (e) { return e.eventType === 'MistakeRecorded'; })
      .slice(-5).reverse();

    if (!mist.length) {
      return U.card('Son Hatalarım', {}, U.emptyState({
        inline: true, icon: 'check', tone: 'inline',
        title: 'Bu kapsamda hata kaydı yok',
        what: 'Seçilen aralıkta adınıza MistakeRecorded event\'i düşmemiş.',
        action: 'Tarih aralığını genişleterek geçmiş hatalarınıza bakabilirsiniz.'
      }));
    }

    return U.card('Son Hatalarım', { desc: 'En yeni 5 MistakeRecorded kaydı.' }, [
      h('div', { class: 'stack' }, mist.map(function (e) {
        const idx = D.actionIndex[e.payload.actionId];
        return h('div', { style: 'padding:10px;background:rgba(0,0,0,.2);border-radius:8px' }, [
          h('div', { style: 'display:flex;gap:8px;align-items:center;margin-bottom:4px' }, [
            U.badge('bad', U.TYPE_LABEL[e.payload.mistakeType] || e.payload.mistakeType, 'x'),
            h('span', { style: 'font-size:.72rem;color:var(--ink-3);margin-left:auto',
                        text: K.relativeDays(e.clientTimestamp) })
          ]),
          h('div', { style: 'font-size:.82rem;font-weight:600',
                     text: idx ? idx.action.name : e.payload.actionId }),
          h('div', { style: 'font-size:.72rem;color:var(--ink-3)',
                     text: idx ? (idx.level.name + ' › ' + idx.sequence.name)
                               : 'Adım katalogda bulunamadı' })
        ]);
      })),
      h('a', { class: 'btn btn--ghost btn--sm mt-4', href: '#/employee/mistakes',
               text: 'Tüm hatalarım' })
    ]);
  }

  // -- Trend kartı (küçük çoklu, sekmeli) -------------------------------------

  function trendCard(app, sum) {
    if (sum.runs.length < 2) {
      return U.card('Gelişim Trendim', {}, U.emptyState({
        inline: true, icon: 'chart', title: 'Karşılaştırma oluşturmak için en az iki deneme gerekir',
        what: 'Şu an ' + sum.runs.length + ' deneme kaydınız var; trend çizgisi için en az iki ' +
              'tamamlanmış deneme gerekiyor.',
        why: 'Sahte bir eğilim çizmemek için tek noktadan grafik üretilmiyor.',
        action: 'Aynı senaryoyu bir kez daha oynayın; ikinci denemeden sonra bu alan dolar.'
      }));
    }

    const runs = sum.runs;
    const labels = runs.map(function (r) { return 'D' + r.attemptNo + ' · ' + K.fmtShortDate(r.startedAt); });

    function series(fn, opts) {
      return runs.map(function (r, i) {
        const m = K.runMetrics(r);
        const v = fn(m, r);
        return { label: labels[i], value: v, eventCount: r.events.length };
      });
    }

    return U.card('Gelişim Trendim', {
      desc: 'Farklı ölçekteki metrikler aynı eksene sıkıştırılmaz — her biri kendi sekmesinde.',
      help: 'Her nokta türetilmiş bir denemedir (LevelStarted → LevelCompleted).'
    }, U.tabbedChart([
      { id: 'acc', label: 'Başarı', render: function () {
        return U.lineChart(series(function (m) { return m.accuracy.ok ? m.accuracy.value * 100 : null; }), {
          color: U.css('--cat-1'), yMin: 0, yMax: 100,
          fmt: function (v) { return v.toFixed(0) + '% doğruluk'; },
          fmtAxis: function (v) { return Math.round(v) + '%'; },
          summary: 'Denemelere göre doğru cevap oranı (%). ' +
                   'Kaynak: QuizAnswered.isCorrect.'
        });
      } },
      { id: 'time', label: 'Süre', render: function () {
        return U.lineChart(series(function (m) { return K.isValidDuration(m.totalTime) ? m.totalTime : null; }), {
          color: U.css('--cat-5'),
          fmt: function (v) { return K.fmtDuration(v); },
          fmtAxis: function (v) { return Math.round(v / 60) + 'dk'; },
          summary: 'Deneme başına toplam süre (saniye). Kaynak: LevelCompleted.timeSpent.'
        });
      } },
      { id: 'mist', label: 'Hata', render: function () {
        return U.lineChart(series(function (m) { return m.mistakes; }), {
          color: U.css('--cat-4'), yMin: 0,
          fmt: function (v) { return v + ' hata'; },
          summary: 'Deneme başına MistakeRecorded adedi. Düşmesi iyidir.'
        });
      } },
      { id: 'att', label: 'Deneme', render: function () {
        return U.lineChart(series(function (m) { return m.avgAttempts.ok ? m.avgAttempts.value : null; }), {
          color: U.css('--cat-2'), yMin: 1,
          fmt: function (v) { return v.toFixed(2) + ' ortalama deneme'; },
          fmtAxis: function (v) { return v.toFixed(1); },
          summary: 'Soru başına ortalama deneme sayısı. Kaynak: QuizAnswered.attempts.'
        });
      } }
    ]));
  }

  // ---------------------------------------------------------------------------
  // 2) SENARYOLARIM
  // ---------------------------------------------------------------------------

  function scenarios(app) {
    const sum = mySummary(app);
    const root = h('div');

    root.appendChild(h('div', { class: 'page-head' }, [
      h('div', { class: 'page-head__text' }, [
        h('h1', { text: 'Senaryolarım' }),
        h('p', { text: 'Thundershock eğitim senaryolarındaki durumunuz. ' +
                       'Tamamlanma yalnızca LevelCompleted event\'i ile belirlenir.' })
      ])
    ]));

    root.appendChild(h('div', { class: 'scenario-grid' }, D.content.levels.map(function (lvl) {
      const runs = sum.runs.filter(function (r) { return r.levelId === lvl.emittedLevelId; });
      const st = K.scenarioStatus(sum.runs, lvl.emittedLevelId);
      const evs = runs.reduce(function (a, r) { return a.concat(r.events); }, []);
      const acc = K.accuracy(evs);
      const mist = K.mistakeCount(evs);
      const lastRun = runs.length ? runs[runs.length - 1] : null;
      const bestScore = runs.reduce(function (b, r) {
        return (r.score !== null && (b === null || r.score > b)) ? r.score : b; }, null);

      const card = h('button', {
        class: 'scenario', type: 'button',
        'aria-label': lvl.name + ' — ' + st.label + '. Detayı görüntüle.',
        onClick: function () { location.hash = '#/employee/scenario/' + encodeURIComponent(lvl.emittedLevelId); }
      }, [
        h('div', { class: 'scenario__art' }, U.assetImg(lvl.icon, '', 62)),
        h('div', { class: 'scenario__body' }, [
          h('div', null, [
            h('div', { style: 'display:flex;gap:8px;align-items:flex-start' }, [
              h('div', { class: 'scenario__title', style: 'flex:1', text: lvl.name }),
              statusBadge(st)
            ]),
            h('div', { class: 'scenario__sub', text: lvl.subtitle })
          ]),
          h('dl', { class: 'metricrow' }, [
            h('div', null, [h('dt', { text: 'Doğruluk' }),
              h('dd', { text: acc.ok ? K.pct(acc, 0) : '—' })]),
            h('div', null, [h('dt', { text: 'Hata' }), h('dd', { text: String(mist) })]),
            h('div', null, [h('dt', { text: 'Deneme' }), h('dd', { text: String(runs.length) })])
          ]),
          h('div', { style: 'font-size:.74rem;color:var(--ink-3)' }, [
            h('div', { text: 'Son oynanma: ' + (lastRun ? K.fmtDate(lastRun.startedAt) : '—') }),
            h('div', { text: 'En iyi skor: ' + (bestScore !== null ? bestScore : '—') +
                             ' · Görev grubu: ' + lvl.sequences.length })
          ]),
          lvl.dataWarning ? h('div', { class: 'badge badge--warn', style: 'align-self:flex-start',
                                       title: lvl.dataWarning }, ['⚠ Veri kalitesi uyarısı']) : null
        ])
      ]);
      return card;
    })));

    root.appendChild(h('div', { class: 'mt-4' }, U.notice('info',
      '<b>Durum nasıl belirleniyor?</b> "Tamamlandı" yalnızca <code>LevelCompleted</code> ' +
      'event\'i geldiyse gösterilir. Son sorunun cevaplanmış olması tamamlanma sayılmaz. ' +
      '"Tekrar Öneriliyor", tamamlanmış ama son denemede doğruluğu %70\'in altında kalan ' +
      'senaryolar içindir — bu eşik bir prototip önerisidir, onaylanmış bir iş kuralı değildir.')));

    return root;
  }

  // ---------------------------------------------------------------------------
  // 3) SENARYO DETAY — yol haritası + action drawer
  // ---------------------------------------------------------------------------

  function scenarioDetail(app, levelId) {
    const lvl = levelOf(levelId);
    const root = h('div');

    if (!lvl) {
      return U.emptyState({
        icon: 'warn', tone: 'warn', title: 'Senaryo bulunamadı',
        what: '"' + levelId + '" kimliğine sahip bir senaryo içerik kataloğunda yok.',
        why: 'Telemetride görülen levelId, oyunun ScriptableObject içeriğiyle eşleşmiyor olabilir.',
        action: 'Senaryo listesine dönüp geçerli bir senaryo seçin.',
        cta: h('a', { class: 'btn btn--ghost', href: '#/employee/scenarios', text: 'Senaryolarım' })
      });
    }

    const sum = mySummary(app);
    const runs = sum.runs.filter(function (r) { return r.levelId === levelId; });

    root.appendChild(h('div', { class: 'page-head' }, [
      h('div', { class: 'page-head__text' }, [
        h('h1', { text: lvl.name }),
        h('p', { text: lvl.subtitle })
      ]),
      h('div', { class: 'page-head__actions' }, [
        h('a', { class: 'btn btn--ghost', href: '#/employee/scenarios', text: '‹ Senaryolarım' })
      ])
    ]));

    if (lvl.dataWarning) {
      root.appendChild(h('div', { style: 'margin-bottom:16px' },
        U.notice('warn', '<b>Veri kalitesi:</b> ' + U.esc(lvl.dataWarning))));
    }

    if (!runs.length) {
      root.appendChild(U.emptyState({
        icon: 'empty', title: 'Bu senaryoyu henüz oynamamışsınız',
        what: 'Bu senaryo için adınıza kayıtlı hiçbir event yok, bu yüzden adım adım ' +
              'yol haritası doldurulamıyor.',
        action: 'Uygulamada bu senaryoyu başlatın.'
      }));
      root.appendChild(h('div', { class: 'mt-5' },
        U.card('Senaryo Yapısı', { desc: 'İçerik kataloğundan — henüz veri yok.' },
          roadmap(lvl, null, app))));
      return root;
    }

    // Deneme seçici
    const selected = { run: runs[runs.length - 1] };
    const body = h('div');

    const picker = h('div', { class: 'field', style: 'max-width:22rem' }, [
      h('label', { for: 'run-pick', text: 'Görüntülenen deneme' }),
      h('select', {
        id: 'run-pick',
        onChange: function (e) {
          selected.run = runs[Number(e.target.value)];
          renderBody();
        }
      }, runs.map(function (r, i) {
        return h('option', {
          value: i, selected: i === runs.length - 1,
          text: 'Deneme #' + r.attemptNo + ' — ' + K.fmtDateTime(r.startedAt) +
                (r.completed ? '' : ' (tamamlanmadı)')
        });
      })),
      h('div', { class: 'hint', text:
        'Denemeler LevelStarted → LevelCompleted event çiftinden TÜRETİLİR; ' +
        'şemada sessionId alanı yoktur.' })
    ]);

    function renderBody() {
      U.clear(body);
      const r = selected.run;
      const m = K.runMetrics(r);

      body.appendChild(h('div', { class: 'grid grid--kpi' }, [
        m.accuracy.ok
          ? U.kpiCard({ label: 'Doğruluk', value: K.pct(m.accuracy, 0),
                        sub: m.accuracy.num + '/' + m.accuracy.den + ' soru' })
          : U.kpiCard({ label: 'Doğruluk', unavailable: 'Soru yok', reason: m.accuracy.reason }),
        U.kpiCard({ label: 'Süre', value: K.fmtDuration(m.totalTime),
                    sub: r.completed ? 'LevelCompleted.timeSpent' : 'ActionCompleted toplamı' }),
        U.kpiCard({ label: 'Hata', value: String(m.mistakes),
                    sub: Object.keys(m.mistakesByType).map(function (t) {
                      return (U.TYPE_LABEL[t] || t) + ': ' + m.mistakesByType[t]; }).join(' · ') || '—' }),
        U.kpiCard({ label: 'Skor', value: r.score !== null ? String(r.score) : '—',
                    sub: 'LevelData: maxScore 100 − hata×5', help:
                      'Skor oyun tarafından hesaplanıp LevelCompleted.score ile gönderilir.' })
      ]));

      body.appendChild(h('div', { class: 'mt-5' },
        U.card('Senaryo Yol Haritası', {
          desc: 'Level › Görev Grubu (Sequence) › Adım (Action). ' +
                'Bir adıma tıklayınca sağ panelde tüm detayı açılır.',
          aside: r.completed ? U.badge('ok', 'Tamamlandı', 'check')
                             : U.badge('warn', 'Tamamlanmadı', 'warn')
        }, roadmap(lvl, r, app))));

      // Karşılaştırma
      let prev = null;
      for (let i = runs.indexOf(r) - 1; i >= 0; i--) { prev = runs[i]; break; }
      body.appendChild(h('div', { class: 'mt-5' }, comparisonCard(prev, r)));
    }

    renderBody();
    root.appendChild(picker);
    root.appendChild(body);
    return root;
  }

  // -- Yol haritası -----------------------------------------------------------

  function roadmap(lvl, run, app) {
    // Adım durumunu bu denemedeki eventlerden çıkar
    const byAction = {};
    if (run) {
      run.events.forEach(function (e) {
        const aid = e.payload && e.payload.actionId;
        if (!aid) return;
        byAction[aid] = byAction[aid] || { quiz: [], mistakes: [], completed: null, drag: null };
        if (e.eventType === 'QuizAnswered') byAction[aid].quiz.push(e);
        else if (e.eventType === 'MistakeRecorded') byAction[aid].mistakes.push(e);
        else if (e.eventType === 'ActionCompleted') byAction[aid].completed = e;
        else if (e.eventType === 'DragDropAttempt') byAction[aid].drag = e;
      });
    }

    const seqCompleted = {};
    if (run) {
      run.events.filter(function (e) { return e.eventType === 'SequenceCompleted'; })
        .forEach(function (e) { seqCompleted[e.payload.sequenceId] = e; });
    }

    return h('div', { class: 'roadmap' }, lvl.sequences.map(function (seq) {
      const nodes = seq.actions.map(function (act, i) {
        const st = nodeState(byAction[act.id]);
        return h('button', {
          class: 'node node--' + st.cls, type: 'button', 'aria-pressed': 'false',
          'aria-label': (i + 1) + '. ' + act.name + ' — ' + st.label,
          onClick: function (e) {
            const all = e.currentTarget.closest('.roadmap').querySelectorAll('.node');
            Array.prototype.forEach.call(all, function (n) { n.setAttribute('aria-pressed', 'false'); });
            e.currentTarget.setAttribute('aria-pressed', 'true');
            openActionDrawer(lvl, seq, act, byAction[act.id], run);
          }
        }, [
          h('span', { class: 'node__idx', text: String(i + 1).padStart(2, '0') + ' · ' + act.type }),
          h('span', { class: 'node__name', text: act.name }),
          h('span', { class: 'node__state' }, [U.icon(st.icon, 11), st.label])
        ]);
      });

      return h('div', { class: 'roadmap__seq' }, [
        h('div', { class: 'roadmap__seqhead' }, [
          U.assetImg(seq.icon, '', 26),
          h('div', { style: 'flex:1' }, [
            h('div', { class: 'roadmap__seqname', text: seq.name }),
            h('div', { class: 'roadmap__seqid',
                       text: 'sequenceID: ' + seq.id +
                             (seq.rawName !== seq.name ? '  ·  asset adı: "' + seq.rawName + '"' : '') })
          ]),
          run ? (seqCompleted[seq.id]
            ? U.badge('ok', 'Tamamlandı', 'check')
            : U.badge('neutral', 'Kayıt yok', 'info')) : null
        ]),
        h('div', { class: 'nodes' }, nodes)
      ]);
    }));
  }

  function nodeState(d) {
    if (!d) return { cls: 'nodata', label: 'Veri yok', icon: 'info' };
    if (d.quiz.length) {
      const lastQ = d.quiz[d.quiz.length - 1];
      const anyWrong = d.quiz.some(function (e) { return !e.payload.isCorrect; });
      if (!lastQ.payload.isCorrect) return { cls: 'wrong', label: 'Yanlış', icon: 'x' };
      if (anyWrong) return { cls: 'retry', label: 'Tekrar denendi', icon: 'repeat' };
      return { cls: 'ok', label: 'Doğru', icon: 'check' };
    }
    if (d.mistakes.length) return { cls: 'mistake', label: 'Hata kaydı', icon: 'warn' };
    if (d.completed) return { cls: 'ok', label: 'Tamamlandı', icon: 'check' };
    return { cls: 'skipped', label: 'Atlandı', icon: 'minus' };
  }

  // -- Action detay drawer ----------------------------------------------------

  function openActionDrawer(lvl, seq, act, d, run) {
    const body = [];

    body.push(h('dl', { class: 'deflist' }, [
      h('dt', { text: 'Level' }), h('dd', { text: lvl.name }),
      h('dt', { text: 'Görev grubu' }), h('dd', { text: seq.name }),
      h('dt', { text: 'Adım' }), h('dd', { text: act.name }),
      h('dt', { text: 'Adım tipi' }), h('dd', { text: act.rawType + ' → telemetride "' + act.type + '"' })
    ]));

    if (!d) {
      body.push(h('div', { class: 'mt-4' }, U.emptyState({
        inline: true, icon: 'empty', title: 'Bu adım için kayıt yok',
        what: 'Seçili denemede bu adıma ait hiçbir event bulunamadı.',
        why: 'Adıma hiç ulaşılmamış olabilir, ya da oturum bu adımdan önce sonlanmış olabilir.',
        action: 'Başka bir deneme seçin veya senaryoyu tekrar oynayın.'
      })));
      U.openDrawer(act.name, body, seq.name);
      return;
    }

    // Quiz cevapları
    if (d.quiz.length) {
      const bank = D.quizBank[act.id];
      body.push(h('h3', { class: 'mt-4', style: 'margin-bottom:8px', text: 'Soru ve cevaplar' }));
      if (bank) body.push(h('p', { style: 'font-size:.86rem', text: bank.q }));
      else body.push(h('p', { class: 'card__desc',
        text: 'Bu quiz adımının soru metni ScriptableObject içinde boş bırakılmış.' }));

      d.quiz.forEach(function (e, i) {
        const p = e.payload;
        body.push(h('div', { class: 'answer-row mt-4' }, [
          h('div', { style: 'font-size:.74rem;color:var(--ink-3);font-weight:700' },
            'DENEME ' + p.attempts + (i === d.quiz.length - 1 ? ' (son)' : '')),
          h('div', { class: 'answer answer--' + (p.isCorrect ? 'given-ok' : 'given-wrong') }, [
            U.icon(p.isCorrect ? 'check' : 'x', 15),
            h('div', null, [
              h('span', { class: 'answer__label', text: 'Sizin cevabınız' }),
              h('span', { text: p.selectedAnswer })
            ])
          ]),
          !p.isCorrect ? h('div', { class: 'answer answer--correct' }, [
            U.icon('check', 15),
            h('div', null, [
              h('span', { class: 'answer__label', text: 'Doğru cevap' }),
              h('span', { text: p.correctAnswer })
            ])
          ]) : null,
          h('dl', { class: 'deflist', style: 'margin-top:8px' }, [
            h('dt', { text: 'Sonuç' }),
            h('dd', null, p.isCorrect ? U.badge('ok', 'Doğru', 'check') : U.badge('bad', 'Yanlış', 'x')),
            h('dt', { text: 'Deneme (attempts)' }), h('dd', { text: String(p.attempts) }),
            h('dt', { text: 'Süre (timeSpent)' }),
            h('dd', null, K.isValidDuration(p.timeSpent)
              ? h('span', { text: K.fmtDuration(p.timeSpent) })
              : U.badge('warn', 'Kayıt eksik', 'warn')),
            h('dt', { text: 'Zaman' }), h('dd', { text: K.fmtDateTime(e.clientTimestamp) })
          ])
        ]));
      });
    }

    // Sürükle-bırak denemeleri
    if (d.drag) {
      const p = d.drag.payload;
      body.push(h('h3', { class: 'mt-4', style: 'margin-bottom:8px', text: 'Sürükle-bırak denemeleri' }));
      body.push(h('dl', { class: 'deflist' }, [
        h('dt', { text: 'Hedef' }), h('dd', { text: p.targetObject }),
        h('dt', { text: 'Toplam deneme' }), h('dd', { text: String(p.attempts) })
      ]));
      body.push(h('div', { class: 'answer-row mt-4' }, (p.placements || []).map(function (pl, i) {
        return h('div', { class: 'answer answer--' + (pl.correct ? 'given-ok' : 'given-wrong') }, [
          U.icon(pl.correct ? 'check' : 'x', 15),
          h('div', null, [
            h('span', { class: 'answer__label', text: 'Deneme ' + (i + 1) }),
            h('span', { text: pl.item + ' → ' + pl.droppedOn })
          ])
        ]);
      })));
    }

    // Hata kayıtları
    if (d.mistakes.length) {
      body.push(h('h3', { class: 'mt-4', style: 'margin-bottom:8px', text: 'Hata kayıtları' }));
      body.push(h('div', { class: 'stack' }, d.mistakes.map(function (e) {
        return h('div', { style: 'padding:10px;background:var(--bad-bg);border:1px solid rgba(217,38,38,.35);border-radius:8px' }, [
          h('div', { style: 'display:flex;gap:8px;align-items:center;margin-bottom:6px' }, [
            U.badge('bad', U.TYPE_LABEL[e.payload.mistakeType] || e.payload.mistakeType, 'x'),
            h('span', { style: 'font-size:.72rem;color:var(--ink-2);margin-left:auto',
                        text: K.fmtDateTime(e.clientTimestamp) })
          ]),
          h('div', { style: 'font-size:.76rem;color:var(--ink-2)',
                     text: 'severity: ' + e.payload.severity + ' — bu değer bir önem derecesi ' +
                           'DEĞİLDİR; oyun kodu her hatada sabit 1 gönderir.' })
        ]);
      })));
    }

    // Önerilen sonraki adım — yalnızca veriden çıkarılabilen, uydurma neden yok
    const suggestion = suggest(d, act, seq, lvl);
    if (suggestion) {
      body.push(h('div', { class: 'mt-4' }, U.notice('info', suggestion)));
    }

    // Teknik detay (ham event) — varsayılan olarak KAPALI
    const raw = []
      .concat(d.quiz, d.mistakes, d.drag ? [d.drag] : [], d.completed ? [d.completed] : []);
    body.push(h('details', { class: 'tech' }, [
      h('summary', { text: 'Teknik Detay (ham event kaydı) — ' + raw.length + ' event' }),
      h('div', { class: 'tech__body' }, [
        h('p', { class: 'card__desc', text:
          'PlayFab WritePlayerEvent gövdesi. "_serverTimestamp" istemci şemasının parçası ' +
          'değildir; PlayFab tarafından eklenir.' }),
        h('pre', { class: 'json', text: JSON.stringify(raw, null, 2) })
      ])
    ]));

    U.openDrawer(act.name, body, lvl.name + ' › ' + seq.name);
  }

  function suggest(d, act, seq, lvl) {
    if (d.quiz.length) {
      const wrong = d.quiz.filter(function (e) { return !e.payload.isCorrect; }).length;
      if (wrong >= 2) {
        return '<b>Öneri:</b> Bu soruyu ' + wrong + ' kez yanlış cevapladınız. ' +
               '"' + U.esc(seq.name) + '" görev grubunu baştan oynayarak prosedür sırasını ' +
               'tekrar gözden geçirin.';
      }
      if (wrong === 1) {
        return '<b>Öneri:</b> İlk denemede yanlış cevap verdiniz, ikinci denemede doğru ' +
               'bildiniz. Doğru cevabı yukarıda görebilirsiniz.';
      }
    }
    if (d.drag && d.drag.payload.attempts > 2) {
      return '<b>Öneri:</b> Bu adımda ' + d.drag.payload.attempts + ' yerleştirme denemesi ' +
             'yaptınız. Doğru ekipman/alet eşleşmesini tekrar çalışmanız faydalı olabilir.';
    }
    if (lvl.criticalNote && d.mistakes.length) {
      return '<b>Senaryo notu:</b> ' + U.esc(lvl.criticalNote);
    }
    return null;
  }

  // -- Karşılaştırma kartı ----------------------------------------------------

  function comparisonCard(prev, last) {
    const cmp = K.compareRuns(prev, last);

    if (!cmp.ok) {
      return U.card('Deneme Karşılaştırması', {}, U.emptyState({
        inline: true, icon: 'chart', title: 'Karşılaştırma oluşturmak için en az iki deneme gerekir',
        what: 'Bu senaryo için tek deneme kaydı var.',
        why: 'Tek noktadan sahte bir gelişim eğilimi üretilmiyor.',
        action: 'Senaryoyu tekrar oynadığınızda bu alan otomatik dolar.'
      }));
    }

    function row(label, prevV, lastV, delta, opts) {
      opts = opts || {};
      return h('tr', null, [
        h('td', { text: label }),
        h('td', { class: 'num', text: prevV }),
        h('td', { class: 'num', text: lastV }),
        h('td', { class: 'num' }, U.deltaBadge(delta, opts))
      ]);
    }

    const a = cmp.prev, b = cmp.last;

    const table = h('div', { class: 'tablewrap' }, h('table', { class: 'data' }, [
      h('thead', null, h('tr', null, [
        h('th', { text: 'Metrik' }), h('th', { class: 'num', text: 'Önceki' }),
        h('th', { class: 'num', text: 'Son' }), h('th', { class: 'num', text: 'Değişim' })
      ])),
      h('tbody', null, [
        row('Doğruluk',
          a.accuracy.ok ? K.pct(a.accuracy, 0) : '—',
          b.accuracy.ok ? K.pct(b.accuracy, 0) : '—',
          cmp.deltas.accuracy !== null ? cmp.deltas.accuracy * 100 : null,
          { fmt: function (v) { return (v > 0 ? '+' : '') + v.toFixed(0) + ' puan'; } }),
        row('Toplam süre', K.fmtDuration(a.totalTime), K.fmtDuration(b.totalTime),
          cmp.deltas.totalTime !== null ? -cmp.deltas.totalTime : null,
          { fmt: function (v) { return (v > 0 ? '−' : '+') + K.fmtDuration(Math.abs(v)); } }),
        row('Hata sayısı', String(a.mistakes), String(b.mistakes), cmp.deltas.mistakes,
          { invert: true, fmt: function (v) { return (v > 0 ? '+' : '') + v; } }),
        row('Ortalama deneme',
          a.avgAttempts.ok ? a.avgAttempts.value.toFixed(2) : '—',
          b.avgAttempts.ok ? b.avgAttempts.value.toFixed(2) : '—',
          cmp.deltas.avgAttempts, { invert: true, eps: 0.01,
            fmt: function (v) { return (v > 0 ? '+' : '') + v.toFixed(2); } }),
        row('Skor', a.score !== null ? String(a.score) : '—',
          b.score !== null ? String(b.score) : '—', cmp.deltas.score,
          { fmt: function (v) { return (v > 0 ? '+' : '') + v; } })
      ])
    ]));

    function list(title, keys, tone, iconName) {
      return h('div', null, [
        // Kart başlığı h2 olduğu için alt başlıklar h3 (atlama yok).
        h('h3', { style: 'margin-bottom:8px', text: title + ' (' + keys.length + ')' }),
        keys.length
          ? h('div', { class: 'stack' }, keys.map(function (k) {
              const parts = k.split('::');
              const idx = D.actionIndex[parts[0]];
              return h('div', { style: 'display:flex;gap:8px;align-items:center;font-size:.8rem' }, [
                U.badge(tone, U.TYPE_LABEL[parts[1]] || parts[1], iconName),
                h('span', { text: idx ? idx.action.name : parts[0] })
              ]);
            }))
          : h('p', { class: 'card__desc', text: 'Yok.' })
      ]);
    }

    return U.card('Son Deneme ↔ Önceki Deneme', {
      desc: 'Metrikler tek bir "gelişim skoru" içinde birleştirilmez — ' +
            'doğruluk artarken hata sayısı da artmış olabilir.',
      help: 'Karşılaştırma aynı senaryonun ardışık iki denemesi arasında yapılır.'
    }, [
      table,
      mixedSignalNotice(cmp),
      h('div', { class: 'grid grid--3 mt-5' }, [
        list('Tekrar eden hatalar', cmp.repeatedMistakes, 'bad', 'repeat'),
        list('Çözülen hatalar', cmp.resolvedMistakes, 'ok', 'check'),
        list('Yeni ortaya çıkan hatalar', cmp.newMistakes, 'warn', 'warn')
      ])
    ]);
  }

  /** Doğruluk artıp hata da arttıysa "gelişti" etiketini engelleyen uyarı. */
  function mixedSignalNotice(cmp) {
    const accUp = cmp.deltas.accuracy !== null && cmp.deltas.accuracy > 0;
    const mistUp = cmp.deltas.mistakes !== null && cmp.deltas.mistakes > 0;
    if (accUp && mistUp) {
      return h('div', { class: 'mt-4' }, U.notice('warn',
        '<b>Karışık sinyal.</b> Doğruluk oranınız yükselmiş ancak toplam hata sayınız da ' +
        'artmış. Bu deneme tek başına "gelişme" olarak etiketlenmiyor — iki metriği ' +
        'ayrı ayrı değerlendirin.'));
    }
    if (cmp.newMistakes.length) {
      return h('div', { class: 'mt-4' }, U.notice('warn',
        '<b>' + cmp.newMistakes.length + ' yeni hata türü</b> bu denemede ilk kez ortaya çıktı. ' +
        'Genel skor iyileşmiş olsa bile bu adımları gözden geçirin.'));
    }
    return null;
  }

  // ---------------------------------------------------------------------------
  // 4) PERFORMANSIM
  // ---------------------------------------------------------------------------

  function performance(app) {
    const sum = mySummary(app);
    const root = h('div');

    root.appendChild(h('div', { class: 'page-head' }, [
      h('div', { class: 'page-head__text' }, [
        h('h1', { text: 'Performansım' }),
        h('p', { text: 'Deneme bazlı gelişiminiz ve senaryo kırılımınız.' })
      ])
    ]));

    if (!sum.runs.length) {
      root.appendChild(U.emptyState({
        icon: 'chart', title: 'Performans verisi yok',
        what: 'Henüz hiçbir senaryo denemesi kaydedilmemiş.',
        action: 'Bir senaryo oynadıktan sonra bu sayfa dolar.'
      }));
      return root;
    }

    root.appendChild(trendCard(app, sum));

    // Senaryo bazlı kırılım
    const perLevel = D.content.levels.map(function (lvl) {
      const runs = sum.runs.filter(function (r) { return r.levelId === lvl.emittedLevelId; });
      const evs = runs.reduce(function (a, r) { return a.concat(r.events); }, []);
      const acc = K.accuracy(evs);
      return {
        name: lvl.name, levelId: lvl.emittedLevelId, runs: runs.length,
        acc: acc, mistakes: K.mistakeCount(evs),
        time: K.avgQuizTime(evs)
      };
    }).filter(function (r) { return r.runs > 0; });

    root.appendChild(h('div', { class: 'mt-5' },
      U.card('Senaryo Bazlı Kırılım', { desc: 'Yalnızca oynadığınız senaryolar listelenir.' },
        U.barList(perLevel.map(function (r, i) {
          return {
            label: r.name,
            value: r.acc.ok ? r.acc.value * 100 : 0,
            display: r.acc.ok ? K.pct(r.acc, 0) : 'oran yok',
            color: U.css('--cat-' + ((i % 5) + 1)),
            sub: r.runs + ' deneme · ' + r.mistakes + ' hata · ' +
                 (r.time.ok ? 'ort. ' + K.fmtDuration(r.time.value) : 'süre verisi eksik'),
            onClick: function () {
              location.hash = '#/employee/scenario/' + encodeURIComponent(r.levelId);
            }
          };
        }), { max: 100, emptyTitle: 'Oynanmış senaryo yok' }))));

    // Level bazlı deneme tablosu
    root.appendChild(h('div', { class: 'mt-5' },
      U.card('Tüm Denemelerim', { desc: 'Kronolojik deneme kaydı.' },
        U.dataTable({
          columns: [
            { key: 'date', label: 'Tarih', value: function (r) { return r._ts; },
              render: function (r) { return K.fmtDateTime(r._ts); } },
            { key: 'level', label: 'Senaryo',
              render: function (r) {
                return h('button', { class: 'rowlink', type: 'button', text: r.level,
                  onClick: function () {
                    location.hash = '#/employee/scenario/' + encodeURIComponent(r.levelId); } });
              } },
            { key: 'attempt', label: 'Deneme', num: true },
            { key: 'accuracy', label: 'Doğruluk', num: true,
              value: function (r) { return r._acc; },
              render: function (r) { return r.accuracy; } },
            { key: 'mistakes', label: 'Hata', num: true },
            { key: 'time', label: 'Süre', num: true, value: function (r) { return r._time; },
              render: function (r) { return K.fmtDuration(r._time); } },
            { key: 'status', label: 'Durum', sortable: false,
              render: function (r) {
                return r.completed ? U.badge('ok', 'Tamamlandı', 'check')
                                   : U.badge('warn', 'Tamamlanmadı', 'warn'); } }
          ],
          rows: sum.runs.slice().reverse().map(function (r) {
            const m = K.runMetrics(r);
            return {
              _ts: r.startedAt, level: levelName(r.levelId), levelId: r.levelId,
              attempt: r.attemptNo, _acc: m.accuracy.ok ? m.accuracy.value : null,
              accuracy: m.accuracy.ok ? K.pct(m.accuracy, 0) : '—',
              mistakes: m.mistakes, _time: m.totalTime, completed: r.completed
            };
          }),
          defaultSort: 'date', defaultDir: 'desc', pageSize: 10,
          emptyTitle: 'Deneme kaydı yok'
        }))));

    return root;
  }

  // ---------------------------------------------------------------------------
  // 5) HATALARIM
  // ---------------------------------------------------------------------------

  function mistakes(app) {
    const root = h('div');
    const sum = mySummary(app);

    root.appendChild(h('div', { class: 'page-head' }, [
      h('div', { class: 'page-head__text' }, [
        h('h1', { text: 'Hatalarım' }),
        h('p', { text: 'MistakeRecorded event kayıtlarınız — senaryo, adım ve hata türü kırılımında.' })
      ])
    ]));

    // Yerel filtreler
    const local = { level: '', type: '', repeatedOnly: false };
    const listWrap = h('div');

    const filterBar = h('div', { class: 'filterbar' }, [
      h('div', { class: 'filterbar__row' }, [
        h('div', { class: 'filterbar__field' }, [
          h('label', { for: 'mf-level', text: 'Senaryo' }),
          h('select', { id: 'mf-level', onChange: function (e) { local.level = e.target.value; render(); } },
            [h('option', { value: '', text: 'Tümü' })].concat(D.content.levels.map(function (l) {
              return h('option', { value: l.emittedLevelId, text: l.name });
            })))
        ]),
        h('div', { class: 'filterbar__field' }, [
          h('label', { for: 'mf-type', text: 'Hata türü' }),
          h('select', { id: 'mf-type', onChange: function (e) { local.type = e.target.value; render(); } }, [
            h('option', { value: '', text: 'Tümü' }),
            h('option', { value: 'wrong_answer', text: 'Yanlış cevap' }),
            h('option', { value: 'wrong_drop', text: 'Yanlış yerleştirme' })
          ])
        ]),
        h('div', { class: 'filterbar__field filterbar__field--off' }, [
          h('label', { for: 'mf-sev', text: 'Önem (severity)' }),
          h('select', { id: 'mf-sev', disabled: true, 'aria-describedby': 'mf-sev-note' },
            [h('option', { text: 'Ölçek tanımsız' })]),
          h('div', { class: 'hint', id: 'mf-sev-note', text: 'Kodda severity sabit 1' })
        ]),
        h('div', { class: 'filterbar__field' }, [
          h('label', { for: 'mf-rep', text: 'Yalnızca tekrar edenler' }),
          h('select', { id: 'mf-rep', onChange: function (e) {
            local.repeatedOnly = e.target.value === '1'; render(); } }, [
            h('option', { value: '', text: 'Hayır' }),
            h('option', { value: '1', text: 'Evet — 2+ kez yapılanlar' })
          ])
        ])
      ])
    ]);

    function render() {
      U.clear(listWrap);

      let mist = sum.events.filter(function (e) { return e.eventType === 'MistakeRecorded'; });
      if (local.level) mist = mist.filter(function (e) { return K.levelIdOf(e) === local.level; });
      if (local.type) mist = mist.filter(function (e) { return e.payload.mistakeType === local.type; });

      // actionId + mistakeType bazında grupla
      const groups = {};
      mist.forEach(function (e) {
        const k = e.payload.actionId + '::' + e.payload.mistakeType;
        groups[k] = groups[k] || { key: k, actionId: e.payload.actionId,
                                   type: e.payload.mistakeType, count: 0, last: null, events: [] };
        groups[k].count += 1;
        groups[k].events.push(e);
        if (!groups[k].last || e.clientTimestamp > groups[k].last) groups[k].last = e.clientTimestamp;
      });

      let rows = Object.keys(groups).map(function (k) { return groups[k]; });
      if (local.repeatedOnly) rows = rows.filter(function (r) { return r.count >= 2; });
      rows.sort(function (a, b) { return b.count - a.count; });

      if (!rows.length) {
        listWrap.appendChild(U.emptyState({
          icon: mist.length === 0 && !local.level && !local.type ? 'check' : 'search',
          title: (local.level || local.type || local.repeatedOnly)
            ? 'Filtre sonucu boş' : 'Hata kaydınız yok',
          what: (local.level || local.type || local.repeatedOnly)
            ? 'Seçtiğiniz filtrelerle eşleşen MistakeRecorded kaydı bulunamadı.'
            : 'Adınıza kayıtlı hiç MistakeRecorded event\'i yok.',
          why: (local.level || local.type || local.repeatedOnly)
            ? null : 'Henüz hiç oynamamış ya da hiç hata yapmamış olabilirsiniz.',
          action: (local.level || local.type || local.repeatedOnly)
            ? 'Filtreleri "Tümü" yapıp tekrar deneyin.' : null
        }));
        return;
      }

      listWrap.appendChild(h('div', { class: 'grid grid--2' }, rows.map(function (r) {
        const idx = D.actionIndex[r.actionId];
        return h('div', { class: 'card' }, [
          h('div', { style: 'display:flex;gap:8px;align-items:flex-start;margin-bottom:10px' }, [
            U.badge('bad', U.TYPE_LABEL[r.type] || r.type, 'x'),
            r.count >= 2 ? U.badge('warn', r.count + ' kez tekrarlandı', 'repeat') : null,
            h('span', { style: 'margin-left:auto;font-size:.72rem;color:var(--ink-3)',
                        text: K.relativeDays(r.last) })
          ]),
          h('h2', { class: 'card-title--sm', style: 'margin-bottom:4px',
                    text: idx ? idx.action.name : r.actionId }),
          h('p', { class: 'card__desc', text: idx
            ? (idx.level.name + ' › ' + idx.sequence.name)
            : 'Bu actionId içerik kataloğunda bulunamadı — hata senaryoya bağlanamıyor.' }),
          h('dl', { class: 'deflist', style: 'margin-top:12px' }, [
            h('dt', { text: 'Ne oldu' }),
            h('dd', { text: r.type === 'wrong_answer'
              ? 'Bilgi sorusuna yanlış cevap verildi'
              : 'Ekipman/alet yanlış bölgeye bırakıldı' }),
            h('dt', { text: 'Nerede' }),
            h('dd', { text: idx ? (idx.sequence.name + ' · adım: ' + idx.action.name) : '—' }),
            h('dt', { text: 'Kaç kez' }), h('dd', { text: r.count + ' kez' }),
            h('dt', { text: 'Son yapılma' }), h('dd', { text: K.fmtDateTime(r.last) })
          ]),
          idx ? h('a', { class: 'btn btn--ghost btn--sm mt-4',
                         href: '#/employee/scenario/' + encodeURIComponent(idx.level.emittedLevelId),
                         text: 'Senaryo detayında gör' }) : null
        ]);
      })));
    }

    render();
    root.appendChild(filterBar);
    root.appendChild(h('div', { class: 'mt-4' }, U.notice('info',
      '<b>Neden "bu hatayı şu yüzden yaptınız" yazmıyoruz?</b> ' +
      'Telemetride hatanın nedenini gösteren bir alan yok. Portal yalnızca ne olduğunu, ' +
      'nerede olduğunu ve kaç kez tekrarlandığını gösterir; neden üretmez.')));
    root.appendChild(h('div', { class: 'mt-4' }, listWrap));

    return root;
  }

  // ---------------------------------------------------------------------------
  // 6) GELİŞİMİM  (rozet sistemi projede TANIMLI DEĞİL → konsept olarak işaretli)
  // ---------------------------------------------------------------------------

  function progress(app) {
    const sum = mySummary(app);
    const root = h('div');

    root.appendChild(h('div', { class: 'page-head' }, [
      h('div', { class: 'page-head__text' }, [
        h('h1', { text: 'Gelişimim' }),
        h('p', { text: 'Ölçülebilir kilometre taşlarınız ve önerilen çalışma alanları.' })
      ])
    ]));

    root.appendChild(U.notice('warn',
      '<b>Rozet sistemi projede tanımlı değil.</b> Repoda bir başarım/rozet ScriptableObject\'i ' +
      'veya kazanma kuralı bulunmuyor. Aşağıdaki kartlar KONSEPT seviyesindedir; her biri ' +
      'yalnızca gerçekten var olan event alanlarından hesaplanır. Kazanma eşikleri ' +
      'onaylanmış iş kuralı değildir.'));

    if (!sum.runs.length) {
      root.appendChild(h('div', { class: 'mt-4' }, U.emptyState({
        icon: 'trophy', title: 'Henüz kilometre taşı yok',
        what: 'Kilometre taşları oyun kayıtlarınızdan hesaplanır; henüz kayıt yok.',
        action: 'İlk senaryonuzu tamamlayın.'
      })));
      return root;
    }

    // Gerçekten hesaplanabilir kilometre taşları
    const completedRuns = sum.runs.filter(function (r) { return r.completed; });
    const flawless = completedRuns.filter(function (r) { return K.runMetrics(r).mistakes === 0; });
    const distinctLevels = sum.levelsPlayed.length;
    const cmp = sum.comparison;
    const improvedAcc = cmp.ok && cmp.deltas.accuracy !== null && cmp.deltas.accuracy > 0;
    const improvedTime = cmp.ok && cmp.deltas.totalTime !== null && cmp.deltas.totalTime < 0;
    const fixedMistakes = cmp.ok ? cmp.resolvedMistakes.length : 0;

    const items = [
      { title: 'Hatasız Senaryo', done: flawless.length > 0,
        detail: flawless.length + ' tamamlanmış denemede hiç MistakeRecorded yok.',
        basis: 'MistakeRecorded sayısı = 0 olan tamamlanmış deneme.' },
      { title: 'Senaryo Tamamlama', done: completedRuns.length >= 3,
        detail: completedRuns.length + ' / 3 tamamlanmış deneme.',
        basis: 'LevelCompleted event sayısı.' },
      { title: 'Çok Senaryolu Deneyim', done: distinctLevels >= 2,
        detail: distinctLevels + ' farklı senaryo oynandı.',
        basis: 'Benzersiz levelId sayısı.' },
      { title: 'Doğrulukta İyileşme', done: improvedAcc,
        detail: cmp.ok && cmp.deltas.accuracy !== null
          ? 'Son denemede doğruluk değişimi: ' + (cmp.deltas.accuracy * 100).toFixed(0) + ' puan.'
          : 'Karşılaştırma için en az iki deneme gerekir.',
        basis: 'Ardışık iki denemede QuizAnswered doğruluk oranı farkı.' },
      { title: 'Süre İyileştirme', done: improvedTime,
        detail: cmp.ok && cmp.deltas.totalTime !== null
          ? 'Son denemede süre değişimi: ' + (cmp.deltas.totalTime > 0 ? '+' : '') +
            K.fmtDuration(Math.abs(cmp.deltas.totalTime))
          : 'Karşılaştırma için en az iki deneme gerekir.',
        basis: 'LevelCompleted.timeSpent farkı.' },
      { title: 'Tekrar Eden Hatayı Düzeltme', done: fixedMistakes > 0,
        detail: fixedMistakes + ' hata önceki denemede vardı, son denemede tekrarlanmadı.',
        basis: 'actionId + mistakeType kümelerinin farkı.' },
      { title: 'Kritik Hatasız Tamamlama', done: null,
        detail: 'Hesaplanamıyor — kritik severity tanımı yok.',
        basis: 'severity ölçeği tanımlanmadan bu kural uygulanamaz.' }
    ];

    root.appendChild(h('div', { class: 'grid grid--3 mt-5' }, items.map(function (it) {
      return h('div', { class: 'card' }, [
        h('div', { style: 'display:flex;gap:8px;align-items:center;margin-bottom:10px' }, [
          it.done === null ? U.badge('neutral', 'Uygulanamaz', 'info')
            : it.done ? U.badge('ok', 'Kazanıldı', 'check')
                      : U.badge('neutral', 'Henüz değil', 'clock'),
          U.badge('proto', 'Konsept', 'info')
        ]),
        h('h2', { class: 'card-title--sm', style: 'margin-bottom:6px', text: it.title }),
        h('p', { style: 'font-size:.82rem;color:var(--ink-2);margin-bottom:8px', text: it.detail }),
        h('p', { class: 'card__desc', text: 'Hesap temeli: ' + it.basis })
      ]);
    })));

    return root;
  }

  // ---------------------------------------------------------------------------
  // 7) PROFİL
  // ---------------------------------------------------------------------------

  function profile(app) {
    const sum = mySummary(app);
    const e = sum.employee;
    const root = h('div');

    root.appendChild(h('div', { class: 'page-head' }, [
      h('div', { class: 'page-head__text' }, [
        h('h1', { text: 'Profil' }),
        h('p', { text: 'Hesap bilgileriniz ve veri erişim kapsamınız.' })
      ])
    ]));

    root.appendChild(h('div', { class: 'grid grid--2' }, [
      U.card('Hesap', { desc: 'Kaynak: PlayFab Title Data whitelist (PlayerEntry).' }, [
        h('dl', { class: 'deflist' }, [
          h('dt', { text: 'Çalışan ID' }), h('dd', null, h('code', { text: e.id })),
          h('dt', { text: 'Görünen ad' }), h('dd', { text: e.name }),
          h('dt', { text: 'Rol' }), h('dd', { text: e.role }),
          h('dt', { text: 'Toplam event' }), h('dd', { text: String(sum.eventCount) }),
          h('dt', { text: 'Son aktivite' }), h('dd', { text: K.fmtDateTime(sum.lastActivity) })
        ]),
        h('div', { class: 'mt-4' }, U.notice('info',
          '<b>Rol alanı telemetriye yazılmıyor.</b> <code>role</code> whitelist kaydında var ' +
          '(PlayFabDataManager.PlayerEntry) ancak hiçbir event payload\'ında gönderilmiyor. ' +
          'Bu yüzden rol bazlı filtreleme şu an üretilemiyor.'))
      ]),
      U.card('Veri Erişimi', { desc: 'Bu portalda ne görebilirsiniz?' }, [
        h('ul', { style: 'margin:0;padding-left:1.15rem;font-size:.85rem;color:var(--ink-2);line-height:1.9' }, [
          h('li', { text: 'Yalnızca kendi eğitim kayıtlarınızı görürsünüz.' }),
          h('li', { text: 'Diğer çalışanların verisi bu portalda hiç yüklenmez.' }),
          h('li', { text: 'Kurum ortalaması, ancak yeterli veri varsa ve yönetici paylaşırsa gösterilir.' })
        ]),
        h('div', { class: 'mt-4' }, U.notice('warn',
          '<b>Bu bir prototiptir.</b> Gerçek kimlik doğrulama yoktur; rol ayrımı yalnızca ' +
          'istemci tarafında temsil edilir. Üretimde sunucu tarafı yetkilendirme ' +
          '(RBAC + employee data isolation) zorunludur.'))
      ])
    ]));

    return root;
  }

  // ---------------------------------------------------------------------------
  window.TS_EMPLOYEE = {
    dashboard: dashboard,
    scenarios: scenarios,
    scenarioDetail: scenarioDetail,
    performance: performance,
    mistakes: mistakes,
    progress: progress,
    profile: profile,
    statusBadge: statusBadge
  };
})();
