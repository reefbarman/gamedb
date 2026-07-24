using System;
using System.Collections.Generic;
using System.Linq;
using GameDBLibrary;
using UnityEngine;
using GameDBHelpers;

/**************************************************************************************
*
*
*                     THIS IS A GENERATED FILE! DO NOT EDIT!
*
*
**************************************************************************************/

namespace GameDBTypeTest
{
    public class TypeTest1 : RowBase
    {
#pragma warning disable 0414
        private GameDB m_gameDB = null;
#pragma warning restore 0414

        public TypeTest1(string key, GameDB gameDB) : base(key) {
            m_gameDB = gameDB;
        }

        public bool boolVal
        {
            get { return (System.Boolean)Convert.ChangeType(GetValue(TypeTest1Schema.Fieldbool), typeof(System.Boolean)); }
        }

        public UnityEngine.Color colorVal
        {
            get { return ((GameDBLibrary.Color)GetValue(TypeTest1Schema.Fieldcolor)).ToUnityColor(); }
        }

        public Days enumVal
        {
            get { return (Days)GetValue(TypeTest1Schema.Fieldenum); }
        }

        public float floatVal
        {
            get { return (System.Single)Convert.ChangeType(GetValue(TypeTest1Schema.Fieldfloat), typeof(System.Single)); }
        }

        public int intVal
        {
            get { return (System.Int32)Convert.ChangeType(GetValue(TypeTest1Schema.Fieldint), typeof(System.Int32)); }
        }

        public System.String objPathVal
        {
            get { return (System.String)Convert.ChangeType(GetValue(TypeTest1Schema.Fieldobj), typeof(System.String)); }
        }

        public UnityEngine.Object objObjectVal
        {
            get { return UnityEngine.Resources.Load(objPathVal.Substring(objPathVal.IndexOf("Resources") + 10, objPathVal.LastIndexOf(".")  - (objPathVal.IndexOf("Resources") + 10)), typeof(UnityEngine.Object)); }
        }

        public string stringVal
        {
            get { return (System.String)Convert.ChangeType(GetValue(TypeTest1Schema.Fieldstring), typeof(System.String)); }
        }

        public System.String tableRefKeyVal
        {
            get { return (System.String)Convert.ChangeType(GetValue(TypeTest1Schema.FieldtableRef), typeof(System.String)); }
        }

        public TypeTest1 tableRefVal
        {
            get { return tableRefKeyVal != null ? m_gameDB.TypeTest1Table.GetByKey(tableRefKeyVal) : null; }
        }

        public UnityEngine.Vector2 vec2Val
        {
            get { return ((GameDBLibrary.Vector2)GetValue(TypeTest1Schema.Fieldvec2)).ToUnityVector(); }
        }

        public UnityEngine.Vector3 vec3Val
        {
            get { return ((GameDBLibrary.Vector3)GetValue(TypeTest1Schema.Fieldvec3)).ToUnityVector(); }
        }

        public UnityEngine.Vector4 vec4Val
        {
            get { return ((GameDBLibrary.Vector4)GetValue(TypeTest1Schema.Fieldvec4)).ToUnityVector(); }
        }

    }
}
