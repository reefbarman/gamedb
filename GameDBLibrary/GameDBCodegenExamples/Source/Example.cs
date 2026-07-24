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

namespace GameDBCodegenExample
{
    public class Example : RowBase
    {
#pragma warning disable 0414
        private GameDB m_gameDB = null;
#pragma warning restore 0414

        public Example(string key, GameDB gameDB) : base(key) {
            m_gameDB = gameDB;
        }

        public int HealthVal
        {
            get { return (System.Int32)Convert.ChangeType(GetValue(ExampleSchema.FieldHealth), typeof(System.Int32)); }
        }

        public string NameVal
        {
            get { return (System.String)Convert.ChangeType(GetValue(ExampleSchema.FieldName), typeof(System.String)); }
        }

        public System.String TexturePathVal
        {
            get { return (System.String)Convert.ChangeType(GetValue(ExampleSchema.FieldTexture), typeof(System.String)); }
        }

    }
}
