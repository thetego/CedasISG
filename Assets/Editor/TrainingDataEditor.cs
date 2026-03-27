using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;
using System.IO;

namespace SafetyTraining
{
	public class TrainingDataEditor : EditorWindow
	{
		// ═══════════════════════════════════════════════════════
		// CONSTANTS
		// ═══════════════════════════════════════════════════════

		private const string PREFS_SAVE_PATH = "SafetyTraining_SavePath";
		private const string DEFAULT_SAVE_PATH = "Assets/Data/Training";

		private const float COLUMN_WIDTH = 240f;
		private const float HEADER_HEIGHT = 28f;
		private const float ITEM_HEIGHT = 28f;
		private const float TOOLBAR_HEIGHT = 40f;
		private const float SEPARATOR = 2f;

		// ═══════════════════════════════════════════════════════
		// STATE
		// ═══════════════════════════════════════════════════════

		private string _savePath;

		private LevelData _selectedLevel;
		private SequenceData _selectedSequence;
		private ActionData _selectedAction;

		private List<LevelData> _allLevels = new List<LevelData>();

		private ReorderableList _levelList;
		private ReorderableList _sequenceList;
		private ReorderableList _actionList;

		private object _renamingTarget;
		private string _renamingText = "";

		private Vector2 _scrollLevels;
		private Vector2 _scrollSequences;
		private Vector2 _scrollActions;
		private Vector2 _scrollInspector;
		private float _inspectorContentHeight = 2000f; // dinamik güncellenir

		// Embedded inspector
		private Editor _embeddedEditor;
		private Object _lastInspectorTarget;

		// Renkler
		private static readonly Color COL_BG = new Color(0.18f, 0.18f, 0.18f);
		private static readonly Color COL_HEADER = new Color(0.13f, 0.13f, 0.13f);
		private static readonly Color COL_SELECTED = new Color(0.17f, 0.36f, 0.53f);
		private static readonly Color COL_HOVER = new Color(0.25f, 0.25f, 0.25f);
		private static readonly Color COL_BORDER = new Color(0.10f, 0.10f, 0.10f);
		private static readonly Color COL_ADD = new Color(0.20f, 0.55f, 0.25f);
		private static readonly Color COL_DELETE = new Color(0.55f, 0.18f, 0.18f);
		private static readonly Color COL_ACCENT = new Color(0.20f, 0.60f, 0.86f);

		// ═══════════════════════════════════════════════════════
		// OPEN
		// ═══════════════════════════════════════════════════════

		[MenuItem("Safety Training/Training Data Editor")]
		public static void OpenWindow()
		{
			var w = GetWindow<TrainingDataEditor>("Training Data Editor");
			w.minSize = new Vector2(1100f, 560f);
			w.Show();
		}

		// ═══════════════════════════════════════════════════════
		// LIFECYCLE
		// ═══════════════════════════════════════════════════════

		private void OnEnable()
		{
			_savePath = EditorPrefs.GetString(PREFS_SAVE_PATH, DEFAULT_SAVE_PATH);
			RefreshLevelList();
			BuildLevelReorderableList();
		}

		private void OnFocus() => RefreshLevelList();

		private void OnDisable()
		{
			DestroyEmbeddedEditor();
		}

		// ═══════════════════════════════════════════════════════
		// MAIN GUI
		// ═══════════════════════════════════════════════════════

		private void OnGUI()
		{
			DrawToolbar();
			DrawColumns();
		}

		// ─── Toolbar ────────────────────────────────────────────

		private void DrawToolbar()
		{
			EditorGUI.DrawRect(new Rect(0, 0, position.width, TOOLBAR_HEIGHT), COL_HEADER);

			GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
			{
				fontSize = 13,
				normal = { textColor = Color.white }
			};
			GUI.Label(new Rect(12, 8, 220, 24), "Training Data Editor", titleStyle);

			GUIStyle pathLabel = new GUIStyle(EditorStyles.label)
			{
				normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
			};
			GUI.Label(new Rect(220, 10, 65, 20), "Save Path:", pathLabel);

			string newPath = GUI.TextField(
				new Rect(288, 10, position.width - 380, 20),
				_savePath, EditorStyles.textField);

			if (newPath != _savePath)
			{
				_savePath = newPath;
				EditorPrefs.SetString(PREFS_SAVE_PATH, _savePath);
			}

			if (DrawButton(new Rect(position.width - 176, 8, 82, 24), "⬇ Export", new Color(0.25f, 0.50f, 0.30f)))
				ShowExportMenu();

			if (DrawButton(new Rect(position.width - 86, 8, 78, 24), "↺ Refresh", COL_ACCENT))
			{
				RefreshLevelList();
				BuildLevelReorderableList();
				BuildSequenceReorderableList();
				BuildActionReorderableList();
				DestroyEmbeddedEditor();
			}


		}

		// ─── Kolonlar ────────────────────────────────────────────

		private void DrawColumns()
		{
			float y = TOOLBAR_HEIGHT;
			float h = position.height - TOOLBAR_HEIGHT;
			float cw = COLUMN_WIDTH;

			EditorGUI.DrawRect(new Rect(0, y, position.width, h), COL_BG);

			// Kolon 1 — Levels
			DrawColumn(new Rect(0, y, cw, h), "LEVELS",
				_levelList, ref _scrollLevels, OnAddLevel);

			DrawSep(cw, y, h);

			// Kolon 2 — Sequences
			DrawColumn(new Rect(cw + SEPARATOR, y, cw, h),
				_selectedLevel != null ? $"SEQUENCES  —  {_selectedLevel.levelName}" : "SEQUENCES",
				_sequenceList, ref _scrollSequences, OnAddSequence,
				_selectedLevel == null);

			// Prerequisite yenile butonu — sequence kolonunun altında
			if (_selectedLevel != null)
			{
				Rect prereqBtn = new Rect(cw + SEPARATOR + 4, y + h - 30, cw - 8, 24);
				if (DrawButton(prereqBtn, "↺ Prerequisite'leri Yenile", new Color(0.35f, 0.25f, 0.55f)))
					RebuildAllPrerequisites();
			}

			DrawSep(cw * 2 + SEPARATOR, y, h);

			// Kolon 3 — Actions
			DrawColumn(new Rect(cw * 2 + SEPARATOR * 2, y, cw, h),
				_selectedSequence != null ? $"ACTIONS  —  {_selectedSequence.sequenceName}" : "ACTIONS",
				_actionList, ref _scrollActions, OnAddAction,
				_selectedSequence == null);

			DrawSep(cw * 3 + SEPARATOR * 2, y, h);

			// Kolon 4 — Embedded Inspector
			float inspX = cw * 3 + SEPARATOR * 3;
			float inspW = position.width - inspX;
			if (inspW > 20)
				DrawEmbeddedInspector(new Rect(inspX, y, inspW, h));
		}

		private void DrawSep(float x, float y, float h) =>
			EditorGUI.DrawRect(new Rect(x, y, SEPARATOR, h), COL_BORDER);

		// ─── Tek Kolon ───────────────────────────────────────────

		private void DrawColumn(Rect rect, string title, ReorderableList list,
			ref Vector2 scroll, System.Action onAdd, bool disabled = false)
		{
			EditorGUI.DrawRect(rect, COL_BG);

			// Header
			Rect hr = new Rect(rect.x, rect.y, rect.width, HEADER_HEIGHT);
			EditorGUI.DrawRect(hr, COL_HEADER);

			GUIStyle hs = new GUIStyle(EditorStyles.boldLabel)
			{
				fontSize = 11,
				normal = { textColor = disabled ? new Color(0.5f, 0.5f, 0.5f) : new Color(0.85f, 0.85f, 0.85f) }
			};
			GUI.Label(new Rect(rect.x + 10, rect.y + 5, rect.width - 50, HEADER_HEIGHT), title, hs);

			if (!disabled)
			{
				if (DrawButton(new Rect(rect.x + rect.width - 36, rect.y + 4, 28, 20), "+", COL_ADD))
					onAdd?.Invoke();
			}

			Rect listArea = new Rect(rect.x, rect.y + HEADER_HEIGHT, rect.width, rect.height - HEADER_HEIGHT);

			if (disabled)
			{
				GUIStyle hint = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
				{
					normal = { textColor = new Color(0.4f, 0.4f, 0.4f) }
				};
				GUI.Label(new Rect(listArea.x, listArea.y + 40, listArea.width, 30),
					"← Önce sol kolondan seçin", hint);
				return;
			}

			if (list == null) return;

			scroll = GUI.BeginScrollView(listArea, scroll,
				new Rect(0, 0, rect.width - 16, list.count * ITEM_HEIGHT + 40));
			list.DoList(new Rect(4, 4, rect.width - 20, list.count * ITEM_HEIGHT + 40));
			GUI.EndScrollView();
		}

		// ─── Embedded Inspector ──────────────────────────────────

		private void DrawEmbeddedInspector(Rect rect)
		{
			EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));

			// Header
			Rect hr = new Rect(rect.x, rect.y, rect.width, HEADER_HEIGHT);
			EditorGUI.DrawRect(hr, COL_HEADER);

			Object target = (Object)_selectedAction ?? (Object)_selectedSequence ?? _selectedLevel;

			GUIStyle hs = new GUIStyle(EditorStyles.boldLabel)
			{
				fontSize = 11,
				normal = { textColor = new Color(0.85f, 0.85f, 0.85f) }
			};

			string headerLabel = target == null ? "INSPECTOR"
				: target is LevelData ? $"LEVEL  —  {((LevelData)target).levelName}"
				: target is SequenceData ? $"SEQUENCE  —  {((SequenceData)target).sequenceName}"
				: $"ACTION  —  {((ActionData)target).actionName}";

			GUI.Label(new Rect(rect.x + 10, rect.y + 5, rect.width - 20, HEADER_HEIGHT), headerLabel, hs);

			if (target == null)
			{
				GUIStyle hint = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
				{
					normal = { textColor = new Color(0.4f, 0.4f, 0.4f) }
				};
				GUI.Label(new Rect(rect.x, rect.y + 80, rect.width, 30), "Bir öğe seçin", hint);
				return;
			}

			// Editor'ı oluştur / güncelle
			if (target != _lastInspectorTarget)
			{
				DestroyEmbeddedEditor();
				_embeddedEditor = Editor.CreateEditor(target);
				_lastInspectorTarget = target;
			}

			if (_embeddedEditor == null) return;

			// Inspector scroll alanı — içerik yüksekliği dinamik
			Rect scrollArea = new Rect(rect.x, rect.y + HEADER_HEIGHT, rect.width, rect.height - HEADER_HEIGHT);

			_scrollInspector = GUI.BeginScrollView(scrollArea, _scrollInspector,
				new Rect(0, 0, rect.width - 16, _inspectorContentHeight));

			GUILayout.BeginArea(new Rect(4, 4, rect.width - 20, _inspectorContentHeight));
			EditorGUIUtility.labelWidth = Mathf.Max(80f, rect.width * 0.38f);

			EditorGUI.BeginChangeCheck();

			// İçerik yüksekliğini ölç
			float beforeY = GUILayoutUtility.GetLastRect().yMax;
			_embeddedEditor.OnInspectorGUI();
			Rect lastRect = GUILayoutUtility.GetLastRect();
			if (Event.current.type == EventType.Repaint && lastRect.yMax > 10f)
				_inspectorContentHeight = Mathf.Max(lastRect.yMax + 40f, rect.height - HEADER_HEIGHT);

			if (EditorGUI.EndChangeCheck())
			{
				EditorUtility.SetDirty(target);
				AssetDatabase.SaveAssets();
			}

			GUILayout.EndArea();
			GUI.EndScrollView();
		}

		private void DestroyEmbeddedEditor()
		{
			if (_embeddedEditor != null)
			{
				DestroyImmediate(_embeddedEditor);
				_embeddedEditor = null;
				_lastInspectorTarget = null;
			}
		}

		// ═══════════════════════════════════════════════════════
		// REORDERABLE LISTS
		// ═══════════════════════════════════════════════════════

		private void BuildLevelReorderableList()
		{
			_levelList = new ReorderableList(_allLevels, typeof(LevelData), true, false, false, false);
			_levelList.elementHeight = ITEM_HEIGHT;
			_levelList.drawElementCallback = DrawLevelElement;
			_levelList.onSelectCallback = (list) =>
			{
				_selectedLevel = _allLevels[list.index];
				_selectedSequence = null;
				_selectedAction = null;
				BuildSequenceReorderableList();
				BuildActionReorderableList();
				DestroyEmbeddedEditor();
				Repaint();
			};
			_levelList.onReorderCallbackWithDetails = (list, oi, ni) => Repaint();
		}

		private void BuildSequenceReorderableList()
		{
			if (_selectedLevel == null) { _sequenceList = null; return; }

			var sequences = new List<SequenceData>(_selectedLevel.sequences ?? new SequenceData[0]);
			_sequenceList = new ReorderableList(sequences, typeof(SequenceData), true, false, false, false);
			_sequenceList.elementHeight = ITEM_HEIGHT;
			_sequenceList.drawElementCallback = DrawSequenceElement;
			_sequenceList.onSelectCallback = (list) =>
			{
				_selectedSequence = sequences[list.index];
				_selectedAction = null;
				BuildActionReorderableList();
				DestroyEmbeddedEditor();
				Repaint();
			};
			_sequenceList.onReorderCallbackWithDetails = (list, oi, ni) =>
			{
				ApplySequenceOrder(sequences);
				Repaint();
			};
		}

		private void BuildActionReorderableList()
		{
			if (_selectedSequence == null) { _actionList = null; return; }

			var actions = new List<ActionData>(_selectedSequence.actions ?? new ActionData[0]);
			_actionList = new ReorderableList(actions, typeof(ActionData), true, false, false, false);
			_actionList.elementHeight = ITEM_HEIGHT;
			_actionList.drawElementCallback = DrawActionElement;
			_actionList.onSelectCallback = (list) =>
			{
				_selectedAction = actions[list.index];
				DestroyEmbeddedEditor();
				Repaint();
			};
			_actionList.onReorderCallbackWithDetails = (list, oi, ni) =>
			{
				ApplyActionOrder(actions);
				Repaint();
			};
		}

		// ─── Element Çiziciler ────────────────────────────────────

		private void DrawLevelElement(Rect rect, int index, bool isActive, bool isFocused)
		{
			if (index >= _allLevels.Count) return;
			LevelData level = _allLevels[index];
			DrawItemRow(rect, level, level == _selectedLevel,
				level.levelName, $"{level.GetSequenceCount()} seq",
				() => {
					_selectedLevel = level; _selectedSequence = null; _selectedAction = null;
					BuildSequenceReorderableList(); BuildActionReorderableList(); DestroyEmbeddedEditor();
				},
				() => OnRenameLevel(level),
				() => OnDeleteLevel(level));
		}

		private void DrawSequenceElement(Rect rect, int index, bool isActive, bool isFocused)
		{
			if (_selectedLevel?.sequences == null || index >= _selectedLevel.sequences.Length) return;
			SequenceData seq = _selectedLevel.sequences[index];
			if (seq == null) return;
			DrawItemRow(rect, seq, seq == _selectedSequence,
				seq.sequenceName, $"{seq.GetTotalActionCount()} act",
				() => {
					_selectedSequence = seq; _selectedAction = null;
					BuildActionReorderableList(); DestroyEmbeddedEditor();
				},
				() => OnRenameSequence(seq),
				() => OnDeleteSequence(seq),
				null,
				() => OnDuplicateSequence(seq));
		}

		private void DrawActionElement(Rect rect, int index, bool isActive, bool isFocused)
		{
			if (_selectedSequence?.actions == null || index >= _selectedSequence.actions.Length) return;
			ActionData action = _selectedSequence.actions[index];
			if (action == null) return;
			DrawItemRow(rect, action, action == _selectedAction,
				action.actionName, action.actionType.ToString(),
				() => { _selectedAction = action; DestroyEmbeddedEditor(); },
				() => OnRenameAction(action),
				() => OnDeleteAction(action),
				GetActionTypeColor(action.actionType),
				() => OnDuplicateAction(action));
		}

		// ─── Ortak Satır Çizici ──────────────────────────────────

		private void DrawItemRow(Rect rect, Object target, bool selected,
			string label, string badge,
			System.Action onClick, System.Action onRename, System.Action onDelete,
			Color? badgeColor = null, System.Action onDuplicate = null)
		{
			Color bg = selected ? COL_SELECTED
				: rect.Contains(Event.current.mousePosition) ? COL_HOVER : COL_BG;
			EditorGUI.DrawRect(new Rect(rect.x, rect.y + 1, rect.width, rect.height - 2), bg);

			// Rename modu
			if (_renamingTarget == target)
			{
				GUI.SetNextControlName("RenameField");
				_renamingText = GUI.TextField(
					new Rect(rect.x + 6, rect.y + 4, rect.width - 80, rect.height - 8), _renamingText);
				EditorGUI.FocusTextInControl("RenameField");

				if (DrawButton(new Rect(rect.x + rect.width - 72, rect.y + 4, 34, rect.height - 8), "✓", COL_ADD))
					CommitRename(target);
				if (DrawButton(new Rect(rect.x + rect.width - 36, rect.y + 4, 30, rect.height - 8), "✕", COL_DELETE))
					CancelRename();
				if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
					CommitRename(target);
				if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
					CancelRename();
				return;
			}

			// Etiket
			GUIStyle ls = new GUIStyle(EditorStyles.label)
			{
				normal = { textColor = selected ? Color.white : new Color(0.85f, 0.85f, 0.85f) },
				fontStyle = selected ? FontStyle.Bold : FontStyle.Normal
			};
			GUI.Label(new Rect(rect.x + 8, rect.y + 2, rect.width - 100, rect.height - 2), label, ls);

			// Badge
			Color bc = badgeColor ?? new Color(0.35f, 0.35f, 0.35f);
			EditorGUI.DrawRect(new Rect(rect.x + rect.width - 90, rect.y + 5, 52, rect.height - 10), bc);
			GUIStyle bs = new GUIStyle(EditorStyles.miniLabel)
			{
				alignment = TextAnchor.MiddleCenter,
				normal = { textColor = Color.white }
			};
			GUI.Label(new Rect(rect.x + rect.width - 90, rect.y + 4, 52, rect.height - 8), badge, bs);

			// Duplicate butonu (sadece onDuplicate verilmişse)
			float btnRight = rect.x + rect.width;
			if (onDuplicate != null)
			{
				if (DrawButton(new Rect(btnRight - 52, rect.y + 4, 14, rect.height - 8),
					"❐", new Color(0.20f, 0.40f, 0.55f), 10)) onDuplicate?.Invoke();
			}
			if (DrawButton(new Rect(btnRight - 36, rect.y + 4, 14, rect.height - 8),
				"✎", new Color(0.3f, 0.3f, 0.3f), 10)) onRename?.Invoke();
			if (DrawButton(new Rect(btnRight - 20, rect.y + 4, 14, rect.height - 8),
				"✕", new Color(0.3f, 0.3f, 0.3f), 10)) onDelete?.Invoke();

			if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
			{
				onClick?.Invoke();
				Repaint();
			}
		}

		// ═══════════════════════════════════════════════════════
		// ADD OPERATIONS
		// ═══════════════════════════════════════════════════════

		private void OnAddLevel()
		{
			EnsureSavePath();

			// Level için kendi klasörü
			string levelName = "NewLevel";
			string levelFolder = GetUniqueLevelFolder(levelName);
			string path = $"{levelFolder}/{levelName}.asset";

			LevelData level = CreateInstance<LevelData>();
			level.levelID = levelName;
			level.levelName = levelName;
			SaveAsset(level, path);

			RefreshLevelList();
			BuildLevelReorderableList();
			_selectedLevel = level;
			BuildSequenceReorderableList();
			BuildActionReorderableList();
			DestroyEmbeddedEditor();
			StartRename(level, levelName);
		}

		private void OnAddSequence()
		{
			if (_selectedLevel == null) return;
			EnsureSavePath();

			// Prefix: level adından türet
			string prefix = SanitizeName(_selectedLevel.levelName);
			int nextIndex = (_selectedLevel.sequences?.Length ?? 0) + 1;
			string seqName = $"{prefix}_Seq_{nextIndex:D2}";

			string levelFolder = GetLevelFolder(_selectedLevel);
			string seqFolder = GetUniqueSequenceFolder(levelFolder, seqName);
			string path = $"{seqFolder}/{seqName}.asset";

			SequenceData seq = CreateInstance<SequenceData>();
			seq.sequenceID = seqName;
			seq.sequenceName = seqName;
			SaveAsset(seq, path);

			// Mevcut sequence'ları prerequisite olarak ekle
			if (_selectedLevel.sequences != null && _selectedLevel.sequences.Length > 0)
			{
				var prereqs = new System.Collections.Generic.List<SequenceData>();
				foreach (var existing in _selectedLevel.sequences)
					if (existing != null) prereqs.Add(existing);

				SerializedObject prereqSO = new SerializedObject(seq);
				SerializedProperty prereqArr = prereqSO.FindProperty("prerequisiteSequences");
				prereqArr.arraySize = prereqs.Count;
				for (int i = 0; i < prereqs.Count; i++)
					prereqArr.GetArrayElementAtIndex(i).objectReferenceValue = prereqs[i];
				prereqSO.ApplyModifiedProperties();
				EditorUtility.SetDirty(seq);
				AssetDatabase.SaveAssets();
			}

			SerializedObject so = new SerializedObject(_selectedLevel);
			SerializedProperty arr = so.FindProperty("sequences");
			arr.arraySize++;
			arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = seq;
			so.ApplyModifiedProperties();
			EditorUtility.SetDirty(_selectedLevel);
			AssetDatabase.SaveAssets();

			BuildSequenceReorderableList();
			_selectedSequence = seq;
			BuildActionReorderableList();
			DestroyEmbeddedEditor();
			Repaint();
		}

		// ─── Action Ekleme ───────────────────────────────────────

		private bool _pendingActionCreate = false;
		private string _pendingActionPath = "";

		private void OnAddAction()
		{
			if (_selectedSequence == null) return;
			EnsureSavePath();

			// Prefix: sequence adından türet
			string prefix = SanitizeName(_selectedSequence.sequenceName);
			int nextIndex = (_selectedSequence.actions?.Length ?? 0) + 1;
			string actionName = $"{prefix}_Act_{nextIndex:D2}";

			string seqFolder = GetSequenceFolder(_selectedSequence);
			_pendingActionPath = AssetDatabase.GenerateUniqueAssetPath($"{seqFolder}/{actionName}.asset");
			_pendingActionCreate = true;

			GenericMenu menu = new GenericMenu();
			foreach (ActionType val in System.Enum.GetValues(typeof(ActionType)))
			{
				ActionType captured = val;
				menu.AddItem(new GUIContent(captured.ToString()), false,
					() => CreateActionWithType(captured));
			}
			// If the menu is dismissed without a selection, reset the pending flag
			menu.AddSeparator("");
			menu.AddItem(new GUIContent("İptal"), false, () => { _pendingActionCreate = false; });
			menu.ShowAsContext();
		}

		private void CreateActionWithType(ActionType type)
		{
			if (!_pendingActionCreate || string.IsNullOrEmpty(_pendingActionPath)) return;
			_pendingActionCreate = false;

			string autoName = Path.GetFileNameWithoutExtension(_pendingActionPath);
			ActionData action = CreateInstance<ActionData>();
			action.actionID = autoName;
			action.actionName = autoName;
			action.actionType = type;
			SaveAsset(action, _pendingActionPath);

			SerializedObject so = new SerializedObject(_selectedSequence);
			SerializedProperty arr = so.FindProperty("actions");
			arr.arraySize++;
			arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = action;
			so.ApplyModifiedProperties();
			EditorUtility.SetDirty(_selectedSequence);
			AssetDatabase.SaveAssets();

			BuildActionReorderableList();
			_selectedAction = action;
			DestroyEmbeddedEditor();
			Repaint();
		}

		// ═══════════════════════════════════════════════════════
		// DUPLICATE OPERATIONS
		// ═══════════════════════════════════════════════════════

		private void OnDuplicateSequence(SequenceData source)
		{
			if (_selectedLevel == null || source == null) return;
			EnsureSavePath();

			// Yeni isim ve klasör
			string levelFolder = GetLevelFolder(_selectedLevel);
			string newName = source.sequenceName + "_Copy";
			string seqFolder = GetUniqueSequenceFolder(levelFolder, newName);

			// SequenceData kopyala
			SequenceData copy = Instantiate(source);
			copy.sequenceID = newName;
			copy.sequenceName = newName;
			copy.actions = null; // action'ları ayrı kopyalayacağız

			string seqPath = $"{seqFolder}/{newName}.asset";
			SaveAsset(copy, seqPath);

			// Action'ları da kopyala
			if (source.actions != null && source.actions.Length > 0)
			{
				ActionData[] copiedActions = new ActionData[source.actions.Length];
				for (int i = 0; i < source.actions.Length; i++)
				{
					ActionData srcAction = source.actions[i];
					if (srcAction == null) continue;

					ActionData actionCopy = Instantiate(srcAction);
					string actionName = srcAction.actionName;
					string actionPath = AssetDatabase.GenerateUniqueAssetPath($"{seqFolder}/{actionName}.asset");
					actionCopy.actionID = Path.GetFileNameWithoutExtension(actionPath);
					SaveAsset(actionCopy, actionPath);
					copiedActions[i] = actionCopy;
				}

				// Kopyalanan action'ları sequence'a bağla
				SerializedObject so = new SerializedObject(copy);
				SerializedProperty arr = so.FindProperty("actions");
				arr.arraySize = copiedActions.Length;
				for (int i = 0; i < copiedActions.Length; i++)
					arr.GetArrayElementAtIndex(i).objectReferenceValue = copiedActions[i];
				so.ApplyModifiedProperties();
				EditorUtility.SetDirty(copy);
				AssetDatabase.SaveAssets();
			}

			// Level'a ekle
			SerializedObject lso = new SerializedObject(_selectedLevel);
			SerializedProperty seqArr = lso.FindProperty("sequences");
			seqArr.arraySize++;
			seqArr.GetArrayElementAtIndex(seqArr.arraySize - 1).objectReferenceValue = copy;
			lso.ApplyModifiedProperties();
			EditorUtility.SetDirty(_selectedLevel);
			AssetDatabase.SaveAssets();

			BuildSequenceReorderableList();
			_selectedSequence = copy;
			BuildActionReorderableList();
			DestroyEmbeddedEditor();
			Repaint();
		}

		private void OnDuplicateAction(ActionData source)
		{
			if (_selectedSequence == null || source == null) return;
			EnsureSavePath();

			string seqFolder = GetSequenceFolder(_selectedSequence);
			string newName = source.actionName + "_Copy";
			string actionPath = AssetDatabase.GenerateUniqueAssetPath($"{seqFolder}/{newName}.asset");

			ActionData copy = Instantiate(source);
			copy.actionID = Path.GetFileNameWithoutExtension(actionPath);
			copy.actionName = copy.actionID;
			SaveAsset(copy, actionPath);

			// Sequence'a ekle
			SerializedObject so = new SerializedObject(_selectedSequence);
			SerializedProperty arr = so.FindProperty("actions");
			arr.arraySize++;
			arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = copy;
			so.ApplyModifiedProperties();
			EditorUtility.SetDirty(_selectedSequence);
			AssetDatabase.SaveAssets();

			BuildActionReorderableList();
			_selectedAction = copy;
			DestroyEmbeddedEditor();
			Repaint();
		}

		// ═══════════════════════════════════════════════════════
		// DELETE OPERATIONS
		// ═══════════════════════════════════════════════════════

		private void OnDeleteLevel(LevelData level)
		{
			if (!EditorUtility.DisplayDialog("Level Sil",
				$"'{level.levelName}' ve tüm klasörü silinsin mi?", "Sil", "İptal")) return;

			// Klasörün tamamını sil
			string folder = GetLevelFolder(level);
			if (AssetDatabase.IsValidFolder(folder))
				AssetDatabase.DeleteAsset(folder);
			else
				AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(level));

			if (_selectedLevel == level)
			{ _selectedLevel = null; _selectedSequence = null; _selectedAction = null; }

			DestroyEmbeddedEditor();
			RefreshLevelList();
			BuildLevelReorderableList();
			BuildSequenceReorderableList();
			BuildActionReorderableList();
		}

		private void OnDeleteSequence(SequenceData seq)
		{
			if (!EditorUtility.DisplayDialog("Sequence Sil",
				$"'{seq.sequenceName}' ve tüm action'ları silinsin mi?", "Sil", "İptal")) return;

			SerializedObject so = new SerializedObject(_selectedLevel);
			SerializedProperty arr = so.FindProperty("sequences");
			for (int i = 0; i < arr.arraySize; i++)
			{
				if (arr.GetArrayElementAtIndex(i).objectReferenceValue == seq)
				{
					arr.GetArrayElementAtIndex(i).objectReferenceValue = null;
					arr.DeleteArrayElementAtIndex(i);
					break;
				}
			}
			so.ApplyModifiedProperties();
			EditorUtility.SetDirty(_selectedLevel);
			AssetDatabase.SaveAssets();

			string folder = GetSequenceFolder(seq);
			if (AssetDatabase.IsValidFolder(folder))
				AssetDatabase.DeleteAsset(folder);
			else
				AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(seq));

			if (_selectedSequence == seq) { _selectedSequence = null; _selectedAction = null; }
			DestroyEmbeddedEditor();
			BuildSequenceReorderableList();
			BuildActionReorderableList();
		}

		private void OnDeleteAction(ActionData action)
		{
			if (!EditorUtility.DisplayDialog("Action Sil",
				$"'{action.actionName}' silinsin mi?", "Sil", "İptal")) return;

			SerializedObject so = new SerializedObject(_selectedSequence);
			SerializedProperty arr = so.FindProperty("actions");
			for (int i = 0; i < arr.arraySize; i++)
			{
				if (arr.GetArrayElementAtIndex(i).objectReferenceValue == action)
				{
					arr.GetArrayElementAtIndex(i).objectReferenceValue = null;
					arr.DeleteArrayElementAtIndex(i);
					break;
				}
			}
			so.ApplyModifiedProperties();
			EditorUtility.SetDirty(_selectedSequence);
			AssetDatabase.SaveAssets();

			AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(action));
			if (_selectedAction == action) _selectedAction = null;
			DestroyEmbeddedEditor();
			BuildActionReorderableList();
		}

		// ═══════════════════════════════════════════════════════
		// RENAME
		// ═══════════════════════════════════════════════════════

		private void OnRenameLevel(LevelData level) => StartRename(level, level.levelName);
		private void OnRenameSequence(SequenceData seq) => StartRename(seq, seq.sequenceName);
		private void OnRenameAction(ActionData action) => StartRename(action, action.actionName);

		private void StartRename(Object target, string current)
		{
			_renamingTarget = target;
			_renamingText = current;
		}

		private void CommitRename(Object target)
		{
			if (string.IsNullOrWhiteSpace(_renamingText)) { CancelRename(); return; }
			string name = _renamingText.Trim();

			// Veri alanını güncelle
			if (target is LevelData l) { l.levelName = name; EditorUtility.SetDirty(l); }
			else if (target is SequenceData s) { s.sequenceName = name; EditorUtility.SetDirty(s); }
			else if (target is ActionData a) { a.actionName = name; EditorUtility.SetDirty(a); }

			// Dosya adını güncelle
			string assetPath = AssetDatabase.GetAssetPath(target);
			if (!string.IsNullOrEmpty(assetPath))
				AssetDatabase.RenameAsset(assetPath, name);

			// Level / Sequence için klasör adını da güncelle
			if (target is LevelData ld)
			{
				string oldFolder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(ld)).Replace('\\', '/');
				string parent = Path.GetDirectoryName(oldFolder).Replace('\\', '/');
				string newFolder = AssetDatabase.GenerateUniqueAssetPath($"{parent}/{name}");
				AssetDatabase.MoveAsset(oldFolder, newFolder);
			}
			else if (target is SequenceData sd)
			{
				string oldFolder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(sd)).Replace('\\', '/');
				string parent = Path.GetDirectoryName(oldFolder).Replace('\\', '/');
				string newFolder = AssetDatabase.GenerateUniqueAssetPath($"{parent}/{name}");
				AssetDatabase.MoveAsset(oldFolder, newFolder);
			}

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
			_renamingTarget = null;
			DestroyEmbeddedEditor();
			Repaint();
		}

		private void CancelRename() { _renamingTarget = null; Repaint(); }

		// ═══════════════════════════════════════════════════════
		// ORDER APPLY
		// ═══════════════════════════════════════════════════════

		private void ApplySequenceOrder(List<SequenceData> ordered)
		{
			SerializedObject so = new SerializedObject(_selectedLevel);
			SerializedProperty arr = so.FindProperty("sequences");
			arr.arraySize = ordered.Count;
			for (int i = 0; i < ordered.Count; i++)
				arr.GetArrayElementAtIndex(i).objectReferenceValue = ordered[i];
			so.ApplyModifiedProperties();
			EditorUtility.SetDirty(_selectedLevel);
			AssetDatabase.SaveAssets();
		}

		private void ApplyActionOrder(List<ActionData> ordered)
		{
			SerializedObject so = new SerializedObject(_selectedSequence);
			SerializedProperty arr = so.FindProperty("actions");
			arr.arraySize = ordered.Count;
			for (int i = 0; i < ordered.Count; i++)
				arr.GetArrayElementAtIndex(i).objectReferenceValue = ordered[i];
			so.ApplyModifiedProperties();
			EditorUtility.SetDirty(_selectedSequence);
			AssetDatabase.SaveAssets();
		}

		// ═══════════════════════════════════════════════════════
		// KLASÖR YARDIMCILARI
		// ═══════════════════════════════════════════════════════

		/// Levels kök klasörü
		private string LevelsRoot => $"{_savePath}/Levels";

		/// Level'ın klasörü: .../Levels/LevelAdı/
		private string GetLevelFolder(LevelData level)
		{
			string assetPath = AssetDatabase.GetAssetPath(level);
			if (!string.IsNullOrEmpty(assetPath))
				return Path.GetDirectoryName(assetPath).Replace('\\', '/');
			return $"{LevelsRoot}/{SanitizeName(level.levelName)}";
		}

		/// Sequence'ın klasörü: .../Levels/LevelAdı/SequenceAdı/
		private string GetSequenceFolder(SequenceData seq)
		{
			string assetPath = AssetDatabase.GetAssetPath(seq);
			if (!string.IsNullOrEmpty(assetPath))
				return Path.GetDirectoryName(assetPath).Replace('\\', '/');
			return "";
		}

		/// Benzersiz level klasörü yolu
		private string GetUniqueLevelFolder(string name)
		{
			EnsureFolderExists(LevelsRoot);
			string folder = AssetDatabase.GenerateUniqueAssetPath($"{LevelsRoot}/{SanitizeName(name)}");
			AssetDatabase.CreateFolder(LevelsRoot, Path.GetFileName(folder));
			return folder;
		}

		/// Benzersiz sequence klasörü yolu (level klasörü altında)
		private string GetUniqueSequenceFolder(string levelFolder, string name)
		{
			EnsureFolderExists(levelFolder);
			string folder = AssetDatabase.GenerateUniqueAssetPath($"{levelFolder}/{SanitizeName(name)}");
			AssetDatabase.CreateFolder(levelFolder, Path.GetFileName(folder));
			return folder;
		}

		private string SanitizeName(string name) =>
			string.IsNullOrEmpty(name) ? "Unnamed"
			: name.Replace("/", "-").Replace("\\", "-").Trim();

		// ═══════════════════════════════════════════════════════
		// EXPORT
		// ═══════════════════════════════════════════════════════

		private void ShowExportMenu()
		{
			GenericMenu menu = new GenericMenu();

			if (_selectedAction != null)
				menu.AddItem(new GUIContent("Seçili Action'ı Export Et"), false, () => ExportAction(_selectedAction));
			else
				menu.AddDisabledItem(new GUIContent("Seçili Action'ı Export Et"));

			if (_selectedSequence != null)
				menu.AddItem(new GUIContent("Seçili Sequence'ı Export Et"), false, () => ExportSequence(_selectedSequence));
			else
				menu.AddDisabledItem(new GUIContent("Seçili Sequence'ı Export Et"));

			if (_selectedLevel != null)
				menu.AddItem(new GUIContent("Tüm Level'ı Export Et"), false, () => ExportLevel(_selectedLevel));
			else
				menu.AddDisabledItem(new GUIContent("Tüm Level'ı Export Et"));

			menu.ShowAsContext();
		}

		// ─── Action export ───────────────────────────────────────

		private string ActionToMarkdown(ActionData a, int index = -1)
		{
			var sb = new System.Text.StringBuilder();

			string prefix = index >= 0 ? $"{index + 1}. " : "";
			sb.AppendLine($"### {prefix}{EscapeMd(a.actionName)}");
			sb.AppendLine();
			sb.AppendLine("| Alan | Değer |");
			sb.AppendLine("|------|-------|");
			sb.AppendLine($"| ID | `{EscapeMd(a.actionID)}` |");
			sb.AppendLine($"| Tip | **{a.actionType}** |");

			if (!string.IsNullOrEmpty(a.instructionText))
				sb.AppendLine($"| Talimat | {EscapeMd(a.instructionText)} |");

			if (!string.IsNullOrEmpty(a.targetObjectID))
				sb.AppendLine($"| Hedef Obje | `{EscapeMd(a.targetObjectID)}` |");

			if (a.activatesTablet)
				sb.AppendLine($"| Tablet | ✅ Aktifleştirir |");

			if (a.deactivatesTablet)
				sb.AppendLine($"| Tablet | ❌ Pasifleştirir |");

			// Type'a özel alanlar
			switch (a.actionType)
			{
				case ActionType.Quiz when a.quizData != null:
					sb.AppendLine($"| Soru | {EscapeMd(a.quizData.questionText)} |");
					for (int i = 0; i < a.quizData.options.Length; i++)
					{
						string mark = (i == a.quizData.correctOptionIndex) ? " ✅" : "";
						sb.AppendLine($"| Seçenek {(char)('A' + i)} | {EscapeMd(a.quizData.options[i])}{mark} |");
					}
					break;

				case ActionType.Survey when a.surveyData != null:
					sb.AppendLine($"| Survey Başlık | {EscapeMd(a.surveyData.panelTitle)} |");
					sb.AppendLine($"| Soru Sayısı | {a.surveyData.questions?.Count ?? 0} |");
					sb.AppendLine($"| Foto Slotu | {a.surveyData.photoSlotCount} |");
					break;

				case ActionType.CameraMove:
					if (!string.IsNullOrEmpty(a.virtualCameraID))
						sb.AppendLine($"| Kamera | `{EscapeMd(a.virtualCameraID)}` |");
					break;
			}

			sb.AppendLine();
			return sb.ToString();
		}

		private void ExportAction(ActionData action)
		{
			string md = $"## {EscapeMd(action.actionName)}\n\n" + ActionToMarkdown(action);
			SaveMarkdown(md, $"{action.actionID}_export");
		}

		private void ExportSequence(SequenceData seq)
		{
			var sb = new System.Text.StringBuilder();
			sb.AppendLine($"# {EscapeMd(seq.sequenceName)}");
			sb.AppendLine();
			sb.AppendLine($"> **ID:** `{EscapeMd(seq.sequenceID)}`");
			if (!string.IsNullOrEmpty(seq.sequenceDescription))
				sb.AppendLine($"> {EscapeMd(seq.sequenceDescription)}");
			sb.AppendLine();

			if (seq.actions != null)
			{
				sb.AppendLine("## Action'lar");
				sb.AppendLine();
				for (int i = 0; i < seq.actions.Length; i++)
				{
					if (seq.actions[i] == null) continue;
					sb.Append(ActionToMarkdown(seq.actions[i], i));
				}
			}

			SaveMarkdown(sb.ToString(), $"{seq.sequenceID}_export");
		}

		private void ExportLevel(LevelData level)
		{
			var sb = new System.Text.StringBuilder();
			sb.AppendLine($"# {EscapeMd(level.levelName)}");
			sb.AppendLine();
			sb.AppendLine($"> **ID:** `{EscapeMd(level.levelID)}`");
			sb.AppendLine();

			if (level.sequences != null)
			{
				foreach (var seq in level.sequences)
				{
					if (seq == null) continue;
					sb.AppendLine($"---");
					sb.AppendLine();
					sb.AppendLine($"## {EscapeMd(seq.sequenceName)}");
					sb.AppendLine();
					sb.AppendLine($"> **ID:** `{EscapeMd(seq.sequenceID)}`");
					if (!string.IsNullOrEmpty(seq.sequenceDescription))
						sb.AppendLine($"> {EscapeMd(seq.sequenceDescription)}");
					sb.AppendLine();

					if (seq.actions != null)
					{
						for (int i = 0; i < seq.actions.Length; i++)
						{
							if (seq.actions[i] == null) continue;
							sb.Append(ActionToMarkdown(seq.actions[i], i));
						}
					}
				}
			}

			SaveMarkdown(sb.ToString(), $"{level.levelID}_export");
		}

		// ─── Helpers ─────────────────────────────────────────────

		private void SaveMarkdown(string content, string defaultName)
		{
			string path = EditorUtility.SaveFilePanel(
				"Markdown Export", "", defaultName, "md");

			if (string.IsNullOrEmpty(path)) return;

			System.IO.File.WriteAllText(path, content, System.Text.Encoding.UTF8);

			// Panoya da kopyala
			EditorGUIUtility.systemCopyBuffer = content;

		}


		private string GetActionExtras(ActionData action)
		{
			var sb = new System.Text.StringBuilder();

			switch (action.actionType)
			{
				case ActionType.Click:
				case ActionType.OpenClose:
					if (!string.IsNullOrEmpty(action.targetObjectID))
						sb.AppendLine($"**Hedef Obje:** `{action.targetObjectID}`  ");
					break;

				case ActionType.WearEquipment:
					if (action.requiredEquipments != null && action.requiredEquipments.Length > 0)
					{
						var names = new System.Collections.Generic.List<string>();
						foreach (var e in action.requiredEquipments)
							if (e != null) names.Add(e.name);
						sb.AppendLine($"**Gerekli Ekipmanlar:** {string.Join(", ", names)}  ");
					}
					break;

				case ActionType.CameraMove:
					if (!string.IsNullOrEmpty(action.virtualCameraID))
						sb.AppendLine($"**Kamera ID:** `{action.virtualCameraID}`  ");
					break;

				case ActionType.Quiz:
					if (action.quizData != null)
					{
						sb.AppendLine($"**Soru:** {EscapeMd(action.quizData.questionText)}  ");
						if (action.quizData.options != null)
						{
							for (int i = 0; i < action.quizData.options.Length; i++)
							{
								bool correct = i == action.quizData.correctOptionIndex;
								string opt = EscapeMd(action.quizData.options[i] ?? "");
								sb.AppendLine($"- {(char)('A' + i)}) {opt}{(correct ? " ✓" : "")}");
							}
						}
					}
					break;

				case ActionType.Survey:
					if (action.surveyData != null)
					{
						sb.AppendLine($"**Panel Başlığı:** {action.surveyData.panelTitle}  ");
						if (action.surveyData.questions != null)
							sb.AppendLine($"**Soru Sayısı:** {action.surveyData.questions.Count}  ");
						sb.AppendLine($"**Fotoğraf Slotu:** {action.surveyData.photoSlotCount}  ");
					}
					break;
			}

			if (action.activatesTablet)
				sb.AppendLine("**Tablet:** Aktifleştirir  ");
			if (action.deactivatesTablet)
				sb.AppendLine("**Tablet:** Pasifleştirir  ");

			return sb.ToString().Trim();
		}

		private string EscapeMd(string text)
		{
			if (string.IsNullOrEmpty(text)) return "";
			return text.Replace("|", "\\|").Replace("\n", " ").Replace("\r\n", " ");
		}

		// ═══════════════════════════════════════════════════════
		// MARKDOWN EXPORT
		// ═══════════════════════════════════════════════════════

		private void ExportLevelToMarkdown(LevelData level)
		{
			if (level == null) return;

			string defaultName = $"{SanitizeName(level.levelName)}_export.md";
			string path = EditorUtility.SaveFilePanel(
				"Level Export — Markdown",
				System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop),
				defaultName, "md");

			if (string.IsNullOrEmpty(path)) return;

			System.Text.StringBuilder sb = new System.Text.StringBuilder();

			// ── Başlık ──
			sb.AppendLine($"# {level.levelName}");
			sb.AppendLine();
			sb.AppendLine($"> **Level ID:** `{level.levelID}`");
			sb.AppendLine();
			sb.AppendLine("---");
			sb.AppendLine();

			if (level.sequences == null || level.sequences.Length == 0)
			{
				sb.AppendLine("*Bu level'da henüz sequence yok.*");
			}
			else
			{
				for (int si = 0; si < level.sequences.Length; si++)
				{
					SequenceData seq = level.sequences[si];
					if (seq == null) continue;

					// ── Sequence başlığı ──
					sb.AppendLine($"## {si + 1}. {seq.sequenceName}");
					sb.AppendLine();
					sb.AppendLine($"| Alan | Değer |");
					sb.AppendLine($"|------|-------|");
					sb.AppendLine($"| ID | `{seq.sequenceID}` |");

					if (!string.IsNullOrEmpty(seq.sequenceDescription))
						sb.AppendLine($"| Açıklama | {seq.sequenceDescription.Replace("\n", " ")} |");

					sb.AppendLine($"| Survey | {(seq.hasSurvey ? "✅ Var" : "—")} |");

					if (seq.prerequisiteSequences != null && seq.prerequisiteSequences.Length > 0)
					{
						var prereqNames = new System.Collections.Generic.List<string>();
						foreach (var p in seq.prerequisiteSequences)
							if (p != null) prereqNames.Add(p.sequenceName);
						sb.AppendLine($"| Önkoşullar | {string.Join(", ", prereqNames)} |");
					}

					sb.AppendLine();

					// ── Action'lar ──
					if (seq.actions == null || seq.actions.Length == 0)
					{
						sb.AppendLine("*Bu sequence'da henüz action yok.*");
						sb.AppendLine();
					}
					else
					{
						sb.AppendLine($"### Action'lar");
						sb.AppendLine();
						sb.AppendLine("| # | Ad | Tip | Talimat | Hedef Obje | Tablet |");
						sb.AppendLine("|---|-----|-----|---------|------------|--------|");

						for (int ai = 0; ai < seq.actions.Length; ai++)
						{
							ActionData action = seq.actions[ai];
							if (action == null) continue;

							string tip = action.actionType.ToString();
							string talimat = string.IsNullOrEmpty(action.instructionText)
								? "—" : action.instructionText.Replace("\n", " ").Replace("|", "\\|");
							string hedef = string.IsNullOrEmpty(action.targetObjectID)
								? "—" : $"`{action.targetObjectID}`";
							string tablet = action.activatesTablet ? "✅ Aktifleştirir" : "—";

							sb.AppendLine($"| {ai + 1} | {action.actionName} | `{tip}` | {talimat} | {hedef} | {tablet} |");
						}

						sb.AppendLine();

						// ── Survey detayları ──
						if (seq.hasSurvey && seq.surveyData != null)
						{
							sb.AppendLine($"### Survey Detayları");
							sb.AppendLine();
							sb.AppendLine($"**Panel Başlığı:** {seq.surveyData.panelTitle}");
							sb.AppendLine();

							if (seq.surveyData.questions != null && seq.surveyData.questions.Count > 0)
							{
								sb.AppendLine("#### Sorular");
								sb.AppendLine();
								for (int qi = 0; qi < seq.surveyData.questions.Count; qi++)
								{
									var q = seq.surveyData.questions[qi];
									if (q == null) continue;
									sb.AppendLine($"**{qi + 1}. {q.questionText}**");
									if (q.options != null)
									{
										for (int oi = 0; oi < q.options.Count; oi++)
										{
											bool isCorrect = q.correctOptionIndices != null &&
												q.correctOptionIndices.Contains(oi);
											string mark = isCorrect ? " ✅" : "";
											sb.AppendLine($"- {q.options[oi]}{mark}");
										}
									}
									sb.AppendLine();
								}
							}

							if (seq.surveyData.photoSlotCount > 0 && seq.surveyData.photoSlots != null)
							{
								sb.AppendLine("#### Fotoğraf Slotları");
								sb.AppendLine();
								sb.AppendLine("| # | Başlık | Hedef Obje | Hizalama Eşiği |");
								sb.AppendLine("|---|--------|------------|----------------|");
								int slotCount = Mathf.Min(seq.surveyData.photoSlotCount, seq.surveyData.photoSlots.Count);
								for (int pi = 0; pi < slotCount; pi++)
								{
									var slot = seq.surveyData.photoSlots[pi];
									if (slot == null) continue;
									sb.AppendLine($"| {pi + 1} | {slot.slotLabel} | `{slot.targetObjectID}` | %{(slot.alignmentThreshold * 100f):F0} |");
								}
								sb.AppendLine();
							}
						}
					}

					sb.AppendLine("---");
					sb.AppendLine();
				}
			}

			// Yaz
			System.IO.File.WriteAllText(path, sb.ToString(), System.Text.Encoding.UTF8);
			EditorUtility.RevealInFinder(path);

			Debug.Log($"<color=lime>[TrainingDataEditor] Export tamamlandı: {path}</color>");
		}

		// ═══════════════════════════════════════════════════════
		// PREREQUISITE REBUILD
		// ═══════════════════════════════════════════════════════

		/// <summary>
		/// Seçili level'daki tüm sequence'ları sırayla tarar.
		/// Her sequence'a kendinden önceki tüm sequence'ları prerequisite olarak atar.
		/// </summary>
		private void RebuildAllPrerequisites()
		{
			if (_selectedLevel == null || _selectedLevel.sequences == null) return;

			int count = _selectedLevel.sequences.Length;
			int updated = 0;

			for (int i = 0; i < count; i++)
			{
				SequenceData seq = _selectedLevel.sequences[i];
				if (seq == null) continue;

				// İlk sequence'ın prerequisite'i olmaz
				SerializedObject so = new SerializedObject(seq);
				SerializedProperty arr = so.FindProperty("prerequisiteSequences");

				if (i == 0)
				{
					arr.arraySize = 0;
				}
				else
				{
					arr.arraySize = i;
					for (int j = 0; j < i; j++)
						arr.GetArrayElementAtIndex(j).objectReferenceValue = _selectedLevel.sequences[j];
				}

				so.ApplyModifiedProperties();
				EditorUtility.SetDirty(seq);
				updated++;
			}

			AssetDatabase.SaveAssets();
			DestroyEmbeddedEditor();
			Repaint();


		}

		// ═══════════════════════════════════════════════════════
		// DATA HELPERS
		// ═══════════════════════════════════════════════════════

		private void RefreshLevelList()
		{
			_allLevels.Clear();
			string[] guids = AssetDatabase.FindAssets("t:LevelData");
			foreach (string guid in guids)
			{
				LevelData lvl = AssetDatabase.LoadAssetAtPath<LevelData>(AssetDatabase.GUIDToAssetPath(guid));
				if (lvl != null) _allLevels.Add(lvl);
			}

			if (_selectedLevel != null && !_allLevels.Contains(_selectedLevel))
			{
				_selectedLevel = null; _selectedSequence = null; _selectedAction = null;
				DestroyEmbeddedEditor();
			}
		}

		private void EnsureSavePath()
		{
			EnsureFolderExists(_savePath);
			EnsureFolderExists(LevelsRoot);
		}

		private void EnsureFolderExists(string path)
		{
			if (AssetDatabase.IsValidFolder(path)) return;
			string parent = Path.GetDirectoryName(path).Replace('\\', '/');
			string folder = Path.GetFileName(path);
			if (!AssetDatabase.IsValidFolder(parent)) EnsureFolderExists(parent);
			AssetDatabase.CreateFolder(parent, folder);
		}

		private void SaveAsset(ScriptableObject asset, string path)
		{
			AssetDatabase.CreateAsset(asset, path);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}

		// ═══════════════════════════════════════════════════════
		// UI HELPERS
		// ═══════════════════════════════════════════════════════

		private bool DrawButton(Rect rect, string label, Color bg, int fontSize = 11)
		{
			EditorGUI.DrawRect(rect, bg);
			GUIStyle s = new GUIStyle(EditorStyles.miniLabel)
			{
				alignment = TextAnchor.MiddleCenter,
				fontSize = fontSize,
				normal = { textColor = Color.white }
			};
			GUI.Label(rect, label, s);
			return Event.current.type == EventType.MouseDown
				&& rect.Contains(Event.current.mousePosition);
		}

		private Color GetActionTypeColor(ActionType type) => type switch
		{
			ActionType.WearEquipment => new Color(0.55f, 0.27f, 0.07f),
			ActionType.OpenClose => new Color(0.13f, 0.45f, 0.55f),
			ActionType.DragToWorld => new Color(0.40f, 0.20f, 0.55f),
			ActionType.Click => new Color(0.20f, 0.45f, 0.20f),
			ActionType.CameraMove => new Color(0.50f, 0.45f, 0.10f),
			ActionType.PanelInteraction => new Color(0.20f, 0.35f, 0.55f),
			ActionType.Quiz => new Color(0.55f, 0.20f, 0.35f),
			ActionType.Survey => new Color(0.20f, 0.45f, 0.45f),
			_ => new Color(0.35f, 0.35f, 0.35f)
		};
	}
}