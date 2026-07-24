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
    public class EnumKeyArrays : Row
    {
#pragma warning disable 0414
        private readonly GameDB m_gameDB;
#pragma warning restore 0414

        public EnumKeyArrays(string key, GameDB gameDB) : base(key) {
            m_gameDB = gameDB;
        }

        public List<bool> BoolArrayVal
        {
            get { return GetCacheOrCreateListAccessor<List<bool>>(EnumKeyArraysSchema.FieldBoolArray, () => (GetValue(EnumKeyArraysSchema.FieldBoolArray) as List<object>).Select(objVal => new BoolAccessor(objVal).GetValue()).ToList()); }
        }

        public List<UnityEngine.Color> ColorArrayVal
        {
            get { return GetCacheOrCreateListAccessor<List<UnityEngine.Color>>(EnumKeyArraysSchema.FieldColorArray, () => (GetValue(EnumKeyArraysSchema.FieldColorArray) as List<object>).Select(objVal => new GameDBLibraryUnity.ColorAccessor(objVal).GetValue()).ToList()); }
        }

        public List<Colors> EnumArrayVal
        {
            get { return GetCacheOrCreateListAccessor<List<Colors>>(EnumKeyArraysSchema.FieldEnumArray, () => (GetValue(EnumKeyArraysSchema.FieldEnumArray) as List<object>).Select(objVal => new EnumAccessor<Colors>(objVal).GetValue()).ToList()); }
        }

        public List<float> FloatArrayVal
        {
            get { return GetCacheOrCreateListAccessor<List<float>>(EnumKeyArraysSchema.FieldFloatArray, () => (GetValue(EnumKeyArraysSchema.FieldFloatArray) as List<object>).Select(objVal => new FloatAccessor(objVal).GetValue()).ToList()); }
        }

        public List<int> IntArrayVal
        {
            get { return GetCacheOrCreateListAccessor<List<int>>(EnumKeyArraysSchema.FieldIntArray, () => (GetValue(EnumKeyArraysSchema.FieldIntArray) as List<object>).Select(objVal => new IntAccessor(objVal).GetValue()).ToList()); }
        }

        public List<string> StringArrayVal
        {
            get { return GetCacheOrCreateListAccessor<List<string>>(EnumKeyArraysSchema.FieldStringArray, () => (GetValue(EnumKeyArraysSchema.FieldStringArray) as List<object>).Select(objVal => new StringAccessor(objVal).GetValue()).ToList()); }
        }

        public List<TableReferenceAccessor<string, StringKeySingle>> TableRefArrayVal
        {
            get { return GetCacheOrCreateListAccessor<List<TableReferenceAccessor<string, StringKeySingle>>>(EnumKeyArraysSchema.FieldTableRefArray, () => (GetValue(EnumKeyArraysSchema.FieldTableRefArray) as List<object>).Select(objVal => new TableReferenceAccessor<string, StringKeySingle>(objVal, m_gameDB)).ToList()); }
        }

        public List<string> UnityObjectArrayPathVal
        {
            get { return GetCacheOrCreateListAccessor<List<string>>(EnumKeyArraysSchema.FieldUnityObjectArray + "Path", () => (GetValue(EnumKeyArraysSchema.FieldUnityObjectArray) as List<object>).Select(objVal => new GameDBLibraryUnity.UnityObjectAccessor(objVal).GetValue()).ToList()); }
        }

        public List<UnityEngine.Object> UnityObjectArrayObjectVal
        {
            get { return GetCacheOrCreateListAccessor<List<UnityEngine.Object>>(EnumKeyArraysSchema.FieldUnityObjectArray + "Object", () => (GetValue(EnumKeyArraysSchema.FieldUnityObjectArray) as List<object>).Select(objVal => new GameDBLibraryUnity.UnityObjectAccessor(objVal).GetObject()).ToList()); }
        }

        public List<UnityEngine.Vector2> Vector2ArrayVal
        {
            get { return GetCacheOrCreateListAccessor<List<UnityEngine.Vector2>>(EnumKeyArraysSchema.FieldVector2Array, () => (GetValue(EnumKeyArraysSchema.FieldVector2Array) as List<object>).Select(objVal => new GameDBLibraryUnity.Vector2Accessor(objVal).GetValue()).ToList()); }
        }

        public List<UnityEngine.Vector3> Vector3ArrayVal
        {
            get { return GetCacheOrCreateListAccessor<List<UnityEngine.Vector3>>(EnumKeyArraysSchema.FieldVector3Array, () => (GetValue(EnumKeyArraysSchema.FieldVector3Array) as List<object>).Select(objVal => new GameDBLibraryUnity.Vector3Accessor(objVal).GetValue()).ToList()); }
        }

        public List<UnityEngine.Vector4> Vector4ArrayVal
        {
            get { return GetCacheOrCreateListAccessor<List<UnityEngine.Vector4>>(EnumKeyArraysSchema.FieldVector4Array, () => (GetValue(EnumKeyArraysSchema.FieldVector4Array) as List<object>).Select(objVal => new GameDBLibraryUnity.Vector4Accessor(objVal).GetValue()).ToList()); }
        }

    }
}
