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
    public class StringKeySingle : Row
    {
#pragma warning disable 0414
        private readonly GameDB m_gameDB;
#pragma warning restore 0414

        public StringKeySingle(string key, GameDB gameDB) : base(key) {
            m_gameDB = gameDB;
        }

        public bool BoolVal
        {
            get { return GetCacheOrCreateAccessor<BoolAccessor>(StringKeySingleSchema.FieldBool, () => new BoolAccessor(GetValue(StringKeySingleSchema.FieldBool))).GetValue(); }
        }

        public UnityEngine.Color ColorVal
        {
            get { return GetCacheOrCreateAccessor<GameDBLibraryUnity.ColorAccessor>(StringKeySingleSchema.FieldColor, () => new GameDBLibraryUnity.ColorAccessor(GetValue(StringKeySingleSchema.FieldColor))).GetValue(); }
        }

        public Days EnumVal
        {
            get { return GetCacheOrCreateAccessor<EnumAccessor<Days>>(StringKeySingleSchema.FieldEnum, () => new EnumAccessor<Days>(GetValue(StringKeySingleSchema.FieldEnum))).GetValue(); }
        }

        public float FloatVal
        {
            get { return GetCacheOrCreateAccessor<FloatAccessor>(StringKeySingleSchema.FieldFloat, () => new FloatAccessor(GetValue(StringKeySingleSchema.FieldFloat))).GetValue(); }
        }

        public int IntVal
        {
            get { return GetCacheOrCreateAccessor<IntAccessor>(StringKeySingleSchema.FieldInt, () => new IntAccessor(GetValue(StringKeySingleSchema.FieldInt))).GetValue(); }
        }

        public string StringVal
        {
            get { return GetCacheOrCreateAccessor<StringAccessor>(StringKeySingleSchema.FieldString, () => new StringAccessor(GetValue(StringKeySingleSchema.FieldString))).GetValue(); }
        }

        public TableReferenceAccessor<Days, EnumKeyArrays> TableRefVal
        {
            get { return GetCacheOrCreateAccessor<TableReferenceAccessor<Days, EnumKeyArrays>>(StringKeySingleSchema.FieldTableRef, () => new TableReferenceAccessor<Days, EnumKeyArrays>(GetValue(StringKeySingleSchema.FieldTableRef), m_gameDB)); }
        }

        public string UnityObjectPathVal
        {
            get { return GetCacheOrCreateAccessor<GameDBLibraryUnity.UnityObjectAccessor>(StringKeySingleSchema.FieldUnityObject + "Path", () => new GameDBLibraryUnity.UnityObjectAccessor(GetValue(StringKeySingleSchema.FieldUnityObject))).GetValue(); }
        }

        public UnityEngine.Object UnityObjectObjectVal
        {
            get { return GetCacheOrCreateAccessor<GameDBLibraryUnity.UnityObjectAccessor>(StringKeySingleSchema.FieldUnityObject + "Object", () => new GameDBLibraryUnity.UnityObjectAccessor(GetValue(StringKeySingleSchema.FieldUnityObject))).GetObject(); }
        }

        public UnityEngine.Vector2 Vector2Val
        {
            get { return GetCacheOrCreateAccessor<GameDBLibraryUnity.Vector2Accessor>(StringKeySingleSchema.FieldVector2, () => new GameDBLibraryUnity.Vector2Accessor(GetValue(StringKeySingleSchema.FieldVector2))).GetValue(); }
        }

        public UnityEngine.Vector3 Vector3Val
        {
            get { return GetCacheOrCreateAccessor<GameDBLibraryUnity.Vector3Accessor>(StringKeySingleSchema.FieldVector3, () => new GameDBLibraryUnity.Vector3Accessor(GetValue(StringKeySingleSchema.FieldVector3))).GetValue(); }
        }

        public UnityEngine.Vector4 Vector4Val
        {
            get { return GetCacheOrCreateAccessor<GameDBLibraryUnity.Vector4Accessor>(StringKeySingleSchema.FieldVector4, () => new GameDBLibraryUnity.Vector4Accessor(GetValue(StringKeySingleSchema.FieldVector4))).GetValue(); }
        }

    }
}
