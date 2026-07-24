using System;
using System.Collections.Generic;
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
    public class TableRefEnumKeysDicts : Row
    {
#pragma warning disable 0414
        private readonly GameDB m_gameDB;
#pragma warning restore 0414

        public TableRefEnumKeysDicts(string key, GameDB gameDB) : base(key) {
            m_gameDB = gameDB;
        }

        public Dictionary<string, bool> DictStrBoolVal
        {
            get { return GetCacheOrCreateAccessor<DictionaryAccessor<string, bool>>(TableRefEnumKeysDictsSchema.FieldDictStrBool, () => new DictionaryAccessor<string, bool>(GetValue(TableRefEnumKeysDictsSchema.FieldDictStrBool), m_gameDB, typeof(StringAccessor), typeof(BoolAccessor))).GetValue(); }
        }

        public Dictionary<string, UnityEngine.Color> DictStrColorVal
        {
            get { return GetCacheOrCreateAccessor<DictionaryAccessor<string, UnityEngine.Color>>(TableRefEnumKeysDictsSchema.FieldDictStrColor, () => new DictionaryAccessor<string, UnityEngine.Color>(GetValue(TableRefEnumKeysDictsSchema.FieldDictStrColor), m_gameDB, typeof(StringAccessor), typeof(GameDBLibraryUnity.ColorAccessor))).GetValue(); }
        }

        public Dictionary<string, Rarity> DictStrEnumVal
        {
            get { return GetCacheOrCreateAccessor<DictionaryAccessor<string, Rarity>>(TableRefEnumKeysDictsSchema.FieldDictStrEnum, () => new DictionaryAccessor<string, Rarity>(GetValue(TableRefEnumKeysDictsSchema.FieldDictStrEnum), m_gameDB, typeof(StringAccessor), typeof(EnumAccessor<Rarity>))).GetValue(); }
        }

        public Dictionary<string, float> DictStrFltVal
        {
            get { return GetCacheOrCreateAccessor<DictionaryAccessor<string, float>>(TableRefEnumKeysDictsSchema.FieldDictStrFlt, () => new DictionaryAccessor<string, float>(GetValue(TableRefEnumKeysDictsSchema.FieldDictStrFlt), m_gameDB, typeof(StringAccessor), typeof(FloatAccessor))).GetValue(); }
        }

        public Dictionary<string, int> DictStrIntVal
        {
            get { return GetCacheOrCreateAccessor<DictionaryAccessor<string, int>>(TableRefEnumKeysDictsSchema.FieldDictStrInt, () => new DictionaryAccessor<string, int>(GetValue(TableRefEnumKeysDictsSchema.FieldDictStrInt), m_gameDB, typeof(StringAccessor), typeof(IntAccessor))).GetValue(); }
        }

        public Dictionary<string, string> DictStrStrVal
        {
            get { return GetCacheOrCreateAccessor<DictionaryAccessor<string, string>>(TableRefEnumKeysDictsSchema.FieldDictStrStr, () => new DictionaryAccessor<string, string>(GetValue(TableRefEnumKeysDictsSchema.FieldDictStrStr), m_gameDB, typeof(StringAccessor), typeof(StringAccessor))).GetValue(); }
        }

        public Dictionary<string, TableReferenceAccessor<Days, EnumKeyArrays>> DictStrTableRefVal
        {
            get { return GetCacheOrCreateAccessor<DictionaryAccessor<string, TableReferenceAccessor<Days, EnumKeyArrays>>>(TableRefEnumKeysDictsSchema.FieldDictStrTableRef, () => new DictionaryAccessor<string, TableReferenceAccessor<Days, EnumKeyArrays>>(GetValue(TableRefEnumKeysDictsSchema.FieldDictStrTableRef), m_gameDB, typeof(StringAccessor), typeof(TableReferenceAccessor<Days, EnumKeyArrays>))).GetValue(); }
        }

        public Dictionary<string, GameDBLibraryUnity.UnityObjectAccessor> DictStrUObjVal
        {
            get { return GetCacheOrCreateAccessor<DictionaryAccessor<string, GameDBLibraryUnity.UnityObjectAccessor>>(TableRefEnumKeysDictsSchema.FieldDictStrUObj, () => new DictionaryAccessor<string, GameDBLibraryUnity.UnityObjectAccessor>(GetValue(TableRefEnumKeysDictsSchema.FieldDictStrUObj), m_gameDB, typeof(StringAccessor), typeof(GameDBLibraryUnity.UnityObjectAccessor))).GetValue(); }
        }

        public Dictionary<string, UnityEngine.Vector2> DictStrVec2Val
        {
            get { return GetCacheOrCreateAccessor<DictionaryAccessor<string, UnityEngine.Vector2>>(TableRefEnumKeysDictsSchema.FieldDictStrVec2, () => new DictionaryAccessor<string, UnityEngine.Vector2>(GetValue(TableRefEnumKeysDictsSchema.FieldDictStrVec2), m_gameDB, typeof(StringAccessor), typeof(GameDBLibraryUnity.Vector2Accessor))).GetValue(); }
        }

        public Dictionary<string, UnityEngine.Vector3> DictStrVec3Val
        {
            get { return GetCacheOrCreateAccessor<DictionaryAccessor<string, UnityEngine.Vector3>>(TableRefEnumKeysDictsSchema.FieldDictStrVec3, () => new DictionaryAccessor<string, UnityEngine.Vector3>(GetValue(TableRefEnumKeysDictsSchema.FieldDictStrVec3), m_gameDB, typeof(StringAccessor), typeof(GameDBLibraryUnity.Vector3Accessor))).GetValue(); }
        }

        public Dictionary<string, UnityEngine.Vector4> DictStrVec4Val
        {
            get { return GetCacheOrCreateAccessor<DictionaryAccessor<string, UnityEngine.Vector4>>(TableRefEnumKeysDictsSchema.FieldDictStrVec4, () => new DictionaryAccessor<string, UnityEngine.Vector4>(GetValue(TableRefEnumKeysDictsSchema.FieldDictStrVec4), m_gameDB, typeof(StringAccessor), typeof(GameDBLibraryUnity.Vector4Accessor))).GetValue(); }
        }

    }
}
