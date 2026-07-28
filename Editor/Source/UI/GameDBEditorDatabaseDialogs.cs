using GameDBEditorLibrary;
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameDBEditorLibrary.UI
{
    internal sealed class GameDBCreateDatabaseSelection
    {
        internal string AssetPath { get; }
        internal string ScopeName { get; }
        internal bool Localization { get; }

        internal GameDBCreateDatabaseSelection(string assetPath, string scopeName,
            bool localization)
        {
            AssetPath = assetPath;
            ScopeName = scopeName;
            Localization = localization;
        }
    }

    internal interface IGameDBEditorDatabaseDialogs
    {
        GameDBCreateDatabaseSelection SelectCreateDatabase();
        string SelectOpenDatabase();
        string SelectRegisterDatabase();
    }

    internal sealed class GameDBEditorNativeDatabaseDialogs : IGameDBEditorDatabaseDialogs
    {
        public GameDBCreateDatabaseSelection SelectCreateDatabase()
        {
            var absolutePath = EditorUtility.SaveFilePanel("Create GameDB Database",
                Application.dataPath, "database", "json");
            var assetPath = ToAssetPath(absolutePath);
            return assetPath == null
                ? null
                : new GameDBCreateDatabaseSelection(assetPath,
                    Path.GetFileNameWithoutExtension(assetPath), false);
        }

        public string SelectOpenDatabase()
        {
            return ToAssetPath(EditorUtility.OpenFilePanel("Open GameDB Database",
                Application.dataPath, "json"));
        }

        public string SelectRegisterDatabase()
        {
            return ToAssetPath(EditorUtility.OpenFilePanel("Register GameDB Database",
                Application.dataPath, "json"));
        }

        private static string ToAssetPath(string absolutePath)
        {
            var relativePath = Utils.GetRelativeDataPath(absolutePath);
            return string.IsNullOrWhiteSpace(relativePath) || relativePath == "."
                ? null
                : "Assets/" + relativePath;
        }
    }
}
