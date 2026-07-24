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
    public class TypeTest2 : RowBase
    {
#pragma warning disable 0414
        private GameDB m_gameDB = null;
#pragma warning restore 0414

        public TypeTest2(string key, GameDB gameDB) : base(key) {
            m_gameDB = gameDB;
        }

        public List<bool> boolVal
        {
            get { return (GetValue(TypeTest2Schema.Fieldbool) as List<object>).Select(objVal => (System.Boolean)Convert.ChangeType(objVal, typeof(System.Boolean))).ToList(); }
        }

        public List<UnityEngine.Color> colorVal
        {
            get { return (GetValue(TypeTest2Schema.Fieldcolor) as List<object>).Select(objVal => ((GameDBLibrary.Color)objVal).ToUnityColor()).ToList(); }
        }

        public List<Rarity> enumVal
        {
            get { return (GetValue(TypeTest2Schema.Fieldenum) as List<object>).Select(objVal => (Rarity)objVal).ToList(); }
        }

        public List<float> floatVal
        {
            get { return (GetValue(TypeTest2Schema.Fieldfloat) as List<object>).Select(objVal => (System.Single)Convert.ChangeType(objVal, typeof(System.Single))).ToList(); }
        }

        public List<int> intVal
        {
            get { return (GetValue(TypeTest2Schema.Fieldint) as List<object>).Select(objVal => (System.Int32)Convert.ChangeType(objVal, typeof(System.Int32))).ToList(); }
        }

        public List<System.String> objPathVal
        {
            get { return (GetValue(TypeTest2Schema.Fieldobj) as List<object>).Select(objVal => (System.String)Convert.ChangeType(objVal, typeof(System.String))).ToList(); }
        }

        public List<UnityEngine.Object> objObjectVal
        {
            get { return objPathVal.Select(path => UnityEngine.Resources.Load(path.Substring(path.IndexOf("Resources") + 10, path.LastIndexOf(".")  - (path.IndexOf("Resources") + 10)), typeof(UnityEngine.Object))).ToList();; }
        }

        public List<string> stringVal
        {
            get { return (GetValue(TypeTest2Schema.Fieldstring) as List<object>).Select(objVal => (System.String)Convert.ChangeType(objVal, typeof(System.String))).ToList(); }
        }

        public List<System.String> tableRefKeyVal
        {
            get { return (GetValue(TypeTest2Schema.FieldtableRef) as List<object>).Select(objVal => (System.String)Convert.ChangeType(objVal, typeof(System.String))).ToList(); }
        }

        public List<TypeTest1> tableRefVal
        {
            get { return tableRefKeyVal.Select(key => key != null ? m_gameDB.TypeTest1Table.GetByKey(key) : null).ToList(); }
        }

        public List<UnityEngine.Vector2> vec2Val
        {
            get { return (GetValue(TypeTest2Schema.Fieldvec2) as List<object>).Select(objVal => ((GameDBLibrary.Vector2)objVal).ToUnityVector()).ToList(); }
        }

        public List<UnityEngine.Vector3> vec3Val
        {
            get { return (GetValue(TypeTest2Schema.Fieldvec3) as List<object>).Select(objVal => ((GameDBLibrary.Vector3)objVal).ToUnityVector()).ToList(); }
        }

        public List<UnityEngine.Vector4> vec4Val
        {
            get { return (GetValue(TypeTest2Schema.Fieldvec4) as List<object>).Select(objVal => ((GameDBLibrary.Vector4)objVal).ToUnityVector()).ToList(); }
        }

    }
}
