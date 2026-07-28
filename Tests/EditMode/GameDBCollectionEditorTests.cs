using GameDBEditorLibrary.Automation;
using GameDBEditorLibrary.Documents;
using GameDBEditorLibrary.UI;
using GameDBEditorLibrary.Workspace;
using GameDBLibrary;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using Color = GameDBLibrary.Color;
using Vector2 = GameDBLibrary.Vector2;
using Vector3 = GameDBLibrary.Vector3;
using Vector4 = GameDBLibrary.Vector4;

namespace GameDBLibrary.Tests
{
    public class GameDBCollectionEditorTests
    {
        private enum SparseEnum
        {
            First = 10,
            Second = 30
        }

        [Test]
        public void Factory_UsesCollectionLauncherOnlyWhenCallbackIsAvailable()
        {
            var field = new GameDBFieldSnapshot
            {
                Name = "Values",
                FieldType = FieldType.@int,
                IsArray = true
            };
            GameDBCollectionEditRequest opened = null;
            var editable = GameDBValueEditorFactory.Create(field, null,
                request => opened = request);
            var readOnly = GameDBValueEditorFactory.Create(field, null);
            var snapshot = Snapshot(field, new List<object> { 1, 2 });
            var table = snapshot.Tables.Single();
            var row = table.Rows.Single();

            GameDBValueEditorFactory.Bind(editable, field, snapshot, table, row,
                snapshot.Revision);
            ((GameDBCollectionValueCell)editable).Open();

            Assert.That(editable, Is.TypeOf<GameDBCollectionValueCell>());
            Assert.That(readOnly, Is.TypeOf<GameDBReadOnlyValueCell>());
            Assert.That(editable.Q<Label>().text, Is.EqualTo("2 items"));
            Assert.That(opened, Is.Not.Null);
            Assert.That(opened.Row, Is.SameAs(row));
            GameDBValueEditorFactory.Unbind(editable);
            Assert.That(editable.userData, Is.Null);
            Assert.That(editable.Q<Button>().enabledSelf, Is.False);
        }

        [Test]
        public void ScalarAdapter_ConvertsStoredCollectionValuesToCanonicalWireValues()
        {
            var snapshot = new GameDBSnapshot
            {
                Tables = new List<GameDBTableSnapshot>
                {
                    new GameDBTableSnapshot
                    {
                        Name = "Targets",
                        Rows = new List<GameDBRowSnapshot>
                        {
                            new GameDBRowSnapshot { Key = "Target" }
                        }
                    }
                }
            };

            Assert.That(Wire(FieldType.@enum, SparseEnum.Second, snapshot,
                typeof(SparseEnum).FullName), Is.EqualTo("Second"));
            Assert.That(Wire(FieldType.tableRef, null, snapshot, "Targets"), Is.Null);
            Assert.That(Wire(FieldType.tableRef, "Target", snapshot, "Targets"),
                Is.EqualTo("Target"));
            Assert.That(Wire(FieldType.color, new Color(1, 2, 3, 4), snapshot),
                Is.EqualTo("#01020304"));
            Assert.That(Wire(FieldType.vector2, new Vector2(1.5f, -2f), snapshot),
                Is.EqualTo("1.5,-2"));
            Assert.That(Wire(FieldType.vector3, new Vector3(1f, 2f, 3f), snapshot),
                Is.EqualTo("1,2,3"));
            Assert.That(Wire(FieldType.vector4, new Vector4(1f, 2f, 3f, 4f), snapshot),
                Is.EqualTo("1,2,3,4"));

            var enumDescriptor = Descriptor(FieldType.@enum, snapshot,
                typeof(SparseEnum).FullName);
            Assert.That(GameDBScalarDraftAdapter.DefaultStoredValue(enumDescriptor),
                Is.EqualTo("First"));
            Assert.That(GameDBScalarDraftAdapter.DefaultStoredValue(
                Descriptor(FieldType.tableRef, snapshot, "Targets")), Is.Null);
            Assert.That(GameDBScalarDraftAdapter.DefaultStoredValue(
                Descriptor(FieldType.unityObject, snapshot)),
                Is.EqualTo(UnityObjectReference.Empty));
        }

        [Test]
        public void ScalarAdapter_RebindsControlShapedValuesAndUsesDelayedNumericFields()
        {
            var snapshot = new GameDBSnapshot();
            var colorDescriptor = Descriptor(FieldType.color, snapshot);
            var color = (ColorField)GameDBScalarDraftAdapter.CreateControl(
                colorDescriptor, _ => { });
            var unityColor = new UnityEngine.Color(0.1f, 0.2f, 0.3f, 0.4f);
            Assert.DoesNotThrow(() => GameDBScalarDraftAdapter.SetStoredValue(
                color, colorDescriptor, unityColor));
            Assert.That(color.value, Is.EqualTo(unityColor));
            Assert.DoesNotThrow(() => GameDBScalarDraftAdapter.SetStoredValue(
                color, colorDescriptor, null));
            Assert.That(color.value, Is.EqualTo(UnityEngine.Color.clear));

            var vectorDescriptor = Descriptor(FieldType.vector4, snapshot);
            var vector = (Vector4Field)GameDBScalarDraftAdapter.CreateControl(
                vectorDescriptor, _ => { });
            var unityVector = new UnityEngine.Vector4(1f, 2f, 3f, 4f);
            Assert.DoesNotThrow(() => GameDBScalarDraftAdapter.SetStoredValue(
                vector, vectorDescriptor, unityVector));
            Assert.That(vector.value, Is.EqualTo(unityVector));

            Assert.That(((TextField)GameDBScalarDraftAdapter.CreateControl(
                Descriptor(FieldType.@string, snapshot), _ => { })).isDelayed, Is.True);
            Assert.That(((IntegerField)GameDBScalarDraftAdapter.CreateControl(
                Descriptor(FieldType.@int, snapshot), _ => { })).isDelayed, Is.True);
            Assert.That(((FloatField)GameDBScalarDraftAdapter.CreateControl(
                Descriptor(FieldType.@float, snapshot), _ => { })).isDelayed, Is.True);
        }

        [Test]
        public void ArrayApply_ExecutesOneSetValueCommandAndStoresCanonicalValues()
        {
            using (var fixture = Fixture.Array(new List<object> { 1, 2 }))
            {
                Assert.That(fixture.Open(), Is.True);
                fixture.Controller.SetDraftValue(0, 5);
                fixture.Controller.Add();
                fixture.Controller.SetDraftValue(2, 9);

                var result = fixture.Controller.Apply();

                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(result.CommandKind, Is.EqualTo(GameDBCommandKind.SetValue));
                Assert.That(fixture.ChangeCount, Is.EqualTo(1));
                Assert.That(fixture.RefreshCount, Is.EqualTo(1));
                Assert.That(fixture.Controller.IsOpen, Is.False);
                Assert.That(fixture.StoredValues(), Is.EqualTo(new object[] { 5, 2, 9 }));
            }
        }

        [Test]
        public void DictionaryDuplicateKeys_BlockApplyWithoutChangingRevision()
        {
            using (var fixture = Fixture.Dictionary(new Dictionary<string, object>
            {
                { "first", 1 }
            }))
            {
                Assert.That(fixture.Open(), Is.True);
                fixture.Controller.Add();
                fixture.Controller.SetDraftKey(0, "duplicate");
                fixture.Controller.SetDraftKey(1, "duplicate");
                var revision = fixture.Session.CreateSnapshot().Revision;

                var result = fixture.Controller.Apply();

                Assert.That(result, Is.Null);
                Assert.That(fixture.Controller.DraftErrors,
                    Is.All.EqualTo("Dictionary keys must be unique."));
                Assert.That(fixture.Root.Q<Button>("collection-apply-button").enabledSelf,
                    Is.False);
                Assert.That(fixture.Session.CreateSnapshot().Revision, Is.EqualTo(revision));
                Assert.That(fixture.ChangeCount, Is.Zero);
            }
        }

        [Test]
        public void RevisionConflict_RetainsDraftUntilReloadCurrent()
        {
            using (var fixture = Fixture.Array(new List<object> { 1 }))
            {
                Assert.That(fixture.Open(), Is.True);
                fixture.Controller.SetDraftValue(0, 7);
                var changed = fixture.Session.ApplyTransaction(new GameDBCommand[]
                {
                    new SetDatabaseMetadataCommand("ChangedElsewhere", false)
                });
                Assert.That(changed.Success, Is.True, changed.Message);

                var conflict = fixture.Controller.Apply();

                Assert.That(conflict.Success, Is.False);
                Assert.That(conflict.FailureKind,
                    Is.EqualTo(GameDBTransactionFailureKind.RevisionConflict));
                Assert.That(fixture.Controller.IsOpen, Is.True);
                Assert.That(fixture.Controller.IsStale, Is.True);
                Assert.That(fixture.Controller.DraftValues, Is.EqualTo(new object[] { 7 }));
                Assert.That(fixture.Root.Q<Button>("collection-reload-button").style.display.value,
                    Is.EqualTo(DisplayStyle.Flex));

                fixture.Controller.ReloadCurrent();
                Assert.That(fixture.Controller.IsStale, Is.False);
                Assert.That(fixture.Controller.DraftValues, Is.EqualTo(new object[] { 1 }));
                Assert.That(fixture.Controller.Apply().Success, Is.True);
            }
        }

        [Test]
        public void TabChangeCancelAndDispose_NeverApplyToAnotherDocument()
        {
            using (var fixture = Fixture.Array(new List<object> { 1 }, includeSecondTab: true))
            {
                Assert.That(fixture.Open(), Is.True);
                fixture.Controller.SetDraftValue(0, 9);
                var originalTab = fixture.Workspace.ActiveTab;
                var otherTab = fixture.Workspace.Tabs.Single(tab => tab.TabId != originalTab.TabId);
                Assert.That(fixture.Workspace.TryActivateTab(otherTab.TabId), Is.True);

                Assert.That(fixture.Controller.Apply(), Is.Null);
                Assert.That(fixture.Controller.IsOpen, Is.True);
                Assert.That(originalTab.Session.CreateSnapshot().Revision,
                    Is.EqualTo(fixture.OpeningRevision));
                fixture.Controller.SetDraftValue(0, 10);
                Assert.That(fixture.Root.Q<Button>("collection-apply-button").enabledSelf,
                    Is.False);
                Assert.That(fixture.Controller.Apply(), Is.Null);

                fixture.Controller.Cancel();
                Assert.That(fixture.Controller.IsOpen, Is.False);
                Assert.That(fixture.ChangeCount, Is.Zero);
                fixture.Controller.Dispose();
                Assert.That(fixture.Controller.Open(fixture.Request()), Is.False);
                Assert.That(fixture.Controller.Apply(), Is.Null);
            }
        }

        private static object Wire(FieldType type, object value, GameDBSnapshot snapshot,
            string typeArgument = null)
        {
            return GameDBScalarDraftAdapter.ToWireValue(
                Descriptor(type, snapshot, typeArgument), value);
        }

        private static GameDBScalarDraftDescriptor Descriptor(FieldType type,
            GameDBSnapshot snapshot, string typeArgument = null)
        {
            return new GameDBScalarDraftDescriptor(type, typeArgument, snapshot);
        }

        private static GameDBSnapshot Snapshot(GameDBFieldSnapshot field, object value)
        {
            return new GameDBSnapshot
            {
                Revision = "revision",
                Tables = new List<GameDBTableSnapshot>
                {
                    new GameDBTableSnapshot
                    {
                        Name = "Items",
                        Fields = new List<GameDBFieldSnapshot> { field },
                        Rows = new List<GameDBRowSnapshot>
                        {
                            new GameDBRowSnapshot
                            {
                                Key = "Item",
                                Values = new Dictionary<string, object>
                                {
                                    { field.Name, value }
                                }
                            }
                        }
                    }
                }
            };
        }

        private sealed class Fixture : IDisposable
        {
            private readonly string m_fieldName;
            internal VisualElement Root { get; }
            internal GameDBEditorWorkspace Workspace { get; }
            internal GameDBAssetSession Session { get; }
            internal GameDBCollectionEditorController Controller { get; }
            internal string OpeningRevision { get; private set; }
            internal int ChangeCount { get; private set; }
            internal int RefreshCount { get; private set; }
            private readonly Action<GameDBDocumentChange> m_changed;

            private Fixture(GameDBFieldTypeSpec type, object value, bool includeSecondTab)
            {
                m_fieldName = type.FieldType == FieldType.dictionary ? "Lookup" : "Values";
                var primary = CreateDocument("primary", type, m_fieldName, value);
                var tabs = new List<GameDBWorkspaceRecoveryTab>
                {
                    new GameDBWorkspaceRecoveryTab("primary", primary.CaptureState())
                };
                if (includeSecondTab)
                {
                    tabs.Add(new GameDBWorkspaceRecoveryTab("secondary",
                        GameDBDocument.CreateNew(
                            $"Assets/GameDBCollectionEditorTests/{Guid.NewGuid():N}.json",
                            "Secondary", false).CaptureState()));
                }
                var store = new MemoryRecoveryStore();
                var recovery = new GameDBWorkspaceRecoveryService(store);
                Assert.That(recovery.Save(new GameDBWorkspaceRecoverySnapshot(tabs,
                    "primary")).Success, Is.True);
                Workspace = new GameDBEditorWorkspace(
                    new GameDBDocumentLeaseRegistry(GameDBFilePairStore.Instance),
                    recovery, new GameDBActiveWorkspaceHub());
                Session = Workspace.ActiveTab.Session;
                m_changed = _ => ChangeCount++;
                Session.Changed += m_changed;
                Root = new VisualElement();
                GameDBEditorUiAssets.Build(Root);
                Controller = new GameDBCollectionEditorController(Root, Workspace,
                    () => RefreshCount++);
            }

            internal static Fixture Array(object value, bool includeSecondTab = false)
            {
                return new Fixture(new GameDBFieldTypeSpec(FieldType.@int, true, null),
                    value, includeSecondTab);
            }

            internal static Fixture Dictionary(object value)
            {
                return new Fixture(new GameDBFieldTypeSpec(FieldType.dictionary, false,
                    null, new GameDBDictionaryTypeSpec(KeyType.@string, null,
                        FieldType.@int, null)), value, false);
            }

            internal bool Open()
            {
                var request = Request();
                OpeningRevision = request.Revision;
                return Controller.Open(request);
            }

            internal GameDBCollectionEditRequest Request()
            {
                var snapshot = Session.CreateSnapshot();
                var table = snapshot.Tables.Single(table => table.Name == "Items");
                return new GameDBCollectionEditRequest(snapshot, table,
                    table.Rows.Single(row => row.Key == "Item"),
                    table.Fields.Single(field => field.Name == m_fieldName),
                    snapshot.Revision, null);
            }

            internal object[] StoredValues()
            {
                var value = Session.CreateSnapshot().Tables.Single(table =>
                    table.Name == "Items").Rows.Single(row => row.Key == "Item")
                    .Values[m_fieldName];
                return ((IEnumerable)value).Cast<object>().ToArray();
            }

            public void Dispose()
            {
                Session.Changed -= m_changed;
                Controller.Dispose();
                Workspace.Dispose();
            }

            private static GameDBDocument CreateDocument(string name,
                GameDBFieldTypeSpec type, string fieldName, object value)
            {
                var document = GameDBDocument.CreateNew(
                    $"Assets/GameDBCollectionEditorTests/{name}-{Guid.NewGuid():N}.json",
                    "CollectionTests", false);
                var result = document.ApplyTransaction(new GameDBCommand[]
                {
                    new AddTableCommand("Items", KeyType.@string, null),
                    new AddFieldCommand("Items", fieldName, type),
                    new AddRowCommand("Items", "Item", new Dictionary<string, object>
                    {
                        { fieldName, value }
                    })
                });
                Assert.That(result.Success, Is.True, result.Message);
                return document;
            }
        }

        private sealed class MemoryRecoveryStore : IGameDBWorkspaceRecoveryStore
        {
            private string m_contents;
            public bool Exists => m_contents != null;
            public string ReadAllText() => m_contents;
            public void WriteAtomically(string contents) => m_contents = contents;
            public string QuarantinePrimary()
            {
                m_contents = null;
                return null;
            }
            public string WriteQuarantine(string label, string contents) => null;
        }
    }
}
