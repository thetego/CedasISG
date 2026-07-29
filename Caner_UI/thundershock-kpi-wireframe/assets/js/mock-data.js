/* =============================================================================
   THUNDERSHOCK KPI PORTALI — TEMSİLİ (MOCK) VERİ
   =============================================================================
   BU DOSYADAKİ TÜM ÇALIŞAN VE OLAY KAYITLARI TEMSİLİDİR.
   Gerçek çalışan verisi, gerçek isim veya gerçek PlayFab kaydı İÇERMEZ.

   Olay zarfı (envelope) birebir Assets/_Project/Scripts/PlayFabDataManager.cs
   `SendEvent()` metodundaki formata göre üretilir:

       { eventType, clientTimestamp, employeeId, payload: { ... } }

   PlayFab sunucusu bu zarfa ayrıca kendi `Timestamp` ve entity bilgisini ekler;
   burada onu `_serverTimestamp` alanıyla temsil ediyoruz (alan adı `_` ile
   başlıyor çünkü istemci şemasının parçası DEĞİL — DATA_MAPPING.md'ye bakın).

   İçerik kataloğu (level / sequence / action ID'leri, quiz metinleri) repodaki
   gerçek ScriptableObject dosyalarından çıkarılmıştır. Kaynak yolları
   DESIGN_SOURCE_AUDIT.md ve PRODUCT_CONTEXT.md içinde listelenmiştir.
============================================================================= */

(function () {
  'use strict';

  // ---------------------------------------------------------------------------
  // Deterministik sözde-rastgele üretici (mulberry32).
  // Her sayfa yenilemesinde aynı veri setinin oluşmasını garanti eder.
  // ---------------------------------------------------------------------------
  function rng(seed) {
    let a = seed >>> 0;
    return function () {
      a |= 0; a = (a + 0x6D2B79F5) | 0;
      let t = Math.imul(a ^ (a >>> 15), 1 | a);
      t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
  }

  // Prototipin "bugün"ü. Gerçek üründe Date.now() olur.
  const TODAY = new Date('2026-07-27T09:00:00.000Z');

  function iso(dayOffset, hour, minute, second) {
    const d = new Date(TODAY.getTime());
    d.setUTCDate(d.getUTCDate() - dayOffset);
    d.setUTCHours(hour, minute, second || 0, 0);
    return d.toISOString();
  }

  // ===========================================================================
  // 1) İÇERİK KATALOĞU — repodaki gerçek ScriptableObject verisinden
  // ===========================================================================
  //
  // ÖNEMLİ: `emittedLevelId`, oyunun telemetriye GERÇEKTEN gönderdiği değerdir
  // (LevelData.levelID alanı). Bu değerler üretim seviyelerinde tutarsızdır:
  //   level_1.asset -> "level 1"
  //   level_2.asset -> "lvl1"      (yanlış / Level 1 ile karışıyor)
  //   level_3.asset -> "NewLevel"  (Unity varsayılanı, hiç ayarlanmamış)
  // Portal bunu "Veri Kalitesi" uyarısı olarak yüzeye çıkarır.

  const CONTENT = {
    levels: [
      {
        key: 'L1',
        emittedLevelId: 'level 1',
        assetPath: 'Assets/_Project/_Level 1/DATA/level_1.asset',
        name: 'Direk & Trafo Köşkü',
        subtitle: 'Hat bakımı — bağlantı kesme, klemens montajı, yeniden enerjilendirme',
        icon: 'klemens',
        criticalNote:
          'Kritik Güvenlik Noktası (GDD §6): Elektrik kesilmeden klemens montajı ' +
          'yapılmaya çalışılırsa eğitim sonlandırılır.',
        sequences: [
          {
            id: 'equipment', rawName: 'E qu ip me nt', name: 'Ekipman Hazırlık',
            icon: 'kask',
            actions: [
              { id: 'wear', name: 'Uygun ekipmanları giy', type: 'drag_drop', rawType: 'WearEquipment' },
              { id: 'Equipment_Act_02', name: 'Bilgi sorusu — Ekipman hazırlık', type: 'quiz', rawType: 'Quiz' }
            ]
          },
          {
            id: 'direk1', rawName: 'd ir ek 1', name: 'Direk 1 — Pano Kontrolü',
            icon: 'sigorta',
            actions: [
              { id: 'direk1_Act_03', name: 'Kamera geçişi', type: 'interaction', rawType: 'CameraMove' },
              { id: 'openPlate', name: 'Kapağı aç', type: 'drag_drop', rawType: 'DragToWorld' },
              { id: 'checkEnergy1', name: 'Kaçak kontrolü yap', type: 'drag_drop', rawType: 'DragToWorld' },
              { id: 'direk1_Act_04', name: 'Bilgi sorusu — Direk 1', type: 'quiz', rawType: 'Quiz' },
              { id: 'direk1_Act_04_2', name: 'Bilgi sorusu — Direk 1 (2)', type: 'quiz', rawType: 'Quiz' }
            ]
          },
          {
            id: 'direk2', rawName: 'd ir ek 2', name: 'Direk 2 — Klemens Montajı',
            icon: 'matkap',
            actions: [
              { id: 'Kablolardakacakkontroluyap', name: 'Kablolarda kaçak kontrolü yap', type: 'click', rawType: 'Click' },
              { id: 'cutDuct', name: 'Bantları temizle', type: 'drag_drop', rawType: 'DragToWorld' },
              { id: 'klemens', name: 'Klemensleme işlemini yap', type: 'drag_drop', rawType: 'DragToWorld' },
              { id: 'sigorta', name: 'Sigortayı yerleştir', type: 'drag_drop', rawType: 'DragToWorld' },
              { id: 'closePlate', name: 'Kapağı kapat', type: 'drag_drop', rawType: 'DragToWorld' },
              { id: 'direk2_Act_07', name: 'Bilgi sorusu — Klemens montajı', type: 'quiz', rawType: 'Quiz' },
              { id: 'direk2_Act_09', name: 'Kamera geçişi', type: 'interaction', rawType: 'CameraMove' }
            ]
          },
          {
            id: 'direk3', rawName: 'd ir ek 3', name: 'Direk 3 — Uyarı Tabelası',
            icon: 'tabela',
            actions: [
              { id: 'tabela', name: 'Uyarı tabelasını yerleştir', type: 'drag_drop', rawType: 'DragToWorld' },
              { id: 'tabelaMat', name: 'Uyarı tabelasını sabitle', type: 'drag_drop', rawType: 'DragToWorld' },
              { id: 'direk3_Act_04', name: 'Bilgi sorusu — Uyarı tabelası', type: 'quiz', rawType: 'Quiz' }
            ]
          },
          {
            id: 'trafo1', rawName: 't ra fo 1', name: 'Trafo 1 — Enerji Kesme',
            icon: 'kontrol_kalemi',
            actions: [
              { id: 'openDoor', name: 'Köşkün kapısını aç', type: 'click', rawType: 'Click' },
              { id: 'SwitchIn', name: 'Kamera — içeri geçiş', type: 'interaction', rawType: 'CameraMove' },
              { id: 'openin', name: 'Pano kapağını aç', type: 'click', rawType: 'Click' },
              { id: 'SwitchPanel', name: 'Kamera — pano yakın plan', type: 'interaction', rawType: 'CameraMove' },
              { id: 'off', name: 'Enerjiyi kes', type: 'click', rawType: 'Click' },
              { id: 'trafo1_Act_07', name: 'Bilgi sorusu — Enerji kesme', type: 'quiz', rawType: 'Quiz' }
            ]
          },
          {
            id: 'trafo2', rawName: 't ra fo 2', name: 'Trafo 2 — Enerji Verme',
            icon: 'pense',
            actions: [
              { id: 'on', name: 'Enerjiyi aç', type: 'click', rawType: 'Click' },
              { id: 'SwitchTrafo', name: 'Kamera — trafo', type: 'interaction', rawType: 'CameraMove' },
              { id: 'openin_2', name: 'Pano kapağını kapat', type: 'click', rawType: 'Click' },
              { id: 'openDoor_2', name: 'Köşk kapısını kapat', type: 'click', rawType: 'Click' },
              { id: 'trafo2_Act_07', name: 'Armatürün raporlama için fotoğrafını çek', type: 'survey', rawType: 'Survey' },
              { id: 'trafo2_Act_08', name: 'Bilgi sorusu — Enerji verme', type: 'quiz', rawType: 'Quiz' }
            ]
          }
        ]
      },

      {
        key: 'L2',
        emittedLevelId: 'lvl1',
        assetPath: 'Assets/_Project/Level 2/DATA/level_2.asset',
        name: 'Hücre / Pano Odası',
        subtitle: 'AG hücre odası — kesici açma, SCADA doğrulama, hücre arkası kontrolü',
        icon: 'cell',
        criticalNote:
          'Kritik Güvenlik Noktası (GDD §6): Yanlış alet seçimi ölçüm hatasına yol açar — uyarı verilir.',
        dataWarning:
          'Bu level telemetriye "lvl1" levelId değerini gönderiyor (level_2.asset). ' +
          'Level 1 ile karışma riski var.',
        sequences: [
          {
            id: 'EquipmentSequence', rawName: 'E qu ip me nt Se qu en ce', name: 'Ekipman Hazırlık',
            icon: 'long_gloves',
            actions: [
              { id: 'EquipmentSequence_Act_01', name: 'Uygun ekipmanları giy', type: 'drag_drop', rawType: 'WearEquipment' },
              { id: 'EquipmentSequence_Act_02', name: 'Bilgi sorusu — göreve başlama', type: 'quiz', rawType: 'Quiz' }
            ]
          },
          {
            id: 'Box1', rawName: 'B ox 1', name: 'Box 1 — Test Cihazı Operasyonu',
            icon: 'megger',
            actions: [
              { id: 'boxopen', name: 'Test cihazını aktifleştir', type: 'click', rawType: 'Click' },
              { id: 'kutucheck1', name: 'Test çubuğunu iliştir (Faz 1)', type: 'drag_drop', rawType: 'DragToWorld' },
              { id: 'boxclick1', name: 'Testi başlat (Faz 1)', type: 'click', rawType: 'Click' },
              { id: 'kutucheck2', name: 'Test çubuğunu iliştir (Faz 2)', type: 'drag_drop', rawType: 'DragToWorld' },
              { id: 'boxclick2', name: 'Testi başlat (Faz 2)', type: 'click', rawType: 'Click' },
              { id: 'kutucheck3', name: 'Test çubuğunu iliştir (Faz 3)', type: 'drag_drop', rawType: 'DragToWorld' },
              { id: 'boxclick3', name: 'Testi başlat (Faz 3)', type: 'click', rawType: 'Click' },
              { id: 'Q4', name: 'Bilgi sorusu — kablo bağlantısı', type: 'quiz', rawType: 'Quiz' },
              { id: 'Q5', name: 'Bilgi sorusu — test sonrası', type: 'quiz', rawType: 'Quiz' },
              { id: 'boxcable', name: 'Kabloları bağla', type: 'drag_drop', rawType: 'DragToWorld' },
              { id: 'boxclose', name: 'Kutuyu kapat', type: 'click', rawType: 'Click' },
              { id: 'tabClick', name: 'Uyarı tabelasını as', type: 'click', rawType: 'Click' }
            ]
          },
          {
            id: 'BuildingSequence', rawName: 'B ui ld in gS eq ue nc e', name: 'Kesici Operasyonu',
            icon: 'tornavida',
            actions: [
              { id: 'BuildingSequence_Act_16', name: 'Kesiciyi aç', type: 'click', rawType: 'Click' },
              { id: 'BuildingSequence_Act_161', name: 'Bilgi sorusu — kesici durumu', type: 'quiz', rawType: 'Quiz' },
              { id: 'BuildingSequence_Act_17', name: 'Bilgi sorusu — sonraki işlem', type: 'quiz', rawType: 'Quiz' },
              { id: 'BuildingSequence_Act_17_2', name: 'Bilgi sorusu — topraklama', type: 'quiz', rawType: 'Quiz' },
              { id: 'BuildingSequence_Act_17_4', name: 'Bilgi sorusu — neon ıstanka', type: 'quiz', rawType: 'Quiz' }
            ]
          },
          {
            id: 'BackSequence', rawName: 'B ac kS eq ue nc e', name: 'Hücre Arkası Kontrolü',
            icon: 'catal_cubugu',
            actions: [
              { id: 'openback', name: 'Hücre arkasını aç', type: 'click', rawType: 'Click' },
              { id: 'backrot', name: 'Mekanizmayı çevir', type: 'drag_drop', rawType: 'DragToWorld' },
              { id: 'holderr', name: 'Tutucuyu yerleştir', type: 'drag_drop', rawType: 'DragToWorld' },
              { id: 'backclick', name: 'Görsel kontrolü tamamla', type: 'click', rawType: 'Click' }
            ]
          },
          {
            id: 'scada', rawName: 's ca da', name: 'SCADA Doğrulama',
            icon: 'kontrol_kalemi',
            actions: [
              { id: 'scada_panel', name: 'Tablet üzerinden sistem durumunu teyit et', type: 'click', rawType: 'PanelInteraction' },
              { id: 'Q6', name: 'Bilgi sorusu — çalışma sonu', type: 'quiz', rawType: 'Quiz' }
            ]
          }
        ]
      },

      {
        key: 'L3',
        emittedLevelId: 'NewLevel',
        assetPath: 'Assets/_Project/Level3/Data/Training/Levels/level_3.asset',
        name: 'AG Trafo Dairesi & Otopark',
        subtitle: 'Periyodik bakım — termal tarama, kısmi deşarj tespiti, kablo montajı',
        icon: 'termal_kamera',
        criticalNote:
          'Kritik Güvenlik Noktası (GDD §6): Termal tarama tamamlanmadan panel açılmaya ' +
          'çalışılırsa eğitim sonlandırılır.',
        dataWarning:
          'Bu level telemetriye Unity varsayılanı olan "NewLevel" levelId değerini gönderiyor ' +
          '(level_3.asset). levelID alanı hiç ayarlanmamış.',
        sequences: [
          {
            id: 'Level3_Seq_01', rawName: 'Equipment', name: 'Ekipman Hazırlık',
            icon: 'isg_boot',
            actions: [
              { id: 'Equipment_Act_01', name: 'Uygun ekipmanları giy', type: 'drag_drop', rawType: 'WearEquipment' },
              { id: 'Equipment_Act_02_2', name: 'Bilgi sorusu — ilk işlem', type: 'quiz', rawType: 'Quiz' }
            ]
          },
          {
            id: 'Level3_Seq_09', rawName: 'AG Eldivenleri', name: 'AG Eldivenleri',
            icon: 'ag_eldiven',
            actions: [
              { id: 'Equipment_Act_01_ag', name: 'AG eldivenlerini giy', type: 'drag_drop', rawType: 'WearEquipment' }
            ]
          },
          {
            id: 'Level3_Seq_02', rawName: 'Trafo Check', name: 'Trafo Kontrolü',
            icon: 'termal_kamera',
            actions: [
              { id: 'TrafoCheck_Act_01', name: 'Trafo kapısını aç', type: 'click', rawType: 'Click' },
              { id: 'TrafoCheck_Act_02', name: 'Tablet raporlaması için 2 adet resim çek', type: 'survey', rawType: 'Survey' },
              { id: 'TrafoCheck_Act_03', name: 'Trafonun termal görüşünü kontrol et ve fotoğrafını çek', type: 'survey', rawType: 'Survey' },
              { id: 'TrafoCheck_Act_09', name: 'Bilgi sorusu — kapı fotoğrafı', type: 'quiz', rawType: 'Quiz' },
              { id: 'TrafoCheck_Act_10', name: 'Bilgi sorusu — termal kamera', type: 'quiz', rawType: 'Quiz' },
              { id: 'TrafoCheck_Act_11', name: 'Bilgi sorusu — kısmi deşarj', type: 'quiz', rawType: 'Quiz' },
              { id: 'trafokapi_copy', name: 'Trafo kapısını kapat', type: 'click', rawType: 'Click' }
            ]
          },
          {
            id: 'Level3_Seq_03', rawName: 'AG Check', name: 'AG Panel Kontrolü',
            icon: 'kismi_desarj',
            actions: [
              { id: 'AGCheck_Act_01', name: 'AG kapısını aç', type: 'click', rawType: 'Click' },
              { id: 'AGCheck_Act_02', name: 'Odanın fotoğrafını çek', type: 'survey', rawType: 'Survey' },
              { id: 'AGCheck_Act_04', name: 'Kapağı aç ve akım trafolarının fotoğrafını çek', type: 'survey', rawType: 'Survey' },
              { id: 'AGCheck_Act_09', name: 'Kısmi deşarj kontrolü yap ve fotoğrafını çek', type: 'survey', rawType: 'Survey' },
              { id: 'AGCheck_Act_12', name: 'Pano etiketinin fotoğrafını çek', type: 'survey', rawType: 'Survey' },
              { id: 'AGCheck_Act_16', name: 'Bilgi sorusu — AG panel', type: 'quiz', rawType: 'Quiz' }
            ]
          },
          {
            id: 'Level3_Seq_04', rawName: 'Trafo Merkez', name: 'Trafo Merkez',
            icon: 'cell',
            actions: [
              { id: 'TrafoMerkez_Act_01', name: 'Trafo merkez kapısını aç', type: 'click', rawType: 'Click' },
              { id: 'TrafoMerkez_Act_02', name: 'Plakanın ve kapının resmini çek', type: 'survey', rawType: 'Survey' },
              { id: 'TrafoMerkez_Act_07', name: 'Bilgi sorusu — Trafo merkez', type: 'quiz', rawType: 'Quiz' }
            ]
          }
        ]
      }
    ]
  };

  // ===========================================================================
  // 2) QUIZ BANKASI — repodaki gerçek soru metinleri
  // ===========================================================================
  // Kaynak: ActionData.quizData (QuizActionData) alanları.
  // Cevap metinleri, UIQuizPanel.cs:68 içindeki "A) ", "B) " … önekleriyle
  // birebir aynı biçimde saklanır — çünkü QuizAnswered eventi selectedAnswer /
  // correctAnswer alanlarını bu önekli haliyle gönderir (UIQuizPanel.cs:122).

  const QUIZ_BANK = {
    'Q4': {
      q: 'Test cihazı bağlantısı yapılırken kablolar nereye bağlanmalıdır?',
      options: ['Topraklama noktasına', 'Ayırıcıya', 'Fazlara', 'Kesici koluna'],
      correctIndex: 2,
      asset: 'Assets/_Project/Level 2/DATA/Box1/q4__q_4.asset'
    },
    'Q5': {
      q: 'Üç fazın tamamı test edildikten sonra ilk yapılması gereken işlem hangisidir?',
      options: ['Hücre kapısını kapatmak', 'SCADA\'yı aramak', 'Test cihazını kaldırıp bağlantıları sökmek', 'Uyarı levhasını asmak'],
      correctIndex: 2,
      asset: 'Assets/_Project/Level 2/DATA/Box1/q5__q_5.asset'
    },
    'Q6': {
      q: 'Çalışma tamamlandıktan sonra aşağıdakilerden hangisi son adım olarak gerçekleştirilmelidir?',
      options: ['Hücre kapağını açmak', 'Ayırıcıyı kapatmak', 'SCADA operatörüne çalışma sonucunu bildirmek', 'Test cihazını tekrar bağlamak'],
      correctIndex: 2,
      asset: 'Assets/_Project/Level 2/DATA/SCAda/q6__q_6.asset'
    },
    'EquipmentSequence_Act_02': {
      q: 'Göreve başlamadan önce aşağıdakilerden hangisi ilk olarak kontrol edilmelidir?',
      options: ['Test cihazının kabloları', 'Hücre kapağı', 'Kişisel koruyucu donanımlar (İSG ekipmanları)', 'SCADA bağlantısı'],
      correctIndex: 2,
      asset: 'Assets/_Project/Level 2/DATA/E qu ip me nt Se qu en ce_Act_02.asset'
    },
    'BuildingSequence_Act_161': {
      q: 'Kesici üzerinde yeşil butona basıldıktan sonra hangi durum gözlemlenmelidir?',
      options: ['"1" konumu', '"0" konumu', 'Arıza konumu', 'Topraklama konumu'],
      correctIndex: 1,
      asset: 'Assets/_Project/Level 2/DATA/B ui ld in gS eq ue nc e_Act_16 1.asset'
    },
    'BuildingSequence_Act_17': {
      q: 'Kesici açıldıktan sonra gerçekleştirilecek bir sonraki işlem hangisidir?',
      options: ['Hücre kapağını açmak', 'Fazlara gerilim uygulamak', 'Kesici anahtarını çıkarıp üst bölüme takmak', 'Test cihazını bağlamak'],
      correctIndex: 2,
      asset: 'Assets/_Project/Level 2/DATA/B ui ld in gS eq ue nc e_Act_17.asset'
    },
    'BuildingSequence_Act_17_2': {
      q: 'Ayırıcı açıldıktan sonra hücre üzerinde çalışmaya başlamadan önce hangi işlem yapılmalıdır?',
      options: ['Test cihazı bağlanmalıdır', 'Hücre topraklanmalıdır', 'SCADA aranmalıdır', 'Uyarı levhası asılmalıdır'],
      correctIndex: 1,
      asset: 'Assets/_Project/Level 2/DATA/B ui ld in gS eq ue nc e_Act_17_Copy.asset'
    },
    'BuildingSequence_Act_17_4': {
      q: 'Neon lambalı ıstanka hangi amaçla kullanılır?',
      options: ['Faz sırası belirlemek için', 'Gerilim varlığını kontrol etmek için', 'Topraklama yapmak için', 'Kablo izolasyonu ölçmek için'],
      correctIndex: 1,
      asset: 'Assets/_Project/Level 2/DATA/B ui ld in gS eq ue nc e_Act_17_Copy_Copy_Copy.asset'
    },
    'Equipment_Act_02_2': {
      q: 'Trafo merkezindeki kontrol çalışmalarına başlamadan önce yapılması gereken ilk işlem hangisidir?',
      options: ['Termal ölçüm yapmak', 'Kontrol formunu doldurmak', 'İSG ekipmanlarını giymek', 'Trafo fotoğrafı çekmek'],
      correctIndex: 2,
      asset: 'Assets/_Project/Level3/Data/Training/Levels/Equipment/Equipment_Act_02.asset'
    },
    'TrafoCheck_Act_09': {
      q: 'Trafo odasına girişte kapının fotoğrafının çekilmesinin temel amacı nedir?',
      options: ['Kapının rengini kayıt altına almak', 'Odanın giriş durumunu belgelemek', 'Termal analiz yapmak', 'Kısmi deşarj ölçmek'],
      correctIndex: 1,
      asset: 'Assets/_Project/Level3/Data/Training/Levels/Level 3_Seq_02/Trafo Check_Act_09.asset'
    },
    'TrafoCheck_Act_10': {
      q: 'Termal kamera ile yapılan ölçümün amacı nedir?',
      options: ['Kablo kesitini belirlemek', 'Aşırı ısınan ekipmanları tespit etmek', 'Topraklama direncini ölçmek', 'Faz sırasını belirlemek'],
      correctIndex: 1,
      asset: 'Assets/_Project/Level3/Data/Training/Levels/Level 3_Seq_02/Trafo Check_Act_10.asset'
    },
    'TrafoCheck_Act_11': {
      q: 'Kısmi deşarj ölçümü hangi amaçla gerçekleştirilir?',
      options: ['Enerji tüketimini belirlemek', 'İzolasyon kaynaklı arızaları erken tespit etmek', 'Sigorta seçmek', 'Röle ayarı yapmak'],
      correctIndex: 1,
      asset: 'Assets/_Project/Level3/Data/Training/Levels/Level 3_Seq_02/Trafo Check_Act_11.asset'
    }
  };

  // Quiz metni bulunmayan action'lar için jenerik gösterim.
  // (Repoda 34 Quiz action var, 12'sinin metni dolu; kalanları boş bırakılmış.)
  function quizFor(actionId) {
    return QUIZ_BANK[actionId] || null;
  }

  // ===========================================================================
  // 3) ÇALIŞAN DİZİNİ (temsili)
  // ===========================================================================
  // NOT: Bu dizin telemetriden GELMEZ. Gerçek üründe PlayFab Title Data
  // whitelist'inden (PlayFabDataManager.PlayerEntry) gelir: playerId,
  // displayName, role, level, xp, createdAt, lastLogin.
  // `role` event payload'ındaki doküman context'i ile de taşınır.

  const EMPLOYEES = [
    { id: 'TEST001',  name: 'Demo Çalışan',       role: 'trainee',   profile: 'rich' },
    { id: 'EMP-1042', name: 'Çalışan #1042',      role: 'worker',    profile: 'strong' },
    { id: 'EMP-1043', name: 'Çalışan #1043',      role: 'worker',    profile: 'fewWrong' },
    { id: 'EMP-1044', name: 'Çalışan #1044',      role: 'trainee',   profile: 'retries' },
    { id: 'EMP-1045', name: 'Çalışan #1045',      role: 'trainee',   profile: 'manyMistakes' },
    { id: 'EMP-1046', name: 'Çalışan #1046',      role: 'inspector', profile: 'multiScenario' },
    { id: 'EMP-1047', name: 'Çalışan #1047',      role: 'worker',    profile: 'singleAttempt' },
    { id: 'EMP-1048', name: 'Çalışan #1048',      role: 'trainee',   profile: 'noData' },
    { id: 'EMP-1049', name: 'Çalışan #1049',      role: 'worker',    profile: 'improving' },
    { id: 'EMP-1050', name: 'Çalışan #1050',      role: 'worker',    profile: 'worsening' },
    { id: 'EMP-1051', name: 'Çalışan #1051',      role: 'trainee',   profile: 'missingTimeSpent' },
    { id: 'EMP-1052', name: 'Çalışan #1052',      role: 'worker',    profile: 'orphanMistakes' },
    { id: 'EMP-1053', name: 'Çalışan #1053',      role: 'inspector', profile: 'abandoned' }
  ];

  // Yönetici demo hesabı — oyun oynamaz, sadece portalı görüntüler.
  const MANAGERS = [
    { id: 'ADMIN_DEMO', name: 'Demo Yönetici', role: 'manager' }
  ];

  // ===========================================================================
  // 4) OLAY ÜRETİCİ
  // ===========================================================================

  const events = [];
  let clock = 0; // monoton artan saniye sayacı, run içi sıralama için
  const runSessions = {};

  function push(eventType, employeeId, dayOffset, payload) {
    clock += 1;
    const hour = 8 + Math.floor((clock % 300) / 60);
    const minute = clock % 60;
    const second = (clock * 7) % 60;
    const t = iso(dayOffset, hour, minute, second);
    const levelId = payload.levelId || 'unknown';
    const sessionKey = employeeId + '::' + levelId + '::' + dayOffset;
    if (eventType === 'LevelStarted' || !runSessions[sessionKey]) {
      runSessions[sessionKey] = employeeId + '-' + dayOffset + '-' + levelId;
    }
    const employee = EMPLOYEES.find(function (item) { return item.id === employeeId; });
    const documentPayload = Object.assign({}, payload, {
      sessionId: runSessions[sessionKey],
      playerId: employeeId,
      role: employee ? employee.role : '',
      timestamp: t
    });
    events.push({
      eventType: eventType,
      clientTimestamp: t,
      employeeId: employeeId,
      payload: documentPayload,
      // PlayFab tarafından eklenen sunucu zamanı (istemci şemasının parçası değil).
      _serverTimestamp: new Date(new Date(t).getTime() + 380).toISOString()
    });
  }

  // Profil parametreleri: hata oranı, deneme sayısı, süre çarpanı
  const PROFILES = {
    rich:             { wrongRate: 0.16, dropErrRate: 0.14, timeMul: 1.00 },
    strong:           { wrongRate: 0.04, dropErrRate: 0.03, timeMul: 0.82 },
    fewWrong:         { wrongRate: 0.22, dropErrRate: 0.08, timeMul: 1.05 },
    retries:          { wrongRate: 0.34, dropErrRate: 0.30, timeMul: 1.45 },
    manyMistakes:     { wrongRate: 0.46, dropErrRate: 0.42, timeMul: 1.60 },
    multiScenario:    { wrongRate: 0.14, dropErrRate: 0.11, timeMul: 0.95 },
    singleAttempt:    { wrongRate: 0.20, dropErrRate: 0.15, timeMul: 1.10 },
    improving:        { wrongRate: 0.40, dropErrRate: 0.34, timeMul: 1.40 },
    worsening:        { wrongRate: 0.08, dropErrRate: 0.06, timeMul: 0.90 },
    missingTimeSpent: { wrongRate: 0.18, dropErrRate: 0.12, timeMul: 1.00 },
    orphanMistakes:   { wrongRate: 0.10, dropErrRate: 0.10, timeMul: 1.00 },
    abandoned:        { wrongRate: 0.25, dropErrRate: 0.20, timeMul: 1.20 }
  };

  /**
   * Tek bir "deneme"yi (LevelStarted → LevelCompleted) üretir.
   *
   * sessionId gerçek event payload'ındaki doküman context'i ile aynı biçimde
   * üretilir; attemptIndex ise QuizAnswered retry'larını ayrıştırır.
   */
  function emitRun(emp, level, dayOffset, opts) {
    opts = opts || {};
    const p = PROFILES[emp.profile] || PROFILES.rich;
    const r = rng(
      (emp.id.length * 7919) +
      (level.key.charCodeAt(1) * 104729) +
      (dayOffset * 31) +
      (opts.seedSalt || 0)
    );
    const drift = opts.drift || 0;          // hata oranı kaydırması (gelişim/kötüleşme)
    const wrongRate = Math.max(0.01, Math.min(0.9, p.wrongRate + drift));
    const dropRate = Math.max(0.0, Math.min(0.9, p.dropErrRate + drift));

    push('LevelStarted', emp.id, dayOffset, {
      levelId: level.emittedLevelId,
      displayName: emp.name
    });

    let mistakes = 0;
    let quizTotal = 0;
    let quizCorrect = 0;
    let levelSeconds = 0;

    const seqs = opts.sequences
      ? level.sequences.filter(function (s) { return opts.sequences.indexOf(s.id) >= 0; })
      : level.sequences;

    for (let si = 0; si < seqs.length; si++) {
      const seq = seqs[si];
      let seqMistakes = 0;
      let seqSeconds = 0;

      push('SequenceStarted', emp.id, dayOffset, {
        sequenceId: seq.id,
        levelId: level.emittedLevelId
      });

      for (let ai = 0; ai < seq.actions.length; ai++) {
        const act = seq.actions[ai];
        const baseDur = Math.round((8 + r() * 26) * p.timeMul);

        if (act.type === 'quiz') {
          const bank = quizFor(act.id);
          const optCount = bank ? bank.options.length : 4;
          const correctIdx = bank ? bank.correctIndex : 0;
          const letters = ['A) ', 'B) ', 'C) ', 'D) ', 'E) '];
          const correctText = bank
            ? letters[correctIdx] + bank.options[correctIdx]
            : letters[correctIdx] + 'Doğru seçenek';

          let attempts = 0;
          let solved = false;
          // UIQuizPanel: yanlışta RetryAfterDelay ile tekrar denenir → her
          // deneme ayrı bir QuizAnswered event'i üretir, attempts kümülatiftir.
          while (!solved && attempts < 3) {
            attempts += 1;
            const isCorrect = r() > wrongRate || attempts === 3;
            let pickIdx = correctIdx;
            if (!isCorrect) {
              pickIdx = Math.floor(r() * optCount);
              if (pickIdx === correctIdx) pickIdx = (correctIdx + 1) % optCount;
            }
            const selText = bank
              ? letters[pickIdx] + bank.options[pickIdx]
              : letters[pickIdx] + 'Seçenek ' + (pickIdx + 1);

            const ts = Math.round(baseDur * (0.6 + attempts * 0.4));
            // EMP-1051 senaryosu: bazı eventlerde timeSpent eksik/0 gelir.
            const tsOut = (emp.profile === 'missingTimeSpent' && r() < 0.35)
              ? (r() < 0.5 ? null : 0)
              : ts;

            push('QuizAnswered', emp.id, dayOffset, {
              actionId: act.id,
              levelId: level.emittedLevelId,
              sequenceId: seq.id,
              // UIQuizPanel.cs:173 — questionId parametresine _actionID geçiliyor,
              // yani questionId her zaman actionId ile AYNI. Ayrı soru kimliği yok.
              questionId: act.id,
              selectedAnswer: selText,
               correctAnswer: correctText,
               isCorrect: isCorrect,
               attempts: attempts,
               attemptIndex: attempts,
               timeSpent: tsOut
            });

             if (isCorrect) { solved = true; }
            else {
              mistakes += 1; seqMistakes += 1;
              // SequenceManager.cs:671 — severity SABİT 1 gönderiliyor.
              push('MistakeRecorded', emp.id, dayOffset, {
                mistakeType: 'wrong_answer',
                actionId: act.id,
                levelId: level.emittedLevelId,
                sequenceId: seq.id,
                severity: 1
              });
           }
           quizTotal += 1;
           if (solved) quizCorrect += 1;
            seqSeconds += ts;
          }

          push('ActionCompleted', emp.id, dayOffset, {
            actionId: act.id,
            levelId: level.emittedLevelId,
            sequenceId: seq.id,
            type: 'quiz',
            objectId: null,
            duration: Math.round(baseDur * attempts),
            result: 'success'
          });

        } else if (act.type === 'drag_drop') {
          const placements = [];
          let attempts = 0;
          let done = false;
          while (!done && attempts < 4) {
            attempts += 1;
            const ok = r() > dropRate || attempts === 4;
            placements.push({
              item: act.id + '_item',
              droppedOn: ok ? (act.id + '_zone') : ('wrong_zone_' + attempts),
              correct: ok
            });
            if (ok) done = true;
            else {
              mistakes += 1; seqMistakes += 1;
              // UIDropZone.cs:179 — severity SABİT 1.
            push('MistakeRecorded', emp.id, dayOffset, {
              mistakeType: 'wrong_drop',
              actionId: act.id,
              levelId: level.emittedLevelId,
              sequenceId: seq.id,
              severity: 1
              });
            }
          }
          push('DragDropAttempt', emp.id, dayOffset, {
            actionId: act.id,
            levelId: level.emittedLevelId,
            sequenceId: seq.id,
            targetObject: act.id + '_zone',
            attempts: attempts,
            placements: placements
          });
          push('ActionCompleted', emp.id, dayOffset, {
            actionId: act.id,
            levelId: level.emittedLevelId,
            sequenceId: seq.id,
            type: 'drag_drop',
            objectId: act.id + '_target',
            duration: baseDur * attempts,
            result: 'success'
          });
          seqSeconds += baseDur * attempts;

        } else {
          // click / interaction / survey — sadece ActionCompleted üretir.
          // NOT: Survey sonuçları (cevaplar, fotoğraflar) HİÇBİR event'e yazılmıyor;
          // SurveyResultTracker.cs verileri yalnızca bellekte tutuyor.
          push('ActionCompleted', emp.id, dayOffset, {
            actionId: act.id,
            levelId: level.emittedLevelId,
            sequenceId: seq.id,
            type: act.type,
            objectId: act.id + '_obj',
            duration: baseDur,
            result: 'success'
          });
          seqSeconds += baseDur;
        }
      }

      levelSeconds += seqSeconds;

      if (!opts.abandonAfter || si < opts.abandonAfter) {
        push('SequenceCompleted', emp.id, dayOffset, {
          sequenceId: seq.id,
          levelId: level.emittedLevelId,
          timeSpent: seqSeconds,
          mistakes: seqMistakes,
          completed: true
        });
      }

      if (opts.abandonAfter && si >= opts.abandonAfter) {
        // Terk edilmiş oturum: LevelCompleted hiç gelmez, sadece SessionEnded.
        push('SessionEnded', emp.id, dayOffset, { levelId: level.emittedLevelId });
        return;
      }
    }

    // EMP-1052 senaryosu: QuizAnswered ile eşleşmeyen "yetim" MistakeRecorded.
    // Gerçekte de olabilir: MistakeRecorded'da levelId/sequenceId yok, bu yüzden
    // bir hatayı bir denemeye bağlamak her zaman mümkün olmayabilir.
    if (emp.profile === 'orphanMistakes') {
      const orphanActions = ['klemens', 'boxclick2', 'AGCheck_Act_09'];
      for (let i = 0; i < orphanActions.length; i++) {
        push('MistakeRecorded', emp.id, dayOffset, {
          mistakeType: i % 2 === 0 ? 'wrong_drop' : 'wrong_answer',
          actionId: orphanActions[i],
          severity: 1
        });
        mistakes += 1;
      }
    }

    // LevelData.maxScore=100, penaltyPerMistake=5 (LevelData.cs varsayılanları)
    const score = Math.max(0, 100 - mistakes * 5);

    push('LevelCompleted', emp.id, dayOffset, {
      levelId: level.emittedLevelId,
      completed: true,
      score: score,
      timeSpent: levelSeconds,
      mistakes: mistakes,
      // PlayFabDataManager.cs:311 — bu bir TÜRETİLMİŞ değerdir, gerçek
      // "tamamlanma yüzdesi" değil: Clamp01(1 - mistakes * 0.05)
      completionRate: Math.max(0, Math.min(1, 1 - mistakes * 0.05))
    });

    // LogLevelCompleted, quiz varsa QuizSummary'i otomatik gönderir.
    if (quizTotal > 0) {
      push('QuizSummary', emp.id, dayOffset, {
        levelId: level.emittedLevelId,
        totalQuestions: quizTotal,
        correctAnswers: quizCorrect,
        wrongAnswers: quizTotal - quizCorrect,
        accuracy: quizCorrect / quizTotal
      });
    }

    push('SessionEnded', emp.id, dayOffset, { levelId: level.emittedLevelId });
  }

  // ---------------------------------------------------------------------------
  // Senaryo planı — her persona farklı bir uç durumu kapsar.
  // ---------------------------------------------------------------------------
  const L1 = CONTENT.levels[0];
  const L2 = CONTENT.levels[1];
  const L3 = CONTENT.levels[2];

  function emp(id) { return EMPLOYEES.filter(function (e) { return e.id === id; })[0]; }

  // TEST001 — zengin veri: L1 üç deneme (gelişiyor), L2 iki deneme, L3 bir deneme
  emitRun(emp('TEST001'), L1, 46, { drift: +0.14, seedSalt: 1 });
  emitRun(emp('TEST001'), L1, 24, { drift: +0.02, seedSalt: 2 });
  emitRun(emp('TEST001'), L1, 5,  { drift: -0.09, seedSalt: 3 });
  emitRun(emp('TEST001'), L2, 19, { drift: +0.06, seedSalt: 4 });
  emitRun(emp('TEST001'), L2, 3,  { drift: -0.04, seedSalt: 5 });
  emitRun(emp('TEST001'), L3, 2,  { drift: 0.00,  seedSalt: 6 });

  // Başarılı çalışan
  emitRun(emp('EMP-1042'), L1, 33, { seedSalt: 11 });
  emitRun(emp('EMP-1042'), L1, 12, { drift: -0.02, seedSalt: 12 });
  emitRun(emp('EMP-1042'), L2, 6,  { seedSalt: 13 });

  // Birkaç yanlış cevabı olan
  emitRun(emp('EMP-1043'), L1, 28, { seedSalt: 21 });
  emitRun(emp('EMP-1043'), L2, 9,  { seedSalt: 22 });

  // Çok tekrar deneyen
  emitRun(emp('EMP-1044'), L1, 30, { seedSalt: 31 });
  emitRun(emp('EMP-1044'), L1, 14, { seedSalt: 32 });
  emitRun(emp('EMP-1044'), L2, 4,  { seedSalt: 33 });

  // Çok hata yapan
  emitRun(emp('EMP-1045'), L1, 21, { seedSalt: 41 });
  emitRun(emp('EMP-1045'), L2, 8,  { seedSalt: 42 });
  emitRun(emp('EMP-1045'), L3, 1,  { seedSalt: 43 });

  // Üç senaryoyu da oynayan denetçi
  emitRun(emp('EMP-1046'), L1, 40, { seedSalt: 51 });
  emitRun(emp('EMP-1046'), L2, 26, { seedSalt: 52 });
  emitRun(emp('EMP-1046'), L3, 11, { seedSalt: 53 });
  emitRun(emp('EMP-1046'), L3, 2,  { drift: -0.03, seedSalt: 54 });

  // Tek denemesi olan — karşılaştırma yapılamaz
  emitRun(emp('EMP-1047'), L1, 7, { seedSalt: 61 });

  // EMP-1048 — hiç event yok (boş durum testi). Bilerek emitRun çağrılmıyor.

  // Gelişen performans (hata oranı denemeden denemeye düşüyor)
  emitRun(emp('EMP-1049'), L1, 44, { drift: +0.10, seedSalt: 71 });
  emitRun(emp('EMP-1049'), L1, 27, { drift: -0.06, seedSalt: 72 });
  emitRun(emp('EMP-1049'), L1, 10, { drift: -0.22, seedSalt: 73 });

  // Kötüleşen performans
  emitRun(emp('EMP-1050'), L1, 41, { drift: -0.04, seedSalt: 81 });
  emitRun(emp('EMP-1050'), L1, 23, { drift: +0.16, seedSalt: 82 });
  emitRun(emp('EMP-1050'), L1, 6,  { drift: +0.30, seedSalt: 83 });

  // timeSpent alanı eksik gelen eventler
  emitRun(emp('EMP-1051'), L1, 16, { seedSalt: 91 });
  emitRun(emp('EMP-1051'), L2, 5,  { seedSalt: 92 });

  // QuizAnswered ile eşleşmeyen MistakeRecorded kayıtları
  emitRun(emp('EMP-1052'), L1, 18, { seedSalt: 101 });

  // Terk edilmiş oturum — LevelCompleted hiç gelmiyor
  emitRun(emp('EMP-1053'), L2, 13, { seedSalt: 111, abandonAfter: 2 });
  emitRun(emp('EMP-1053'), L1, 4,  { seedSalt: 112, abandonAfter: 3 });

  // Kronolojik sırala (gerçek bir sorgu sonucu gibi)
  events.sort(function (a, b) {
    return a.clientTimestamp < b.clientTimestamp ? -1 : a.clientTimestamp > b.clientTimestamp ? 1 : 0;
  });

  // ===========================================================================
  // DIŞA AKTAR
  // ===========================================================================
  window.TS_DATA = {
    IS_MOCK: true,
    TODAY: TODAY,
    content: CONTENT,
    quizBank: QUIZ_BANK,
    employees: EMPLOYEES,
    managers: MANAGERS,
    events: events,

    // Yardımcı arama tabloları -------------------------------------------------

    /** Telemetride görülen levelId → katalog kaydı */
    levelByEmittedId: (function () {
      const m = {};
      CONTENT.levels.forEach(function (l) { m[l.emittedLevelId] = l; });
      return m;
    })(),

    /** actionId → { level, sequence, action }
     *  MistakeRecorded eventinde levelId/sequenceId OLMADIĞI için hatayı
     *  senaryodaki yerine bağlamanın TEK yolu bu tablodur. Tablo, oyunun
     *  ScriptableObject içeriğinden türetilir; runtime'dan gelmez. */
    actionIndex: (function () {
      const m = {};
      CONTENT.levels.forEach(function (l) {
        l.sequences.forEach(function (s) {
          s.actions.forEach(function (a) {
            m[a.id] = { level: l, sequence: s, action: a };
          });
        });
      });
      return m;
    })(),

    employeeById: (function () {
      const m = {};
      EMPLOYEES.forEach(function (e) { m[e.id] = e; });
      MANAGERS.forEach(function (e) { m[e.id] = e; });
      return m;
    })()
  };
})();
