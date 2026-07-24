using GameDBLibrary;
using System;
using System.Collections.Generic;
using System.Linq;

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
    /// Each table in the GameDB will have an associated class generated
    /// for it, that provides accessors for all the Rows or specifc rows.
    /// Table classes are generated with names that match the table name
    /// For example: {TableName}Table ie. MyDataTable where table name is "MyData"
    /// </summary>
    /// <seealso cref="GameDBLibrary.TableBase" />
    public class ExampleTable : TableBase
    {
        public ExampleTable(Func<string, RowBase> rowFactory) : base(ExampleSchema.TableName, KeyType.@string, null, rowFactory) {
        }

        /// <summary>
        /// Will get a particular row in the table by Key.
        /// </summary>
        /// <param name="key">The key to the required row.</param>
        /// <returns>An instance of the required row</returns>
        /// <exception cref="KeyNotFoundException">Throws an exception if the key is not found in the table.</exception>
        public Example GetByKey(string key) { return m_data[key] as Example; }

        /// <summary>
        /// Tries to get a particular row in the table by Key.
        /// </summary>
        /// <param name="key">The key to the required row.</param>
        /// <param name="row">The returned row if successful.</param>
        /// <returns><c>true</c>/<c>false</c> indicating if the row was found.</returns>
        public bool TryGetByKey(string key, out Example row) { row = null; if (m_data.ContainsKey(key)) { row = m_data[key] as Example; return row != null; } return false; }

        /// <summary>
        /// Returns a Dictionary keyed by table key representing all the rows in the table.
        /// </summary>
        /// <returns>The rows in the table.</returns>
        public Dictionary<string, Example> GetRows() { return m_data.ToDictionary(entry => entry.Key, entry => (Example)entry.Value); }
    }
}
