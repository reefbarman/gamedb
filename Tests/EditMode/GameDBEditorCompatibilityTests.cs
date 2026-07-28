using GameDBEditorLibrary;
using GameDBEditorLibrary.Workspace;
using GameDBLibrary;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace GameDBLibrary.Tests
{
    public class GameDBEditorCompatibilityTests
    {
        private static readonly FieldInfo s_editorDatabaseInstanceField = typeof(Singleton<GameDB>).GetField(
            "s_instance", BindingFlags.NonPublic | BindingFlags.Static);

        private bool m_editorDatabaseReplaced;
        private GameDB m_previousEditorDatabase;

        [TearDown]
        public void TearDown()
        {
            if (!m_editorDatabaseReplaced)
            {
                return;
            }

            SetEditorDatabase(m_previousEditorDatabase);
            m_editorDatabaseReplaced = false;
            m_previousEditorDatabase = null;
        }

        [Test]
        public void EditorWindow_PreservesGlobalPublicTypeIdentity()
        {
            var windowType = typeof(global::GameDBEditorWindow);

            Assert.That(windowType.FullName, Is.EqualTo("GameDBEditorWindow"));
            Assert.That(windowType.Namespace, Is.Null);
            Assert.That(windowType.IsPublic, Is.True);
            Assert.That(windowType.IsSubclassOf(typeof(EditorWindow)), Is.True);
        }

        [Test]
        public void LegacyImguiLifecycleAndComponentTypesAreRemoved()
        {
            Assert.That(typeof(GameDBEditor).GetMethod("Init",
                BindingFlags.Public | BindingFlags.Static), Is.Null);
            Assert.That(typeof(GameDBEditor).GetMethod("OnGUI",
                BindingFlags.Public | BindingFlags.Static), Is.Null);
            Assert.That(typeof(GameDBEditor).GetMethod("Update",
                BindingFlags.Public | BindingFlags.Static), Is.Null);
            var assembly = typeof(GameDBEditor).Assembly;
            Assert.That(assembly.GetType("GameDBEditorLibrary.EditorComponent", false), Is.Null);
            Assert.That(assembly.GetType("GameDBEditorLibrary.EventSystem", false), Is.Null);
            Assert.That(assembly.GetType("GameDBEditorLibrary.Settings", false), Is.Null);
            Assert.That(typeof(GameDB).GetMethod("LoadRuntimeDB",
                BindingFlags.Public | BindingFlags.Instance), Is.Null);
            Assert.That(typeof(GameDB).GetMethod("ReloadRuntimeDB",
                BindingFlags.Public | BindingFlags.Instance), Is.Null);
        }

        [Test]
        public void AddRuntimeDB_PreservesGeneratedReflectionBridgeSignature()
        {
            Assert.That(typeof(GameDBEditor).Assembly.GetName().Name,
                Does.StartWith("GameDBEditorLibrary"));
            Assert.That(typeof(GameDBEditor).Namespace, Is.EqualTo("GameDBEditorLibrary"));

            MethodInfo method = null;
            Assert.DoesNotThrow(() => method = typeof(GameDBEditor).GetMethod(
                nameof(GameDBEditor.AddRuntimeDB), BindingFlags.Public | BindingFlags.Static));
            Assert.That(method, Is.Not.Null);
            Assert.That(method.ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(method.GetParameters(), Has.Length.EqualTo(1));
            Assert.That(method.GetParameters()[0].ParameterType, Is.EqualTo(typeof(GameDBBase)));

            var invoker = typeof(GameDBEditorInvoker).GetMethod(
                nameof(GameDBEditorInvoker.AddRuntimeDB), BindingFlags.Public | BindingFlags.Static);
            Assert.That(invoker, Is.Not.Null);
            Assert.That(invoker.ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(invoker.GetParameters(), Has.Length.EqualTo(1));
            Assert.That(invoker.GetParameters()[0].ParameterType, Is.EqualTo(typeof(GameDBBase)));
        }

        [Test]
        public void AddRowToTable_UnknownTableThrowsArgumentOutOfRangeException()
        {
            PrepareEditorDatabase();
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                GameDBEditor.AddRowToTable("Missing", "Sword", new Dictionary<string, object>()));

            Assert.That(exception.ParamName, Is.EqualTo("table"));
            Assert.That(exception.ActualValue, Is.EqualTo("Missing"));
        }

        [Test]
        public void AddRowToTable_DuplicateKeyThrowsArgumentOutOfRangeException()
        {
            PrepareEditorDatabase();
            CreateItemsTable();
            GameDBEditor.AddRowToTable("Items", "Sword",
                new Dictionary<string, object> { { "Power", 12 } });

            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                GameDBEditor.AddRowToTable("Items", "Sword",
                    new Dictionary<string, object> { { "Power", 15 } }));

            Assert.That(exception.ParamName, Is.EqualTo("key"));
            Assert.That(exception.ActualValue, Is.EqualTo("Sword"));
        }

        [Test]
        public void AddRowToTable_UnknownFieldThrowsArgumentOutOfRangeException()
        {
            PrepareEditorDatabase();
            CreateItemsTable();

            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                GameDBEditor.AddRowToTable("Items", "Sword",
                    new Dictionary<string, object> { { "Missing", 12 } }));

            Assert.That(exception.ParamName, Is.EqualTo("Field"));
            Assert.That(exception.ActualValue, Is.EqualTo("Missing"));
        }

        [Test]
        public void AddRowToTable_InvalidValueThrowsInvalidCastException()
        {
            PrepareEditorDatabase();
            CreateItemsTable();

            Assert.Throws<InvalidCastException>(() =>
                GameDBEditor.AddRowToTable("Items", "Sword",
                    new Dictionary<string, object> { { "Power", "high" } }));
        }

        private static void CreateItemsTable()
        {
            Assert.That(GameDB.Instance.AddTable("Items", KeyType.@string), Is.True);
            var items = (TableModel)GameDB.Instance.Tables["Items"];
            Assert.That(items.AddField("Power", FieldType.@int, false), Is.True);
        }

        private void PrepareEditorDatabase()
        {
            Assert.That(GameDBEditorDomainServices.ActiveWorkspaceHub.TryGetActive(out _),
                Is.False, "Headless compatibility tests require no focused workspace route.");
            Assert.That(s_editorDatabaseInstanceField, Is.Not.Null);
            m_previousEditorDatabase = (GameDB)s_editorDatabaseInstanceField.GetValue(null);

            var isolatedDatabase = new GameDB();
            isolatedDatabase.CreateInMemory("GameDBEditorCompatibilityTests/database.json");
            isolatedDatabase.ScopeName = "CompatibilityTests";
            SetEditorDatabase(isolatedDatabase);
            m_editorDatabaseReplaced = true;
        }

        private static void SetEditorDatabase(GameDB database)
        {
            s_editorDatabaseInstanceField.SetValue(null, database);
        }
    }
}
