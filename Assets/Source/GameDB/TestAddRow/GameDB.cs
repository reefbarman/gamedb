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

namespace GameDBTestAddRow
{
    public class GameDB : GameDBBase
    {
        public TestTable TestTable
        {
            get { return (TestTable)Tables[TestSchema.TableName]; }
        }


        public GameDB(string name) : base(name, "TestAddRow") {

			Tables.Add(TestSchema.TableName, new TestTable((string key) => { return new Test(key, this); }));
        
#if UNITY_EDITOR
            System.Reflection.Assembly editorAssembly = System.AppDomain.CurrentDomain.GetAssemblies().First(a => a.FullName.StartsWith("GameDBEditorLibrary"));
            var gameDBEditorType = editorAssembly.GetTypes().FirstOrDefault(t => t.Namespace == "GameDBEditorLibrary" && t.FullName.EndsWith(".GameDBEditor"));
            var method = gameDBEditorType.GetMethod("AddRuntimeDB", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            method.Invoke(obj: null, parameters: new [] { this });
#endif
            this.Logger = new UnityLogger();
        }

        
        public Exception Load(string path, bool notify = true, bool binary = false, string key = null, string salt = null) {
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
