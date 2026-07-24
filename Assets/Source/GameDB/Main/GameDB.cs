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

namespace GameDBMain
{
    public class GameDB : GameDBBase
    {
        public DictOtherKeysTable DictOtherKeysTable
        {
            get { return (DictOtherKeysTable)Tables[DictOtherKeysSchema.TableName]; }
        }

        public EnumKeyArraysTable EnumKeyArraysTable
        {
            get { return (EnumKeyArraysTable)Tables[EnumKeyArraysSchema.TableName]; }
        }

        public StringKeySingleTable StringKeySingleTable
        {
            get { return (StringKeySingleTable)Tables[StringKeySingleSchema.TableName]; }
        }

        public TableRefEnumKeysDictsTable TableRefEnumKeysDictsTable
        {
            get { return (TableRefEnumKeysDictsTable)Tables[TableRefEnumKeysDictsSchema.TableName]; }
        }


        public GameDB(string name) : base(name, "Main") 
        {
            Tables.Add(DictOtherKeysSchema.TableName, new DictOtherKeysTable((string key) => { return new DictOtherKeys(key, this); }));
            Tables.Add(EnumKeyArraysSchema.TableName, new EnumKeyArraysTable((string key) => { return new EnumKeyArrays(key, this); }));
            Tables.Add(StringKeySingleSchema.TableName, new StringKeySingleTable((string key) => { return new StringKeySingle(key, this); }));
            Tables.Add(TableRefEnumKeysDictsSchema.TableName, new TableRefEnumKeysDictsTable((string key) => { return new TableRefEnumKeysDicts(key, this); }));
        
#if UNITY_EDITOR
            GameDBEditorInvoker.AddRuntimeDB(this);
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
