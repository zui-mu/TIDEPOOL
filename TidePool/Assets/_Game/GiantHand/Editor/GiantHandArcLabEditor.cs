using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace TidePool.GiantHand.Editor
{
    [CustomEditor(typeof(GiantHandArcLab))]
    internal sealed class GiantHandArcLabEditor : UnityEditor.Editor
    {
        private const string ScenePath = "Assets/_Game/Scenes/GiantHand_ArcLab_01.unity";

        private static readonly Color SolvedColor = new Color(0.24f, 0.91f, 1f);
        private static readonly Color FailedColor = new Color(1f, 0.29f, 0.29f);
        private static readonly Color WristColor = new Color(0.38f, 1f, 0.45f);
        private static readonly Color PoleColor = new Color(1f, 0.73f, 0.25f);

        public override void OnInspectorGUI()
        {
            var lab = (GiantHandArcLab)target;
            EditorGUILayout.HelpBox(
                "在场景视图拖动绿色腕部目标可改变终点，拖动琥珀色弯曲极点可改变弯曲方向。编辑模式下即可操作。",
                MessageType.Info);

            if (DrawDefaultInspector())
            {
                lab.Refresh();
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(lab.Status ?? string.Empty, lab.IsSolved ? MessageType.Info : MessageType.Warning);
            EditorGUILayout.LabelField("骨段长度", FormatLength(lab.SegmentLength));
            EditorGUILayout.LabelField("腕部位置误差", FormatLength(lab.WristError));
            EditorGUILayout.LabelField("最大骨段长度误差", FormatLength(lab.MaxBoneLengthError));
        }

        private void OnSceneGUI()
        {
            var lab = (GiantHandArcLab)target;
            bool wristMoved = DrawPositionHandle(lab.WristTarget, "Move Wrist Target");
            bool poleMoved = DrawPositionHandle(lab.BendPole, "Move Bend Pole");
            if (wristMoved || poleMoved)
            {
                lab.Refresh();
                Repaint();
                SceneView.RepaintAll();
            }
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        private static void DrawArc(GiantHandArcLab lab, GizmoType gizmoType)
        {
            if (Event.current?.type != EventType.Repaint)
                return;

            CompareFunction previousDepthTest = Handles.zTest;
            using (new Handles.DrawingScope(lab.IsSolved ? SolvedColor : FailedColor, Matrix4x4.identity))
            {
                Handles.zTest = CompareFunction.Always;
                try
                {
                    DrawJoints(lab);
                    var labelStyle = new GUIStyle(EditorStyles.boldLabel);
                    DrawControl(lab.WristTarget, WristColor, "Wrist Target / 腕部目标", labelStyle);
                    DrawControl(lab.BendPole, PoleColor, "Bend Pole / 弯曲极点", labelStyle);
                }
                finally
                {
                    Handles.zTest = previousDepthTest;
                }
            }
        }

        [MenuItem("Tools/GiantHand/Open Arc Lab")]
        private static void OpenScene()
        {
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))
            {
                Debug.LogError($"GiantHand Arc Lab scene could not be found at {ScenePath}.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var lab = root.GetComponentInChildren<GiantHandArcLab>();
                if (!lab)
                    continue;

                Selection.activeGameObject = lab.gameObject;
                var view = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
                view.drawGizmos = true;
                view.LookAt(new Vector3(5f, 1.4f, 0f), Quaternion.identity, 7.5f, true);
                view.Focus();
                return;
            }

            Debug.LogWarning($"GiantHand Arc Lab scene does not contain a {nameof(GiantHandArcLab)} component.");
        }

        private static bool DrawPositionHandle(Transform control, string undoName)
        {
            if (!control)
                return false;

            EditorGUI.BeginChangeCheck();
            Vector3 position = Handles.PositionHandle(control.position, Quaternion.identity);
            if (!EditorGUI.EndChangeCheck())
                return false;

            Undo.RecordObject(control, undoName);
            control.position = position;
            return true;
        }

        private static void DrawJoints(GiantHandArcLab lab)
        {
            Transform previous = null;
            foreach (Transform joint in lab.Joints)
            {
                if (joint)
                {
                    if (previous)
                        Handles.DrawAAPolyLine(4f, previous.position, joint.position);

                    float size = HandleUtility.GetHandleSize(joint.position);
                    Handles.DotHandleCap(0, joint.position, Quaternion.identity, 0.035f * size, EventType.Repaint);
                }

                previous = joint;
            }
        }

        private static void DrawControl(Transform control, Color color, string label, GUIStyle labelStyle)
        {
            if (!control)
                return;

            Vector3 position = control.position;
            float size = HandleUtility.GetHandleSize(position);
            Handles.color = color;
            Handles.DotHandleCap(0, position, Quaternion.identity, 0.075f * size, EventType.Repaint);
            labelStyle.normal.textColor = color;
            Handles.Label(position + Vector3.up * (0.24f * size), label, labelStyle);
        }

        private static string FormatLength(float value)
        {
            return $"{value:G5}";
        }
    }
}
