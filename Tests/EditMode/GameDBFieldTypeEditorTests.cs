using GameDBEditorLibrary.Documents;
using GameDBEditorLibrary.UI;
using GameDBLibrary;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace GameDBLibrary.Tests
{
    public class GameDBFieldTypeEditorTests
    {
        [Test]
        public void Adapter_BuildsScalarArrayAndDictionarySpecs()
        {
            var scalar = GameDBFieldTypeDraftAdapter.Validate(
                Draft(FieldType.@int), Array.Empty<string>(),
                Array.Empty<string>(), false);
            var array = GameDBFieldTypeDraftAdapter.Validate(
                Draft(FieldType.@enum, GameDBFieldShape.Array, "Game.Rarity"),
                new[] { "Game.Rarity" }, Array.Empty<string>(), false);
            var dictionary = GameDBFieldTypeDraftAdapter.Validate(
                new GameDBInspectorFieldTypeDraft(FieldType.dictionary, false, null,
                    new GameDBDictionaryTypeDefinition
                    {
                        KeyType = KeyType.@enum,
                        KeyTypeArgument = "Game.Stat",
                        ValueType = FieldType.tableRef,
                        ValueTypeArgument = "Items"
                    }), new[] { "Game.Stat" }, new[] { "Items" }, false);

            Assert.That(scalar.Success, Is.True);
            Assert.That(scalar.TypeSpec.FieldType, Is.EqualTo(FieldType.@int));
            Assert.That(scalar.TypeSpec.IsArray, Is.False);
            Assert.That(array.Success, Is.True);
            Assert.That(array.TypeSpec.IsArray, Is.True);
            Assert.That(array.TypeSpec.TypeArgument, Is.EqualTo("Game.Rarity"));
            Assert.That(dictionary.Success, Is.True, dictionary.Message);
            Assert.That(dictionary.TypeSpec.FieldType, Is.EqualTo(FieldType.dictionary));
            Assert.That(dictionary.TypeSpec.IsArray, Is.False);
            Assert.That(dictionary.TypeSpec.DictionaryType.KeyType,
                Is.EqualTo(KeyType.@enum));
            Assert.That(dictionary.TypeSpec.DictionaryType.ValueType,
                Is.EqualTo(FieldType.tableRef));
            Assert.That(dictionary.TypeSpec.DictionaryType.ValueTypeArgument,
                Is.EqualTo("Items"));
        }

        [Test]
        public void Adapter_RejectsMissingArgumentsAndLocalizationCollections()
        {
            var missingEnum = GameDBFieldTypeDraftAdapter.Validate(
                Draft(FieldType.@enum), new[] { "Game.Rarity" },
                Array.Empty<string>(), false);
            var missingTable = GameDBFieldTypeDraftAdapter.Validate(
                Draft(FieldType.tableRef, argument: "Missing"),
                Array.Empty<string>(), new[] { "Items" }, false);
            var localizationArray = GameDBFieldTypeDraftAdapter.Validate(
                Draft(FieldType.@string, GameDBFieldShape.Array),
                Array.Empty<string>(), Array.Empty<string>(), true);

            Assert.That(missingEnum.Success, Is.False);
            Assert.That(missingEnum.Message, Does.Contain("imported enum"));
            Assert.That(missingTable.Success, Is.False);
            Assert.That(missingTable.Message, Does.Contain("existing table"));
            Assert.That(localizationArray.Success, Is.False);
            Assert.That(localizationArray.Message, Does.Contain("scalar string"));
        }

        [Test]
        public void Adapter_FormatsCompleteSchemaShapes()
        {
            Assert.That(GameDBFieldTypeDraftAdapter.Format(
                Draft(FieldType.@enum, GameDBFieldShape.Array, "Game.Rarity")),
                Is.EqualTo("Enum (Game.Rarity)[]"));
            Assert.That(GameDBFieldTypeDraftAdapter.Format(
                new GameDBInspectorFieldTypeDraft(FieldType.dictionary, false, null,
                    new GameDBDictionaryTypeDefinition
                    {
                        KeyType = KeyType.@string,
                        ValueType = FieldType.tableRef,
                        ValueTypeArgument = "Items"
                    })), Is.EqualTo("Dictionary<String, Table Reference (Items)>"));
        }

        [Test]
        public void Controls_BindShapeAndEmitValidatedChanges()
        {
            var root = new VisualElement();
            using (var editor = new GameDBFieldTypeEditor(root))
            {
                GameDBInspectorFieldTypeDraft changedDraft = null;
                GameDBFieldTypeValidationResult changedValidation = null;
                editor.Changed += (draft, validation) =>
                {
                    changedDraft = draft;
                    changedValidation = validation;
                };
                editor.Bind(Draft(FieldType.@string), new[] { "Game.Rarity" },
                    new[] { "Items", "Weapons" }, false);

                Assert.That(root.Q<DropdownField>("field-type-field")
                    .style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(root.Q<VisualElement>("field-dictionary-type-host")
                    .style.display.value, Is.EqualTo(DisplayStyle.None));

                root.Q<DropdownField>("field-shape-field").value =
                    GameDBFieldShape.Dictionary.ToString();
                root.Q<DropdownField>("field-dictionary-value-type-field").value =
                    "Table Reference";
                root.Q<DropdownField>("field-dictionary-value-argument-field").value =
                    "Weapons";

                Assert.That(changedDraft.Shape,
                    Is.EqualTo(GameDBFieldShape.Dictionary));
                Assert.That(changedValidation.Success, Is.True,
                    changedValidation.Message);
                Assert.That(root.Q<VisualElement>("field-dictionary-type-host")
                    .style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(editor.Validate().TypeSpec.DictionaryType.ValueType,
                    Is.EqualTo(FieldType.tableRef));
                Assert.That(editor.Validate().TypeSpec.DictionaryType.ValueTypeArgument,
                    Is.EqualTo("Weapons"));
            }
        }

        [Test]
        public void Controls_SwitchingArgumentCategoryClearsIncompatibleChoice()
        {
            var root = new VisualElement();
            using (var editor = new GameDBFieldTypeEditor(root))
            {
                editor.Bind(Draft(FieldType.@enum, argument: "Game.Rarity"),
                    new[] { "Game.Rarity" }, new[] { "Items" }, false);

                root.Q<DropdownField>("field-type-field").value = "Table Reference";

                var argument = root.Q<DropdownField>("field-type-argument-field");
                Assert.That(argument.value, Is.Null);
                Assert.That(argument.choices, Is.EqualTo(new[] { "Items" }));
                Assert.That(argument.choices, Does.Not.Contain("Game.Rarity"));
            }
        }

        [Test]
        public void Controls_PreserveUnresolvedCanonicalArgumentButRejectSave()
        {
            var root = new VisualElement();
            using (var editor = new GameDBFieldTypeEditor(root))
            {
                editor.Bind(Draft(FieldType.@enum, argument: "Missing.Enum"),
                    Array.Empty<string>(), Array.Empty<string>(), false);

                var argument = root.Q<DropdownField>("field-type-argument-field");
                Assert.That(argument.choices, Does.Contain("Missing.Enum"));
                Assert.That(argument.value, Is.EqualTo("Missing.Enum"));
                Assert.That(editor.Validate().Success, Is.False);
            }
        }

        private static GameDBInspectorFieldTypeDraft Draft(FieldType type,
            GameDBFieldShape shape = GameDBFieldShape.Scalar,
            string argument = null)
        {
            return new GameDBInspectorFieldTypeDraft(type,
                shape == GameDBFieldShape.Array, argument)
            {
                Shape = shape
            };
        }
    }
}
