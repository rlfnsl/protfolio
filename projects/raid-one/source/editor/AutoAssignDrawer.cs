using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(AutoAssignAttribute))]
public class AutoAssignDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        AutoAssignAttribute autoAssign = (AutoAssignAttribute)attribute;
        GameObject obj = (property.serializedObject.targetObject as MonoBehaviour)?.gameObject;

        if (property.propertyType == SerializedPropertyType.ObjectReference)
        {
            if (property.objectReferenceValue == null && obj != null)
            {
                Object foundObject = null;

                if (!string.IsNullOrEmpty(autoAssign.targetName))
                {
                    Transform targetTransform = FindDeepChild(obj.transform, autoAssign.targetName);
                    if (targetTransform != null)
                    {
                        foundObject = GetComponentFromTransform(property, targetTransform);
                    }
                }
                else
                {
                    if (autoAssign.searchInChildren)
                    {
                        foundObject = GetComponentFromTransform(property, obj.transform, true);
                    }
                    else
                    {
                        foundObject = GetComponentFromTransform(property, obj.transform, false);
                    }
                }

                if (foundObject != null)
                {
                    property.objectReferenceValue = foundObject;
                    property.serializedObject.ApplyModifiedProperties();
                }
            }
        }

        EditorGUI.PropertyField(position, property, label);
    }

    private Object GetComponentFromTransform(SerializedProperty property, Transform targetTransform, bool searchInChildren = false)
    {
        System.Type targetType = fieldInfo.FieldType;

        // Check for GameObject or Transform
        if (targetType == typeof(GameObject))
            return targetTransform.gameObject;
        if (targetType == typeof(Transform))
            return targetTransform;

        // Handle RectTransform specifically
        if (targetType == typeof(RectTransform))
            return targetTransform.GetComponent<RectTransform>();

        // Handle Components
        if (targetType.IsSubclassOf(typeof(Component)))
        {
            return searchInChildren ? targetTransform.GetComponentInChildren(targetType) : targetTransform.GetComponent(targetType);
        }

        return null;
    }
    private Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform result = FindDeepChild(child, name);
            if (result != null) return result;
        }
        return null;
    }
}
