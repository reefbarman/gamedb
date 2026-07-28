using GameDBEditorLibrary.Workspace;
using GameDBLibrary;
using System;
using System.Collections.Generic;

namespace GameDBEditorLibrary
{
    //TODO: Determine and Fix what happens when a key, enum or prefab is deleted (do we fail to load it or do we set the value back to the default?, Do we need a warning?)
    //TODO: allow access to data dictionary
    //TODO: add reload editor button
    //TODO: trim/cleanup all names before code export
    //TODO: refactoring tool for changing enums
    /// <summary>
    /// GameDBEditor provides static methods for working with the GameDB Editor within the Unity Editor.
    /// Allowing things such as subscribing to save events and programatically working with the editor.
    /// </summary>
    public class GameDBEditor
    {
        internal static Action<string> OnGameDBSaved = null;

        /// <summary>
        /// Programatically load a GameDB in the editor. Useful if intergrating the GameDB Editor with other tools/editors.
        /// </summary>
        /// <param name="gameDBPath">The path to the GameDB to load.</param>
        /// <returns><c>true</c>/<c>false</c> indicating success of the load operation.</returns>
        public
                static bool LoadGameDB(string gameDBPath)
        {
            return GameDBEditorDomainServices.FacadeRouter.LoadGameDB(gameDBPath);
        }

        /// <summary>
        /// Programatically save the currently loaded GameDB in the editor.
        /// </summary>
        /// <returns><c>true</c>/<c>false</c> indicating success of the save operation.</returns>
        public
                static bool SaveGameDB()
        {
            return GameDBEditorDomainServices.FacadeRouter.SaveGameDB();
        }

        /// <summary>
        /// Allows the adding of a row to a GameDB table via an editor script.
        /// The GameDB needs to already be loaded via <see cref="LoadGameDB"/> method.
        /// After adding rows the GameDB needs to be saved via <see cref="SaveGameDB"/>
        /// </summary>
        /// <param name="table">The table to add the row to.</param>
        /// <param name="key">The key of the row to add.</param>
        /// <param name="data">The dictionary representing the fields and data to add.</param>
        /// <exception cref="System.InvalidCastException">Thrown when data of an invalid type is added for a field.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown when a field in the data doesn't exist in the table.</exception>
        /// <example>
        /// This example shows the code necessary to add a row to a table.
        /// <code>
        /// if (GameDBEditor.LoadGameDB("Test/testGameDB.json"))
        /// {
        ///     GameDBEditor.AddRowToTable(TestSchema.TableName, "testKey1", new Dictionary&lt;string, object> {
        ///         { TestSchema.FieldTest1, "test" },
        ///         { TestSchema.FieldTest2, true }
        ///     });
        ///
        ///     GameDBEditor.AddRowToTable(TestSchema.TableName, "testKey2", new Dictionary&lt;string, object> {
        ///         { TestSchema.FieldTest1, "test2" },
        ///         { TestSchema.FieldTest2, false }
        ///     });
        ///     GameDBEditor.SaveGameDB();
        /// }
        /// </code>
        /// </example>
        public
                static void AddRowToTable(string table, string key, Dictionary<string, object> data)
        {
            GameDBEditorDomainServices.FacadeRouter.AddRowToTable(table, key, data);
        }

        /// <summary>
        /// Allows a callback to be registered when a GameDB has been saved in the editor.
        /// Useful for updating other editors or systems when data has changed.
        /// The Scope Name of the saved GameDB is returned via the callback.
        /// </summary>
        /// <param name="onSaved">The on saved.</param>
        public static void RegisterSavedGameDBCallback(Action<string> onSaved)
        {
            OnGameDBSaved += onSaved;
        }

        public static void AddRuntimeDB(GameDBBase runtimeDB)
        {
            GameDBEditorDomainServices.RuntimeRegistry.Register(runtimeDB);
        }

    }
}
