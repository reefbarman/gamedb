using GameDBLibrary;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GameDBEditorLibrary.Documents
{
    internal static class GameDBUnityObjectNormalizer
    {
        internal static void Normalize(GameDB gameDB)
        {
            if (gameDB == null)
            {
                throw new ArgumentNullException(nameof(gameDB));
            }

            foreach (var tablePair in gameDB.Tables)
            {
                var table = (TableModel)tablePair.Value;
                foreach (var rowPair in table.Data)
                {
                    var row = (RowModel)rowPair.Value;
                    foreach (var fieldPair in table.Fields)
                    {
                        NormalizeField(tablePair.Key, rowPair.Key, row, fieldPair.Key,
                            fieldPair.Value);
                    }
                }
            }
        }

        private static void NormalizeField(string tableName, string rowName, RowModel row,
            string fieldName, FieldBase field)
        {
            var value = row.Data[fieldName];
            if (field.Type == FieldType.unityObject)
            {
                if (field.IsArray)
                {
                    var source = (IList)value;
                    var normalized = new List<object>(source.Count);
                    for (var index = 0; index < source.Count; index++)
                    {
                        normalized.Add(NormalizeReference((UnityObjectReference)source[index],
                            $"{tableName}[{rowName}].{fieldName}[{index}]"));
                    }

                    row.SetValue(fieldName, normalized);
                }
                else
                {
                    row.SetValue(fieldName, NormalizeReference((UnityObjectReference)value,
                        $"{tableName}[{rowName}].{fieldName}"));
                }

                return;
            }

            if (field.Type != FieldType.dictionary)
            {
                return;
            }

            var dictionaryType = field.GetTypeArg<DictionaryType>();
            if (dictionaryType.ValueType != FieldType.unityObject)
            {
                return;
            }

            var sourceDictionary = (IDictionary)value;
            var normalizedDictionary = new Dictionary<object, object>();
            foreach (DictionaryEntry entry in sourceDictionary)
            {
                normalizedDictionary.Add(entry.Key,
                    NormalizeReference((UnityObjectReference)entry.Value,
                        $"{tableName}[{rowName}].{fieldName}[{entry.Key}]"));
            }

            row.SetValue(fieldName, normalizedDictionary);
        }

        private static UnityObjectReference NormalizeReference(UnityObjectReference reference,
            string context)
        {
            if (reference == null)
            {
                throw new InvalidOperationException($"{context} has no Unity object reference value.");
            }

            if (reference.IsEmpty)
            {
                return UnityObjectReference.Empty;
            }

            var path = AssetDatabase.GUIDToAssetPath(reference.Guid);
            if (string.IsNullOrEmpty(path))
            {
                throw new InvalidOperationException(
                    $"{context} references missing asset GUID '{reference.Guid}'.");
            }

            UnityEngine.Object asset;
            try
            {
                asset = AssetDatabase.LoadMainAssetAtPath(path);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"{context} could not load asset GUID '{reference.Guid}' at '{path}'.",
                    exception);
            }

            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"{context} references missing or unloadable asset GUID '{reference.Guid}' at '{path}'.");
            }

            if (!AssetDatabase.IsMainAsset(asset))
            {
                throw new InvalidOperationException(
                    $"{context} must reference a main project asset; GUID '{reference.Guid}' resolved to '{path}'.");
            }

            try
            {
                return new UnityObjectReference(reference.Guid, path);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException(
                    $"{context} asset '{path}' must be beneath exactly one Resources directory.",
                    exception);
            }
        }
    }
}
