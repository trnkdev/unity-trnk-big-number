#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TRnK.BigNum.Editor
{
    /// <summary>
    /// Inspector drawer for <see cref="BigNumber"/>. Shows a single editable text field that accepts
    /// any format the parser handles ("1500000", "1.5M", "1.5e30", "2ab"), with the formatted value
    /// and raw mantissa/exponent visible in a foldout for debugging.
    /// </summary>
    [CustomPropertyDrawer(typeof(BigNumber))]
    internal sealed class BigNumberPropertyDrawer : PropertyDrawer
    {
        private const float Padding = 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty mantissaProp = property.FindPropertyRelative("_mantissa");
            SerializedProperty exponentProp = property.FindPropertyRelative("_exponent");

            if (mantissaProp == null || exponentProp == null)
            {
                EditorGUI.LabelField(position, label.text, "BigNumber: serialized fields not found.");
                return;
            }

            BigNumber current = new(mantissaProp.doubleValue, exponentProp.longValue);

            EditorGUI.BeginProperty(position, label, property);

            float lineHeight = EditorGUIUtility.singleLineHeight;
            Rect mainRect = new(position.x, position.y, position.width, lineHeight);

            // Foldout + label on the left, edit field on the right.
            Rect labelRect = new(mainRect.x, mainRect.y, EditorGUIUtility.labelWidth, lineHeight);
            Rect fieldRect = new(
                mainRect.x + EditorGUIUtility.labelWidth,
                mainRect.y,
                mainRect.width - EditorGUIUtility.labelWidth,
                lineHeight);

            property.isExpanded = EditorGUI.Foldout(labelRect, property.isExpanded, label, toggleOnLabelClick: true);

            string editKey = $"{property.propertyPath}__edit";
            string buffer = SessionState.GetString(editKey, current.ToString(BigNumberFormat.Mixed));

            EditorGUI.BeginChangeCheck();
            string typed = EditorGUI.DelayedTextField(fieldRect, buffer);
            if (EditorGUI.EndChangeCheck())
            {
                if (BigNumber.TryParse(typed, out BigNumber parsed))
                {
                    mantissaProp.doubleValue = parsed.Mantissa;
                    exponentProp.longValue = parsed.Exponent;
                    SessionState.SetString(editKey, parsed.ToString(BigNumberFormat.Mixed));
                }
                else
                {
                    SessionState.SetString(editKey, typed); // keep what user typed so they can fix it
                }
            }
            else
            {
                // Keep buffer in sync with serialized value when not actively editing.
                SessionState.SetString(editKey, current.ToString(BigNumberFormat.Mixed));
            }

            // Foldout contents
            if (property.isExpanded)
            {
                Rect detailRect = new(position.x, position.y + lineHeight + Padding,
                    position.width, lineHeight);
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUI.LabelField(detailRect, "Scientific", current.ToString(BigNumberFormat.Scientific));
                    detailRect.y += lineHeight + Padding;
                    EditorGUI.LabelField(detailRect, "Engineering", current.ToString(BigNumberFormat.Engineering));
                    detailRect.y += lineHeight + Padding;

                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUI.DoubleField(detailRect, "Mantissa", current.Mantissa);
                        detailRect.y += lineHeight + Padding;
                        EditorGUI.LongField(detailRect, "Exponent", current.Exponent);
                    }
                }
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded) return line;
            // 1 main + 4 detail rows
            return line * 5f + Padding * 4f;
        }
    }
}
#endif
