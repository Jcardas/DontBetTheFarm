using UnityEditor;

[CustomEditor(typeof(AnimalPen))]
public class AnimalPenEditor : Editor
{
    private SerializedProperty _isStartingPenProp;
    private SerializedProperty _startingAnimalsProp;

    private void OnEnable()
    {
        _isStartingPenProp = serializedObject.FindProperty("isStartingPen");
        _startingAnimalsProp = serializedObject.FindProperty("startingAnimals");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;

        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;

            // Skip script field
            if (property.name == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.PropertyField(property, true);
                }
                continue;
            }

            // Hide starting animals unless starting pen is checked
            if (property.name == _startingAnimalsProp.name && !_isStartingPenProp.boolValue)
            {
                continue;
            }

            EditorGUILayout.PropertyField(property, true);
        }

        serializedObject.ApplyModifiedProperties();
    }
}