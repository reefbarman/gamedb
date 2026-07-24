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
    public class DictOtherKeys : Row
    {
#pragma warning disable 0414
        private readonly GameDB m_gameDB;
#pragma warning restore 0414

        public DictOtherKeys(string key, GameDB gameDB) : base(key) {
            m_gameDB = gameDB;
        }

        public Dictionary<Colors, string> DictEnumStrVal
        {
            get { return GetCacheOrCreateAccessor<DictionaryAccessor<Colors, string>>(DictOtherKeysSchema.FieldDictEnumStr, () => new DictionaryAccessor<Colors, string>(GetValue(DictOtherKeysSchema.FieldDictEnumStr), m_gameDB, typeof(EnumAccessor<Colors>), typeof(StringAccessor))).GetValue(); }
        }

    }
}
