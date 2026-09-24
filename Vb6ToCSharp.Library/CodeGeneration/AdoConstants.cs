using System.Collections.Generic;
using System.Text;
using static Vb6ToCSharp.Runtime.VbConstants;
using static Vb6ToCSharp.Runtime.VbStrings;
using static Vb6ToCSharp.CodeConversion.ConversionUtility;

namespace Vb6ToCSharp.CodeGeneration;

/// <summary>
/// The ADO constants file of a converted project that references Microsoft ActiveX Data Objects.
/// <para>
/// VB6 publishes the enums of a referenced type library as global constants and treats them as Long, so the same
/// constant serves an enum parameter (<c>cmd.CommandType = adCmdText</c>) and an Integer one
/// (<c>rs.Open ..., adCmdText</c>). C# has no implicit conversion either way, so the converted project declares the
/// constants itself, as a value type that converts to int and to every enum of the library.
/// </para>
/// </summary>
public static class AdoConstants
{
    /// <summary>The generated file, next to the converted code.</summary>
    public const string FileName = "AdoConstants.cs";

    /// <summary>The class holding the constants; converted files bring it in with a using static.</summary>
    public const string ClassName = "AdoConstants";

    /// <summary>The value type of the constants.</summary>
    private const string ValueType = "AdoValue";

    /// <summary>The interop assembly the constants come from.</summary>
    private const string Library = "ADODB";

    /// <summary>
    /// The enums of the ADO type library (msado*.tlb), "Enum:member,member;": their members are what VB6 code names.
    /// Only the names are needed - the values come from the type library itself when the converted project compiles.
    /// </summary>
    private const string Catalog =
            "ADCPROP_ASYNCTHREADPRIORITY_ENUM:adPriorityLowest,adPriorityBelowNormal,adPriorityNormal,adPriorityAboveNormal,adPriorityHighest;"
          + "ADCPROP_AUTORECALC_ENUM:adRecalcUpFront,adRecalcAlways;"
          + "ADCPROP_UPDATECRITERIA_ENUM:adCriteriaKey,adCriteriaAllCols,adCriteriaUpdCols,adCriteriaTimeStamp;"
          + "ADCPROP_UPDATERESYNC_ENUM:adResyncNone,adResyncAutoIncrement,adResyncConflicts,adResyncUpdates,adResyncInserts,adResyncAll;"
          + "AffectEnum:adAffectCurrent,adAffectGroup,adAffectAll,adAffectAllChapters;"
          + "BookmarkEnum:adBookmarkCurrent,adBookmarkFirst,adBookmarkLast;"
          + "CommandTypeEnum:adCmdText,adCmdTable,adCmdStoredProc,adCmdUnknown,adCmdFile,adCmdTableDirect,adCmdUnspecified;"
          + "CompareEnum:adCompareLessThan,adCompareEqual,adCompareGreaterThan,adCompareNotEqual,adCompareNotComparable;"
          + "ConnectModeEnum:adModeUnknown,adModeRead,adModeWrite,adModeReadWrite,adModeShareDenyRead,adModeShareDenyWrite,adModeShareExclusive,adModeShareDenyNone,adModeRecursive;"
          + "ConnectOptionEnum:adAsyncConnect,adConnectUnspecified;"
          + "ConnectPromptEnum:adPromptAlways,adPromptComplete,adPromptCompleteRequired,adPromptNever;"
          + "CopyRecordOptionsEnum:adCopyOverWrite,adCopyNonRecursive,adCopyAllowEmulation,adCopyUnspecified;"
          + "CursorLocationEnum:adUseNone,adUseServer,adUseClient,adUseClientBatch;"
          + "CursorOptionEnum:adHoldRecords,adMovePrevious,adBookmark,adApproxPosition,adUpdateBatch,adResync,adNotify,adFind,adSeek,adIndex,adAddNew,adDelete,adUpdate;"
          + "CursorTypeEnum:adOpenForwardOnly,adOpenKeyset,adOpenDynamic,adOpenStatic,adOpenUnspecified;"
          + "DataTypeEnum:adEmpty,adSmallInt,adInteger,adSingle,adDouble,adCurrency,adDate,adBSTR,adIDispatch,adError,adBoolean,adVariant,adIUnknown,adDecimal,adTinyInt,adUnsignedTinyInt,adUnsignedSmallInt,adUnsignedInt,adBigInt,adUnsignedBigInt,adFileTime,adGUID,adBinary,adChar,adWChar,adNumeric,adUserDefined,adDBDate,adDBTime,adDBTimeStamp,adChapter,adPropVariant,adVarNumeric,adVarChar,adLongVarChar,adVarWChar,adLongVarWChar,adVarBinary,adLongVarBinary,adArray;"
          + "EditModeEnum:adEditNone,adEditInProgress,adEditAdd,adEditDelete;"
          + "ErrorValueEnum:adErrProviderFailed,adErrInvalidArgument,adErrOpeningFile,adErrReadFile,adErrWriteFile,adErrNoCurrentRecord,adErrIllegalOperation,adErrCantChangeProvider,adErrInTransaction,adErrFeatureNotAvailable,adErrItemNotFound,adErrObjectInCollection,adErrObjectNotSet,adErrDataConversion,adErrObjectClosed,adErrObjectOpen,adErrProviderNotFound,adErrBoundToCommand,adErrInvalidParamInfo,adErrInvalidConnection,adErrNotReentrant,adErrStillExecuting,adErrOperationCancelled,adErrStillConnecting,adErrInvalidTransaction,adErrNotExecuting,adErrUnsafeOperation,adwrnSecurityDialog,adwrnSecurityDialogHeader,adErrIntegrityViolation,adErrPermissionDenied,adErrDataOverflow,adErrSchemaViolation,adErrSignMismatch,adErrCantConvertvalue,adErrCantCreate,adErrColumnNotOnThisRow,adErrURLDoesNotExist,adErrTreePermissionDenied,adErrInvalidURL,adErrResourceLocked,adErrResourceExists,adErrCannotComplete,adErrVolumeNotFound,adErrOutOfSpace,adErrResourceOutOfScope,adErrUnavailable,adErrURLNamedRowDoesNotExist,adErrDelResOutOfScope,adErrPropInvalidColumn,adErrPropInvalidOption,adErrPropInvalidValue,adErrPropConflicting,adErrPropNotAllSettable,adErrPropNotSet,adErrPropNotSettable,adErrPropNotSupported,adErrCatalogNotSet,adErrCantChangeConnection,adErrFieldsUpdateFailed,adErrDenyNotSupported,adErrDenyTypeNotSupported,adErrProviderNotSpecified,adErrConnectionStringTooLong;"
          + "EventReasonEnum:adRsnAddNew,adRsnDelete,adRsnUpdate,adRsnUndoUpdate,adRsnUndoAddNew,adRsnUndoDelete,adRsnRequery,adRsnResynch,adRsnClose,adRsnMove,adRsnFirstChange,adRsnMoveFirst,adRsnMoveNext,adRsnMovePrevious,adRsnMoveLast;"
          + "EventStatusEnum:adStatusOK,adStatusErrorsOccurred,adStatusCantDeny,adStatusCancel,adStatusUnwantedEvent;"
          + "ExecuteOptionEnum:adAsyncExecute,adAsyncFetch,adAsyncFetchNonBlocking,adExecuteNoRecords,adExecuteStream,adExecuteRecord,adOptionUnspecified;"
          + "FieldAttributeEnum:adFldMayDefer,adFldUpdatable,adFldUnknownUpdatable,adFldFixed,adFldIsNullable,adFldMayBeNull,adFldLong,adFldRowID,adFldRowVersion,adFldCacheDeferred,adFldIsChapter,adFldNegativeScale,adFldKeyColumn,adFldIsRowURL,adFldIsDefaultStream,adFldIsCollection,adFldUnspecified;"
          + "FieldEnum:adRecordURL,adDefaultStream;"
          + "FieldStatusEnum:adFieldOK,adFieldCantConvertValue,adFieldIsNull,adFieldTruncated,adFieldSignMismatch,adFieldDataOverflow,adFieldCantCreate,adFieldUnavailable,adFieldPermissionDenied,adFieldIntegrityViolation,adFieldSchemaViolation,adFieldBadStatus,adFieldDefault,adFieldIgnore,adFieldDoesNotExist,adFieldInvalidURL,adFieldResourceLocked,adFieldResourceExists,adFieldCannotComplete,adFieldVolumeNotFound,adFieldOutOfSpace,adFieldCannotDeleteSource,adFieldReadOnly,adFieldResourceOutOfScope,adFieldAlreadyExists,adFieldPendingInsert,adFieldPendingDelete,adFieldPendingChange,adFieldPendingUnknown,adFieldPendingUnknownDelete;"
          + "FilterGroupEnum:adFilterNone,adFilterPendingRecords,adFilterAffectedRecords,adFilterFetchedRecords,adFilterPredicate,adFilterConflictingRecords;"
          + "GetRowsOptionEnum:adGetRowsRest;"
          + "IsolationLevelEnum:adXactChaos,adXactReadUncommitted,adXactBrowse,adXactCursorStability,adXactReadCommitted,adXactRepeatableRead,adXactSerializable,adXactIsolated,adXactUnspecified;"
          + "LineSeparatorEnum:adLF,adCR,adCRLF;"
          + "LockTypeEnum:adLockReadOnly,adLockPessimistic,adLockOptimistic,adLockBatchOptimistic,adLockUnspecified;"
          + "MarshalOptionsEnum:adMarshalAll,adMarshalModifiedOnly;"
          + "MoveRecordOptionsEnum:adMoveOverWrite,adMoveDontUpdateLinks,adMoveAllowEmulation,adMoveUnspecified;"
          + "ObjectStateEnum:adStateClosed,adStateOpen,adStateConnecting,adStateExecuting,adStateFetching;"
          + "ParameterAttributesEnum:adParamSigned,adParamNullable,adParamLong;"
          + "ParameterDirectionEnum:adParamUnknown,adParamInput,adParamOutput,adParamInputOutput,adParamReturnValue;"
          + "PersistFormatEnum:adPersistADTG,adPersistXML;"
          + "PositionEnum:adPosEOF,adPosBOF,adPosUnknown;"
          + "PositionEnum_Param:adPosEOF,adPosBOF,adPosUnknown;"
          + "PropertyAttributesEnum:adPropNotSupported,adPropRequired,adPropOptional,adPropRead,adPropWrite;"
          + "RecordCreateOptionsEnum:adCreateNonCollection,adCreateCollection,adOpenIfExists,adCreateOverwrite,adCreateStructDoc,adFailIfNotExists;"
          + "RecordOpenOptionsEnum:adOpenAsync,adDelayFetchStream,adDelayFetchFields,adOpenExecuteCommand,adOpenSource,adOpenOutput,adOpenRecordUnspecified;"
          + "RecordStatusEnum:adRecOK,adRecNew,adRecModified,adRecDeleted,adRecUnmodified,adRecInvalid,adRecMultipleChanges,adRecPendingChanges,adRecCanceled,adRecCantRelease,adRecConcurrencyViolation,adRecIntegrityViolation,adRecMaxChangesExceeded,adRecObjectOpen,adRecOutOfMemory,adRecPermissionDenied,adRecSchemaViolation,adRecDBDeleted;"
          + "RecordTypeEnum:adSimpleRecord,adCollectionRecord,adStructDoc;"
          + "ResyncEnum:adResyncUnderlyingValues,adResyncAllValues;"
          + "SaveOptionsEnum:adSaveCreateNotExist,adSaveCreateOverWrite;"
          + "SchemaEnum:adSchemaAsserts,adSchemaCatalogs,adSchemaCharacterSets,adSchemaCollations,adSchemaColumns,adSchemaCheckConstraints,adSchemaConstraintColumnUsage,adSchemaConstraintTableUsage,adSchemaKeyColumnUsage,adSchemaReferentialConstraints,adSchemaReferentialContraints,adSchemaTableConstraints,adSchemaColumnsDomainUsage,adSchemaIndexes,adSchemaColumnPrivileges,adSchemaTablePrivileges,adSchemaUsagePrivileges,adSchemaProcedures,adSchemaSchemata,adSchemaSQLLanguages,adSchemaStatistics,adSchemaTables,adSchemaTranslations,adSchemaProviderTypes,adSchemaViews,adSchemaViewColumnUsage,adSchemaViewTableUsage,adSchemaProcedureParameters,adSchemaForeignKeys,adSchemaPrimaryKeys,adSchemaProcedureColumns,adSchemaDBInfoKeywords,adSchemaDBInfoLiterals,adSchemaCubes,adSchemaDimensions,adSchemaHierarchies,adSchemaLevels,adSchemaMeasures,adSchemaProperties,adSchemaMembers,adSchemaTrustees,adSchemaFunctions,adSchemaActions,adSchemaCommands,adSchemaSets,adSchemaProviderSpecific;"
          + "SearchDirection:adSearchForward,adSearchBackward;"
          + "SearchDirectionEnum:adSearchForward,adSearchBackward;"
          + "SeekEnum:adSeekFirstEQ,adSeekLastEQ,adSeekAfterEQ,adSeekAfter,adSeekBeforeEQ,adSeekBefore;"
          + "StreamOpenOptionsEnum:adOpenStreamAsync,adOpenStreamFromRecord,adOpenStreamUnspecified;"
          + "StreamReadEnum:adReadLine,adReadAll;"
          + "StreamTypeEnum:adTypeBinary,adTypeText;"
          + "StreamWriteEnum:adWriteChar,stWriteChar,adWriteLine,stWriteLine;"
          + "StringFormatEnum:adClipString;"
          + "XactAttributeEnum:adXactCommitRetaining,adXactAbortRetaining,adXactAsyncPhaseOne,adXactSyncPhaseOne;";

    /// <summary>
    /// The constants file of a project whose assembly (and root namespace) is <paramref name="assembly"/>.
    /// A member declared by two enums (an alias kept for compatibility) is emitted once, as VB6 resolves it.
    /// </summary>
    public static string File(string assembly)
    {
        var n = vbCrLf;
        var s = new StringBuilder();
        s.Append("// <auto-generated />" + n);
        s.Append("// The " + Library + " constants VB6 code uses unqualified; VB6 sees a type library's enums as Long." + n);
        s.Append("using Vb6ToCSharp.UpgradeHelpers.Interop;" + n + n);
        s.Append("namespace " + assembly + n + "{" + n);
        s.Append("    /// <summary>A constant of the " + Library + " library: a Long that assigns to any of its enums, as in VB6.</summary>" + n);
        s.Append("    public readonly struct " + ValueType + " : IVbLibraryConstant" + n + "    {" + n);
        s.Append("        private readonly int value;" + n);
        s.Append("        public " + ValueType + "(int value) { this.value = value; }" + n);
        s.Append("        public int Value { get { return value; } }" + n);
        s.Append("        public override string ToString() { return value.ToString(); }" + n);
        s.Append("        public static implicit operator int(" + ValueType + " c) { return c.value; }" + n);
        foreach (var e in Enums())
        {
            s.Append("        public static implicit operator " + Library + "." + e.Name + "(" + ValueType + " c) { return (" + Library + "." + e.Name + ")c.value; }" + n);
        }
        s.Append("    }" + n + n);
        s.Append("    /// <summary>The " + Library + " constants, as VB6 publishes them: global and untyped.</summary>" + n);
        s.Append("    public static class " + ClassName + n + "    {" + n);
        var seen = new List<string>();
        foreach (var e in Enums())
        {
            foreach (var member in e.Members)
            {
                if (seen.Contains(member)) continue; // two enums declare it (PositionEnum / PositionEnum_Param)
                seen.Add(member);
                s.Append("        public static readonly " + ValueType + " " + member + " = new " + ValueType + "((int)"
                         + Library + "." + e.Name + "." + member + ");" + n);
            }
        }
        s.Append("    }" + n + "}" + n);
        return s.ToString();
    }

    /// <summary>The catalog, parsed.</summary>
    private static IEnumerable<(string Name, string[] Members)> Enums()
    {
        foreach (var e in Split(Catalog, ";"))
        {
            if (e == "") continue;
            yield return (SplitWord(e, 1, ":"), Split(SplitWord(e, 2, ":"), ","));
        }
    }
}
