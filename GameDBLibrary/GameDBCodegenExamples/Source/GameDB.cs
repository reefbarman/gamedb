using GameDBLibrary;
using System;

/**************************************************************************************
*
*
*                     THIS IS A GENERATED FILE! DO NOT EDIT!
*
*
**************************************************************************************/

/// <summary>
/// The GameDB plugin provides generated code for each GameDB and its associated tables
/// to allow easy typed access to all your saved GameDB data.
/// 
/// The generated classes will be contained within a namespace based 
/// on the Scope Name entered for the relevant GameDB. 
/// For example: GameDB{ScopeName} ie. GameDBMyTestScope if the Scope Name was "MyTestScope".
/// </summary>
namespace GameDBCodegenExample
{
    /// <summary>
    /// Each GameDB will output a generated GameDB class that will 
    /// allow access to the Tables within the GameDB. Because this is
    /// scoped to a custom namespace there are no conflicts between
    /// multiple GameDBs in the same project.
    /// </summary>
    /// <seealso cref="GameDBLibrary.GameDBBase" />
    public class GameDB : GameDBBase
    {
        /// <summary>
        /// A getter will be generated for each table within the GameDB.
        /// This will give access to the loaded instance of a table,
        /// allowing each row and its fields to be easily access.
        /// The getter will be generated with names matching the table name.
        /// For Example: {TableName}Table ie. MyTestTable for a table name "MyTest"
        /// </summary>
        /// <value>
        /// The loaded instance of the associated table.
        /// </value>
        /// <seealso cref="GameDBCodegenExample.ExampleTable" />
        public ExampleTable ExampleTable
        {
            get { return (ExampleTable)Tables[ExampleSchema.TableName]; }
        }

        /// <summary>
        /// Initializes a new instance of the GameDB class.
        /// Multiple instances of the GameDB can be loaded at run-time. 
        /// The GameDB can then be identified in the GameDB Editor window by a unique name
        /// associated when the instance is created.
        /// This allows run-time balancing/modification of the DB to be scoped to a particular
        /// instance of the GameDB if for example, you don't want one characters data to change 
        /// when balancing another.
        /// </summary>
        /// <param name="name">The run-time name of the GameDB shown in the GameDB editor.</param>
        public GameDB(string name) : base(name, "Example") {
        }

        /// <summary>
        /// (Free Version only) (Unity only)<br/> 
        /// This method will load a GameDB from the specified path.
        /// This should only be used on GameDBs stored in Unity Resources folders as it uses <see cref="UnityEngine.Resources.Load"/>
        /// If loading GameDBs from non Resources folders <see cref="GameDBBase.Import(string,bool)"/> (or other overloads) should
        /// be used instead.
        /// </summary>
        /// <param name="path">The path is relative to Application.dataPath (ie. Assets/) and does not include a file extension. For example to load Assets/Resources/GameDBs/gameDB.json use the path "GameDBs/gameDB" </param>
        /// <param name="notify">if set to <c>true</c> the <see cref="GameDBBase.OnDBLoaded"/> callback will be triggered for the GameDB (defaults to <c>true</c>).</param>
        /// <returns></returns>
        public Exception Load(string path, bool notify = true)
        {
            return null;
        }

        /// <summary>
        /// <strong>(Pro Version only) (Unity only)</strong><br/> 
        /// This method will load a GameDB from the specified path.
        /// The GameDB supports being compressed (via LZF) and encrypted (via AES) using the <see cref="GameDBLibrary.BinaryGameDB"/> utilities.
        /// This method will load both a binary or text based GameDB.
        /// This should only be used on GameDBs stored in Unity Resources folders as it uses <see cref="UnityEngine.Resources.Load"/>
        /// If loading GameDBs from non Resources folders <see cref="GameDBBase.Import(string,bool)"/> (or other overloads) should
        /// be used instead.
        /// </summary>
        /// <param name="path">The path is relative to Application.dataPath (ie. Assets/) and does not include a file extension. For example to load Assets/Resources/GameDBs/gameDB.json use the path "GameDBs/gameDB" </param>
        /// <param name="notify">if set to <c>true</c> the <see cref="GameDBBase.OnDBLoaded"/> callback will be triggered for the GameDB (defaults to <c>true</c>).</param>
        /// <param name="binary">if set to <c>true</c> the GameDB will be loaded as a binary source (defaults to <c>false</c>) (<c>key</c> and <c>salt</c> required if loading binary)</param>
        /// <param name="key">the key used to encrypt the database via the <see cref="GameDBLibrary.BinaryGameDB"/> utilities. Required if using <c>binary = true</c></param>
        /// <param name="salt">the salt used to encrypt the database via the <see cref="GameDBLibrary.BinaryGameDB"/> utilities. Required if using <c>binary = true</c></param>
        /// <returns>An exception is returned if the GameDB fails to load</returns>
        public Exception Load(string path, bool notify = true, bool binary = false, string key = null, string salt = null) {
            return null;
        }

        /// <summary>
        /// <strong>(Pro Version only) (Unity only) (Localization DB only)</strong><br/> 
        /// This method will load a GameDB from the specified path.
        /// This method is provided when a GameDB is marked as a LocalizationDB. It allows only a certain field (representing a language) 
        /// across all tables to be loaded, saving on memory and simplyfying access to the required language.
        /// The GameDB supports being compressed (via LZF) and encrypted (via AES) using the <see cref="GameDBLibrary.BinaryGameDB"/> utilities.
        /// This method will load both a binary or text based GameDB.
        /// This should only be used on GameDBs stored in Unity Resources folders as it uses <see cref="UnityEngine.Resources.Load"/>
        /// If loading GameDBs from non Resources folders <see cref="GameDBBase.Import(string,bool)"/> (or other overloads) should
        /// be used instead.
        /// </summary>
        /// <param name="path">The path is relative to Application.dataPath (ie. Assets/) and does not include a file extension. For example to load Assets/Resources/GameDBs/gameDB.json use the path "GameDBs/gameDB" </param>
        /// <param name="language">The name of the language field to load.</param>
        /// <param name="notify">if set to <c>true</c> the <see cref="GameDBBase.OnDBLoaded"/> callback will be triggered for the GameDB (defaults to <c>true</c>).</param>
        /// <param name="binary">if set to <c>true</c> the GameDB will be loaded as a binary source (defaults to <c>false</c>) (<c>key</c> and <c>salt</c> required if loading binary)</param>
        /// <param name="key">the key used to encrypt the database via the <see cref="GameDBLibrary.BinaryGameDB"/> utilities. Required if using <c>binary = true</c></param>
        /// <param name="salt">the salt used to encrypt the database via the <see cref="GameDBLibrary.BinaryGameDB"/> utilities. Required if using <c>binary = true</c></param>
        /// <returns>An exception is returned if the GameDB fails to load</returns>
        public Exception Load(string path, string language, bool notify = true, bool binary = false, string key = null, string salt = null) {
            return null;
        }

        /// <summary>
        /// <strong>(Pro Version only) (Unity only) (Localization DB only)</strong><br/> 
        /// Imports JSON representing the GameDB.
        /// This method is provided when a GameDB is marked as a LocalizationDB. It allows only a certain field (representing a language) 
        /// across all tables to be imported, saving on memory and simplyfying access to the required language.
        /// </summary>
        /// <param name="json">A string representing the JSON format of the GameDB to import</param>
        /// <param name="language">The name of the language field to load.</param>
        /// <param name="notify">if set to <c>true</c> the <see cref="GameDBBase.OnDBLoaded"/> callback will be triggered for the GameDB (defaults to <c>true</c>).</param>
        /// <returns>An exception is returned if the GameDB fails to imprt</returns>
        public Exception Import(string json, string language, bool notify = true)
        {
            return null;
        }


        /// <summary>
        /// <strong>(Unity only)</strong><br/> 
        /// By default logging by the GameDB is logged via a <see cref="GameDBLibrary.Logger"/>. 
        /// This allows the used logger to be replace if logging needs to be redirected.
        /// This class redirects logging to the Unity console via Debug.Log etc
        /// To replace the Logger set <see cref="GameDBBase.Logger"/>
        /// </summary>
        /// <seealso cref="GameDBLibrary.Logger" />
        public class UnityLogger : GameDBLibrary.Logger
        {
        }
    }
}
