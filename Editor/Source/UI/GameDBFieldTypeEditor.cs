using GameDBEditorLibrary.Automation;
using GameDBEditorLibrary.Documents;
using GameDBLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace GameDBEditorLibrary.UI
{
    internal sealed class GameDBFieldTypeValidationResult
    {
        internal bool Success { get; }
        internal string Message { get; }
        internal GameDBFieldTypeSpec TypeSpec { get; }

        private GameDBFieldTypeValidationResult(bool success, string message,
            GameDBFieldTypeSpec typeSpec)
        {
            Success = success;
            Message = message;
            TypeSpec = typeSpec;
        }

        internal static GameDBFieldTypeValidationResult Valid(
            GameDBFieldTypeSpec typeSpec)
        {
            return new GameDBFieldTypeValidationResult(true, null, typeSpec);
        }

        internal static GameDBFieldTypeValidationResult Invalid(string message)
        {
            return new GameDBFieldTypeValidationResult(false, message, null);
        }
    }

    internal static class GameDBFieldTypeDraftAdapter
    {
        internal static GameDBFieldTypeValidationResult Validate(
            GameDBInspectorFieldTypeDraft draft,
            IReadOnlyCollection<string> importedEnumTypes,
            IReadOnlyCollection<string> tableNames,
            bool localizationDatabase)
        {
            if (draft == null)
            {
                throw new ArgumentNullException(nameof(draft));
            }

            importedEnumTypes = importedEnumTypes ?? Array.Empty<string>();
            tableNames = tableNames ?? Array.Empty<string>();
            if (localizationDatabase
                && (draft.Shape != GameDBFieldShape.Scalar
                    || draft.FieldType != FieldType.@string))
            {
                return GameDBFieldTypeValidationResult.Invalid(
                    "Localization databases support only scalar string fields.");
            }

            if (draft.Shape == GameDBFieldShape.Dictionary)
            {
                if (draft.DictionaryValueType == FieldType.dictionary)
                {
                    return GameDBFieldTypeValidationResult.Invalid(
                        "Nested dictionary fields are not supported.");
                }
                var keyArgument = ValidateArgument(KeyTypeToFieldType(
                        draft.DictionaryKeyType), draft.DictionaryKeyTypeArgument,
                    importedEnumTypes, tableNames, "dictionary key");
                if (!keyArgument.Success)
                {
                    return keyArgument;
                }
                var valueArgument = ValidateArgument(draft.DictionaryValueType,
                    draft.DictionaryValueTypeArgument, importedEnumTypes, tableNames,
                    "dictionary value");
                if (!valueArgument.Success)
                {
                    return valueArgument;
                }
                return GameDBFieldTypeValidationResult.Valid(new GameDBFieldTypeSpec(
                    FieldType.dictionary, false, null, new GameDBDictionaryTypeSpec(
                        draft.DictionaryKeyType, keyArgument.TypeSpec?.TypeArgument,
                        draft.DictionaryValueType,
                        valueArgument.TypeSpec?.TypeArgument)));
            }

            if (draft.FieldType == FieldType.dictionary)
            {
                return GameDBFieldTypeValidationResult.Invalid(
                    "Choose Dictionary as the field shape.");
            }
            var argument = ValidateArgument(draft.FieldType, draft.TypeArgument,
                importedEnumTypes, tableNames, "field");
            if (!argument.Success)
            {
                return argument;
            }
            return GameDBFieldTypeValidationResult.Valid(new GameDBFieldTypeSpec(
                draft.FieldType, draft.Shape == GameDBFieldShape.Array,
                argument.TypeSpec?.TypeArgument));
        }

        internal static string Format(GameDBInspectorFieldTypeDraft draft)
        {
            if (draft == null)
            {
                return string.Empty;
            }
            if (draft.Shape == GameDBFieldShape.Dictionary)
            {
                return $"Dictionary<{FormatKey(draft.DictionaryKeyType, draft.DictionaryKeyTypeArgument)}, {FormatType(draft.DictionaryValueType, draft.DictionaryValueTypeArgument)}>";
            }
            return FormatType(draft.FieldType, draft.TypeArgument)
                + (draft.Shape == GameDBFieldShape.Array ? "[]" : string.Empty);
        }

        private static GameDBFieldTypeValidationResult ValidateArgument(FieldType type,
            string argument, IReadOnlyCollection<string> importedEnumTypes,
            IReadOnlyCollection<string> tableNames, string label)
        {
            argument = argument?.Trim();
            if (type == FieldType.@enum)
            {
                if (string.IsNullOrEmpty(argument)
                    || !importedEnumTypes.Contains(argument, StringComparer.Ordinal))
                {
                    return GameDBFieldTypeValidationResult.Invalid(
                        $"Choose an imported enum type for the {label}.");
                }
            }
            else if (type == FieldType.tableRef)
            {
                if (string.IsNullOrEmpty(argument)
                    || !tableNames.Contains(argument, StringComparer.Ordinal))
                {
                    return GameDBFieldTypeValidationResult.Invalid(
                        $"Choose an existing table for the {label} reference.");
                }
            }
            else if (!string.IsNullOrEmpty(argument))
            {
                argument = null;
            }
            return GameDBFieldTypeValidationResult.Valid(new GameDBFieldTypeSpec(
                type, false, type == FieldType.@enum || type == FieldType.tableRef
                    ? argument : null));
        }

        private static FieldType KeyTypeToFieldType(KeyType keyType)
        {
            return keyType == KeyType.@enum ? FieldType.@enum : FieldType.@string;
        }

        private static string FormatKey(KeyType type, string argument)
        {
            return type == KeyType.@enum && !string.IsNullOrWhiteSpace(argument)
                ? $"Enum ({argument})" : type == KeyType.@enum ? "Enum" : "String";
        }

        private static string FormatType(FieldType type, string argument)
        {
            var name = type == FieldType.tableRef ? "Table Reference" : Friendly(type);
            return (type == FieldType.@enum || type == FieldType.tableRef)
                && !string.IsNullOrWhiteSpace(argument)
                ? $"{name} ({argument})" : name;
        }

        private static string Friendly(FieldType type)
        {
            var value = type.ToString();
            return char.ToUpperInvariant(value[0]) + value.Substring(1);
        }
    }

    internal sealed class GameDBFieldTypeEditor : IDisposable
    {
        private static readonly List<string> ShapeChoices = Enum
            .GetNames(typeof(GameDBFieldShape)).ToList();
        private static readonly List<string> FieldTypeChoices = DictionaryType
            .GetSupportedFieldTypes().Select(DisplayName).ToList();
        private static readonly List<string> KeyTypeChoices = new List<string>
        {
            DisplayName(KeyType.@string),
            DisplayName(KeyType.@enum)
        };

        private readonly VisualElement m_root;
        private readonly DropdownField m_shape;
        private readonly DropdownField m_type;
        private readonly DropdownField m_typeArgument;
        private readonly VisualElement m_dictionaryHost;
        private readonly DropdownField m_dictionaryKeyType;
        private readonly DropdownField m_dictionaryKeyArgument;
        private readonly DropdownField m_dictionaryValueType;
        private readonly DropdownField m_dictionaryValueArgument;
        private readonly Label m_message;
        private IReadOnlyList<string> m_enumTypes = Array.Empty<string>();
        private IReadOnlyList<string> m_tableNames = Array.Empty<string>();
        private bool m_localizationDatabase;
        private FieldType m_previousFieldType;
        private KeyType m_previousDictionaryKeyType;
        private FieldType m_previousDictionaryValueType;
        private bool m_binding;
        private bool m_disposed;

        internal event Action<GameDBInspectorFieldTypeDraft,
            GameDBFieldTypeValidationResult> Changed;

        internal GameDBFieldTypeEditor(VisualElement root)
        {
            m_root = root ?? throw new ArgumentNullException(nameof(root));
            m_root.Clear();
            m_root.AddToClassList("gamedb-editor__field-type-editor");
            m_shape = Dropdown("field-shape-field", "Shape", ShapeChoices);
            m_type = Dropdown("field-type-field", "Type", FieldTypeChoices);
            m_typeArgument = Dropdown("field-type-argument-field", "Type argument",
                Array.Empty<string>());
            m_dictionaryHost = new VisualElement
            {
                name = "field-dictionary-type-host"
            };
            m_dictionaryHost.AddToClassList("gamedb-editor__field-type-dictionary");
            m_dictionaryKeyType = Dropdown("field-dictionary-key-type-field",
                "Key type", KeyTypeChoices);
            m_dictionaryKeyArgument = Dropdown("field-dictionary-key-argument-field",
                "Key enum", Array.Empty<string>());
            m_dictionaryValueType = Dropdown("field-dictionary-value-type-field",
                "Value type", FieldTypeChoices);
            m_dictionaryValueArgument = Dropdown(
                "field-dictionary-value-argument-field", "Value argument",
                Array.Empty<string>());
            m_message = new Label { name = "field-type-validation-message" };
            m_message.AddToClassList("gamedb-editor__validation-message");
            m_root.Add(m_shape);
            m_root.Add(m_type);
            m_root.Add(m_typeArgument);
            m_dictionaryHost.Add(m_dictionaryKeyType);
            m_dictionaryHost.Add(m_dictionaryKeyArgument);
            m_dictionaryHost.Add(m_dictionaryValueType);
            m_dictionaryHost.Add(m_dictionaryValueArgument);
            m_root.Add(m_dictionaryHost);
            m_root.Add(m_message);
            Register(m_shape);
            Register(m_type);
            Register(m_typeArgument);
            Register(m_dictionaryKeyType);
            Register(m_dictionaryKeyArgument);
            Register(m_dictionaryValueType);
            Register(m_dictionaryValueArgument);
        }

        internal void Bind(GameDBInspectorFieldTypeDraft draft,
            IEnumerable<string> importedEnumTypes, IEnumerable<string> tableNames,
            bool localizationDatabase)
        {
            if (m_disposed)
            {
                return;
            }
            draft = draft ?? throw new ArgumentNullException(nameof(draft));
            m_enumTypes = Normalize(importedEnumTypes);
            m_tableNames = Normalize(tableNames);
            m_localizationDatabase = localizationDatabase;
            m_binding = true;
            try
            {
                m_shape.SetValueWithoutNotify(draft.Shape.ToString());
                m_type.SetValueWithoutNotify(DisplayName(draft.FieldType));
                m_dictionaryKeyType.SetValueWithoutNotify(
                    DisplayName(draft.DictionaryKeyType));
                m_dictionaryValueType.SetValueWithoutNotify(
                    DisplayName(draft.DictionaryValueType));
                SetArgument(m_typeArgument, draft.FieldType, draft.TypeArgument);
                SetArgument(m_dictionaryKeyArgument,
                    draft.DictionaryKeyType == KeyType.@enum
                        ? FieldType.@enum : FieldType.@string,
                    draft.DictionaryKeyTypeArgument);
                SetArgument(m_dictionaryValueArgument, draft.DictionaryValueType,
                    draft.DictionaryValueTypeArgument);
                ApplyVisibility();
                RememberArgumentTypes();
                PresentValidation(Validate());
            }
            finally
            {
                m_binding = false;
            }
        }

        internal GameDBInspectorFieldTypeDraft CaptureDraft()
        {
            var shape = Parse(m_shape.value, GameDBFieldShape.Scalar);
            var fieldType = ParseFieldType(m_type.value);
            var keyType = ParseKeyType(m_dictionaryKeyType.value);
            var valueType = ParseFieldType(m_dictionaryValueType.value);
            return new GameDBInspectorFieldTypeDraft(fieldType,
                shape == GameDBFieldShape.Array, Argument(m_typeArgument, fieldType),
                shape == GameDBFieldShape.Dictionary
                    ? new GameDBDictionaryTypeDefinition
                    {
                        KeyType = keyType,
                        KeyTypeArgument = Argument(m_dictionaryKeyArgument,
                            keyType == KeyType.@enum
                                ? FieldType.@enum : FieldType.@string),
                        ValueType = valueType,
                        ValueTypeArgument = Argument(m_dictionaryValueArgument, valueType)
                    }
                    : null)
            {
                Shape = shape
            };
        }

        internal GameDBFieldTypeValidationResult Validate()
        {
            return GameDBFieldTypeDraftAdapter.Validate(CaptureDraft(),
                m_enumTypes, m_tableNames, m_localizationDatabase);
        }

        internal void UpdateChoices(IEnumerable<string> importedEnumTypes,
            IEnumerable<string> tableNames)
        {
            if (m_disposed)
            {
                return;
            }
            var draft = CaptureDraft();
            m_enumTypes = Normalize(importedEnumTypes);
            m_tableNames = Normalize(tableNames);
            m_binding = true;
            try
            {
                SetArgument(m_typeArgument, draft.FieldType, draft.TypeArgument);
                SetArgument(m_dictionaryKeyArgument,
                    draft.DictionaryKeyType == KeyType.@enum
                        ? FieldType.@enum : FieldType.@string,
                    draft.DictionaryKeyTypeArgument);
                SetArgument(m_dictionaryValueArgument,
                    draft.DictionaryValueType, draft.DictionaryValueTypeArgument);
                ApplyVisibility();
            }
            finally
            {
                m_binding = false;
            }
            draft = CaptureDraft();
            var validation = GameDBFieldTypeDraftAdapter.Validate(draft,
                m_enumTypes, m_tableNames, m_localizationDatabase);
            PresentValidation(validation);
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }
            m_disposed = true;
            Unregister(m_shape);
            Unregister(m_type);
            Unregister(m_typeArgument);
            Unregister(m_dictionaryKeyType);
            Unregister(m_dictionaryKeyArgument);
            Unregister(m_dictionaryValueType);
            Unregister(m_dictionaryValueArgument);
            Changed = null;
            m_root.Clear();
        }

        private DropdownField Dropdown(string name, string label,
            IEnumerable<string> choices)
        {
            var field = new DropdownField(label)
            {
                name = name,
                choices = choices.ToList()
            };
            if (field.choices.Count > 0)
            {
                field.index = 0;
            }
            field.AddToClassList("gamedb-editor__property-field");
            return field;
        }

        private void Register(DropdownField field)
        {
            field.RegisterValueChangedCallback(OnControlChanged);
        }

        private void Unregister(DropdownField field)
        {
            field.UnregisterValueChangedCallback(OnControlChanged);
        }

        private void OnControlChanged(ChangeEvent<string> _)
        {
            if (m_binding || m_disposed)
            {
                return;
            }
            var fieldType = ParseFieldType(m_type.value);
            var dictionaryKeyType = ParseKeyType(m_dictionaryKeyType.value);
            var dictionaryValueType = ParseFieldType(m_dictionaryValueType.value);
            m_binding = true;
            try
            {
                if (ArgumentCategoryChanged(m_previousFieldType, fieldType))
                {
                    m_typeArgument.SetValueWithoutNotify(null);
                }
                if (m_previousDictionaryKeyType != dictionaryKeyType)
                {
                    m_dictionaryKeyArgument.SetValueWithoutNotify(null);
                }
                if (ArgumentCategoryChanged(m_previousDictionaryValueType,
                    dictionaryValueType))
                {
                    m_dictionaryValueArgument.SetValueWithoutNotify(null);
                }
            }
            finally
            {
                m_binding = false;
            }
            RememberArgumentTypes();
            var draft = CaptureDraft();
            m_binding = true;
            try
            {
                SetArgument(m_typeArgument, draft.FieldType, draft.TypeArgument);
                SetArgument(m_dictionaryKeyArgument,
                    draft.DictionaryKeyType == KeyType.@enum
                        ? FieldType.@enum : FieldType.@string,
                    draft.DictionaryKeyTypeArgument);
                SetArgument(m_dictionaryValueArgument,
                    draft.DictionaryValueType, draft.DictionaryValueTypeArgument);
                ApplyVisibility();
                draft = CaptureDraft();
            }
            finally
            {
                m_binding = false;
            }
            var validation = GameDBFieldTypeDraftAdapter.Validate(draft,
                m_enumTypes, m_tableNames, m_localizationDatabase);
            PresentValidation(validation);
            Changed?.Invoke(draft, validation);
        }

        private void ApplyVisibility()
        {
            var dictionary = m_shape.value == GameDBFieldShape.Dictionary.ToString();
            m_type.style.display = dictionary ? DisplayStyle.None : DisplayStyle.Flex;
            m_dictionaryHost.style.display = dictionary
                ? DisplayStyle.Flex : DisplayStyle.None;
            m_typeArgument.style.display = !dictionary && RequiresArgument(
                ParseFieldType(m_type.value))
                ? DisplayStyle.Flex : DisplayStyle.None;
            m_dictionaryKeyArgument.style.display = dictionary
                && ParseKeyType(m_dictionaryKeyType.value) == KeyType.@enum
                ? DisplayStyle.Flex : DisplayStyle.None;
            m_dictionaryValueArgument.style.display = dictionary && RequiresArgument(
                ParseFieldType(m_dictionaryValueType.value))
                ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void SetArgument(DropdownField field, FieldType type, string value)
        {
            var choices = type == FieldType.@enum ? m_enumTypes
                : type == FieldType.tableRef ? m_tableNames : Array.Empty<string>();
            var normalized = choices.Concat(string.IsNullOrWhiteSpace(value)
                    ? Array.Empty<string>() : new[] { value })
                .Distinct(StringComparer.Ordinal).OrderBy(item => item,
                    StringComparer.Ordinal).ToList();
            field.choices = normalized;
            field.SetValueWithoutNotify(normalized.Contains(value) ? value : null);
        }

        private void PresentValidation(GameDBFieldTypeValidationResult validation)
        {
            m_message.text = validation.Success ? string.Empty : validation.Message;
            m_message.style.display = validation.Success
                ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private static IReadOnlyList<string> Normalize(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        private void RememberArgumentTypes()
        {
            m_previousFieldType = ParseFieldType(m_type.value);
            m_previousDictionaryKeyType = ParseKeyType(m_dictionaryKeyType.value);
            m_previousDictionaryValueType = ParseFieldType(m_dictionaryValueType.value);
        }

        private static bool ArgumentCategoryChanged(FieldType previous, FieldType current)
        {
            return previous != current && (RequiresArgument(previous)
                || RequiresArgument(current));
        }

        private static bool RequiresArgument(FieldType type)
        {
            return type == FieldType.@enum || type == FieldType.tableRef;
        }

        private static string Argument(DropdownField field, FieldType type)
        {
            return RequiresArgument(type) ? field.value?.Trim() : null;
        }

        private static FieldType ParseFieldType(string value)
        {
            return DictionaryType.GetSupportedFieldTypes().FirstOrDefault(type =>
                string.Equals(DisplayName(type), value, StringComparison.Ordinal));
        }

        private static KeyType ParseKeyType(string value)
        {
            return string.Equals(value, DisplayName(KeyType.@enum),
                StringComparison.Ordinal) ? KeyType.@enum : KeyType.@string;
        }

        private static string DisplayName(FieldType type)
        {
            switch (type)
            {
                case FieldType.tableRef:
                    return "Table Reference";
                case FieldType.unityObject:
                    return "Unity Object";
                default:
                    var value = type.ToString();
                    return char.ToUpperInvariant(value[0]) + value.Substring(1);
            }
        }

        private static string DisplayName(KeyType type)
        {
            return type == KeyType.@enum ? "Enum" : "String";
        }

        private static T Parse<T>(string value, T fallback) where T : struct
        {
            return Enum.TryParse(value, out T parsed) ? parsed : fallback;
        }
    }
}
