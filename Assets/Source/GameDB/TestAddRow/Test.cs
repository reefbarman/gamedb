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

namespace GameDBTestAddRow
{
    public class Test : RowBase
    {
#pragma warning disable 0414
        private GameDB m_gameDB = null;
#pragma warning restore 0414

        public Test(string key, GameDB gameDB) : base(key) {
            m_gameDB = gameDB;
        }

        public string TestVal
        {
            get { return (System.String)Convert.ChangeType(GetValue(TestSchema.FieldTest), typeof(System.String)); }
        }

    }
}
