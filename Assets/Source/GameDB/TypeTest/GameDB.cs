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

namespace GameDBTypeTest
{
    public class GameDB : GameDBBase
    {
        public TypeTest1Table TypeTest1Table
        {
            get { return (TypeTest1Table)Tables[TypeTest1Schema.TableName]; }
        }

        public TypeTest2Table TypeTest2Table
        {
            get { return (TypeTest2Table)Tables[TypeTest2Schema.TableName]; }
        }


        public GameDB(string name) : base(name, "TypeTest") 
        {
            Tables.Add(TypeTest1Schema.TableName, new TypeTest1Table((string key) => { return new TypeTest1(key, this); }));
            Tables.Add(TypeTest2Schema.TableName, new TypeTest2Table((string key) => { return new TypeTest2(key, this); }));
        
#if UNITY_EDITOR
            System.Reflection.Assembly editorAssembly = System.AppDomain.CurrentDomain.GetAssemblies().First(a => a.FullName.StartsWith("GameDBEditorLibrary"));
            var gameDBEditorType = editorAssembly.GetTypes().FirstOrDefault(t => t.Namespace == "GameDBEditorLibrary" && t.FullName.EndsWith(".GameDBEditor"));
            var method = gameDBEditorType.GetMethod("AddRuntimeDB", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            method.Invoke(obj: null, parameters: new [] { this });
#endif
            this.Logger = new UnityLogger();
            WebRequestHelper.Request = new GameDBLibraryUnity.WebRequest();
            WebRequestHelper.FormFactory = new GameDBLibraryUnity.FormFactory();
        }

        
        public Exception Load(string path, bool notify = true, bool binary = false, string key = null, string salt = null) 
        {
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
                    error = Import(BinaryGameDB.Deserialize(gameDBResource.bytes, key, salt), notify);
                }
                catch (Exception e)
                {
                    error = e;
                }
            }
            else
            {
                error = Import(gameDBResource.text, notify);
            }

            return error;
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
