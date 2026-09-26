// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System;
using System.Runtime.InteropServices;
using System.Reflection;

namespace Microsoft.DiaSymReader
{
    internal unsafe class MetadataAdapterBase : IMetadataImport, IMetadataEmit
    {
        public virtual int GetTokenFromSig(byte* voidPointerSig, int byteCountSig)
            => HResult.E_NOTIMPL;

        public virtual int GetSigFromToken(
            int standaloneSignature,
            [Out] byte** signature,
            [Out] int* signatureLength)
            => HResult.E_NOTIMPL;

        public virtual int GetTypeDefProps(
            int typeDef,
            [Out] char* qualifiedName,
            int qualifiedNameBufferLength,
            [Out] int* qualifiedNameLength,
            [Out] TypeAttributes* attributes,
            [Out] int* baseType)
            => HResult.E_NOTIMPL;

        public virtual int GetTypeRefProps(
            int typeRef,
            [Out] int* resolutionScope, // ModuleRef or AssemblyRef
            [Out] char* qualifiedName,
            int qualifiedNameBufferLength,
            [Out] int* qualifiedNameLength)
            => HResult.E_NOTIMPL;

        public virtual int GetNestedClassProps(int nestedClass, out int enclosingClass)
        {
            enclosingClass = 0;
            return HResult.E_NOTIMPL;
        }

        public virtual int GetMethodProps(
            int methodDef,
            [Out] int* declaringTypeDef,
            [Out] char* name,
            int nameBufferLength,
            [Out] int* nameLength,
            [Out] MethodAttributes* attributes,
            [Out] byte** signature,
            [Out] int* signatureLength,
            [Out] int* relativeVirtualAddress,
            [Out] MethodImplAttributes* implAttributes)
            => HResult.E_NOTIMPL;

        void IMetadataImport.CloseEnum(void* enumHandle) { }
        int IMetadataImport.CountEnum(void* enumHandle, out int count) { count = 0; return HResult.E_NOTIMPL; }
        int IMetadataImport.ResetEnum(void* enumHandle, int position) => HResult.E_NOTIMPL;
        int IMetadataImport.EnumTypeDefs(ref void* enumHandle, int* typeDefs, int bufferLength, int* count) => HResult.E_NOTIMPL;
        int IMetadataImport.EnumInterfaceImpls(ref void* enumHandle, int typeDef, int* interfaceImpls, int bufferLength, int* count) => HResult.E_NOTIMPL;
        int IMetadataImport.EnumTypeRefs(ref void* enumHandle, int* typeRefs, int bufferLength, int* count) => HResult.E_NOTIMPL;
        int IMetadataImport.FindTypeDefByName(string name, int enclosingClass, out int typeDef) { typeDef = 0; return HResult.E_NOTIMPL; }
        int IMetadataImport.GetScopeProps(char* name, int bufferLength, int* nameLength, Guid* mvid) => HResult.E_NOTIMPL;
        int IMetadataImport.GetModuleFromScope(out int moduleDef) { moduleDef = 0; return HResult.E_NOTIMPL; }
        int IMetadataImport.GetInterfaceImplProps(int interfaceImpl, int* typeDef, int* interfaceDefRefSpec) => HResult.E_NOTIMPL;
        int IMetadataImport.ResolveTypeRef(int typeRef, ref Guid scopeInterfaceId, out object scope, out int typeDef) { scope = null; typeDef = 0; return HResult.E_NOTIMPL; }
        int IMetadataImport.EnumMembers(ref void* enumHandle, int typeDef, int* memberDefs, int bufferLength, int* count) => HResult.E_NOTIMPL;
        int IMetadataImport.EnumMembersWithName(ref void* enumHandle, int typeDef, string name, int* memberDefs, int bufferLength, int* count) => HResult.E_NOTIMPL;
        int IMetadataImport.EnumMethods(ref void* enumHandle, int typeDef, int* methodDefs, int bufferLength, int* count) => HResult.E_NOTIMPL;
        int IMetadataImport.EnumMethodsWithName(ref void* enumHandle, int typeDef, string name, int* methodDefs, int bufferLength, int* count) => HResult.E_NOTIMPL;
        int IMetadataImport.EnumFields(ref void* enumHandle, int typeDef, int* fieldDefs, int bufferLength, int* count) => HResult.E_NOTIMPL;
        int IMetadataImport.EnumFieldsWithName(ref void* enumHandle, int typeDef, string name, int* fieldDefs, int bufferLength, int* count) => HResult.E_NOTIMPL;
        int IMetadataImport.EnumParams(ref void* enumHandle, int methodDef, int* paramDefs, int bufferLength, int* count) => HResult.E_NOTIMPL;
        int IMetadataImport.EnumMemberRefs(ref void* enumHandle, int parentToken, int* memberRefs, int bufferLength, int* count) => HResult.E_NOTIMPL;
        int IMetadataImport.EnumMethodImpls(ref void* enumHandle, int typeDef, int* implementationTokens, int* declarationTokens, int bufferLength, int* count) => HResult.E_NOTIMPL;
        int IMetadataImport.EnumPermissionSets(ref void* enumHandle, int token, uint action, int* declSecurityTokens, int bufferLength, int* count) => HResult.E_NOTIMPL;
        int IMetadataImport.FindMember(int typeDef, string name, byte* signature, int signatureLength, out int memberDef) { memberDef = 0; return HResult.E_NOTIMPL; }
        int IMetadataImport.FindMethod(int typeDef, string name, byte* signature, int signatureLength, out int methodDef) { methodDef = 0; return HResult.E_NOTIMPL; }
        int IMetadataImport.FindField(int typeDef, string name, byte* signature, int signatureLength, out int fieldDef) { fieldDef = 0; return HResult.E_NOTIMPL; }
        int IMetadataImport.FindMemberRef(int typeDef, string name, byte* signature, int signatureLength, out int memberRef) { memberRef = 0; return HResult.E_NOTIMPL; }
        int IMetadataImport.GetMemberRefProps(int memberRef, int* declaringType, char* name, int nameBufferLength, int* nameLength, byte** signature, int* signatureLength) => HResult.E_NOTIMPL;
        int IMetadataImport.EnumProperties(ref void* enumHandle, int typeDef, int* properties, int bufferLength, int* count) => HResult.E_NOTIMPL;
        uint IMetadataImport.EnumEvents(ref void* enumHandle, int typeDef, int* events, int bufferLength, int* count) => unchecked((uint)HResult.E_NOTIMPL);
        int IMetadataImport.GetEventProps(int @event, int* declaringTypeDef, char* name, int nameBufferLength, int* nameLength, int* attributes, int* eventType, int* adderMethodDef, int* removerMethodDef, int* raiserMethodDef, int* otherMethodDefs, int otherMethodDefBufferLength, int* methodMethodDefsLength) => HResult.E_NOTIMPL;
        int IMetadataImport.EnumMethodSemantics(ref void* enumHandle, int methodDef, int* eventsAndProperties, int bufferLength, int* count) => HResult.E_NOTIMPL;
        int IMetadataImport.GetMethodSemantics(int methodDef, int eventOrProperty, int* semantics) => HResult.E_NOTIMPL;
        int IMetadataImport.GetClassLayout(int typeDef, int* packSize, MetadataImportFieldOffset* fieldOffsets, int bufferLength, int* count, int* typeSize) => HResult.E_NOTIMPL;
        int IMetadataImport.GetFieldMarshal(int fieldDef, byte** nativeTypeSignature, int* nativeTypeSignatureLengvth) => HResult.E_NOTIMPL;
        int IMetadataImport.GetRVA(int methodDef, int* relativeVirtualAddress, int* implAttributes) => HResult.E_NOTIMPL;
        int IMetadataImport.GetPermissionSetProps(int declSecurity, uint* action, byte** permissionBlob, int* permissionBlobLength) => HResult.E_NOTIMPL;
        int IMetadataImport.GetModuleRefProps(int moduleRef, char* name, int nameBufferLength, int* nameLength) => HResult.E_NOTIMPL;
        int IMetadataImport.EnumModuleRefs(ref void* enumHandle, int* moduleRefs, int bufferLength, int* count) => HResult.E_NOTIMPL;
        int IMetadataImport.GetTypeSpecFromToken(int typeSpec, byte** signature, int* signatureLength) => HResult.E_NOTIMPL;
        int IMetadataImport.GetNameFromToken(int token, byte* nameUtf8) => HResult.E_NOTIMPL;
        int IMetadataImport.EnumUnresolvedMethods(ref void* enumHandle, int* methodDefs, int bufferLength, int* count) => HResult.E_NOTIMPL;
        int IMetadataImport.GetUserString(int userStringToken, char* buffer, int bufferLength, int* length) => HResult.E_NOTIMPL;
        int IMetadataImport.GetPinvokeMap(int memberDef, int* attributes, char* importName, int importNameBufferLength, int* importNameLength, int* moduleRef) => HResult.E_NOTIMPL;
        int IMetadataImport.EnumSignatures(ref void* enumHandle, int* signatureTokens, int bufferLength, int* count) => HResult.E_NOTIMPL;
        int IMetadataImport.EnumTypeSpecs(ref void* enumHandle, int* typeSpecs, int bufferLength, int* count) => HResult.E_NOTIMPL;
        int IMetadataImport.EnumUserStrings(ref void* enumHandle, int* userStrings, int bufferLength, int* count) => HResult.E_NOTIMPL;
        int IMetadataImport.GetParamForMethodIndex(int methodDef, int sequenceNumber, out int parameterToken) { parameterToken = 0; return HResult.E_NOTIMPL; }
        int IMetadataImport.EnumCustomAttributes(ref void* enumHandle, int parent, int attributeType, int* customAttributes, int bufferLength, int* count) => HResult.E_NOTIMPL;
        int IMetadataImport.GetCustomAttributeProps(int customAttribute, int* parent, int* constructor, byte** value, int* valueLength) => HResult.E_NOTIMPL;
        int IMetadataImport.FindTypeRef(int resolutionScope, string name, out int typeRef) { typeRef = 0; return HResult.E_NOTIMPL; }
        int IMetadataImport.GetMemberProps(int member, int* declaringTypeDef, char* name, int nameBufferLength, int* nameLength, int* attributes, byte** signature, int* signatureLength, int* relativeVirtualAddress, int* implAttributes, int* constantType, byte** constantValue, int* constantValueLength) => HResult.E_NOTIMPL;
        int IMetadataImport.GetFieldProps(int fieldDef, int* declaringTypeDef, char* name, int nameBufferLength, int* nameLength, int* attributes, byte** signature, int* signatureLength, int* constantType, byte** constantValue, int* constantValueLength) => HResult.E_NOTIMPL;
        int IMetadataImport.GetPropertyProps(int propertyDef, int* declaringTypeDef, char* name, int nameBufferLength, int* nameLength, int* attributes, byte** signature, int* signatureLength, int* constantType, byte** constantValue, int* constantValueLength, int* setterMethodDef, int* getterMethodDef, int* outerMethodDefs, int outerMethodDefsBufferLength, int* otherMethodDefCount) => HResult.E_NOTIMPL;
        int IMetadataImport.GetParamProps(int parameter, int* declaringMethodDef, int* sequenceNumber, char* name, int nameBufferLength, int* nameLength, int* attributes, int* constantType, byte** constantValue, int* constantValueLength) => HResult.E_NOTIMPL;
        int IMetadataImport.GetCustomAttributeByName(int parent, string name, byte** value, int* valueLength) => HResult.E_NOTIMPL;
        bool IMetadataImport.IsValidToken(int token) => false;
        int IMetadataImport.GetNativeCallConvFromSig(byte* signature, int signatureLength, int* callingConvention) => HResult.E_NOTIMPL;
        int IMetadataImport.IsGlobal(int token, bool value) => HResult.E_NOTIMPL;

        void IMetadataEmit.__SetModuleProps() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__Save() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__SaveToStream() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__GetSaveSize() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__DefineTypeDef() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__DefineNestedType() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__SetHandler() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__DefineMethod() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__DefineMethodImpl() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__DefineTypeRefByName() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__DefineImportType() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__DefineMemberRef() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__DefineImportMember() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__DefineEvent() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__SetClassLayout() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__DeleteClassLayout() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__SetFieldMarshal() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__DeleteFieldMarshal() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__DefinePermissionSet() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__SetRVA() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__DefineModuleRef() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__SetParent() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__GetTokenFromTypeSpec() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__SaveToMemory() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__DefineUserString() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__DeleteToken() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__SetMethodProps() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__SetTypeDefProps() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__SetEventProps() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__SetPermissionSetProps() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__DefinePinvokeMap() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__SetPinvokeMap() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__DeletePinvokeMap() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__DefineCustomAttribute() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__SetCustomAttributeValue() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__DefineField() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__DefineProperty() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__DefineParam() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__SetFieldProps() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__SetPropertyProps() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__SetParamProps() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__DefineSecurityAttributeSet() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__ApplyEditAndContinue() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__TranslateSigWithScope() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__SetMethodImplFlags() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__SetFieldRVA() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__Merge() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
        void IMetadataEmit.__MergeEnd() => throw new NotSupportedException("Metadata emission is not supported by this adapter.");
    }
}
