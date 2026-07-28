using GameDBEditorLibrary.Automation;
using GameDBEditorLibrary.UI;
using GameDBLibrary;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Color = GameDBLibrary.Color;
using Vector2 = GameDBLibrary.Vector2;

namespace GameDBLibrary.Tests
{
    public class GameDBValueEditorTests
    {
        private enum SparseEnum
        {
            First = 10,
            Second = 30
        }

        [Test]
        public void Factory_UsesTypedControlsAndLeavesCollectionsReadOnly()
        {
            AssertControl<TextField>(Field("Text", FieldType.@string));
            AssertControl<IntegerField>(Field("Int", FieldType.@int));
            AssertControl<LongField>(Field("Long", FieldType.@long));
            AssertControl<FloatField>(Field("Float", FieldType.@float));
            AssertControl<DoubleField>(Field("Double", FieldType.@double));
            AssertControl<Toggle>(Field("Bool", FieldType.@bool));
            AssertControl<PopupField<string>>(Field("Enum", FieldType.@enum,
                typeArgument: typeof(SparseEnum).FullName));
            AssertControl<PopupField<string>>(Field("Ref", FieldType.tableRef,
                typeArgument: "Targets"));
            AssertControl<ColorField>(Field("Color", FieldType.color));
            AssertControl<Vector2Field>(Field("Vector2", FieldType.vector2));
            AssertControl<Vector3Field>(Field("Vector3", FieldType.vector3));
            AssertControl<Vector4Field>(Field("Vector4", FieldType.vector4));
            AssertControl<ObjectField>(Field("Object", FieldType.unityObject));

            Assert.That(GameDBValueEditorFactory.Create(
                Field("Array", FieldType.@string, isArray: true), _ => null),
                Is.TypeOf<GameDBReadOnlyValueCell>());
            Assert.That(GameDBValueEditorFactory.Create(
                Field("Dictionary", FieldType.dictionary), _ => null),
                Is.TypeOf<GameDBReadOnlyValueCell>());
            Assert.That(GameDBValueEditorFactory.Create(
                Field("ReadOnly", FieldType.@string), null),
                Is.TypeOf<GameDBReadOnlyValueCell>());
        }

        [Test]
        public void EnumAndTableReferenceEditors_UseNamesAndNullableWireValues()
        {
            var intents = new List<GameDBValueEditIntent>();
            var snapshot = Snapshot(
                Table("Items", new[]
                {
                    Field("Rarity", FieldType.@enum,
                        typeArgument: typeof(SparseEnum).FullName),
                    Field("Target", FieldType.tableRef, typeArgument: "Targets")
                }, Row("Sword", ("Rarity", SparseEnum.First), ("Target", null))),
                Table("Targets", Array.Empty<GameDBFieldSnapshot>(),
                    Row("Target1"), Row("Target2")));

            var enumCell = Editor(snapshot, "Items", "Sword", "Rarity", intents);
            var enumPopup = (PopupField<string>)enumCell.Control;
            Assert.That(enumPopup.choices, Is.EqualTo(new[] { "First", "Second" }));
            Assert.That(enumPopup.value, Is.EqualTo("First"));
            enumCell.ApplyControlValue("Second");
            Assert.That(intents.Last().WireValue, Is.EqualTo("Second"));

            var referenceCell = Editor(snapshot, "Items", "Sword", "Target", intents);
            var referencePopup = (PopupField<string>)referenceCell.Control;
            Assert.That(referencePopup.choices,
                Is.EqualTo(new[] { FieldBase.NullRefToken, "Target1", "Target2" }));
            Assert.That(referencePopup.value, Is.EqualTo(FieldBase.NullRefToken));
            referenceCell.ApplyControlValue("Target2");
            Assert.That(intents.Last().WireValue, Is.EqualTo("Target2"));
            referenceCell.ApplyControlValue(FieldBase.NullRefToken);
            Assert.That(intents.Last().WireValue, Is.Null);
        }

        [Test]
        public void ColorAndVectorEditors_EmitCanonicalWireStrings()
        {
            var intents = new List<GameDBValueEditIntent>();
            var snapshot = Snapshot(Table("Items", new[]
            {
                Field("Tint", FieldType.color),
                Field("Offset", FieldType.vector2)
            }, Row("Sword", ("Tint", new Color(1, 2, 3, 4)),
                ("Offset", new Vector2(1f, 2f)))));

            var colorCell = Editor(snapshot, "Items", "Sword", "Tint", intents);
            colorCell.ApplyControlValue(
                (UnityEngine.Color)new UnityEngine.Color32(10, 20, 30, 40));
            Assert.That(intents.Last().WireValue, Is.EqualTo("#0A141E28"));

            var vectorCell = Editor(snapshot, "Items", "Sword", "Offset", intents);
            vectorCell.ApplyControlValue(new UnityEngine.Vector2(3.5f, -2f));
            Assert.That(intents.Last().WireValue, Is.EqualTo("3.5,-2"));
        }

        [Test]
        public void DelayedEditor_CommitsOnceAndEscapeOrFailureRestoresCanonicalValue()
        {
            var intents = new List<GameDBValueEditIntent>();
            var accept = true;
            var field = Field("Name", FieldType.@string);
            var table = Table("Items", new[] { field }, Row("Sword", ("Name", "Iron")));
            var snapshot = Snapshot(table);
            var currentSnapshot = snapshot;
            var cell = (GameDBValueEditorCell)GameDBValueEditorFactory.Create(field,
                intent =>
                {
                    intents.Add(intent);
                    if (accept)
                    {
                        currentSnapshot = Snapshot(Table("Items", new[] { field },
                            Row("Sword", ("Name", intent.WireValue))));
                        currentSnapshot.Revision = "revision-2";
                    }
                    return new GameDBValueEditResult(accept,
                        accept ? null : "Rejected value", currentSnapshot);
                });
            GameDBValueEditorFactory.Bind(cell, field, snapshot, table,
                table.Rows.Single(), snapshot.Revision);
            var text = (TextField)cell.Control;
            Assert.That(text.isDelayed, Is.True);

            cell.ApplyControlValue("Steel");
            Assert.That(intents, Has.Count.EqualTo(1));
            Assert.That(intents[0].TableName, Is.EqualTo("Items"));
            Assert.That(intents[0].RowKey, Is.EqualTo("Sword"));
            Assert.That(intents[0].FieldName, Is.EqualTo("Name"));
            Assert.That(intents[0].WireValue, Is.EqualTo("Steel"));
            Assert.That(intents[0].ExpectedRevision, Is.EqualTo(snapshot.Revision));

            text.SetValueWithoutNotify("Draft");
            cell.CancelDraft();
            Assert.That(text.value, Is.EqualTo("Steel"));
            Assert.That(intents, Has.Count.EqualTo(1));

            accept = false;
            cell.ApplyControlValue("Rejected");
            Assert.That(intents.Last().ExpectedRevision, Is.EqualTo("revision-2"));
            Assert.That(text.value, Is.EqualTo("Steel"));
            Assert.That(cell.ClassListContains(
                "gamedb-editor__value-editor--invalid"), Is.True);
            Assert.That(cell.tooltip, Is.EqualTo("Rejected value"));

            GameDBValueEditorFactory.Unbind(cell);
            Assert.That(cell.userData, Is.Null);
            Assert.That(cell.tooltip, Is.Empty);
            Assert.That(text.value, Is.Empty);
            Assert.That(cell.ClassListContains(
                "gamedb-editor__value-editor--invalid"), Is.False);
        }

        [Test]
        public void MissingEnumAndReferenceValues_RenderExplicitInvalidState()
        {
            var missingEnumField = Field("Rarity", FieldType.@enum,
                typeArgument: "Missing.Enum.Type");
            var missingReferenceField = Field("Target", FieldType.tableRef,
                typeArgument: "MissingTable");
            var table = Table("Items", new[] { missingEnumField, missingReferenceField },
                Row("Sword", ("Rarity", "RemovedMember"), ("Target", "MissingRow")));
            var snapshot = Snapshot(table);

            var enumCell = Editor(snapshot, "Items", "Sword", "Rarity",
                new List<GameDBValueEditIntent>());
            Assert.That(enumCell.ClassListContains(
                "gamedb-editor__value-editor--invalid"), Is.True);
            Assert.That(enumCell.tooltip, Does.Contain("RemovedMember"));

            var referenceCell = Editor(snapshot, "Items", "Sword", "Target",
                new List<GameDBValueEditIntent>());
            Assert.That(referenceCell.ClassListContains(
                "gamedb-editor__value-editor--invalid"), Is.True);
            Assert.That(referenceCell.tooltip, Does.Contain("MissingRow"));
        }


        private static void AssertControl<T>(GameDBFieldSnapshot field)
            where T : VisualElement
        {
            var cell = GameDBValueEditorFactory.Create(field,
                _ => new GameDBValueEditResult(true, null, new GameDBSnapshot()));
            Assert.That(cell, Is.TypeOf<GameDBValueEditorCell>());
            Assert.That(((GameDBValueEditorCell)cell).Control, Is.TypeOf<T>());
        }

        private static GameDBValueEditorCell Editor(GameDBSnapshot snapshot,
            string tableName, string rowKey, string fieldName,
            ICollection<GameDBValueEditIntent> intents)
        {
            var table = snapshot.Tables.Single(candidate => candidate.Name == tableName);
            var field = table.Fields.Single(candidate => candidate.Name == fieldName);
            var row = table.Rows.Single(candidate => candidate.Key == rowKey);
            var cell = (GameDBValueEditorCell)GameDBValueEditorFactory.Create(field,
                intent =>
                {
                    intents.Add(intent);
                    return new GameDBValueEditResult(true, null, snapshot);
                });
            GameDBValueEditorFactory.Bind(cell, field, snapshot, table, row,
                snapshot.Revision);
            return cell;
        }

        private static GameDBSnapshot Snapshot(params GameDBTableSnapshot[] tables)
        {
            return new GameDBSnapshot
            {
                Revision = "revision",
                Tables = tables.ToList()
            };
        }

        private static GameDBTableSnapshot Table(string name,
            IEnumerable<GameDBFieldSnapshot> fields,
            params GameDBRowSnapshot[] rows)
        {
            return new GameDBTableSnapshot
            {
                Name = name,
                Fields = fields.ToList(),
                Rows = rows.ToList()
            };
        }

        private static GameDBRowSnapshot Row(string key,
            params (string Name, object Value)[] values)
        {
            return new GameDBRowSnapshot
            {
                Key = key,
                Values = values.ToDictionary(value => value.Name, value => value.Value)
            };
        }

        private static GameDBFieldSnapshot Field(string name, FieldType type,
            bool isArray = false, string typeArgument = null)
        {
            return new GameDBFieldSnapshot
            {
                Name = name,
                FieldType = type,
                IsArray = isArray,
                TypeArgument = typeArgument
            };
        }
    }
}
