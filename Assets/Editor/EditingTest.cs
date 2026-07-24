using GameDBEditorLibrary;
using GameDBTestAddRow;
using System.Collections.Generic;
using UnityEditor;

public static class EditingTest {
    [MenuItem("Test/AddRow")]
    public static void AddRow() {
        if (GameDBEditor.LoadGameDB("TestNonResourceGameDBs/testAddRow.json")) {
            GameDBEditor.AddRowToTable(TestSchema.TableName, "testAdd1", new Dictionary<string, object> {
                { TestSchema.FieldTest, "test" }
            });
            GameDBEditor.SaveGameDB();
        }
    }
}