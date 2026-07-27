/* =============================================================================
   THUNDERSHOCK KPI PORTALI — KPI HESAPLAMA MODÜLÜ
   =============================================================================
   Tüm metrikler burada hesaplanır. Görünüm katmanı asla ham event üzerinde
   aritmetik yapmaz.

   TEMEL KURAL — "Payda yoksa oran yok":
   Her oran fonksiyonu { value, num, den, ok, reason } döndürür.
   `ok === false` ise arayüz yüzde göstermez; bunun yerine durum etiketi basar.
   Bu, ham sayıyı oran gibi sunmayı yapısal olarak imkânsız kılar.

   SEVERITY HAKKINDA — ÖNEMLİ:
   Repoda MistakeRecorded yalnızca iki yerden çağrılıyor ve İKİSİNDE DE
   severity SABİT 1 olarak gönderiliyor:
     SequenceManager.cs:671  → LogMistakeRecorded(actionID, "wrong_answer", 1)
     UIDropZone.cs:179       → LogMistakeRecorded(_actionID, "wrong_drop", 1)
   Yani projede tanımlı bir severity SKALASI YOK. Bu modül severity=1'i
   "kritik" saymaz; severity değerlerini ham kategori olarak sayar ve
   kritik hata oranı hesaplamayı REDDEDER (bkz. criticalMistakeRate).
============================================================================= */

(function () {
  'use strict';

  const D = window.TS_DATA;

  // ---------------------------------------------------------------------------
  // Oran yardımcıları
  // ---------------------------------------------------------------------------

  /**
   * Güvenli oran. Payda 0 veya tanımsızsa ok:false döner ve arayüz yüzde basmaz.
   * @returns {{value:number|null,num:number,den:number,ok:boolean,reason:string|null}}
   */
  function ratio(num, den, reason) {
    if (!den || den <= 0) {
      return { value: null, num: num || 0, den: den || 0, ok: false, reason: reason || 'Payda bulunamadı' };
    }
    return { value: num / den, num: num, den: den, ok: true, reason: null };
  }

  /** Oranı yüzde metnine çevirir; ok:false ise durum etiketi döner. */
  function pct(r, digits) {
    if (!r || !r.ok) return null;
    return (r.value * 100).toFixed(digits === undefined ? 0 : digits) + '%';
  }

  /**
   * Hata oranı gibi 1'i AŞABİLEN oranlar için biçimlendirme.
   *
   * Neden gerekli: tek bir action için `MistakeRecorded / ActionCompleted`
   * 1'den büyük olabilir — aynı adım bir kez tamamlanırken üç kez yanlış
   * denenmiş olabilir. Bunu "%300" diye göstermek bozuk bir yüzde gibi okunur.
   * 1'i aşan değerler bu yüzden "3.0× hata/adım" biçiminde gösterilir.
   */
  function rateLabel(r, digits) {
    if (!r || !r.ok) return null;
    if (r.value <= 1) return (r.value * 100).toFixed(digits === undefined ? 0 : digits) + '%';
    return r.value.toFixed(1) + '× hata/adım';
  }

  /** timeSpent / duration alanları null, undefined, NaN veya negatif olabilir. */
  function isValidDuration(v) {
    return typeof v === 'number' && isFinite(v) && v >= 0;
  }

  /**
   * Ortalama — geçersiz değerleri SESSİZCE 0 saymaz, dışarıda bırakır ve
   * kaç kaydın atıldığını raporlar.
   */
  function mean(values) {
    const valid = values.filter(isValidDuration);
    if (valid.length === 0) {
      return { value: null, n: 0, dropped: values.length, ok: false, reason: 'Geçerli değer yok' };
    }
    const sum = valid.reduce(function (a, b) { return a + b; }, 0);
    return {
      value: sum / valid.length,
      n: valid.length,
      dropped: values.length - valid.length,
      ok: true,
      reason: null
    };
  }

  function sum(values) {
    return values.filter(isValidDuration).reduce(function (a, b) { return a + b; }, 0);
  }

  // ---------------------------------------------------------------------------
  // Filtreleme
  // ---------------------------------------------------------------------------

  /**
   * @param {Object} f  { from, to, levelId, sequenceId, employeeId, mistakeType, eventType }
   */
  function filterEvents(events, f) {
    f = f || {};
    return events.filter(function (e) {
      if (f.employeeId && e.employeeId !== f.employeeId) return false;
      if (f.eventType && e.eventType !== f.eventType) return false;
      if (f.from && e.clientTimestamp < f.from) return false;
      if (f.to && e.clientTimestamp > f.to) return false;

      if (f.levelId) {
        // MistakeRecorded'da levelId YOK — actionIndex üzerinden türetilir.
        const lvl = levelIdOf(e);
        if (lvl !== f.levelId) return false;
      }
      if (f.sequenceId) {
        const seq = sequenceIdOf(e);
        if (seq !== f.sequenceId) return false;
      }
      if (f.mistakeType && e.eventType === 'MistakeRecorded' &&
          e.payload.mistakeType !== f.mistakeType) return false;
      return true;
    });
  }

  /**
   * Bir event'in ait olduğu levelId. MistakeRecorded eventinde bu alan
   * BULUNMADIĞI için actionId → içerik kataloğu araması yapılır.
   * Katalogda yoksa null döner (arayüz "İlişkilendirilemedi" gösterir).
   */
  function levelIdOf(e) {
    if (e.payload && e.payload.levelId) return e.payload.levelId;
    const aid = e.payload && e.payload.actionId;
    if (aid && D.actionIndex[aid]) return D.actionIndex[aid].level.emittedLevelId;
    return null;
  }

  function sequenceIdOf(e) {
    if (e.payload && e.payload.sequenceId) return e.payload.sequenceId;
    const aid = e.payload && e.payload.actionId;
    if (aid && D.actionIndex[aid]) return D.actionIndex[aid].sequence.id;
    return null;
  }

  // ---------------------------------------------------------------------------
  // DENEME (RUN) TÜRETME
  // ---------------------------------------------------------------------------
  //
  // Şemada sessionId / attemptId ALANI YOK. Bir "deneme", aynı employeeId ve
  // levelId için LevelStarted ile onu izleyen LevelCompleted (veya SessionEnded)
  // arasındaki event bloğu olarak TÜRETİLİR.
  //
  // BU TÜRETME KIRILGANDIR:
  //  - Uygulama çökerse LevelCompleted hiç gelmez, deneme "tamamlanmamış" kalır.
  //  - Aynı anda iki cihazdan oynanırsa bloklar iç içe geçer.
  //  - LevelStarted kaybolursa event'ler hiçbir denemeye bağlanamaz.
  // Gerçek üründe sunucu tarafında bir sessionId üretilmelidir (README §Backend).

  function deriveRuns(events, employeeId) {
    const evs = events
      .filter(function (e) { return !employeeId || e.employeeId === employeeId; })
      .slice()
      .sort(function (a, b) { return a.clientTimestamp < b.clientTimestamp ? -1 : 1; });

    const open = {};   // key: employeeId|levelId
    const runs = [];
    let counter = 0;

    function keyOf(empId, lvlId) { return empId + '|' + lvlId; }

    evs.forEach(function (e) {
      const lvl = levelIdOf(e);

      if (e.eventType === 'LevelStarted') {
        counter += 1;
        const run = {
          runId: 'run-' + counter,
          derived: true,                 // ← türetilmiş, şemadan gelmiyor
          employeeId: e.employeeId,
          levelId: e.payload.levelId,
          startedAt: e.clientTimestamp,
          endedAt: null,
          completed: false,
          score: null,
          reportedMistakes: null,
          reportedTimeSpent: null,
          completionRate: null,
          events: [e]
        };
        open[keyOf(e.employeeId, e.payload.levelId)] = run;
        runs.push(run);
        return;
      }

      const run = lvl ? open[keyOf(e.employeeId, lvl)] : null;
      if (run) {
        run.events.push(e);
        run.endedAt = e.clientTimestamp;
      } else if (e.eventType === 'MistakeRecorded') {
        // Hiçbir açık denemeye bağlanamayan hata → "yetim" kayıt.
        runs.orphanMistakes = runs.orphanMistakes || [];
        runs.orphanMistakes.push(e);
      }

      if (e.eventType === 'LevelCompleted' && run) {
        run.completed = true;
        run.score = e.payload.score;
        run.reportedMistakes = e.payload.mistakes;
        run.reportedTimeSpent = e.payload.timeSpent;
        run.completionRate = e.payload.completionRate;
      }
      if (e.eventType === 'SessionEnded' && run) {
        delete open[keyOf(e.employeeId, lvl)];
      }
    });

    // Deneme numarası: aynı employee + level içinde kronolojik sıra.
    const perKey = {};
    runs.forEach(function (r) {
      const k = keyOf(r.employeeId, r.levelId);
      perKey[k] = (perKey[k] || 0) + 1;
      r.attemptNo = perKey[k];
    });
    runs.forEach(function (r) {
      r.attemptTotal = perKey[keyOf(r.employeeId, r.levelId)];
    });

    return runs;
  }

  // ---------------------------------------------------------------------------
  // TEMEL KPI'LAR
  // ---------------------------------------------------------------------------

  /**
   * Doğruluk oranı = doğru QuizAnswered / toplam QuizAnswered
   * Sadece QuizAnswered event'lerini kullanır. Hiç quiz yoksa ok:false.
   */
  function accuracy(events) {
    const q = events.filter(function (e) { return e.eventType === 'QuizAnswered'; });
    const correct = q.filter(function (e) { return e.payload.isCorrect === true; }).length;
    return ratio(correct, q.length, 'Bu kapsamda hiç QuizAnswered kaydı yok');
  }

  /** Ortalama timeSpent — yalnızca QuizAnswered.timeSpent geçerli olanlar. */
  function avgQuizTime(events) {
    const q = events.filter(function (e) { return e.eventType === 'QuizAnswered'; });
    return mean(q.map(function (e) { return e.payload.timeSpent; }));
  }

  /** Ortalama attempts — QuizAnswered.attempts KÜMÜLATİFTİR (UIQuizPanel.cs:157).
   *  Bu yüzden her soru için sadece SON (en yüksek) attempts değeri alınır. */
  function avgAttempts(events) {
    const byQuestion = {};
    events.filter(function (e) { return e.eventType === 'QuizAnswered'; })
      .forEach(function (e) {
        const k = e.employeeId + '|' + e.payload.questionId + '|' + e.clientTimestamp.slice(0, 10);
        byQuestion[k] = Math.max(byQuestion[k] || 0, e.payload.attempts || 1);
      });
    return mean(Object.keys(byQuestion).map(function (k) { return byQuestion[k]; }));
  }

  /** Toplam hata sayısı (MistakeRecorded event adedi). Bu bir ORAN DEĞİLDİR. */
  function mistakeCount(events) {
    return events.filter(function (e) { return e.eventType === 'MistakeRecorded'; }).length;
  }

  /** mistakeType kırılımı. Repoda yalnızca 'wrong_answer' ve 'wrong_drop' üretiliyor. */
  function mistakesByType(events) {
    const m = {};
    events.filter(function (e) { return e.eventType === 'MistakeRecorded'; })
      .forEach(function (e) {
        const t = e.payload.mistakeType || '(tanımsız)';
        m[t] = (m[t] || 0) + 1;
      });
    return m;
  }

  /** severity kırılımı — HAM kategori sayımı. Hiçbir değer "kritik" sayılmaz. */
  function mistakesBySeverity(events) {
    const m = {};
    events.filter(function (e) { return e.eventType === 'MistakeRecorded'; })
      .forEach(function (e) {
        const s = (e.payload.severity === undefined || e.payload.severity === null)
          ? '(yok)' : String(e.payload.severity);
        m[s] = (m[s] || 0) + 1;
      });
    return m;
  }

  /**
   * Kritik hata oranı — HESAPLANMAZ.
   * Kritik severity tanımı proje belgelerinde YOK ve kodda severity sabit 1.
   * Bu fonksiyon bilerek ok:false döner; arayüz de bunu açıklamayla gösterir.
   */
  function criticalMistakeRate() {
    return {
      value: null, num: 0, den: 0, ok: false,
      reason: 'Kritik severity tanımı yok — kodda severity her zaman 1 gönderiliyor ' +
              '(SequenceManager.cs:671, UIDropZone.cs:179). Eşik tanımlanmadan ' +
              'kritik sınıflandırma yapılamaz.'
    };
  }

  /**
   * Hata oranı = MistakeRecorded / ilgili action denemesi
   * Payda: aynı kapsamda tamamlanan ActionCompleted sayısı.
   * Payda 0 ise oran döndürmez.
   */
  function mistakeRate(events) {
    const mistakes = mistakeCount(events);
    const actions = events.filter(function (e) { return e.eventType === 'ActionCompleted'; }).length;
    return ratio(mistakes, actions,
      'ActionCompleted kaydı yok — hata için payda oluşturulamıyor');
  }

  /** Seçilen aralıkta en az bir geçerli event gönderen benzersiz employeeId sayısı. */
  function activeEmployees(events) {
    const s = {};
    events.forEach(function (e) { if (e.employeeId) s[e.employeeId] = true; });
    return Object.keys(s).length;
  }

  /**
   * Katılım oranı — HESAPLANMAZ.
   * Atanmış çalışan (roster) verisi projede yok; PlayFab whitelist bir "erişim
   * listesi", "eğitim ataması" değil. Payda üretilemez.
   */
  function participationRate() {
    return {
      value: null, num: 0, den: 0, ok: false,
      reason: 'Atanmış eğitim / roster verisi yok. PlayFab whitelist bir erişim ' +
              'listesidir, atama listesi değildir. Bunun yerine "Aktif Çalışan" gösterilir.'
    };
  }

  /**
   * Senaryo tamamlama — SADECE açık completion event'i ile.
   * LevelCompleted { completed: true } gerçekten var, o yüzden bu KPI üretilebilir.
   * Son QuizAnswered ASLA tamamlama sayılmaz.
   */
  function completionStatus(runs) {
    const total = runs.length;
    const done = runs.filter(function (r) { return r.completed === true; }).length;
    return {
      completedRuns: done,
      totalRuns: total,
      rate: ratio(done, total, 'Bu kapsamda deneme kaydı yok')
    };
  }

  // ---------------------------------------------------------------------------
  // KARŞILAŞTIRMA (son deneme ↔ önceki deneme)
  // ---------------------------------------------------------------------------

  function runMetrics(run) {
    const evs = run.events;
    const acc = accuracy(evs);
    const mist = mistakeCount(evs);
    const byType = mistakesByType(evs);
    const t = avgQuizTime(evs);
    const at = avgAttempts(evs);
    return {
      accuracy: acc,
      mistakes: mist,
      mistakesByType: byType,
      avgTime: t,
      avgAttempts: at,
      totalTime: isValidDuration(run.reportedTimeSpent) ? run.reportedTimeSpent : sum(
        evs.filter(function (e) { return e.eventType === 'ActionCompleted'; })
           .map(function (e) { return e.payload.duration; })
      ),
      score: run.score,
      quizCount: evs.filter(function (e) { return e.eventType === 'QuizAnswered'; }).length,
      completed: run.completed
    };
  }

  /**
   * İki denemeyi karşılaştırır. Metrikleri BİRLEŞTİRMEZ —
   * doğruluk artarken hata sayısı artmışsa ikisi de ayrı ayrı görünür.
   */
  function compareRuns(prev, last) {
    if (!prev || !last) {
      return { ok: false, reason: 'Karşılaştırma oluşturmak için en az iki deneme gerekir.' };
    }
    const a = runMetrics(prev);
    const b = runMetrics(last);

    function delta(x, y) {
      if (x === null || y === null || x === undefined || y === undefined) return null;
      return y - x;
    }

    // Hangi hatalar tekrar etti / çözüldü / yeni çıktı — actionId + mistakeType bazında
    function mistakeKeys(run) {
      const s = {};
      run.events.filter(function (e) { return e.eventType === 'MistakeRecorded'; })
        .forEach(function (e) { s[e.payload.actionId + '::' + e.payload.mistakeType] = true; });
      return s;
    }
    const kPrev = mistakeKeys(prev);
    const kLast = mistakeKeys(last);
    const repeated = Object.keys(kLast).filter(function (k) { return kPrev[k]; });
    const resolved = Object.keys(kPrev).filter(function (k) { return !kLast[k]; });
    const appeared = Object.keys(kLast).filter(function (k) { return !kPrev[k]; });

    return {
      ok: true,
      prev: a,
      last: b,
      deltas: {
        accuracy: (a.accuracy.ok && b.accuracy.ok) ? delta(a.accuracy.value, b.accuracy.value) : null,
        mistakes: delta(a.mistakes, b.mistakes),
        totalTime: delta(a.totalTime, b.totalTime),
        avgAttempts: (a.avgAttempts.ok && b.avgAttempts.ok) ? delta(a.avgAttempts.value, b.avgAttempts.value) : null,
        score: delta(a.score, b.score)
      },
      repeatedMistakes: repeated,
      resolvedMistakes: resolved,
      newMistakes: appeared
    };
  }

  // ---------------------------------------------------------------------------
  // ZORLUK FAKTÖRLERİ
  // ---------------------------------------------------------------------------
  //
  // Tek bir "Zorluk Skoru" ÜRETİLMEZ. Bunun yerine faktörler yan yana gösterilir.
  // Sebep: faktörlerin ağırlıkları bir iş kuralıdır ve proje belgelerinde
  // tanımlı değildir. Tek skor, kritik bir güvenlik hatasını yüksek hacimli
  // ama zararsız bir hatanın içinde gizleyebilir.

  function actionFactors(events) {
    const rows = {};

    function row(actionId) {
      if (!rows[actionId]) {
        const idx = D.actionIndex[actionId];
        rows[actionId] = {
          actionId: actionId,
          actionName: idx ? idx.action.name : '(katalogda yok)',
          sequenceId: idx ? idx.sequence.id : null,
          sequenceName: idx ? idx.sequence.name : '—',
          levelId: idx ? idx.level.emittedLevelId : null,
          levelName: idx ? idx.level.name : '—',
          type: idx ? idx.action.type : null,
          quizTotal: 0, quizWrong: 0,
          attemptsSum: 0, attemptsN: 0,
          timeValues: [],
          mistakes: 0,
          actionCompleted: 0,
          employees: {},
          lastSeen: null
        };
      }
      return rows[actionId];
    }

    events.forEach(function (e) {
      const aid = e.payload && e.payload.actionId;
      if (!aid) return;
      const r = row(aid);
      r.employees[e.employeeId] = true;
      if (!r.lastSeen || e.clientTimestamp > r.lastSeen) r.lastSeen = e.clientTimestamp;

      if (e.eventType === 'QuizAnswered') {
        r.quizTotal += 1;
        if (!e.payload.isCorrect) r.quizWrong += 1;
        if (typeof e.payload.attempts === 'number') { r.attemptsSum += e.payload.attempts; r.attemptsN += 1; }
        r.timeValues.push(e.payload.timeSpent);
      } else if (e.eventType === 'MistakeRecorded') {
        r.mistakes += 1;
      } else if (e.eventType === 'ActionCompleted') {
        r.actionCompleted += 1;
        r.timeValues.push(e.payload.duration);
      } else if (e.eventType === 'DragDropAttempt') {
        if (typeof e.payload.attempts === 'number') { r.attemptsSum += e.payload.attempts; r.attemptsN += 1; }
      }
    });

    return Object.keys(rows).map(function (k) {
      const r = rows[k];
      return {
        actionId: r.actionId,
        actionName: r.actionName,
        sequenceId: r.sequenceId,
        sequenceName: r.sequenceName,
        levelId: r.levelId,
        levelName: r.levelName,
        type: r.type,
        // Faktör 1 — yanlış cevap oranı (yalnızca quiz action'larda anlamlı)
        wrongRate: ratio(r.quizWrong, r.quizTotal, 'Quiz kaydı yok'),
        // Faktör 2 — ortalama deneme sayısı
        avgAttempts: r.attemptsN ? { value: r.attemptsSum / r.attemptsN, ok: true, n: r.attemptsN }
                                 : { value: null, ok: false, n: 0, reason: 'attempts alanı yok' },
        // Faktör 3 — ortalama süre
        avgTime: mean(r.timeValues),
        // Faktör 4 — normalize hata oranı: hata / action denemesi
        mistakeRate: ratio(r.mistakes, r.actionCompleted,
          'ActionCompleted kaydı yok — normalize edilemiyor'),
        mistakes: r.mistakes,
        actionCompleted: r.actionCompleted,
        employeeCount: Object.keys(r.employees).length,
        lastSeen: r.lastSeen
      };
    });
  }

  // ---------------------------------------------------------------------------
  // ISI HARİTASI — Action × mistakeType, normalize edilmiş oran
  // ---------------------------------------------------------------------------

  function heatmap(events, opts) {
    opts = opts || {};
    const limit = opts.limit || 14;
    const types = ['wrong_answer', 'wrong_drop'];

    const denom = {};  // actionId → ActionCompleted sayısı (payda)
    events.filter(function (e) { return e.eventType === 'ActionCompleted'; })
      .forEach(function (e) {
        const a = e.payload.actionId;
        denom[a] = (denom[a] || 0) + 1;
      });

    const cells = {};
    events.filter(function (e) { return e.eventType === 'MistakeRecorded'; })
      .forEach(function (e) {
        const a = e.payload.actionId;
        const t = e.payload.mistakeType || '(tanımsız)';
        cells[a] = cells[a] || {};
        cells[a][t] = cells[a][t] || { count: 0, employees: {}, lastSeen: null };
        cells[a][t].count += 1;
        cells[a][t].employees[e.employeeId] = true;
        if (!cells[a][t].lastSeen || e.clientTimestamp > cells[a][t].lastSeen) {
          cells[a][t].lastSeen = e.clientTimestamp;
        }
      });

    const rows = Object.keys(cells).map(function (a) {
      const idx = D.actionIndex[a];
      const total = types.reduce(function (s, t) { return s + (cells[a][t] ? cells[a][t].count : 0); }, 0);
      return {
        actionId: a,
        actionName: idx ? idx.action.name : '(katalogda yok)',
        sequenceName: idx ? idx.sequence.name : '—',
        levelName: idx ? idx.level.name : '—',
        levelId: idx ? idx.level.emittedLevelId : null,
        denominator: denom[a] || 0,
        total: total,
        cells: types.map(function (t) {
          const c = cells[a][t] || { count: 0, employees: {}, lastSeen: null };
          return {
            type: t,
            count: c.count,
            // Ham sayı yerine normalize oran: hata / action denemesi.
            // Böylece çok oynanan bir action otomatik "riskli" görünmez.
            rate: ratio(c.count, denom[a] || 0, 'ActionCompleted kaydı yok'),
            employeeCount: Object.keys(c.employees).length,
            employees: Object.keys(c.employees),
            lastSeen: c.lastSeen
          };
        })
      };
    });

    // Normalize orana göre sırala; payda yoksa en sona at.
    rows.sort(function (x, y) {
      const rx = Math.max.apply(null, x.cells.map(function (c) { return c.rate.ok ? c.rate.value : -1; }));
      const ry = Math.max.apply(null, y.cells.map(function (c) { return c.rate.ok ? c.rate.value : -1; }));
      if (ry !== rx) return ry - rx;
      return y.total - x.total;
    });

    return { types: types, rows: rows.slice(0, limit), truncated: Math.max(0, rows.length - limit) };
  }

  // ---------------------------------------------------------------------------
  // ZAMAN SERİSİ
  // ---------------------------------------------------------------------------

  /** Günlük kova. metric: 'accuracy' | 'mistakes' | 'activeEmployees' | 'runs' | 'avgTime' */
  function timeSeries(events, metric, bucketDays) {
    bucketDays = bucketDays || 7;
    const buckets = {};

    events.forEach(function (e) {
      const d = new Date(e.clientTimestamp);
      const dayIndex = Math.floor((D.TODAY - d) / 86400000);
      const b = Math.floor(dayIndex / bucketDays);
      buckets[b] = buckets[b] || [];
      buckets[b].push(e);
    });

    const keys = Object.keys(buckets).map(Number).sort(function (a, b) { return b - a; });
    return keys.map(function (b) {
      const evs = buckets[b];
      const end = new Date(D.TODAY.getTime() - b * bucketDays * 86400000);
      const start = new Date(end.getTime() - (bucketDays - 1) * 86400000);
      let value = null, ok = true, reason = null, count = evs.length;

      if (metric === 'accuracy') {
        const r = accuracy(evs);
        value = r.ok ? r.value * 100 : null; ok = r.ok; reason = r.reason;
      } else if (metric === 'mistakes') {
        value = mistakeCount(evs);
      } else if (metric === 'activeEmployees') {
        value = activeEmployees(evs);
      } else if (metric === 'runs') {
        value = evs.filter(function (e) { return e.eventType === 'LevelStarted'; }).length;
      } else if (metric === 'avgTime') {
        const m = avgQuizTime(evs);
        value = m.ok ? m.value : null; ok = m.ok; reason = m.reason;
      } else if (metric === 'mistakeRate') {
        const r = mistakeRate(evs);
        value = r.ok ? r.value * 100 : null; ok = r.ok; reason = r.reason;
      }

      return {
        label: fmtShortDate(start) + '–' + fmtShortDate(end),
        start: start, end: end,
        value: value, ok: ok, reason: reason, eventCount: count
      };
    }).reverse();
  }

  // ---------------------------------------------------------------------------
  // ÇALIŞAN ÖZETİ
  // ---------------------------------------------------------------------------

  function employeeSummary(allEvents, employeeId, filter) {
    const own = filterEvents(allEvents, Object.assign({}, filter, { employeeId: employeeId }));
    const runs = deriveRuns(own, employeeId);
    const acc = accuracy(own);
    const emp = D.employeeById[employeeId];

    const lastRun = runs.length ? runs[runs.length - 1] : null;
    const prevRun = runs.length > 1 ? runs[runs.length - 2] : null;

    // Aynı senaryo içinde önceki deneme (level bazlı karşılaştırma için daha doğru)
    let prevSameLevel = null;
    if (lastRun) {
      for (let i = runs.length - 2; i >= 0; i--) {
        if (runs[i].levelId === lastRun.levelId) { prevSameLevel = runs[i]; break; }
      }
    }

    return {
      employee: emp,
      events: own,
      runs: runs,
      eventCount: own.length,
      accuracy: acc,
      avgTime: avgQuizTime(own),
      avgAttempts: avgAttempts(own),
      mistakes: mistakeCount(own),
      mistakesByType: mistakesByType(own),
      mistakesBySeverity: mistakesBySeverity(own),
      mistakeRate: mistakeRate(own),
      completion: completionStatus(runs),
      lastRun: lastRun,
      prevRun: prevRun,
      prevSameLevel: prevSameLevel,
      comparison: compareRuns(prevSameLevel, lastRun),
      lastActivity: own.length ? own[own.length - 1].clientTimestamp : null,
      levelsPlayed: (function () {
        const s = {};
        runs.forEach(function (r) { s[r.levelId] = true; });
        return Object.keys(s);
      })()
    };
  }

  /** Bir senaryonun (level) tek çalışan için durumu — completion event'e dayanır. */
  function scenarioStatus(runs, levelId) {
    const own = runs.filter(function (r) { return r.levelId === levelId; });
    if (own.length === 0) return { code: 'not_started', label: 'Başlanmadı' };

    const anyCompleted = own.some(function (r) { return r.completed; });
    const last = own[own.length - 1];

    if (!anyCompleted && !last.completed) {
      return { code: 'in_progress', label: 'Devam Ediyor',
               note: 'LevelCompleted event\'i alınmadı — oturum yarıda kalmış olabilir.' };
    }
    // Tamamlanmış ama son denemede çok hata varsa tekrar öner
    const m = runMetrics(last);
    if (anyCompleted && m.accuracy.ok && m.accuracy.value < 0.7) {
      return { code: 'retry', label: 'Tekrar Öneriliyor',
               note: 'Son denemede doğruluk %70\'in altında.' };
    }
    if (anyCompleted) return { code: 'completed', label: 'Tamamlandı' };
    return { code: 'insufficient', label: 'Veri Yetersiz' };
  }

  // ---------------------------------------------------------------------------
  // BİÇİMLENDİRME
  // ---------------------------------------------------------------------------

  const MONTHS = ['Oca', 'Şub', 'Mar', 'Nis', 'May', 'Haz', 'Tem', 'Ağu', 'Eyl', 'Eki', 'Kas', 'Ara'];

  function fmtShortDate(d) {
    if (!(d instanceof Date)) d = new Date(d);
    return d.getUTCDate() + ' ' + MONTHS[d.getUTCMonth()];
  }

  function fmtDate(v) {
    if (!v) return '—';
    const d = new Date(v);
    return d.getUTCDate() + ' ' + MONTHS[d.getUTCMonth()] + ' ' + d.getUTCFullYear();
  }

  function fmtDateTime(v) {
    if (!v) return '—';
    const d = new Date(v);
    const p = function (n) { return n < 10 ? '0' + n : '' + n; };
    return fmtDate(v) + ' ' + p(d.getUTCHours()) + ':' + p(d.getUTCMinutes());
  }

  function fmtDuration(sec) {
    if (!isValidDuration(sec)) return '—';
    const s = Math.round(sec);
    if (s < 60) return s + ' sn';
    const m = Math.floor(s / 60);
    const rest = s % 60;
    if (m < 60) return m + ' dk' + (rest ? ' ' + rest + ' sn' : '');
    return Math.floor(m / 60) + ' sa ' + (m % 60) + ' dk';
  }

  function relativeDays(v) {
    if (!v) return '—';
    const days = Math.floor((D.TODAY - new Date(v)) / 86400000);
    if (days <= 0) return 'bugün';
    if (days === 1) return 'dün';
    return days + ' gün önce';
  }

  // ---------------------------------------------------------------------------
  window.TS_KPI = {
    // oran altyapısı
    ratio: ratio, pct: pct, rateLabel: rateLabel, mean: mean, sum: sum,
    isValidDuration: isValidDuration,
    // filtre
    filterEvents: filterEvents, levelIdOf: levelIdOf, sequenceIdOf: sequenceIdOf,
    // deneme
    deriveRuns: deriveRuns, runMetrics: runMetrics, compareRuns: compareRuns,
    // kpi
    accuracy: accuracy, avgQuizTime: avgQuizTime, avgAttempts: avgAttempts,
    mistakeCount: mistakeCount, mistakesByType: mistakesByType,
    mistakesBySeverity: mistakesBySeverity, mistakeRate: mistakeRate,
    criticalMistakeRate: criticalMistakeRate, participationRate: participationRate,
    activeEmployees: activeEmployees, completionStatus: completionStatus,
    // analiz
    actionFactors: actionFactors, heatmap: heatmap, timeSeries: timeSeries,
    employeeSummary: employeeSummary, scenarioStatus: scenarioStatus,
    // biçim
    fmtDate: fmtDate, fmtDateTime: fmtDateTime, fmtShortDate: fmtShortDate,
    fmtDuration: fmtDuration, relativeDays: relativeDays
  };
})();
