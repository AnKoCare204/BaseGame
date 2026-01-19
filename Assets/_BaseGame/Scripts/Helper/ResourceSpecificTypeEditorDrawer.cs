using System;
using CoreData;
using UnityEngine;

#if UNITY_EDITOR
using Sirenix.OdinInspector.Editor;
using Sirenix.OdinInspector.Editor.ValueResolvers;
using UnityEditor;
#endif

namespace Helper
{
    public sealed class ResourceSpecificTypeEditorAttribute : Attribute
    {
        public string ResourceTypeAction { get; set; }
        public ResourceSpecificTypeEditorAttribute(string resourceTypeAction)
        {
            ResourceTypeAction = resourceTypeAction;
        }

    }
#if UNITY_EDITOR
    public sealed class ResourceSpecificTypeEditorDrawer : OdinAttributeDrawer<ResourceSpecificTypeEditorAttribute, int>
    {
        private ValueResolver<ResourceType> resourceTypeResolver;

        protected override void Initialize()
        {
            resourceTypeResolver = ValueResolver.Get<ResourceType>(Property, Attribute.ResourceTypeAction);
        }
        protected override void DrawPropertyLayout(GUIContent label)
        {
            Type enumType = GetSpecificType();

            if (enumType == typeof(ResourceType)) return;
            int currentValue = this.ValueEntry.SmartValue;
            EditorGUI.BeginChangeCheck();
            Enum currentEnum = (Enum)Enum.ToObject(enumType, currentValue);
            Enum newEnum = EditorGUILayout.EnumPopup(label, currentEnum);

            if (EditorGUI.EndChangeCheck())
            {
                this.ValueEntry.SmartValue = Convert.ToInt32(newEnum);
            }
        }
        
        private Type GetSpecificType()
        {
            ResourceType resourceType = resourceTypeResolver.GetValue();
            return resourceType switch
            {
                ResourceType.Currency => typeof(CurrencyType),
                _ => typeof(ResourceType)
            };
        }
    }
#endif
}