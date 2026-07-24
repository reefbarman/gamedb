/**************************************************************************************
*
*
*                     THIS IS A GENERATED FILE! DO NOT EDIT!
*
*
**************************************************************************************/


namespace GameDBCodegenExample
{
    /// <summary>
    /// For each table in the GameDB a schema class will be generated.
    /// This class provides static values that can be used for type safety
    /// to refer to tables, fields and keys in the GameDB.
    /// Table schema classes are generated with names that match the table name
    /// For example: {TableName}Schema ie. MyDataSchema where table name is "MyData"
    /// </summary>
    public static class ExampleSchema
    {

        /// <summary>
        /// The static name of the table. Useful for working with the GameDB
        /// using the <see cref="GameDBEditorLibrary.GameDBEditor"/> methods.
        /// </summary>
        public static string TableName = "Loot";

        /* Field Names */
        /// <summary>
        /// The static names of the fields in a table. Useful for working with the GameDB
        /// using the <see cref="GameDBEditorLibrary.GameDBEditor"/> methods.
        /// Each Field has a static value generated for it based on its name.
        /// For example: Field{FieldName} ie. FieldMyValue where field name is "MyValue"
        /// </summary>
        public static string FieldDay = "Day";
        public static string FieldHealth = "Health";
        public static string FieldName = "Name";
        public static string FieldTexture = "Texture";


        /* Key Names */
        /// <summary>
        /// The static names of the keys in a table. 
        /// Can be used to get rows with statically typed values isntead of 
        /// using strings throughout a codebase.
        /// Useful for working with the GameDB
        /// using the <see cref="GameDBEditorLibrary.GameDBEditor"/> methods.
        /// Each Key has a static value generated for it based on its name.
        /// For example: Key{KeyName} ie. KeyMyRow where field name is "MyRow"
        /// </summary>
        public static string KeyGold = "Gold";

    }
}
