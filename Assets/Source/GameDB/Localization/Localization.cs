using GameDBLibrary;
using System;


/**************************************************************************************
*
*
*                     THIS IS A GENERATED FILE! DO NOT EDIT!
*
*
**************************************************************************************/

namespace GameDBLocalization
{
    public class Localization : RowBase
    {
#pragma warning disable 0414
        private GameDB m_gameDB = null;
#pragma warning restore 0414

        public Localization(string key, GameDB gameDB) : base(key) {
            m_gameDB = gameDB;
        }

        public string TranslatedVal
        {
            get { return (System.String)Convert.ChangeType(GetValue(m_gameDB.LocalizationLanguage), typeof(System.String)); }
        }

        public string LanguageVal
        {
            get { return m_gameDB.LocalizationLanguage; }
        }

    }
}
