using UnityEngine;
using UnityEditor;

namespace SafetyTraining
{
	[CustomEditor(typeof(ActionData))]
	public class ActionDataEditor : Editor
	{
		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			ActionData action = (ActionData)target;

			// ━━━ GENEL ━━━
			DrawSection("━━━ GENEL ━━━", () =>
			{
				EditorGUILayout.PropertyField(serializedObject.FindProperty("actionID"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("actionName"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("instructionText"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("actionType"));
			});

			// ━━━ PREREQUISITE ━━━
			DrawSection("━━━ PREREQUISITE ━━━", () =>
			{
				EditorGUILayout.PropertyField(serializedObject.FindProperty("prerequisiteActionIDs"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("onPrerequisiteFail"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("prerequisiteFailMessage"));
			});

			// ━━━ TAMAMLANMA ━━━
			DrawSection("━━━ TAMAMLANMA ━━━", () =>
			{
				EditorGUILayout.PropertyField(serializedObject.FindProperty("completionDelay"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("autoCompleteAfterDelay"));
			});

			// ━━━ KAMERA ━━━
			DrawSection("━━━ KAMERA ━━━", () =>
			{
				EditorGUILayout.PropertyField(serializedObject.FindProperty("cameraMode"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("virtualCameraID"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("autoReturnCameraOnComplete"));
			});

			// ━━━ HEDEF OBJE ━━━
			// Quiz ve PanelInteraction dışında hepsinde göster
			if (action.actionType != ActionType.Quiz && action.actionType != ActionType.PanelInteraction && action.actionType != ActionType.Survey)
			{
				DrawSection("━━━ HEDEF OBJE ━━━", () =>
				{
					EditorGUILayout.PropertyField(serializedObject.FindProperty("targetObjectID"));
				});
			}

			// ━━━ ACTION TYPE'A ÖZEL ALANLAR ━━━
			switch (action.actionType)
			{
				case ActionType.WearEquipment:
					DrawSection("━━━ EKİPMAN ━━━", () =>
					{
						EditorGUILayout.PropertyField(serializedObject.FindProperty("requiredEquipments"));
						EditorGUILayout.PropertyField(serializedObject.FindProperty("distractorEquipments"));
					});
					break;

				case ActionType.DragToWorld:
					DrawSection("━━━ DRAG TO WORLD ━━━", () =>
					{
						EditorGUILayout.PropertyField(serializedObject.FindProperty("targetObjectID"));
						EditorGUILayout.PropertyField(serializedObject.FindProperty("requiredTools"));
						EditorGUILayout.PropertyField(serializedObject.FindProperty("distractorTools"));
						EditorGUILayout.PropertyField(serializedObject.FindProperty("toolDropMappings"));
						EditorGUILayout.PropertyField(serializedObject.FindProperty("dropZoneSize"));
						EditorGUILayout.PropertyField(serializedObject.FindProperty("toolDropMode"));
					});
					break;

				case ActionType.PanelInteraction:
					DrawSection("━━━ PANEL INTERACTION ━━━", () =>
					{
						EditorGUILayout.PropertyField(serializedObject.FindProperty("interactionPanelPrefab"));
						EditorGUILayout.PropertyField(serializedObject.FindProperty("panelID"));
					});
					break;

				case ActionType.Quiz:
					DrawSection("━━━ QUIZ ━━━", () =>
					{
						EditorGUILayout.PropertyField(serializedObject.FindProperty("interactionPanelPrefab"),
							new GUIContent("Quiz Panel Prefab"));
						EditorGUILayout.PropertyField(serializedObject.FindProperty("quizData"), true);
					});
					break;

				case ActionType.Survey:
					DrawSection("━━━ SURVEY ━━━", () =>
					{
						EditorGUILayout.PropertyField(serializedObject.FindProperty("interactionPanelPrefab"),
							new GUIContent("Survey Panel Prefab"));
						EditorGUILayout.PropertyField(serializedObject.FindProperty("surveyData"), true);
					});
					break;

				case ActionType.Click:
				case ActionType.OpenClose:
					// Hedef obje zaten üstte çizildi, ekstra alan yok
					DrawSection("━━━ CLICK / OPEN CLOSE ━━━", () =>
					{
						EditorGUILayout.PropertyField(serializedObject.FindProperty("targetObjectID"));
					});
					break;

				case ActionType.CameraMove:
					// Kamera ayarları üstteki KAMERA section'ında mevcut
					break;
			}

			// ━━━ UI BEHAVIOR ━━━
			DrawSection("━━━ TABLET ━━━", () =>
			{
				EditorGUILayout.PropertyField(serializedObject.FindProperty("activatesTablet"),
					new GUIContent("Tablet Aktifleştir", "Bu action tamamlanınca tablet butonu aktifleşir"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("deactivatesTablet"),
					new GUIContent("Tablet Pasifleştir", "Bu action tamamlanınca tablet butonu pasifleşir"));
			});

			DrawSection("━━━ UI BEHAVIOR ━━━", () =>
			{
				EditorGUILayout.PropertyField(serializedObject.FindProperty("hideButtonAfterComplete"));
			});

			// ━━━ ANİMASYONLAR ━━━
			DrawSection("━━━ ANİMASYONLAR ━━━", () =>
			{
				EditorGUILayout.PropertyField(serializedObject.FindProperty("animationTriggers"), true);
			});

			// ━━━ EFEKTLER ━━━
			DrawSection("━━━ EFEKTLER ━━━", () =>
			{
				EditorGUILayout.PropertyField(serializedObject.FindProperty("soundClip"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("particleEffectID"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("particleSpawnOffset"));
			});

			// ━━━ EVENTS ━━━
			DrawSection("━━━ EVENTS ━━━", () =>
			{
				EditorGUILayout.PropertyField(serializedObject.FindProperty("onActionStart"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("onActionComplete"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("onActionFail"));
				EditorGUILayout.Space(4);
				EditorGUILayout.PropertyField(serializedObject.FindProperty("onStartEventIDs"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("onCompleteEventIDs"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("onFailEventIDs"));
			});

			serializedObject.ApplyModifiedProperties();
		}

		// ─── Helper: Başlıklı bölüm çizer ───
		private void DrawSection(string title, System.Action drawContent)
		{
			EditorGUILayout.Space(6);
			EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
			EditorGUI.indentLevel++;
			drawContent();
			EditorGUI.indentLevel--;
		}
	}
}