using UnityEngine;
using UnityEditor;
using CaveGeneration;

namespace CaveGeneration
{
    [CustomEditor(typeof(CaveGenerator)), CanEditMultipleObjects]
    public class CaveGeneratorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button(("Generate")))
            {
                ((CaveGenerator)target).Generate();
            }

            if (GUILayout.Button(new GUIContent("Refresh Generator", "Use this to reset the generator if an operation was interrupted")))
            {
                ((CaveGenerator)target).RefreshGenerator();
            }

            DrawDefaultInspector();
        }
    }
}