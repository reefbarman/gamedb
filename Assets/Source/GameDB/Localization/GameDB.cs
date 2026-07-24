using System;
using System.Linq;
using GameDBLibrary;

/**************************************************************************************
*
*
*                     THIS IS A GENERATED FILE! DO NOT EDIT!
*
*
**************************************************************************************/

namespace GameDBLocalization
{
    public class GameDB : GameDBBase
    {
        public LocalizationTable LocalizationTable
        {
            get { return (LocalizationTable)Tables[LocalizationSchema.TableName]; }
        }


        public string LocalizationLanguage { get; private set; }

        public GameDB(string name) : base(name, "Localization") 
        {
            Tables.Add(LocalizationSchema.TableName, new LocalizationTable((string key) => { return new Localization(key, this); }));
        
#if UNITY_EDITOR
            GameDBEditorInvoker.AddRuntimeDB(this);
#endif
            this.Logger = new UnityLogger();
            WebRequestHelper.Request = new GameDBLibraryUnity.WebRequest();
            WebRequestHelper.FormFactory = new GameDBLibraryUnity.FormFactory();
        }

        
        public Exception Load(string path, string language, bool notify = true, bool binary = false, string key = null, string salt = null) 
        {
            LocalizationLanguage = language;

            var gameDBResource = UnityEngine.Resources.Load(path) as UnityEngine.TextAsset;

            if (gameDBResource == null)
            {
                return new ArgumentException(string.Format("Failed to load gameDB {0} at path: {1}", Name, path));
            }

            Exception error = null;

            if (binary)
            {
                try
                {
                    error = Import(BinaryGameDB.Deserialize(gameDBResource.bytes, key, salt), new[]{ language }, notify);
                }
                catch (Exception e)
                {
                    error = e;
                }
            }
            else
            {
                error = Import(gameDBResource.text, new[]{ language }, notify);
            }

            return error;
        }

        public Exception Import(string json, string language, bool notify = true)
        {
            LocalizationLanguage = language;

            return Import(json, new[]{ language }, notify);
        }

        public class UnityLogger : GameDBLibrary.Logger
        {
            public override void Log(string message)
            {
                UnityEngine.Debug.Log(message);
            }

            public override void LogError(string message)
            {
                UnityEngine.Debug.LogError(message);
            }

            public override void LogException(Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }
        }
    }
}
