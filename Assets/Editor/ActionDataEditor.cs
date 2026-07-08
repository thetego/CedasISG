using UnityEditor;
using UnityEngine;

namespace SafetyTraining
{
	[CustomEditor(typeof(ActionData))]
	public class ActionDataEditor : Editor
	{
		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			ActionData action = (ActionData)target;

			DrawSection("GENERAL", () =>
			{
				EditorGUILayout.PropertyField(serializedObject.FindProperty("actionID"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("actionName"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("instructionText"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("actionType"));
			});

			DrawSection("PREREQUISITE", () =>
			{
				EditorGUILayout.PropertyField(serializedObject.FindProperty("prerequisiteActionIDs"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("onPrerequisiteFail"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("prerequisiteFailMessage"));
			});

			DrawSection("COMPLETION", () =>
			{
				EditorGUILayout.PropertyField(serializedObject.FindProperty("completionDelay"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("autoCompleteAfterDelay"));
			});

			DrawSection("CAMERA", () =>
			{
				EditorGUILayout.PropertyField(serializedObject.FindProperty("cameraMode"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("virtualCameraID"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("autoReturnCameraOnComplete"));
			});

			if (action.actionType != ActionType.Quiz &&
				action.actionType != ActionType.PanelInteraction &&
				action.actionType != ActionType.Survey &&
				action.actionType != ActionType.ModalWindow &&
				action.actionType != ActionType.Fade)
			{
				DrawSection("TARGET OBJECT", () =>
				{
					EditorGUILayout.PropertyField(serializedObject.FindProperty("targetObjectID"));
				});
			}

			switch (action.actionType)
			{
				case ActionType.WearEquipment:
					DrawSection("WEAR EQUIPMENT", () =>
					{
						EditorGUILayout.PropertyField(serializedObject.FindProperty("requiredEquipments"));
						EditorGUILayout.PropertyField(serializedObject.FindProperty("distractorEquipments"));
					});
					break;

				case ActionType.DragToWorld:
					DrawSection("DRAG TO WORLD", () =>
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
					DrawSection("PANEL INTERACTION", () =>
					{
						EditorGUILayout.PropertyField(serializedObject.FindProperty("interactionPanelPrefab"));
						EditorGUILayout.PropertyField(serializedObject.FindProperty("panelID"));
					});
					break;

				case ActionType.Quiz:
					DrawSection("QUIZ", () =>
					{
						EditorGUILayout.PropertyField(
							serializedObject.FindProperty("interactionPanelPrefab"),
							new GUIContent("Quiz Panel Prefab"));
						EditorGUILayout.PropertyField(serializedObject.FindProperty("quizData"), true);
					});
					break;

				case ActionType.Survey:
					DrawSection("SURVEY", () =>
					{
						EditorGUILayout.PropertyField(
							serializedObject.FindProperty("interactionPanelPrefab"),
							new GUIContent("Survey Panel Prefab"));
						EditorGUILayout.PropertyField(serializedObject.FindProperty("surveyData"), true);
					});
					break;

				case ActionType.ModalWindow:
					DrawSection("MODAL WINDOW", () =>
					{
						EditorGUILayout.PropertyField(serializedObject.FindProperty("modalWindowPrefab"));
						EditorGUILayout.PropertyField(serializedObject.FindProperty("modalWindowTitle"));
						EditorGUILayout.PropertyField(serializedObject.FindProperty("modalWindowDescription"));
						EditorGUILayout.PropertyField(serializedObject.FindProperty("modalConfirmButtonText"));
					});
					break;

				case ActionType.Click:
				case ActionType.OpenClose:
					DrawSection("CLICK / OPEN CLOSE", () =>
					{
						EditorGUILayout.PropertyField(serializedObject.FindProperty("targetObjectID"));
					});
					break;

				case ActionType.CameraMove:
				case ActionType.Fade:
					break;
			}

			DrawSection("TABLET", () =>
			{
				EditorGUILayout.PropertyField(
					serializedObject.FindProperty("activatesTablet"),
					new GUIContent("Activate Tablet", "Enable tablet button when this action completes"));
				EditorGUILayout.PropertyField(
					serializedObject.FindProperty("deactivatesTablet"),
					new GUIContent("Deactivate Tablet", "Disable tablet button when this action starts"));
			});

			DrawSection("UI BEHAVIOR", () =>
			{
				EditorGUILayout.PropertyField(serializedObject.FindProperty("hideButtonAfterComplete"));
			});

			DrawSection("ANIMATIONS", () =>
			{
				EditorGUILayout.PropertyField(serializedObject.FindProperty("animationTriggers"), true);
			});

			DrawSection("EFFECTS", () =>
			{
				EditorGUILayout.PropertyField(serializedObject.FindProperty("soundClip"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("particleEffectID"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("particleSpawnOffset"));
			});

			DrawSection("EVENTS", () =>
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
