using GameDBEditorLibrary.Automation;
using GameDBEditorLibrary.Documents;
using GameDBEditorLibrary.UI;
using GameDBEditorLibrary.Workspace;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace GameDBLibrary.Tests
{
    public class GameDBTableViewTests
    {
        [Test]
        public void Projection_SelectsFallbackAndOrdersRowsAlphanumericallyByStableKey()
        {
            var snapshot = new GameDBSnapshot
            {
                Tables = new List<GameDBTableSnapshot>
                {
                    Table("Items", "Row10", "Row2", "Row1"),
                    Table("Recipes", "Recipe1")
                }
            };

            var projection = new GameDBTableViewProjection(snapshot, "missing");

            Assert.That(projection.SelectedTable.Name, Is.EqualTo("Items"));
            Assert.That(projection.Rows.Select(row => row.Key),
                Is.EqualTo(new[] { "Row1", "Row2", "Row10" }));
            Assert.That(projection.IndexOfRow("Row2"), Is.EqualTo(1));
            Assert.That(projection.IndexOfRow("missing"), Is.EqualTo(-1));

            var oversized = new GameDBTableViewProjection(new GameDBSnapshot
            {
                Tables = new List<GameDBTableSnapshot>
                {
                    Table("LargeKeys", "Row99999999999999999999", "Row2")
                }
            }, "LargeKeys");
            Assert.That(oversized.Rows.Select(row => row.Key),
                Is.EqualTo(new[] { "Row2", "Row99999999999999999999" }));

            var leadingZeros = new GameDBTableViewProjection(new GameDBSnapshot
            {
                Tables = new List<GameDBTableSnapshot>
                {
                    Table("LeadingZeros", "Row002", "Row02", "Row2")
                }
            }, "LeadingZeros");
            Assert.That(leadingZeros.Rows.Select(row => row.Key),
                Is.EqualTo(new[] { "Row2", "Row02", "Row002" }));
        }

        [Test]
        public void Projection_FormatsScalarAndCollectionValuesWithoutMutatingSnapshot()
        {
            Assert.That(GameDBTableViewProjection.FormatValue(null), Is.Empty);
            Assert.That(GameDBTableViewProjection.FormatValue(true), Is.EqualTo("true"));
            Assert.That(GameDBTableViewProjection.FormatValue(12.5), Is.EqualTo("12.5"));
            Assert.That(GameDBTableViewProjection.FormatValue(new[] { 1, 2 }),
                Is.EqualTo("2 items"));
            Assert.That(GameDBTableViewProjection.FormatValue(
                new Dictionary<string, object> { { "Power", 1 } }),
                Is.EqualTo("1 entry"));
        }

        [Test]
        public void Projection_SearchesKeysAndRenderedValuesWithoutMutatingSnapshot()
        {
            var table = new GameDBTableSnapshot
            {
                Name = "Items",
                Fields = new List<GameDBFieldSnapshot>
                {
                    Field("Name", GameDBLibrary.FieldType.@string),
                    Field("Tags", GameDBLibrary.FieldType.@string, true)
                },
                Rows = new List<GameDBRowSnapshot>
                {
                    Row("Sword10", ("Name", "Iron Blade"),
                        ("Tags", new[] { "weapon", "metal" })),
                    Row("Potion2", ("Name", "Healing"),
                        ("Tags", new[] { "consumable" }))
                }
            };
            var snapshot = new GameDBSnapshot
            {
                Tables = new List<GameDBTableSnapshot> { table }
            };

            Assert.That(new GameDBTableViewProjection(snapshot, "Items", "sWoRd")
                .Rows.Select(row => row.Key), Is.EqualTo(new[] { "Sword10" }));
            Assert.That(new GameDBTableViewProjection(snapshot, "Items", "IRON")
                .Rows.Select(row => row.Key), Is.EqualTo(new[] { "Sword10" }));
            Assert.That(new GameDBTableViewProjection(snapshot, "Items", "2 items")
                .Rows.Select(row => row.Key), Is.EqualTo(new[] { "Sword10" }));
            Assert.That(new GameDBTableViewProjection(snapshot, "Items", "missing").Rows,
                Is.Empty);
            Assert.That(table.Rows, Has.Count.EqualTo(2));
        }

        [Test]
        public void Projection_AppliesSanitizedStableMultiSortAndRowKeyTieBreak()
        {
            var table = new GameDBTableSnapshot
            {
                Name = "Items",
                Fields = new List<GameDBFieldSnapshot>
                {
                    Field("Group", GameDBLibrary.FieldType.@string),
                    Field("Power", GameDBLibrary.FieldType.@double),
                    Field("Enabled", GameDBLibrary.FieldType.@bool)
                },
                Rows = new List<GameDBRowSnapshot>
                {
                    Row("Row10", ("Group", "B"), ("Power", 2), ("Enabled", true)),
                    Row("Row2", ("Group", "A"), ("Power", 10L), ("Enabled", false)),
                    Row("Row1", ("Group", "A"), ("Power", 10.0), ("Enabled", true)),
                    Row("Row3", ("Group", null), ("Power", 100), ("Enabled", false))
                }
            };
            var projection = new GameDBTableViewProjection(new GameDBSnapshot
            {
                Tables = new List<GameDBTableSnapshot> { table }
            }, "Items", null, new[]
            {
                new GameDBWorkspaceSortState("missing", false),
                new GameDBWorkspaceSortState("Group", false),
                new GameDBWorkspaceSortState("Power", true),
                new GameDBWorkspaceSortState("Group", true)
            });

            Assert.That(projection.Sorts.Select(sort => sort.FieldId),
                Is.EqualTo(new[] { "Group", "Power" }));
            Assert.That(projection.Rows.Select(row => row.Key),
                Is.EqualTo(new[] { "Row1", "Row2", "Row10", "Row3" }));

            var keyDescending = new GameDBTableViewProjection(new GameDBSnapshot
            {
                Tables = new List<GameDBTableSnapshot> { table }
            }, "Items", null, new[]
            {
                new GameDBWorkspaceSortState(GameDBTableViewProjection.KeyFieldId, true)
            });
            Assert.That(keyDescending.Rows.Select(row => row.Key),
                Is.EqualTo(new[] { "Row10", "Row3", "Row2", "Row1" }));
        }

        [Test]
        public void Projection_SortsDeterministicallyAcrossCultureAndSchemaValues()
        {
            var previousCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
                var table = new GameDBTableSnapshot
                {
                    Name = "Items",
                    Fields = new List<GameDBFieldSnapshot>
                    {
                        Field("Name", GameDBLibrary.FieldType.@string),
                        Field("Tags", GameDBLibrary.FieldType.@string, true),
                        Field("Target", GameDBLibrary.FieldType.tableRef),
                        Field("Power", GameDBLibrary.FieldType.@int)
                    },
                    Rows = new List<GameDBRowSnapshot>
                    {
                        Row("item10", ("Name", "item"),
                            ("Tags", new[] { "a", "b" }), ("Target", "Target10"),
                            ("Power", 10)),
                        Row("Item2", ("Name", "Item"),
                            ("Tags", new[] { "a" }), ("Target", "Target2"),
                            ("Power", 5)),
                        Row("Item1", ("Name", null),
                            ("Tags", new[] { "a", "b", "c" }), ("Target", "Target1"),
                            ("Power", "1z"))
                    }
                };
                var snapshot = new GameDBSnapshot
                {
                    Tables = new List<GameDBTableSnapshot> { table }
                };

                var text = new GameDBTableViewProjection(snapshot, "Items", null,
                    new[] { new GameDBWorkspaceSortState("Name", false) });
                Assert.That(text.Rows.Select(row => row.Key),
                    Is.EqualTo(new[] { "Item2", "item10", "Item1" }));

                var descendingText = new GameDBTableViewProjection(snapshot, "Items", null,
                    new[] { new GameDBWorkspaceSortState("Name", true) });
                Assert.That(descendingText.Rows.Select(row => row.Key),
                    Is.EqualTo(new[] { "item10", "Item2", "Item1" }));

                var collections = new GameDBTableViewProjection(snapshot, "Items", null,
                    new[] { new GameDBWorkspaceSortState("Tags", true) });
                Assert.That(collections.Rows.Select(row => row.Key),
                    Is.EqualTo(new[] { "Item1", "item10", "Item2" }));

                var references = new GameDBTableViewProjection(snapshot, "Items", null,
                    new[] { new GameDBWorkspaceSortState("Target", false) });
                Assert.That(references.Rows.Select(row => row.Key),
                    Is.EqualTo(new[] { "Item1", "Item2", "item10" }));

                var malformedNumeric = new GameDBTableViewProjection(snapshot, "Items", null,
                    new[] { new GameDBWorkspaceSortState("Power", false) });
                Assert.That(malformedNumeric.Rows.Select(row => row.Key),
                    Is.EqualTo(new[] { "Item2", "item10", "Item1" }));
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
            }
        }

        [Test]
        public void TableController_NormalizesStaleTableAndRowKeysAgainstResolvedTable()
        {
            var snapshot = new GameDBSnapshot
            {
                Tables = new List<GameDBTableSnapshot>
                {
                    Table("Items", "Shared", "ItemOnly"),
                    Table("Recipes", "Shared", "RecipeOnly")
                }
            };
            var navigation = new ListView();
            var grid = new MultiColumnListView();
            var placeholder = new Label();
            using (var controller = new GameDBTableViewController(new ToolbarSearchField(), navigation, grid,
                placeholder, _ => { }))
            {
                var missingTable = controller.Bind(
                    new GameDBWorkspaceTabViewState("Removed", "Shared"), snapshot);
                Assert.That(missingTable.SelectedTableId, Is.EqualTo("Items"));
                Assert.That(missingTable.SelectedRowId, Is.EqualTo("Shared"));

                var wrongTableRow = controller.Bind(
                    new GameDBWorkspaceTabViewState("Recipes", "ItemOnly"), snapshot);
                Assert.That(wrongTableRow.SelectedTableId, Is.EqualTo("Recipes"));
                Assert.That(wrongTableRow.SelectedRowId, Is.Null);
                Assert.That(grid.selectedIndex, Is.EqualTo(-1));
            }
        }

        [Test]
        public void TableController_RendersEmptyDatabaseAndEmptyTableStates()
        {
            var navigation = new ListView();
            var grid = new MultiColumnListView();
            var placeholder = new Label();
            using (var controller = new GameDBTableViewController(new ToolbarSearchField(), navigation, grid,
                placeholder, _ => { }))
            {
                controller.Bind(new GameDBWorkspaceTabViewState(), new GameDBSnapshot());
                Assert.That(placeholder.text, Is.EqualTo("This database has no tables."));
                Assert.That(grid.style.display.value, Is.EqualTo(DisplayStyle.None));
                Assert.That(grid.columns, Is.Empty);

                controller.Bind(new GameDBWorkspaceTabViewState("Empty"),
                    new GameDBSnapshot
                    {
                        Tables = new List<GameDBTableSnapshot>
                        {
                            Table("Empty")
                        }
                    });
                Assert.That(placeholder.text, Is.EqualTo("'Empty' has no rows."));
                Assert.That(grid.columns.Count, Is.EqualTo(2));
                Assert.That(grid.itemsSource, Is.Empty);
            }
        }

        [Test]
        public void TableController_BindsRepresentativeFixtureWithDynamicVirtualizedColumns()
        {
            var snapshot = GameDBRepresentativeFixture.CreateDocument().CreateSnapshot();
            var navigation = new ListView();
            var grid = new MultiColumnListView();
            var placeholder = new Label();
            var selections = new List<string>();
            using (var controller = new GameDBTableViewController(new ToolbarSearchField(), navigation, grid,
                placeholder, state => selections.Add(state.SelectedTableId + "/" + state.SelectedRowId)))
            {
                controller.Bind(new GameDBWorkspaceTabViewState(
                    "Table01", "Row0123"), snapshot);

                Assert.That(navigation.itemsSource.Count,
                    Is.EqualTo(GameDBRepresentativeFixture.DefaultTableCount));
                Assert.That(grid.itemsSource.Count,
                    Is.EqualTo(GameDBRepresentativeFixture.DefaultRowsPerTable));
                Assert.That(grid.columns.Count,
                    Is.EqualTo(GameDBRepresentativeFixture.DefaultFieldsPerTable + 1));
                Assert.That(grid.columns.First().name,
                    Is.EqualTo(GameDBTableViewProjection.KeyFieldId));
                Assert.That(grid.columns.Last().name, Is.EqualTo("Field23"));
                Assert.That(grid.selectedIndex, Is.EqualTo(123));
                Assert.That(placeholder.style.display.value, Is.EqualTo(DisplayStyle.None));
                Assert.That(selections, Is.Empty);

                var firstColumns = grid.columns.ToArray();
                controller.Bind(new GameDBWorkspaceTabViewState(
                    "Table01", "Row0001"), snapshot);
                Assert.That(grid.columns.ToArray(), Is.EqualTo(firstColumns));
                Assert.That(grid.selectedIndex, Is.EqualTo(1));
                Assert.That(selections, Is.Empty);
            }
        }

        [Test]
        public void TableController_RestoresAndPublishesSanitizedColumnLayout()
        {
            var snapshot = new GameDBSnapshot
            {
                Tables = new List<GameDBTableSnapshot>
                {
                    Table("Items", "Row1", "Row2")
                }
            };
            var grid = new MultiColumnListView();
            var states = new List<GameDBWorkspaceTabViewState>();
            var controller = new GameDBTableViewController(new ToolbarSearchField(),
                new ListView(), grid, new Label(), states.Add);
            try
            {
                var resolved = controller.Bind(new GameDBWorkspaceTabViewState(
                    "Items", columns: new[]
                    {
                        new GameDBWorkspaceColumnState("Value", 0f, 0),
                        new GameDBWorkspaceColumnState("Value", 200f, 2),
                        new GameDBWorkspaceColumnState("missing", 300f, 1),
                        new GameDBWorkspaceColumnState(
                            GameDBTableViewProjection.KeyFieldId, 1000f, 1),
                        new GameDBWorkspaceColumnState("invalid-width", -1f, 3)
                    }), snapshot);

                Assert.That(resolved.Columns.Select(column => column.FieldId),
                    Is.EqualTo(new[]
                    {
                        "Value", GameDBTableViewProjection.KeyFieldId
                    }));
                Assert.That(resolved.Columns.Select(column => column.Width),
                    Is.EqualTo(new[] { 140f, 600f }));
                Assert.That(resolved.Columns.Select(column => column.Order),
                    Is.EqualTo(new[] { 0, 1 }));
                Assert.That(grid.columns["Value"].width.value, Is.EqualTo(140f));
                Assert.That(grid.columns[GameDBTableViewProjection.KeyFieldId]
                    .width.value, Is.EqualTo(600f));
                Assert.That(resolved.Columns.All(column =>
                    column.TableId == "Items"), Is.True);
                Assert.That(grid.columns.reorderable, Is.False);
                Assert.That(grid.columns.resizable, Is.True);
                Assert.That(grid.columns.resizePreview, Is.True);
                Assert.That(states, Is.Empty);

                grid.columns["Value"].width = 125f;
                Assert.That(states, Has.Count.EqualTo(1));
                Assert.That(states.Last().Columns.Single(column =>
                    column.FieldId == "Value").Width, Is.EqualTo(125f));

                Assert.That(controller.MoveColumn("Value", 1), Is.True);
                Assert.That(states.Last().Columns.Select(column => column.FieldId),
                    Is.EqualTo(new[]
                    {
                        GameDBTableViewProjection.KeyFieldId, "Value"
                    }));
                Assert.That(controller.MoveColumn("Value", 1), Is.False);
                Assert.That(controller.MoveColumn("Value", -2), Is.False);
                Assert.That(controller.MoveColumn("missing", -1), Is.False);

                var callbacks = states.Count;
                var retainedValueColumn = grid.columns["Value"];
                controller.Dispose();
                retainedValueColumn.width = 200f;
                Assert.That(controller.MoveColumn("Value", -1), Is.False);
                Assert.That(states, Has.Count.EqualTo(callbacks));
            }
            finally
            {
                controller.Dispose();
            }
        }

        [Test]
        public void TableController_PreservesIndependentLayoutsAcrossTables()
        {
            var snapshot = new GameDBSnapshot
            {
                Tables = new List<GameDBTableSnapshot>
                {
                    Table("Items", "Item1"),
                    Table("Recipes", "Recipe1")
                }
            };
            var grid = new MultiColumnListView();
            var states = new List<GameDBWorkspaceTabViewState>();
            using (var controller = new GameDBTableViewController(
                new ToolbarSearchField(), new ListView(), grid,
                new Label(), states.Add))
            {
                var state = controller.Bind(new GameDBWorkspaceTabViewState(
                    "Items"), snapshot);
                grid.columns["Value"].width = 200f;
                state = states.Last();

                state = controller.Bind(new GameDBWorkspaceTabViewState(
                    "Recipes", columns: state.Columns), snapshot);
                grid.columns["Value"].width = 100f;
                state = states.Last();

                state = controller.Bind(new GameDBWorkspaceTabViewState(
                    "Items", columns: state.Columns), snapshot);
                Assert.That(grid.columns["Value"].width.value, Is.EqualTo(200f));
                Assert.That(state.Columns.Single(column => column.TableId == "Items"
                    && column.FieldId == "Value").Width, Is.EqualTo(200f));
                Assert.That(state.Columns.Single(column => column.TableId == "Recipes"
                    && column.FieldId == "Value").Width, Is.EqualTo(100f));
            }
        }

        [Test]
        public void TableController_SeparatesSyntheticKeyFromKeyNamedField()
        {
            var table = new GameDBTableSnapshot
            {
                Name = "Items",
                Fields = new List<GameDBFieldSnapshot>
                {
                    Field("__key", GameDBLibrary.FieldType.@int)
                },
                Rows = new List<GameDBRowSnapshot>
                {
                    Row("Row2", ("__key", 1)),
                    Row("Row1", ("__key", 2))
                }
            };
            var snapshot = new GameDBSnapshot
            {
                Tables = new List<GameDBTableSnapshot> { table }
            };
            var grid = new MultiColumnListView();
            using (var controller = new GameDBTableViewController(
                new ToolbarSearchField(), new ListView(), grid,
                new Label(), _ => { }))
            {
                var state = controller.Bind(new GameDBWorkspaceTabViewState(
                    "Items", sorts: new[]
                    {
                        new GameDBWorkspaceSortState("__key", false)
                    }), snapshot);

                Assert.That(grid.columns.Select(column => column.name),
                    Is.EqualTo(new[]
                    {
                        GameDBTableViewProjection.KeyFieldId, "__key"
                    }));
                Assert.That(grid.itemsSource.Cast<GameDBRowSnapshot>()
                    .Select(row => row.Key), Is.EqualTo(new[] { "Row2", "Row1" }));
                Assert.That(state.Sorts.Single().FieldId, Is.EqualTo("__key"));
            }
        }

        [Test]
        public void TableController_RecycledCellClearsAndRebindsAllTransientState()
        {
            var snapshot = new GameDBSnapshot
            {
                Tables = new List<GameDBTableSnapshot>
                {
                    Table("Items", "Row1", "Row2")
                }
            };
            var navigation = new ListView();
            var grid = new MultiColumnListView();
            var placeholder = new Label();
            using (var controller = new GameDBTableViewController(new ToolbarSearchField(), navigation, grid,
                placeholder, _ => { }))
            {
                controller.Bind(new GameDBWorkspaceTabViewState("Items"), snapshot);
                var column = grid.columns.Single(candidate => candidate.name == "Value");
                var cell = (Label)column.makeCell();

                column.bindCell(cell, 0);
                Assert.That(cell.text, Is.EqualTo("Row1"));
                Assert.That(cell.tooltip, Is.EqualTo("Row1"));
                Assert.That(cell.userData, Is.EqualTo("Row1"));

                column.unbindCell(cell, 0);
                Assert.That(cell.text, Is.Empty);
                Assert.That(cell.tooltip, Is.Empty);
                Assert.That(cell.userData, Is.Null);

                column.bindCell(cell, 1);
                Assert.That(cell.text, Is.EqualTo("Row2"));
                Assert.That(cell.tooltip, Is.EqualTo("Row2"));
                Assert.That(cell.userData, Is.EqualTo("Row2"));
            }
        }

        [Test]
        public void TableController_RepresentativeInteractionsReuseColumnsAndReportProfile()
        {
            var snapshot = GameDBRepresentativeFixture.CreateDocument().CreateSnapshot();
            var grid = new MultiColumnListView
            {
                sortingMode = ColumnSortingMode.Custom
            };
            using (var controller = new GameDBTableViewController(
                new ToolbarSearchField(), new ListView(), grid, new Label(), _ => { }))
            {
                controller.Bind(new GameDBWorkspaceTabViewState("Table00"), snapshot);
                var columns = grid.columns.ToArray();
                controller.SetSearchText("Row0123");
                controller.SetSearchText(string.Empty);

                var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                var stopwatch = Stopwatch.StartNew();
                for (var iteration = 0; iteration < 20; iteration++)
                {
                    controller.SetSearchText(iteration % 2 == 0
                        ? "Row0123"
                        : string.Empty);
                }
                stopwatch.Stop();
                var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

                Assert.That(grid.columns.ToArray(), Is.EqualTo(columns),
                    "Search interactions must not rebuild the dynamic columns.");
                Assert.That(grid.columns.Count,
                    Is.EqualTo(GameDBRepresentativeFixture.DefaultFieldsPerTable + 1));
                Assert.That(grid.itemsSource.Count,
                    Is.EqualTo(GameDBRepresentativeFixture.DefaultRowsPerTable));
                TestContext.Out.WriteLine(
                    $"GameDB representative table interactions: 20 search updates, "
                    + $"{stopwatch.Elapsed.TotalMilliseconds:F2} ms, "
                    + $"{allocated:N0} managed bytes, 0 column rebuilds.");
            }
        }

        [Test]
        public void TableController_SearchAndSortIntentsPreserveStableRowIdentity()
        {
            var snapshot = new GameDBSnapshot
            {
                Tables = new List<GameDBTableSnapshot>
                {
                    new GameDBTableSnapshot
                    {
                        Name = "Items",
                        Fields = new List<GameDBFieldSnapshot>
                        {
                            Field("Power", GameDBLibrary.FieldType.@int)
                        },
                        Rows = new List<GameDBRowSnapshot>
                        {
                            Row("Row1", ("Power", 20)),
                            Row("Row2", ("Power", 10))
                        }
                    }
                }
            };
            var search = new ToolbarSearchField();
            var navigation = new ListView();
            var grid = new MultiColumnListView
            {
                sortingMode = ColumnSortingMode.Custom
            };
            var states = new List<GameDBWorkspaceTabViewState>();
            using (var controller = new GameDBTableViewController(search,
                navigation, grid, new Label(), states.Add))
            {
                controller.Bind(new GameDBWorkspaceTabViewState(
                    "Items", "Row2"), snapshot);

                controller.SetSearchText("Row1");
                Assert.That(states.Last().SearchText, Is.EqualTo("Row1"));
                Assert.That(states.Last().SelectedRowId, Is.EqualTo("Row2"));
                Assert.That(grid.selectedIndex, Is.EqualTo(-1));
                Assert.That(grid.itemsSource.Cast<GameDBRowSnapshot>()
                    .Select(row => row.Key), Is.EqualTo(new[] { "Row1" }));

                controller.SetSearchText(string.Empty);
                Assert.That(grid.selectedIndex, Is.EqualTo(1));
                grid.sortColumnDescriptions.Add(new SortColumnDescription(
                    "Power", SortDirection.Descending));
                Assert.That(states.Last().Sorts.Select(sort => sort.FieldId),
                    Is.EqualTo(new[] { "Power" }));
                Assert.That(states.Last().Sorts.Single().Descending, Is.True);
                Assert.That(grid.itemsSource.Cast<GameDBRowSnapshot>()
                    .Select(row => row.Key), Is.EqualTo(new[] { "Row1", "Row2" }));
                Assert.That(grid.selectedIndex, Is.EqualTo(1));
            }
        }

        [Test]
        public void TableController_RestoresSanitizedSortsWithoutPublishingIntent()
        {
            var snapshot = new GameDBSnapshot
            {
                Tables = new List<GameDBTableSnapshot>
                {
                    Table("Items", "Row1", "Row2")
                }
            };
            var grid = new MultiColumnListView
            {
                sortingMode = ColumnSortingMode.Custom
            };
            var states = new List<GameDBWorkspaceTabViewState>();
            using (var controller = new GameDBTableViewController(new ToolbarSearchField(),
                new ListView(), grid, new Label(), states.Add))
            {
                var resolved = controller.Bind(new GameDBWorkspaceTabViewState(
                    "Items", sorts: new[]
                    {
                        new GameDBWorkspaceSortState("missing", false),
                        new GameDBWorkspaceSortState("Value", true),
                        new GameDBWorkspaceSortState("Value", false)
                    }), snapshot);

                Assert.That(resolved.Sorts.Select(sort => sort.FieldId),
                    Is.EqualTo(new[] { "Value" }));
                Assert.That(resolved.Sorts.Single().Descending, Is.True);
                Assert.That(grid.sortColumnDescriptions, Has.Count.EqualTo(1));
                Assert.That(states, Is.Empty);

                controller.Bind(resolved, snapshot);
                Assert.That(states, Is.Empty);
            }
        }

        [Test]
        public void TableController_UserSelectionUsesTableAndRowKeysAndDisposeDetachesCallbacks()
        {
            var snapshot = new GameDBSnapshot
            {
                Tables = new List<GameDBTableSnapshot>
                {
                    Table("Items", "Row1", "Row2"),
                    Table("Recipes", "Recipe1")
                }
            };
            var search = new ToolbarSearchField();
            var navigation = new ListView();
            var grid = new MultiColumnListView
            {
                sortingMode = ColumnSortingMode.Custom
            };
            var placeholder = new Label();
            var selections = new List<string>();
            var controller = new GameDBTableViewController(search, navigation, grid,
                placeholder, state => selections.Add(state.SelectedTableId + "/" + state.SelectedRowId));
            try
            {
                controller.Bind(new GameDBWorkspaceTabViewState("Items"), snapshot);

                grid.SetSelection(1);
                navigation.SetSelection(1);

                Assert.That(selections, Is.EqualTo(new[]
                {
                    "Items/Row2", "Recipes/"
                }));
                controller.Dispose();
                controller.SetSearchText("Row1");
                grid.sortColumnDescriptions.Add(new SortColumnDescription(
                    "Value", SortDirection.Descending));
                grid.SetSelection(0);
                navigation.SetSelection(0);
                Assert.That(selections, Has.Count.EqualTo(2));
                Assert.That(grid.columns, Is.Empty);
            }
            finally
            {
                controller.Dispose();
            }
        }

        [Test]
        public void WindowController_TableSelectionPersistsThroughWorkspaceRecovery()
        {
            var store = new RecoveryStore();
            var recovery = new GameDBWorkspaceRecoveryService(store);
            var state = GameDBRepresentativeFixture.CreateDocument(
                tableCount: 2, rowsPerTable: 20, fieldsPerTable: 4,
                documentId: "table-view").CaptureState();
            Assert.That(recovery.Save(new GameDBWorkspaceRecoverySnapshot(new[]
            {
                new GameDBWorkspaceRecoveryTab("active", state)
            }, "active")).Success, Is.True);
            var workspace = new GameDBEditorWorkspace(
                new GameDBDocumentLeaseRegistry(GameDBFilePairStore.Instance),
                recovery, new GameDBActiveWorkspaceHub());
            var root = new VisualElement();
            GameDBEditorUiAssets.Build(root);
            using (var controller = new GameDBEditorWindowController(root, workspace,
                projectSettings: CreateSettings(),
                databaseDialogs: new NoOpDialogs()))
            {
                var presentationNotifications = 0;
                workspace.StateChanged += () => presentationNotifications++;
                root.Q<ListView>("table-navigation-list").SetSelection(1);
                root.Q<MultiColumnListView>("table-row-grid").SetSelection(12);
                var selected = workspace.ActiveTab.ViewState;
                Assert.That(workspace.TrySetTabViewState(workspace.ActiveTab.TabId,
                    new GameDBWorkspaceTabViewState(selected.SelectedTableId,
                        selected.SelectedRowId, "Row0012", new[]
                        {
                            new GameDBWorkspaceSortState("Field00", true)
                        }, new[]
                        {
                            new GameDBWorkspaceColumnState("Field00", 222f, 0,
                                "Table01"),
                            new GameDBWorkspaceColumnState(
                                GameDBTableViewProjection.KeyFieldId, 180f, 1,
                                "Table01")
                        })), Is.True);

                Assert.That(workspace.ActiveTab.ViewState.SelectedTableId,
                    Is.EqualTo("Table01"));
                Assert.That(workspace.ActiveTab.ViewState.SelectedRowId,
                    Is.EqualTo("Row0012"));
                Assert.That(workspace.ActiveTab.ViewState.SearchText,
                    Is.EqualTo("Row0012"));
                Assert.That(workspace.ActiveTab.ViewState.Sorts.Single().FieldId,
                    Is.EqualTo("Field00"));
                Assert.That(workspace.ActiveTab.ViewState.Sorts.Single().Descending, Is.True);
                Assert.That(presentationNotifications, Is.Zero);
                Assert.That(workspace.PersistRecovery().Success, Is.True);
            }
            workspace.Dispose();

            var restored = new GameDBEditorWorkspace(
                new GameDBDocumentLeaseRegistry(GameDBFilePairStore.Instance),
                recovery, new GameDBActiveWorkspaceHub());
            Assert.That(restored.ActiveTab.ViewState.SelectedTableId,
                Is.EqualTo("Table01"));
            Assert.That(restored.ActiveTab.ViewState.SelectedRowId,
                Is.EqualTo("Row0012"));
            Assert.That(restored.ActiveTab.ViewState.SearchText,
                Is.EqualTo("Row0012"));
            Assert.That(restored.ActiveTab.ViewState.Sorts.Single().FieldId,
                Is.EqualTo("Field00"));
            Assert.That(restored.ActiveTab.ViewState.Sorts.Single().Descending, Is.True);
            Assert.That(restored.ActiveTab.ViewState.Columns.Select(column => new
            {
                column.TableId,
                column.FieldId,
                column.Width,
                column.Order
            }), Is.EqualTo(new[]
            {
                new
                {
                    TableId = "Table01",
                    FieldId = "Field00",
                    Width = 222f,
                    Order = 0
                },
                new
                {
                    TableId = "Table01",
                    FieldId = GameDBTableViewProjection.KeyFieldId,
                    Width = 180f,
                    Order = 1
                }
            }));
            restored.Dispose();
        }


        private static GameDBFieldSnapshot Field(string name,
            GameDBLibrary.FieldType type, bool isArray = false)
        {
            return new GameDBFieldSnapshot
            {
                Name = name,
                FieldType = type,
                IsArray = isArray
            };
        }

        private static GameDBRowSnapshot Row(string key,
            params (string Name, object Value)[] values)
        {
            return new GameDBRowSnapshot
            {
                Key = key,
                Values = values.ToDictionary(value => value.Name,
                    value => value.Value)
            };
        }

        private static GameDBTableSnapshot Table(string name, params string[] rowKeys)
        {
            return new GameDBTableSnapshot
            {
                Name = name,
                Fields = new List<GameDBFieldSnapshot>
                {
                    new GameDBFieldSnapshot
                    {
                        Name = "Value",
                        FieldType = GameDBLibrary.FieldType.@string
                    }
                },
                Rows = rowKeys.Select(key => new GameDBRowSnapshot
                {
                    Key = key,
                    Values = new Dictionary<string, object> { { "Value", key } }
                }).ToList()
            };
        }


        private static GameDBProjectSettingsService CreateSettings()
        {
            return new GameDBProjectSettingsService(new SettingsStore(),
                _ => true, _ => true);
        }

        private sealed class RecoveryStore : IGameDBWorkspaceRecoveryStore
        {
            internal string Contents { get; private set; }
            public bool Exists => Contents != null;
            public string ReadAllText() => Contents;
            public void WriteAtomically(string contents) => Contents = contents;
            public string QuarantinePrimary()
            {
                Contents = null;
                return "quarantine.json";
            }
            public string WriteQuarantine(string label, string contents)
            {
                return "quarantine-" + label + ".json";
            }
        }

        private sealed class SettingsStore : IGameDBProjectSettingsStore
        {
            private string m_contents;
            public bool Exists => m_contents != null;
            public string ReadAllText() => m_contents;
            public void WriteAtomically(string contents) => m_contents = contents;
        }

        private sealed class NoOpDialogs : IGameDBEditorDatabaseDialogs
        {
            public GameDBCreateDatabaseSelection SelectCreateDatabase() => null;
            public string SelectOpenDatabase() => null;
            public string SelectRegisterDatabase() => null;
        }
    }
}
